using UnityEngine;

[CreateAssetMenu(
    fileName = "RobotData",
    menuName = "Game/Data/Robot Data"
)]
public class RobotData : ScriptableObject
{
    [Header("基本情報")]
    [SerializeField]
    private string robotName;

    [Header("タイプ")]
    [SerializeField]
    private TypeData type1;

    [SerializeField]
    private TypeData type2;

    [Header("ステータス")]
    [SerializeField]
    private int maxHP = 200;

    [SerializeField]
    private int attack = 15;

    [SerializeField]
    private float moveSpeed = 4.0f;

    [SerializeField]
    private float attackInterval = 1.0f;

    [SerializeField]
    private float attackRange = 1.5f;

    [Header("レベル")]
    [SerializeField]
    private int maxLevel = 5;

    public string RobotName => robotName;

    public TypeData Type1 => type1;
    public TypeData Type2 => type2;

    public int MaxHP => maxHP;
    public int Attack => attack;
    public float MoveSpeed => moveSpeed;
    public float AttackInterval => attackInterval;
    public float AttackRange => attackRange;

    public int MaxLevel => maxLevel;
}