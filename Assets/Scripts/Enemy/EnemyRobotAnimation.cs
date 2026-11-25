using UnityEngine;

[DefaultExecutionOrder(100)]
public class EnemyRobotAnimation : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Animator animator;
    [SerializeField] private AnimationClip attackClip;
    [SerializeField] private AnimationClip damageClip;
    [SerializeField] private AnimationClip deathClip;
    private EnemyController controller;
    private EnemyCombat combat;
    private Rigidbody body;
    private Transform cannon;
    private int attackSequence = -1;
    private bool deathPlayed;
    public string CurrentMotion { get; private set; }
    public Animator Animator => animator;

    private void Awake()
    {
        controller = GetComponent<EnemyController>();
        combat = GetComponent<EnemyCombat>();
        body = GetComponent<Rigidbody>();
        foreach (Transform child in visualRoot.GetComponentsInChildren<Transform>())
            if (child.name == "CannonRecoil") { cannon = child; break; }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private void OnEnable()
    {
        CurrentMotion = null;
        attackSequence = -1;
        deathPlayed = false;
        if (animator != null) { animator.speed = 1f; animator.Rebind(); animator.Update(0f); }
    }

    private void Update()
    {
        if (animator == null || controller.CurrentState == EnemyController.EnemyState.Defeated) return;
        if (combat.IsAttacking)
        {
            SetMotion("Attack", attackSequence != combat.AttackSequence);
            attackSequence = combat.AttackSequence;
            animator.speed = attackClip.length / Mathf.Max(.01f, combat.CurrentAttackDuration);
        }
        else if (controller.IsStaggered)
        {
            SetMotion("Damage");
            animator.speed = damageClip.length / .6f;
        }
        else
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            SetMotion(velocity.sqrMagnitude > .01f
                ? controller.CurrentState == EnemyController.EnemyState.Returning ? "Walk" : "Run"
                : "Idle");
            animator.speed = 1f;
        }
    }

    private void SetMotion(string name, bool restart = false)
    {
        if (!restart && CurrentMotion == name) return;
        int state = UnityEngine.Animator.StringToHash("Base Layer." + name);
        if (CurrentMotion == null) animator.Play(state, 0, 0f);
        else animator.CrossFadeInFixedTime(state, .05f, 0, 0f);
        CurrentMotion = name;
    }

    public void ReplayDamage()
    {
        if (isActiveAndEnabled && controller != null && controller.IsStaggered)
            SetMotion("Damage", true);
    }

    public void PlayDeath()
    {
        if (deathPlayed || visualRoot == null) return;
        deathPlayed = true;
        // Gameplay defeat and EXP remain immediate. Only a non-interactive visual finishes the clip.
        GameObject corpse = Instantiate(visualRoot.gameObject, visualRoot.position, visualRoot.rotation);
        corpse.name = name + "_DeathVisual";
        corpse.transform.localScale = visualRoot.lossyScale;
        foreach (Collider collider in corpse.GetComponentsInChildren<Collider>()) Destroy(collider);
        Animator deathAnimator = corpse.GetComponentInChildren<Animator>();
        deathAnimator.enabled = true;
        deathAnimator.applyRootMotion = false;
        deathAnimator.speed = 1f;
        deathAnimator.Rebind();
        deathAnimator.Play("Base Layer.Death", 0, 0f);
        deathAnimator.Update(0f);
        Destroy(corpse, deathClip.length + .1f);
    }

    public void ShowShot()
    {
        var shot = new GameObject("EnemyGunTrace");
        var line = shot.AddComponent<LineRenderer>();
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.color = new Color(1f, .6f, .1f);
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.startWidth = .06f;
        line.endWidth = .025f;
        Vector3 start = cannon != null ? cannon.position + transform.forward * .35f :
            transform.position + transform.forward * 1.05f;
        Vector3 end = start + transform.forward * combat.CurrentAttackArea.y;
        if (Physics.Raycast(start, transform.forward, out RaycastHit hit, combat.CurrentAttackArea.y, ~0,
            QueryTriggerInteraction.Ignore)) end = hit.point;
        line.SetPositions(new[] { start, end });
        Destroy(material, .1f);
        Destroy(shot, .1f);
    }
}
