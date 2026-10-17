using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private EnemyData enemyData;
    private EnemyController controller;
    private BoxCollider body;
    private Transform attackTarget;
    private float attackRemaining;
    private float cooldownRemaining;
    private float activeDuration;
    private float activeInterval;
    private int activeDamage;
    private Vector2 activeArea;
    private bool previousWasStrong;
    private readonly Collider[] hits = new Collider[32];

    public bool IsAttacking { get; private set; }
    public float AttackProgress => IsAttacking && enemyData != null
        ? 1f - attackRemaining / Mathf.Max(0.01f, activeDuration) : 0f;
    public float CooldownRemaining => cooldownRemaining;
    public bool IsStrongAttack { get; private set; }
    public float CurrentAttackDuration => activeDuration;
    public float CurrentAttackInterval => activeInterval;
    public int CurrentAttackDamage => activeDamage;
    public Vector2 CurrentAttackArea => IsAttacking ? activeArea : enemyData.AttackArea;

    private void Awake()
    {
        controller = GetComponent<EnemyController>();
        body = GetComponent<BoxCollider>();
    }

    private void OnEnable()
    {
        IsAttacking = false;
        attackTarget = null;
        cooldownRemaining = 0f;
        previousWasStrong = false;
        IsStrongAttack = false;
    }

    private void Update()
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        if (controller == null || enemyData == null) return;
        if (IsAttacking)
        {
            if (!controller.CanAttack || !controller.IsTargetAvailable(attackTarget))
            {
                CancelAttack();
                return;
            }
            attackRemaining -= Time.deltaTime;
            if (attackRemaining > 0f) return;
            Transform target = attackTarget;
            bool hit = IsTargetInRange(target);
            IsAttacking = false;
            attackTarget = null;
            cooldownRemaining = activeInterval;
            // Recheck the committed box at impact: moving away dodges the attack.
            if (hit && BattleSystem.Instance != null)
                BattleSystem.Instance.Attack(gameObject, target.gameObject,
                    activeDamage, enemyData.Type1);
            return;
        }
        if (cooldownRemaining <= 0f && controller.CanAttack &&
            IsTargetInRange(controller.CurrentTarget))
        {
            attackTarget = controller.CurrentTarget;
            BossAttackSettings boss = enemyData.BossAttacks;
            IsStrongAttack = boss != null && boss.SelectStrongAttack(previousWasStrong);
            previousWasStrong = IsStrongAttack;
            activeDuration = IsStrongAttack ? boss.StrongDuration : enemyData.AttackDuration;
            activeInterval = IsStrongAttack ? boss.StrongInterval : enemyData.AttackInterval;
            activeDamage = IsStrongAttack ? boss.StrongDamage : enemyData.Attack;
            activeArea = IsStrongAttack ? boss.StrongArea : enemyData.AttackArea;
            attackRemaining = activeDuration;
            IsAttacking = true;
        }
    }

    public void CancelAttack()
    {
        if (IsAttacking && enemyData != null)
            cooldownRemaining = Mathf.Max(cooldownRemaining, activeInterval);
        IsAttacking = false;
        attackTarget = null;
        attackRemaining = 0f;
    }

    public bool IsTargetInRange(Transform target)
    {
        if (enemyData == null || !EnemyController.IsLivingTarget(target)) return false;
        IDamageable receiver = target.GetComponentInParent<IDamageable>();
        GetAttackBox(out Vector3 center, out Vector3 halfExtents);
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, hits,
            transform.rotation, LayerMask.GetMask("Player", "Companion"),
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (hits[i].GetComponentInParent<IDamageable>() == receiver) return true;
        return false;
    }

    public void GetAttackBox(out Vector3 center, out Vector3 halfExtents)
    {
        // World metres, measured forward from the body's front face.
        float front = body != null ? body.size.z * transform.lossyScale.z * 0.5f : 0.5f;
        Vector3 origin = body != null ? transform.TransformPoint(body.center) : transform.position;
        Vector2 area = CurrentAttackArea;
        center = origin + transform.forward * (front + area.y * 0.5f);
        halfExtents = new Vector3(area.x, enemyData.AttackHeight, area.y) * 0.5f;
    }

    private void OnDisable() => CancelAttack();

    private void OnDrawGizmosSelected()
    {
        if (enemyData == null) return;
        if (body == null) body = GetComponent<BoxCollider>();
        GetAttackBox(out Vector3 center, out Vector3 halfExtents);
        Gizmos.color = IsAttacking ? Color.red : Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
    }
}
