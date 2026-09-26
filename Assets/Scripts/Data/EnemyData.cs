using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyData",
    menuName = "Game/Data/Enemy Data"
)]
public class EnemyData : ScriptableObject
{
    [Header("基本情報")]
    [SerializeField]
    private string enemyName;

    [Header("タイプ")]
    [SerializeField]
    private TypeData type1;

    [SerializeField]
    private TypeData type2;

    [Header("ステータス")]
    [SerializeField]
    private int maxHP = 100;

    [SerializeField]
    private int attack = 10;

    [SerializeField]
    private float moveSpeed = 3.0f;

    [SerializeField]
    private float attackInterval = 1.5f;

    [SerializeField]
    private float attackRange = 1.5f;

    [Header("経験値")]
    [SerializeField]
    private int experience = 100;

    public string EnemyName => enemyName;

    public TypeData Type1 => type1;
    public TypeData Type2 => type2;

    public int MaxHP => maxHP;
    public int Attack => attack;
    public float MoveSpeed => moveSpeed;
    public float AttackInterval => attackInterval;
    public float AttackRange => attackRange;

    public int Experience => experience;
}