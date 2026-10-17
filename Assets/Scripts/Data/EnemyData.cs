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

    [Header("Alpha attack timing and hit box")]
    [SerializeField, Min(0.01f)] private float attackDuration = 0.5f;
    [SerializeField] private Vector2 attackArea = new Vector2(1.5f, 1.5f);
    [SerializeField, Min(0.1f)] private float attackHeight = 4f;

    [Header("Boss attacks (leave empty for ordinary enemies)")]
    [SerializeField] private BossAttackSettings bossAttacks;

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
    public float AttackDuration => attackDuration;
    public Vector2 AttackArea => attackArea;
    public float AttackHeight => attackHeight;
    public BossAttackSettings BossAttacks => bossAttacks;
    public bool IsBoss => bossAttacks != null;

    public int Experience => experience;
}
