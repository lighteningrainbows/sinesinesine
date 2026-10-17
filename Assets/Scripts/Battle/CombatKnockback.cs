using UnityEngine;

// Shared by enemies and frozen companions. Distances are horizontal, in metres.
public readonly struct CombatKnockback
{
    public readonly float Speed, Angle, Distance, Pause;
    public CombatKnockback(float speed, float angle, float distance, float pause)
    {
        Speed = speed; Angle = angle; Distance = distance; Pause = pause;
    }
    public static CombatKnockback Normal => new CombatKnockback(2.52f, 33.7f, .3f, .2f);
    public static CombatKnockback Finish => new CombatKnockback(7.07f, 20f, 2f, 0f);
    public float Duration => Distance / (Speed * Mathf.Cos(Angle * Mathf.Deg2Rad));
    public Vector3 Offset(float time, Vector3 direction)
    {
        time = Mathf.Clamp(time, 0f, Duration);
        float angle = Angle * Mathf.Deg2Rad;
        return direction * (Speed * Mathf.Cos(angle) * time) + Vector3.up *
            (Speed * Mathf.Sin(angle) * time + .5f * Physics.gravity.y * time * time);
    }
}
