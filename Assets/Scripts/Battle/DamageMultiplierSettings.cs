using UnityEngine;

[CreateAssetMenu(
    fileName = "DamageMultiplierSettings",
    menuName = "Game/Damage Multiplier Settings"
)]
public class DamageMultiplierSettings : ScriptableObject
{
    // =========================================================
    // プレイヤー・味方 → 敵
    // =========================================================

    [Header("プレイヤー・味方 → 敵")]

    [SerializeField]
    private float allyAdvantage = 1.5f;

    [SerializeField]
    private float allyNormal = 0.25f;

    [SerializeField]
    private float allyDisadvantage = 0.0f;

    // =========================================================
    // 敵 → プレイヤー・味方
    // =========================================================

    [Header("敵 → プレイヤー・味方")]

    [SerializeField]
    private float enemyAdvantage = 1.5f;

    [SerializeField]
    private float enemyNormal = 1.0f;

    [SerializeField]
    private float enemyDisadvantage = 0.5f;

    // =========================================================
    // Getter
    // =========================================================

    public float AllyAdvantage =>
        allyAdvantage;

    public float AllyNormal =>
        allyNormal;

    public float AllyDisadvantage =>
        allyDisadvantage;

    public float EnemyAdvantage =>
        enemyAdvantage;

    public float EnemyNormal =>
        enemyNormal;

    public float EnemyDisadvantage =>
        enemyDisadvantage;
}