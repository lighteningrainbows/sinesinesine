using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("プレイヤーデータ")]
    [SerializeField] private PlayerData playerData;

    public PlayerData PlayerData => playerData;

    public int CurrentHP { get; private set; }

    public int MaxHP
    {
        get
        {
            if (playerData == null)
                return 0;

            return playerData.MaxHP;
        }
    }

    private void Awake()
    {
        if (playerData != null)
        {
            CurrentHP = playerData.MaxHP;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0)
            return;

        CurrentHP -= damage;

        if (CurrentHP < 0)
            CurrentHP = 0;

        Debug.Log(
            $"Player HP : {CurrentHP} / {MaxHP}"
        );

        if (CurrentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("Player Defeated");
    }

    public void Heal(int amount)
    {
        if (amount <= 0)
            return;

        CurrentHP += amount;

        if (CurrentHP > MaxHP)
        {
            CurrentHP = MaxHP;
        }
    }
}