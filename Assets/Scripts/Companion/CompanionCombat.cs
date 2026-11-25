using UnityEngine;

public class CompanionCombat : MonoBehaviour
{
    // Empty on the original companions: they retain their immediate prototype attack.
    [SerializeField] private AnimationClip[] attackAnimations;
    [SerializeField, Range(0f, 1f)] private float hitNormalizedTime = .5f;
    private CompanionController controller;
    private RobotRuntimeStats runtimeStats;
    private float attackTimer;
    private Transform attackTarget;
    private float elapsed;
    private bool hasHit;

    public bool IsAttacking { get; private set; }
    public AnimationClip CurrentAttackClip { get; private set; }
    public int AttackSequence { get; private set; }
    public float AttackDuration => CurrentAttackClip != null ? CurrentAttackClip.length : 0f;
    public float AttackProgress => IsAttacking ? Mathf.Clamp01(elapsed / Mathf.Max(.01f, AttackDuration)) : 0f;

    private void Awake()
    {
        controller = GetComponent<CompanionController>();
        runtimeStats = GetComponent<RobotRuntimeStats>();
    }

    private void Update()
    {
        if (controller == null || runtimeStats == null || !controller.enabled ||
            !controller.IsDeployed || controller.IsFrozenPrevious || controller.IsKnockedBack ||
            runtimeStats.CurrentHP <= 0)
        {
            CancelAttack();
            return;
        }
        Transform target = controller.GetCurrentTarget();
        if (IsAttacking)
        {
            if (target != attackTarget || !IsLivingEnemy(attackTarget))
            {
                CancelAttack();
                return;
            }
            attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
            elapsed += Time.deltaTime;
            if (!hasHit && elapsed >= AttackDuration * hitNormalizedTime)
            {
                hasHit = true;
                // Dodgeable: check the target again at the animation's impact frame.
                if (InRange(attackTarget)) Attack(attackTarget);
            }
            if (elapsed >= AttackDuration) CancelAttack();
            return;
        }
        if (controller.GetCurrentState() != CompanionController.CompanionState.Attacking) return;
        if (!IsLivingEnemy(target))
        {
            controller.ClearTarget();
            attackTimer = 0f;
            return;
        }
        attackTimer -= Time.deltaTime;
        if (attackTimer > 0f || !InRange(target)) return;
        attackTimer = runtimeStats.AttackInterval;
        if (attackAnimations == null || attackAnimations.Length == 0)
        {
            Attack(target);
            return;
        }
        CurrentAttackClip = attackAnimations[AttackSequence % attackAnimations.Length];
        if (CurrentAttackClip == null || CurrentAttackClip.length <= 0f) return;
        AttackSequence++;
        attackTarget = target;
        elapsed = 0f;
        hasHit = false;
        IsAttacking = true;
        Vector3 facing = target.position - transform.position;
        facing.y = 0f;
        if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
    }

    private static bool IsLivingEnemy(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        EnemyHealth enemy = target.GetComponentInParent<EnemyHealth>();
        return enemy != null && enemy.CurrentHP > 0;
    }

    private bool InRange(Transform target) => IsLivingEnemy(target) &&
        CombatGeometry.HorizontalDistanceToTarget(transform.position, target) <= runtimeStats.AttackRange;

    private void Attack(Transform target)
    {
        if (BattleSystem.Instance == null) return;
        BattleSystem.Instance.Attack(gameObject, target.gameObject, runtimeStats.Attack, runtimeStats.Type1);
    }

    public void CancelAttack()
    {
        IsAttacking = false;
        CurrentAttackClip = null;
        attackTarget = null;
        elapsed = 0f;
        hasHit = false;
    }

    private void OnDisable() => CancelAttack();
}
