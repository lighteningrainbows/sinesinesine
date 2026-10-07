using UnityEngine;

public class BattleSystem : MonoBehaviour
{
    public static BattleSystem Instance
    {
        get;
        private set;
    }

    [Header("ダメージ倍率設定")]
    [SerializeField]
    private DamageMultiplierSettings damageMultiplierSettings;

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

    // =========================================================
    // 攻撃
    // =========================================================

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

        // =====================================================
        // ダメージを受けるComponentを取得
        // =====================================================

        IDamageable damageable =
            target.GetComponent<IDamageable>();

        if (damageable == null)
        {
            // Colliderが子オブジェクトの場合にも対応
            damageable =
                target.GetComponentInParent<IDamageable>();
        }

        if (damageable == null)
        {
            Debug.LogWarning(
                $"{target.name} はダメージを受けられません。"
            );

            return;
        }

        // =====================================================
        // 実際にダメージを受けるGameObject
        // =====================================================

        Component damageableComponent =
            damageable as Component;

        GameObject actualTarget =
            damageableComponent != null
                ? damageableComponent.gameObject
                : target;

        // =====================================================
        // 防御側属性取得
        // =====================================================

        TypeData targetType1 = null;
        TypeData targetType2 = null;

        EnemyHealth enemyHealth =
            actualTarget.GetComponent<EnemyHealth>();

        if (enemyHealth != null &&
            enemyHealth.EnemyData != null)
        {
            targetType1 =
                enemyHealth.EnemyData.Type1;

            targetType2 =
                enemyHealth.EnemyData.Type2;
        }

        CompanionController companion =
            actualTarget.GetComponent<CompanionController>();

        if (companion != null &&
            companion.RobotData != null)
        {
            targetType1 =
                companion.RobotData.Type1;

            targetType2 =
                companion.RobotData.Type2;
        }

        PlayerHealth playerHealth =
            actualTarget.GetComponent<PlayerHealth>();

        // 現在Playerは属性なし
        if (playerHealth != null)
        {
            targetType1 = null;
            targetType2 = null;
        }

        // =====================================================
        // 属性相性
        // =====================================================

        TypeRelation relation =
            CalculateRelation(
                attackType,
                targetType1,
                targetType2
            );

        // =====================================================
        // 攻撃側が味方か敵か
        // =====================================================

        DamageCalculator.AttackerSide attackerSide =
            GetAttackerSide(attacker);

        // =====================================================
        // 最終ダメージ
        // =====================================================

        int finalDamage =
            DamageCalculator.CalculateDamage(
                baseDamage,
                relation,
                attackerSide,
                damageMultiplierSettings
            );

        Debug.Log(
            $"{attacker.name} → {actualTarget.name} / " +
            $"Side = {attackerSide} / " +
            $"Relation = {relation} / " +
            $"BaseDamage = {baseDamage} / " +
            $"Damage = {finalDamage}"
        );

        damageable.TakeDamage(
            finalDamage
        );
    }

    // =========================================================
    // 攻撃側判定
    // =========================================================

    private DamageCalculator.AttackerSide
        GetAttackerSide(GameObject attacker)
    {
        // EnemyControllerを持っていたら敵
        EnemyController enemy =
            attacker.GetComponent<EnemyController>();

        if (enemy == null)
        {
            enemy =
                attacker.GetComponentInParent<EnemyController>();
        }

        if (enemy != null)
        {
            return DamageCalculator.AttackerSide.Enemy;
        }

        // それ以外はPlayer・Companion側
        return DamageCalculator.AttackerSide.Ally;
    }

    // =========================================================
    // 属性相性計算
    // =========================================================

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
        {
            return TypeRelation.Normal;
        }

        TypeRelation relation1 =
            TypeChart.Instance.GetRelation(
                attackType,
                targetType1
            );

        // 属性が1つだけ
        if (targetType2 == null)
        {
            return relation1;
        }

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