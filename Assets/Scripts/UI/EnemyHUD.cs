
using UnityEngine;
using UnityEngine.UI;

public class EnemyHUD : MonoBehaviour
{
    [Header("敵のステータス")]
    [SerializeField] private EnemyHealth enemyHealth;

    [Header("HP UI")]
    [SerializeField] private Slider hpSlider;

    private void Awake()
    {
        if (enemyHealth == null)
            enemyHealth = GetComponentInParent<EnemyHealth>();
    }

    private void Update()
    {
        if (enemyHealth == null || hpSlider == null)
            return;

        hpSlider.minValue = 0;
        hpSlider.maxValue = Mathf.Max(1, enemyHealth.MaxHP);
        hpSlider.value = enemyHealth.CurrentHP;
    }
}