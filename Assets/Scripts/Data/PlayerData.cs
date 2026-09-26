using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerData",
    menuName = "Game/Data/Player Data"
)]
public class PlayerData : ScriptableObject
{
    [Header("ステータス")]
    [SerializeField]
    private int maxHP = 10000;

    [SerializeField]
    private int attack = 15;

    [SerializeField]
    private float moveSpeed = 4.0f;

    [SerializeField]
    private float attackInterval = 1.0f;

    [SerializeField]
    private Vector2 attackArea = new Vector2(1.5f, 1.5f);

    public int MaxHP => maxHP;
    public int Attack => attack;
    public float MoveSpeed => moveSpeed;
    public float AttackInterval => attackInterval;
    public Vector2 AttackArea => attackArea;
}