using UnityEngine;

public static class DamageCalculator
{
    // =========================================================
    // 攻撃側の種類
    // =========================================================

    public enum AttackerSide
    {
        Ally,   // プレイヤー・味方
        Enemy   // 敵
    }

    // =========================================================
    // ダメージ計算
    // =========================================================

    public static int CalculateDamage(
        int baseDamage,
        TypeRelation relation,
        AttackerSide attackerSide,
        DamageMultiplierSettings settings)
    {
        float multiplier =
            GetMultiplier(
                relation,
                attackerSide,
                settings
            );

        return Mathf.RoundToInt(
            baseDamage * multiplier
        );
    }

    // =========================================================
    // 倍率取得
    // =========================================================

    public static float GetMultiplier(
        TypeRelation relation,
        AttackerSide attackerSide,
        DamageMultiplierSettings settings)
    {
        // 設定がない場合
        if (settings == null)
        {
            Debug.LogWarning(
                "DamageMultiplierSettings が設定されていません。"
            );

            return 1.0f;
        }

        // 味方側
        if (attackerSide == AttackerSide.Ally)
        {
            switch (relation)
            {
                case TypeRelation.Advantage:
                    return settings.AllyAdvantage;

                case TypeRelation.Disadvantage:
                    return settings.AllyDisadvantage;

                case TypeRelation.Normal:
                default:
                    return settings.AllyNormal;
            }
        }

        // 敵側
        switch (relation)
        {
            case TypeRelation.Advantage:
                return settings.EnemyAdvantage;

            case TypeRelation.Disadvantage:
                return settings.EnemyDisadvantage;

            case TypeRelation.Normal:
            default:
                return settings.EnemyNormal;
        }
    }
}