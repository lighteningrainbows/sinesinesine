using UnityEngine;

// Animation changes the child model only. Rigidbody movement belongs to CompanionController.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(CompanionController), typeof(CompanionCombat))]
public class TigerAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float blendDuration = .08f;
    [SerializeField, Min(0f)] private float runningFollowDistance = 6f;
    [SerializeField, Min(0f)] private float roarDuration = 1.65f;
    private CompanionController companion;
    private CompanionCombat combat;
    private RobotRuntimeStats stats;
    private Rigidbody body;
    // プレイヤーの走行状態を取得するため
    private PlayerController playerController;
    private bool wasDeployed;
    private int lastAttackSequence;
    private float roarUntil;

    public string CurrentMotion { get; private set; }
    public bool IsHoldingPose { get; private set; }
    public Animator Animator => animator;

    private void Awake()
    {
        companion = GetComponent<CompanionController>();
        combat = GetComponent<CompanionCombat>();
        stats = GetComponent<RobotRuntimeStats>();
        body = GetComponent<Rigidbody>();

        // 追従対象のプレイヤーを取得
        if (companion.FollowTarget != null)
        {
            playerController =
                companion.FollowTarget.GetComponent<PlayerController>();
        }

        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private void OnEnable()
    {
        wasDeployed = false;
        lastAttackSequence = -1;
        roarUntil = 0f;
        CurrentMotion = null;
        IsHoldingPose = false;
        if (animator == null) return;
        animator.speed = 1f;
        animator.Rebind();
        animator.Update(0f);
    }

    private void Update()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        bool deployed = companion.IsDeployed;
        if (deployed && !wasDeployed) roarUntil = Time.time + roarDuration;
        wasDeployed = deployed;
        IsHoldingPose = companion.IsFrozenPrevious || companion.IsKnockedBack ||
            (stats != null && stats.CurrentHP <= 0);
        if (IsHoldingPose)
        {
            roarUntil = 0f;
            animator.speed = 0f;
            return;
        }
        animator.speed = 1f;
        if (combat.IsAttacking && combat.CurrentAttackClip != null)
        {
            roarUntil = 0f;
            SetMotion(combat.CurrentAttackClip.name, combat.AttackSequence != lastAttackSequence);
            lastAttackSequence = combat.AttackSequence;
            return;
        }
        Vector3 velocity = body.linearVelocity;
        velocity.y = 0f;
        if (deployed && velocity.sqrMagnitude > .000000001f)
        {
            roarUntil = 0f;
            bool chasing = companion.GetCurrentState() == CompanionController.CompanionState.Chasing;

            // PlayerControllerが未取得なら再取得
            if (playerController == null &&
                companion.FollowTarget != null)
            {
                playerController =
                    companion.FollowTarget.GetComponent<PlayerController>();
            }

            // プレイヤーが走っているか
            bool playerRunning =
                playerController != null &&
                playerController.IsRunning;

            bool catchingUp = companion.FollowTarget != null &&
                Vector3.ProjectOnPlane(companion.FollowTarget.position - body.position, Vector3.up).magnitude > runningFollowDistance;

            if (chasing)
            {
                // 敵を追跡中
                SetMotion("Run_Forward");
            }
            else if (playerRunning || catchingUp)
            {
                // プレイヤーが走っている、または距離が離れている
                SetMotion("Run");
            }
            else
            {
                // 通常移動
                SetMotion("Walk");
            }

            animator.speed = Mathf.Clamp(velocity.magnitude / Mathf.Max(.1f, companion.RobotData.MoveSpeed), .2f, 2f);
            return;
        }
        SetMotion(deployed && Time.time < roarUntil ? "Roar" : "Idle");
    }

    private void SetMotion(string motion, bool restart = false)
    {
        if (!restart && CurrentMotion == motion) return;
        int state = UnityEngine.Animator.StringToHash("Base Layer." + motion);
        if (!animator.HasState(0, state)) return;
        if (CurrentMotion == null) animator.Play(state, 0, 0f);
        else animator.CrossFadeInFixedTime(state, blendDuration, 0, 0f);
        CurrentMotion = motion;
    }

    private void OnDisable()
    {
        if (animator != null) animator.speed = 0f;
        CurrentMotion = null;
        roarUntil = 0f;
        wasDeployed = false;
    }
}
