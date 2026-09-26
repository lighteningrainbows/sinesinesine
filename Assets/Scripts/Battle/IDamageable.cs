public interface IDamageable
{
    int CurrentHP { get; }

    int MaxHP { get; }

    void TakeDamage(int damage);
}