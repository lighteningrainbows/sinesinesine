
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionHUD : MonoBehaviour
{
    [Header("ロボットHUD")]
    [SerializeField] private GameObject hudPanel;

    [Header("基本UI")]
    [SerializeField] private TMP_Text companionNameText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text levelText;

    [Header("経験値UI")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private TMP_Text expText;

    private CompanionController currentCompanion;
    private RobotRuntimeStats currentStats;
    private ExperienceComponent currentExperience;

    private void Update()
    {
        UpdateActiveCompanion();
    }

    private void UpdateActiveCompanion()
    {
        CompanionManager manager = CompanionManager.Instance;

        CompanionController active = manager != null
            ? manager.ActiveCompanion
            : null;

        // 出撃中のロボットが切り替わったときに参照を更新
        if (active != currentCompanion)
        {
            currentCompanion = active;

            currentStats = currentCompanion != null
                ? currentCompanion.GetComponent<RobotRuntimeStats>()
                : null;

            currentExperience = currentCompanion != null
                ? currentCompanion.GetComponent<ExperienceComponent>()
                : null;
        }

        // 出撃中のロボットがいない場合
        if (currentCompanion == null)
        {
            if (hudPanel != null)
                hudPanel.SetActive(false);

            return;
        }

        if (hudPanel != null && !hudPanel.activeSelf)
            hudPanel.SetActive(true);

        // ロボット名
        if (companionNameText != null)
        {
            string robotName = currentCompanion.gameObject.name;
            robotName = robotName.Replace("(Clone)", "").Trim();

            companionNameText.text = $"ROBOT : {robotName}";
        }

        // HP表示
        if (currentStats != null)
        {
            int currentHP = currentStats.CurrentHP;
            int maxHP = currentStats.MaxHP;

            if (hpSlider != null)
            {
                hpSlider.minValue = 0;
                hpSlider.maxValue = Mathf.Max(1, maxHP);
                hpSlider.value = currentHP;
            }

            if (hpText != null)
                hpText.text = $"HP : {currentHP} / {maxHP}";
        }

        // レベル表示
        if (currentExperience != null)
        {
            if (levelText != null)
                levelText.text = $"Lv. {currentExperience.Level}";

            UpdateExperienceUI();
        }
        else
        {
            if (expText != null)
                expText.text = "EXP : -";

            if (expSlider != null)
                expSlider.value = 0;
        }
    }

    private void UpdateExperienceUI()
    {
        int currentEXP = currentExperience.Experience;
        int currentLevel = currentExperience.Level;
        LevelData levelData = currentExperience.LevelData;

        if (levelData == null)
        {
            if (expText != null)
                expText.text = $"EXP : {currentEXP}";

            if (expSlider != null)
                expSlider.value = 0;

            return;
        }

        // 次のレベルの情報を取得
        LevelData.LevelInfo nextLevel =
            levelData.GetLevelInfo(currentLevel + 1);

        // 次のレベルが存在しない＝最大レベル
        if (nextLevel == null)
        {
            if (expText != null)
                expText.text = "EXP : MAX";

            if (expSlider != null)
            {
                expSlider.minValue = 0;
                expSlider.maxValue = 1;
                expSlider.value = 1;
            }

            return;
        }

        int nextRequiredEXP = nextLevel.requiredExperience;

        if (expText != null)
        {
            expText.text =
                $"EXP : {currentEXP} / {nextRequiredEXP}";
        }

        if (expSlider != null)
        {
            expSlider.minValue = 0;
            expSlider.maxValue = Mathf.Max(1, nextRequiredEXP);
            expSlider.value = Mathf.Clamp(
                currentEXP,
                0,
                nextRequiredEXP
            );
        }
    }
}