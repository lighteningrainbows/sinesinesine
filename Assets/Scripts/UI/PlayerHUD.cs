
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("プレイヤー参照")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private ExperienceComponent experienceComponent;

    [Header("HP UI")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text hpText;

    [Header("レベル・経験値UI")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text expText;

    private void Update()
    {
        UpdateHP();
        UpdateLevelAndEXP();
    }

    private void UpdateHP()
    {
        if (playerHealth == null) return;

        int currentHP = playerHealth.CurrentHP;
        int maxHP = playerHealth.MaxHP;

        if (hpSlider != null)
        {
            hpSlider.minValue = 0;
            hpSlider.maxValue = Mathf.Max(1, maxHP);
            hpSlider.value = currentHP;
        }

        if (hpText != null)
        {
            hpText.text = $"HP : {currentHP} / {maxHP}";
        }
    }

    private void UpdateLevelAndEXP()
    {
        if (experienceComponent == null) return;

        if (levelText != null)
        {
            levelText.text = $"Lv. {experienceComponent.Level}";
        }

        if (expText != null)
        {
            expText.text = $"EXP : {experienceComponent.Experience}";
        }
    }
}