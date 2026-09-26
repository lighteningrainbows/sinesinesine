using UnityEngine;

public static class DamageCalculator
{
    public static float GetMultiplier(TypeRelation relation)
    {
        switch (relation)
        {
            case TypeRelation.Advantage:
                return 1.5f;

            case TypeRelation.Disadvantage:
                return 0.5f;

            case TypeRelation.Normal:
            default:
                return 0.75f;
        }
    }

    public static int CalculateDamage(
        int baseDamage,
        TypeRelation relation)
    {
        float multiplier = GetMultiplier(relation);

        return Mathf.RoundToInt(
            baseDamage * multiplier
        );
    }
}