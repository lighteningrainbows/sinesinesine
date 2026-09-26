using UnityEngine;

public class RobotRuntimeStats : MonoBehaviour, IDamageable
{
    [Header("参照")]
    [SerializeField] private CompanionController companionController;
    [SerializeField] private ExperienceComponent experienceComponent;

    private int currentHP;
    private int cachedLevel = -1;
    private int maxHP;
    private int attack;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public int Attack => attack;

    // ========================================
    // RobotDataから取得する戦闘ステータス
    // ========================================

    public float AttackRange
    {
        get
        {
            if (RobotData == null)
                return 0.0f;

            return RobotData.AttackRange;
        }
    }

    public float AttackInterval
    {
        get
        {
            if (RobotData == null)
                return 0.0f;

            return RobotData.AttackInterval;
        }
    }

    public TypeData Type1
    {
        get
        {
            if (RobotData == null)
                return null;

            return RobotData.Type1;
        }
    }

    public TypeData Type2
    {
        get
        {
            if (RobotData == null)
                return null;

            return RobotData.Type2;
        }
    }

    private RobotData RobotData
    {
        get
        {
            if (companionController == null)
                return null;

            return companionController.RobotData;
        }
    }

    private void Awake()
    {
        if (companionController == null)
            companionController =
                GetComponent<CompanionController>();

        if (experienceComponent == null)
            experienceComponent =
                GetComponent<ExperienceComponent>();

        if (RobotData == null)
        {
            Debug.LogError(
                $"{name} : RobotDataが未設定です。"
            );

            enabled = false;
            return;
        }

        UpdateStats(true);

        currentHP = maxHP;
    }

    private void Update()
    {
        if (experienceComponent == null)
            return;

        if (experienceComponent.Level != cachedLevel)
        {
            UpdateStats(false);
        }
    }

    private void UpdateStats(bool isFirstSetup)
    {
        int oldMaxHP = maxHP;

        // ----------------------------------------
        // 基本ステータス
        // ----------------------------------------

        maxHP = RobotData.MaxHP;
        attack = RobotData.Attack;

        // ----------------------------------------
        // レベルによるステータス上昇
        // ----------------------------------------

        LevelData levelData =
            experienceComponent != null
                ? experienceComponent.LevelData
                : null;

        int currentLevel =
            experienceComponent != null
                ? experienceComponent.Level
                : 1;

        if (levelData != null &&
            levelData.Levels != null)
        {
            foreach (LevelData.LevelInfo info
                in levelData.Levels)
            {
                if (info == null)
                    continue;

                // Lv1は初期ステータス
                // Lv2以降の強化値を加算
                if (info.level <= 1 ||
                    info.level > currentLevel)
                {
                    continue;
                }

                maxHP += info.hpBonus;
                attack += info.attackBonus;
            }
        }

        cachedLevel = currentLevel;

        // ----------------------------------------
        // レベルアップ時のHP処理
        // ----------------------------------------

        if (!isFirstSetup)
        {
            int hpIncrease =
                maxHP - oldMaxHP;

            if (hpIncrease > 0)
            {
                currentHP += hpIncrease;
            }

            currentHP =
                Mathf.Clamp(
                    currentHP,
                    0,
                    maxHP
                );
        }

        Debug.Log(
            $"{name} ステータス更新 | " +
            $"Lv.{cachedLevel} | " +
            $"HP:{currentHP}/{maxHP} | " +
            $"攻撃力:{attack}"
        );
    }

    // ========================================
    // ダメージ
    // ========================================

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        if (currentHP <= 0)
            return;

        currentHP =
            Mathf.Max(
                0,
                currentHP - damage
            );

        Debug.Log(
            $"{name} HP : " +
            $"{currentHP} / {maxHP}"
        );

        // HP0になった場合
        if (currentHP <= 0)
        {
            Debug.Log(
                $"{name} : 戦闘不能"
            );
        }
    }

    // ========================================
    // 回復
    // ========================================

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        if (currentHP <= 0)
            return;

        currentHP =
            Mathf.Min(
                maxHP,
                currentHP + amount
            );
    }
}