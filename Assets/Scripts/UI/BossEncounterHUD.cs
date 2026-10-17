using UnityEngine;
using UnityEngine.UI;

public class BossEncounterHUD : MonoBehaviour
{
    [SerializeField] private BossEncounter encounter;
    [SerializeField] private CanvasGroup panel;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Text hpText;
    [SerializeField] private Text statusText;
    [SerializeField] private Text rewardText;

    private void Update()
    {
        if (encounter == null || panel == null) return;
        bool visible = encounter.HasStarted && encounter.Player != null &&
            encounter.IsInsideArea(encounter.Player.position);
        panel.alpha = visible ? 1f : 0f;
        panel.blocksRaycasts = false;
        panel.interactable = false;
        if (!visible) return;
        EnemyController boss = encounter.ActiveBoss;
        EnemyHealth health = boss != null ? boss.GetComponent<EnemyHealth>() : null;
        int hp = health != null ? health.CurrentHP : 0;
        int maxHP = health != null ? health.MaxHP : 400;
        hpSlider.maxValue = maxHP;
        hpSlider.value = hp;
        hpText.text = $"{hp} / {maxHP}";
        if (encounter.IsCleared)
        {
            statusText.text = "AREA CLEAR";
            rewardText.text = $"ALL ROBOTS  EXP +{encounter.RewardExperience}";
            return;
        }
        EnemyCombat combat = boss != null ? boss.GetComponent<EnemyCombat>() : null;
        statusText.text = combat != null && combat.IsAttacking
            ? combat.IsStrongAttack ? "HEAVY STRIKE" : "QUICK STRIKE"
            : boss != null && boss.IsStaggered ? "STAGGERED" : "SPIDER GUARDIAN";
        rewardText.text = "INSECT TYPE";
    }
}
