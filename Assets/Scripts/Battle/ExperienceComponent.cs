using UnityEngine;

public class ExperienceComponent : MonoBehaviour
{
    [Header("初期レベル")]
    [SerializeField] private int level = 1;

    [Header("現在の経験値")]
    [SerializeField] private int experience = 0;

    [Header("レベルデータ")]
    [SerializeField] private LevelData levelData;

    public int Level => level;

    public int Experience => experience;

    public LevelData LevelData => levelData;

    public void AddExperience(int amount)
    {
        if (amount <= 0)
            return;

        experience += amount;

        Debug.Log(
            $"{gameObject.name} : " +
            $"+{amount} EXP / " +
            $"Total EXP = {experience}"
        );

        CheckLevelUp();
    }

    private void CheckLevelUp()
    {
        if (levelData == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} : " +
                "LevelDataが設定されていません。"
            );

            return;
        }

        while (true)
        {
            LevelData.LevelInfo nextLevel =
                levelData.GetLevelInfo(level + 1);

            if (nextLevel == null)
                break;

            if (experience < nextLevel.requiredExperience)
                break;

            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;

        Debug.Log(
            $"{gameObject.name} : " +
            $"LEVEL UP! → Lv.{level}"
        );
    }
}