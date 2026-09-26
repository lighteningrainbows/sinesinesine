using UnityEngine;

public class BattleSystem : MonoBehaviour
{
    public static BattleSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Attack(
        GameObject attacker,
        GameObject target,
        int baseDamage,
        TypeData attackType)
    {
        if (attacker == null)
            return;

        if (target == null)
            return;

        IDamageable damageable =
            target.GetComponent<IDamageable>();

        if (damageable == null)
        {
            Debug.LogWarning(
                $"{target.name} はダメージを受けられません。"
            );

            return;
        }

        TypeData targetType1 = null;
        TypeData targetType2 = null;

        EnemyHealth enemyHealth =
            target.GetComponent<EnemyHealth>();

        if (enemyHealth != null &&
            enemyHealth.EnemyData != null)
        {
            targetType1 =
                enemyHealth.EnemyData.Type1;

            targetType2 =
                enemyHealth.EnemyData.Type2;
        }

        CompanionController companion =
            target.GetComponent<CompanionController>();

        if (companion != null &&
            companion.RobotData != null)
        {
            targetType1 =
                companion.RobotData.Type1;

            targetType2 =
                companion.RobotData.Type2;
        }

        PlayerHealth playerHealth =
            target.GetComponent<PlayerHealth>();

        // 現在の設計ではPlayerはタイプを持たない
        if (playerHealth != null)
        {
            targetType1 = null;
            targetType2 = null;
        }

        TypeRelation relation =
            CalculateRelation(
                attackType,
                targetType1,
                targetType2
            );

        int finalDamage =
            DamageCalculator.CalculateDamage(
                baseDamage,
                relation
            );

        Debug.Log(
            $"{attacker.name} → {target.name} / " +
            $"Relation = {relation} / " +
            $"Damage = {finalDamage}"
        );

        damageable.TakeDamage(finalDamage);
    }

    private TypeRelation CalculateRelation(
        TypeData attackType,
        TypeData targetType1,
        TypeData targetType2)
    {
        if (TypeChart.Instance == null)
        {
            Debug.LogWarning(
                "TypeChart.Instance が存在しません。"
            );

            return TypeRelation.Normal;
        }

        if (attackType == null)
            return TypeRelation.Normal;

        TypeRelation relation1 =
            TypeChart.Instance.GetRelation(
                attackType,
                targetType1
            );

        if (targetType2 == null)
            return relation1;

        TypeRelation relation2 =
            TypeChart.Instance.GetRelation(
                attackType,
                targetType2
            );

        return TypeChart.Instance.CombineRelations(
            relation1,
            relation2
        );
    }
}