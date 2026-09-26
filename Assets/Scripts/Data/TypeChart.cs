using System.Collections.Generic;
using UnityEngine;

public class TypeChart : MonoBehaviour
{
    public static TypeChart Instance { get; private set; }

    [System.Serializable]
    public class TypeMatchup
    {
        public TypeData attackType;
        public TypeData defenseType;
        public TypeRelation relation;
    }

    [SerializeField]
    private List<TypeMatchup> matchups = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public TypeRelation GetRelation(
        TypeData attackType,
        TypeData defenseType)
    {
        if (attackType == null || defenseType == null)
        {
            return TypeRelation.Normal;
        }

        foreach (TypeMatchup matchup in matchups)
        {
            if (matchup.attackType == attackType &&
                matchup.defenseType == defenseType)
            {
                return matchup.relation;
            }
        }

        return TypeRelation.Normal;
    }

    public TypeRelation CombineRelations(
    TypeRelation relationA,
    TypeRelation relationB)
    {
        if (relationA == TypeRelation.Disadvantage &&
            relationB == TypeRelation.Disadvantage)
        {
            return TypeRelation.Disadvantage;
        }

        if ((relationA == TypeRelation.Advantage &&
             relationB == TypeRelation.Disadvantage) ||
            (relationA == TypeRelation.Disadvantage &&
             relationB == TypeRelation.Advantage))
        {
            return TypeRelation.Normal;
        }

        if (relationA == TypeRelation.Advantage ||
            relationB == TypeRelation.Advantage)
        {
            return TypeRelation.Advantage;
        }

        return TypeRelation.Normal;
    }
}