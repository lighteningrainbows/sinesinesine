using UnityEngine;

[CreateAssetMenu(fileName = "BossAttackSettings", menuName = "Game/Data/Boss Attack Settings")]
public class BossAttackSettings : ScriptableObject
{
    [SerializeField, Min(1)] private int strongDamage = 40;
    [SerializeField, Min(.01f)] private float strongDuration = 2f;
    [SerializeField, Min(0f)] private float strongInterval = 4f;
    [SerializeField] private Vector2 strongArea = Vector2.one * 2f;
    [SerializeField, Range(0f, 1f)] private float strongProbability = .3f;

    public int StrongDamage => strongDamage;
    public float StrongDuration => strongDuration;
    public float StrongInterval => strongInterval;
    public Vector2 StrongArea => strongArea;
    public float StrongProbability => strongProbability;

    public bool SelectStrongAttack(bool previousWasStrong)
    {
        return !previousWasStrong && Random.value < strongProbability;
    }
}
