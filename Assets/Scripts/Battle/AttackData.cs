using UnityEngine;

public class AttackData
{
    public int Damage { get; }

    public TypeData AttackType { get; }

    public AttackData(
        int damage,
        TypeData attackType)
    {
        Damage = damage;
        AttackType = attackType;
    }
}