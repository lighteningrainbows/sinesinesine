
using UnityEngine;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; private set; }

    [Header("プレイヤー")]
    [SerializeField] private ExperienceComponent playerExperience;

    [Header("仲間管理")]
    [SerializeField] private CompanionManager companionManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void GiveExperienceFromEnemy(EnemyData enemyData)
    {
        if (enemyData == null)
        {
            Debug.LogWarning("EnemyDataが設定されていません。");
            return;
        }

        int baseExperience = enemyData.Experience;

        // プレイヤーに経験値を付与
        if (playerExperience != null)
        {
            playerExperience.AddExperience(baseExperience);

            Debug.Log($"プレイヤー EXP +{baseExperience}");
        }
        else
        {
            Debug.LogWarning(
                "ExperienceManagerにPlayer Experienceが設定されていません。"
            );
        }

        // 仲間ロボットへの経験値付与
        if (companionManager == null)
        {
            Debug.LogWarning(
                "CompanionManagerが設定されていません。"
            );

            return;
        }

        GiveExperienceForType(enemyData.Type1, baseExperience);
        GiveExperienceForType(enemyData.Type2, baseExperience);
    }

    private void GiveExperienceForType(
        TypeData enemyType,
        int baseExperience)
    {
        if (enemyType == null)
            return;

        int companionCount = companionManager.GetCompanionCount();

        for (int i = 0; i < companionCount; i++)
        {
            CompanionController companion =
                companionManager.GetCompanion(i);

            if (companion == null)
                continue;

            RobotData robotData = companion.RobotData;

            if (robotData == null)
                continue;

            ExperienceComponent experience =
                companion.GetComponent<ExperienceComponent>();

            if (experience == null)
                continue;

            // メインタイプ一致：経験値100%
            if (robotData.Type1 == enemyType)
            {
                experience.AddExperience(baseExperience);

                Debug.Log(
                    $"{companion.gameObject.name} EXP +{baseExperience}"
                );

                continue;
            }

            // サブタイプ一致：経験値30%
            if (robotData.Type2 == enemyType)
            {
                int subExperience =
                    Mathf.RoundToInt(baseExperience * 0.3f);

                experience.AddExperience(subExperience);

                Debug.Log(
                    $"{companion.gameObject.name} EXP +{subExperience}"
                );
            }
        }
    }
}