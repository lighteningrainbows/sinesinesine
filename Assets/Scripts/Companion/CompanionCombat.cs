using UnityEngine;

public class CompanionCombat : MonoBehaviour
{
    private CompanionController controller;
    private RobotRuntimeStats runtimeStats;

    private float attackTimer;

    private void Awake()
    {
        controller =
            GetComponent<CompanionController>();

        runtimeStats =
            GetComponent<RobotRuntimeStats>();
    }

    private void Update()
    {
        if (controller == null)
            return;

        if (runtimeStats == null)
            return;

        // 攻撃状態でなければ何もしない
        if (controller.GetCurrentState() !=
            CompanionController.CompanionState.Attacking)
        {
            return;
        }

        Transform target =
            controller.GetCurrentTarget();

        // ターゲットが存在しない
        // または倒されて非アクティブになった
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
            TryAttack(target);
        }
    }

    private void TryAttack(Transform target)
    {
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

        if (distance > runtimeStats.AttackRange)
        {
            return;
        }

        attackTimer =
            runtimeStats.AttackInterval;

        Attack(target);
    }

    private void Attack(Transform target)
    {
        if (BattleSystem.Instance == null)
        {
            Debug.LogWarning(
                "BattleSystemが存在しません。"
            );

            return;
        }

        BattleSystem.Instance.Attack(
            gameObject,
            target.gameObject,
            runtimeStats.Attack,
            runtimeStats.Type1
        );
    }

    /// <summary>
    /// XZ平面上だけで距離を計算する
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