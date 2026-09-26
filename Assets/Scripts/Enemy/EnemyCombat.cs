using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [Header("“Gƒf[ƒ^")]
    [SerializeField] private EnemyData enemyData;

    private EnemyController controller;

    private float attackTimer;

    private void Awake()
    {
        controller =
            GetComponent<EnemyController>();
    }

    private void Update()
    {
        if (controller == null)
            return;

        if (enemyData == null)
            return;

        // UŒ‚ó‘Ô‚Å‚È‚¯‚ê‚Î‰½‚à‚µ‚È‚¢
        if (controller.CurrentState !=
            EnemyController.EnemyState.Attacking)
        {
            return;
        }

        // ƒ^[ƒQƒbƒg‚ªÁ–Å‚µ‚Ä‚¢‚éê‡
        Transform target =
            controller.CurrentTarget;

        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            controller.ClearTarget();
            attackTimer = 0.0f;
            return;
        }

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0.0f)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        Transform target =
            controller.CurrentTarget;

        if (target == null ||
            !target.gameObject.activeInHierarchy)
        {
            controller.ClearTarget();
            return;
        }

        float distance =
            HorizontalDistance(
                transform.position,
                target.position
            );

        // UŒ‚”ÍˆÍ‚©‚ço‚Ä‚¢‚½‚çUŒ‚‚µ‚È‚¢
        if (distance > enemyData.AttackRange)
            return;

        attackTimer =
            enemyData.AttackInterval;

        Attack(target);
    }

    private void Attack(Transform target)
    {
        if (BattleSystem.Instance == null)
        {
            Debug.LogWarning(
                "BattleSystem‚ª‘¶İ‚µ‚Ü‚¹‚ñB"
            );

            return;
        }

        BattleSystem.Instance.Attack(
            gameObject,
            target.gameObject,
            enemyData.Attack,
            enemyData.Type1
        );
    }

    /// <summary>
    /// XZ•½–Êã‚¾‚¯‚Å‹——£‚ğŒvZ‚·‚é
    /// </summary>
    private float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        a.y = 0.0f;
        b.y = 0.0f;

        return Vector3.Distance(a, b);
    }
}