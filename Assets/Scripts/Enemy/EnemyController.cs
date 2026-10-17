using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Waiting, Chasing, Attacking, Returning,
        Knockback, AirPause, Falling, Recovering, Defeated
    }

    [SerializeField] private EnemyData enemyData;
    [SerializeField] private bool isFlying;
    [SerializeField] private float hoverHeight = 1.5f;
    [SerializeField] private float targetSearchInterval = 0.2f;
    [SerializeField] private float returnDistance = 8f;

    [Header("Alpha hit reaction")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.3f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.15f;
    [SerializeField, Min(0f)] private float knockbackHeight = 0.1f;
    [SerializeField, Min(0f)] private float airPauseDuration = 0.2f;
    [SerializeField, Min(0f)] private float recoveryDuration = 1f;

    private Rigidbody rb;
    private EnemyCombat combat;
    private EnemyHealth health;
    private Collider bodyCollider;
    private EnemySpawnGroup spawnGroup;
    private BossEncounter bossEncounter;
    private EnemyState currentState;
    private Transform currentTarget;
    private Vector3 waitingPosition;
    private Vector3 knockbackDirection;
    private float hoverY;
    private float targetSearchTimer;
    private float reactionTimer;
    private bool hasDetectedPlayer;
    private CombatKnockback? comboKnockback;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    public EnemyState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;
    public Vector3 WaitingPosition => waitingPosition;
    public bool IsFlying => isFlying;
    public bool IsStaggered => currentState >= EnemyState.Knockback &&
                               currentState <= EnemyState.Recovering;
    public bool CanAct => !IsStaggered && currentState != EnemyState.Defeated &&
                          (health == null || health.CurrentHP > 0);
    public bool CanAttack => CanAct && hasDetectedPlayer &&
                             currentState == EnemyState.Attacking;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        combat = GetComponent<EnemyCombat>();
        health = GetComponent<EnemyHealth>();
        bodyCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        waitingPosition = transform.position;
        hoverY = waitingPosition.y + hoverHeight;
        currentState = EnemyState.Waiting;
        currentTarget = null;
        hasDetectedPlayer = false;
        targetSearchTimer = 0f;
        rb.useGravity = !isFlying;
        rb.linearVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (enemyData == null || currentState == EnemyState.Defeated)
            return;
        if (IsStaggered)
        {
            UpdateHitReaction();
            return;
        }
        if (!CanAct) return;
        if (isFlying) MaintainHover();

        // Finish returning before acquiring another target, avoiding leash oscillation.
        if (!hasDetectedPlayer || currentState == EnemyState.Returning ||
            HorizontalDistance(rb.position, waitingPosition) > returnDistance)
        {
            currentState = EnemyState.Returning;
            currentTarget = null;
            if (combat != null) combat.CancelAttack();
            ReturnHome();
            return;
        }

        targetSearchTimer -= Time.fixedDeltaTime;
        if (!IsTargetAvailable(currentTarget) || targetSearchTimer <= 0f)
        {
            targetSearchTimer = targetSearchInterval;
            if (!IsTargetAvailable(currentTarget)) currentTarget = ChooseTarget();
        }
        if (currentTarget == null)
        {
            currentState = EnemyState.Returning;
            if (combat != null) combat.CancelAttack();
            ReturnHome();
            return;
        }

        // An attack commits its facing; targets can dodge during its wind-up.
        if (combat != null && combat.IsAttacking)
        {
            currentState = EnemyState.Attacking;
            StopMoving();
            return;
        }
        FaceTarget(currentTarget.position);
        if (combat != null && combat.IsTargetInRange(currentTarget))
        {
            currentState = EnemyState.Attacking;
            StopMoving();
        }
        else
        {
            currentState = EnemyState.Chasing;
            MoveTowards(currentTarget.position);
        }
    }

    public void SetSpawnGroup(EnemySpawnGroup group) => spawnGroup = group;
    public void SetBossEncounter(BossEncounter encounter) => bossEncounter = encounter;

    public void DetectPlayer()
    {
        hasDetectedPlayer = true;
        targetSearchTimer = 0f;
    }

    public void LosePlayerDetection()
    {
        hasDetectedPlayer = false;
        currentTarget = null;
        if (combat != null) combat.CancelAttack();
        if (CanAct) currentState = EnemyState.Returning;
    }

    public static bool IsLivingTarget(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.CurrentHP <= 0) return false;
        CompanionController companion = target.GetComponentInParent<CompanionController>();
        return companion == null || (companion.IsDeployed && !companion.IsFrozenPrevious);
    }

    public bool IsTargetAvailable(Transform target)
    {
        return IsLivingTarget(target) &&
               (spawnGroup == null || spawnGroup.ContainsTarget(target)) &&
               (bossEncounter == null || bossEncounter.ContainsTarget(target));
    }

    private Transform ChooseTarget()
    {
        Transform player = bossEncounter != null ? bossEncounter.Player :
            spawnGroup != null ? spawnGroup.Player : null;
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
        CompanionController companion = CompanionManager.Instance != null
            ? CompanionManager.Instance.ActiveCompanion : null;
        Transform ally = companion != null ? companion.transform : null;
        bool playerValid = IsTargetAvailable(player);
        bool allyValid = IsTargetAvailable(ally);
        if (playerValid && allyValid) return Random.Range(0, 2) == 0 ? player : ally;
        return playerValid ? player : allyValid ? ally : null;
    }

    public void ReceiveHit(Vector3 attackerPosition, CombatKnockback? knockback = null)
    {
        if (currentState == EnemyState.Defeated ||
            (health != null && health.CurrentHP <= 0)) return;
        if (combat != null) combat.CancelAttack();
        comboKnockback = knockback;
        knockbackDirection = rb.position - attackerPosition;
        knockbackDirection.y = 0f;
        knockbackDirection = knockbackDirection.sqrMagnitude > 0.0001f
            ? knockbackDirection.normalized : -transform.forward;
        // Follow-ups during AirPause restart the complete reaction.
        currentState = EnemyState.Knockback;
        reactionTimer = 0f;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
    }

    private void UpdateHitReaction()
    {
        float dt = Time.fixedDeltaTime;
        switch (currentState)
        {
            case EnemyState.Knockback:
                float duration = comboKnockback.HasValue ? comboKnockback.Value.Duration : knockbackDuration;
                if (reactionTimer >= duration)
                {
                    BeginReactionPhase(EnemyState.AirPause);
                    return;
                }
                float step = Mathf.Min(dt, duration - reactionTimer);
                if (comboKnockback.HasValue)
                {
                    CombatKnockback profile = comboKnockback.Value;
                    rb.linearVelocity = (profile.Offset(reactionTimer + step, knockbackDirection) -
                        profile.Offset(reactionTimer, knockbackDirection)) / dt;
                }
                else rb.linearVelocity = (knockbackDirection * knockbackDistance +
                    Vector3.up * knockbackHeight) / duration * (step / dt);
                reactionTimer += step;
                break;
            case EnemyState.AirPause:
                rb.linearVelocity = Vector3.zero;
                reactionTimer += dt;
                if (reactionTimer >= (comboKnockback.HasValue ? comboKnockback.Value.Pause : airPauseDuration))
                {
                    BeginReactionPhase(EnemyState.Falling);
                    rb.useGravity = true;
                }
                break;
            case EnemyState.Falling:
                StopMoving();
                if (rb.linearVelocity.y <= 0f && IsGrounded())
                    BeginReactionPhase(EnemyState.Recovering);
                break;
            case EnemyState.Recovering:
                StopMoving();
                reactionTimer += dt;
                if (reactionTimer >= recoveryDuration)
                {
                    rb.useGravity = !isFlying;
                    currentState = EnemyState.Waiting;
                    targetSearchTimer = 0f;
                }
                break;
        }
    }

    private void BeginReactionPhase(EnemyState state)
    {
        currentState = state;
        reactionTimer = 0f;
        rb.linearVelocity = Vector3.zero;
    }

    private bool IsGrounded()
    {
        if (bodyCollider == null) return false;
        Bounds bounds = bodyCollider.bounds;
        int count = Physics.RaycastNonAlloc(bounds.center, Vector3.down, groundHits,
            bounds.extents.y + 0.04f, ~LayerMask.GetMask("Enemy", "Player", "Companion"),
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (groundHits[i].normal.y > 0.5f) return true;
        return false;
    }

    private void ReturnHome()
    {
        if (HorizontalDistance(rb.position, waitingPosition) <= 0.1f)
        {
            StopMoving();
            currentState = EnemyState.Waiting;
            return;
        }
        MoveTowards(waitingPosition);
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - rb.position;
        direction.y = 0f;
        float distance = direction.magnitude;
        Vector3 velocity = direction.normalized *
            Mathf.Min(enemyData.MoveSpeed, distance / Time.fixedDeltaTime);
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
        FaceTarget(targetPosition);
    }

    private void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - rb.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private void StopMoving()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.x = velocity.z = 0f;
        rb.linearVelocity = velocity;
    }

    private void MaintainHover()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Clamp((hoverY - rb.position.y) * 2f,
            -enemyData.MoveSpeed, enemyData.MoveSpeed);
        rb.linearVelocity = velocity;
    }

    public void ClearTarget()
    {
        currentTarget = null;
        if (combat != null) combat.CancelAttack();
        if (CanAct) currentState = EnemyState.Waiting;
    }

    public void Defeat()
    {
        currentTarget = null;
        hasDetectedPlayer = false;
        currentState = EnemyState.Defeated;
        if (combat != null) combat.CancelAttack();
        rb.linearVelocity = Vector3.zero;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
