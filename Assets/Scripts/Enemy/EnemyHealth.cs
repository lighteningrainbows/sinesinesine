using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("“Gƒf[ƒ^")]
    [SerializeField] private EnemyData enemyData;

    public EnemyData EnemyData => enemyData;

    public int CurrentHP { get; private set; }

    public int MaxHP
    {
        get
        {
            if (enemyData == null)
                return 0;

            return enemyData.MaxHP;
        }
    }

    private bool isDefeated;

    private void Awake()
    {
        if (enemyData != null)
        {
            CurrentHP = enemyData.MaxHP;
        }

        isDefeated = false;
    }

    public void TakeDamage(int damage)
    {
        if (isDefeated)
            return;

        if (damage <= 0)
            return;

        CurrentHP -= damage;

        if (CurrentHP < 0)
        {
            CurrentHP = 0;
        }

        Debug.Log(
            $"Enemy HP : {CurrentHP} / {MaxHP}"
        );

        if (CurrentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDefeated)
            return;

        isDefeated = true;

        Debug.Log(
            $"{gameObject.name} Defeated"
        );

        EnemyController controller =
            GetComponent<EnemyController>();

        if (controller != null)
        {
            controller.ClearTarget();
        }

        // “GŒ‚”jŽž‚ÉEXP‚ð”z‚é
        if (ExperienceManager.Instance != null)
        {
            ExperienceManager.Instance
                .GiveExperienceFromEnemy(
                    enemyData
                );
        }

        gameObject.SetActive(false);
    }
}