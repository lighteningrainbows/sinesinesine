using UnityEngine;

// Temporarily releases a fixed companion, without routing the strike through HP damage.
[RequireComponent(typeof(Rigidbody), typeof(CompanionController))]
public class FrozenCompanionKnockback : MonoBehaviour
{
    private Rigidbody body;
    private CompanionController companion;
    private Collider bodyCollider;
    private CombatKnockback profile;
    private Vector3 direction;
    private float elapsed;
    private int phase;
    public bool IsActive { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        companion = GetComponent<CompanionController>();
        bodyCollider = GetComponent<Collider>();
    }

    public void ReceiveHit(Vector3 origin, CombatKnockback knockback)
    {
        if (!companion.IsFrozenPrevious) return;
        direction = body.position - origin;
        direction.y = 0f;
        direction = direction.sqrMagnitude > .001f ? direction.normalized : -transform.forward;
        profile = knockback;
        phase = 0;
        elapsed = 0f;
        IsActive = true;
        body.isKinematic = false;
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!IsActive) return;
        if (!companion.IsFrozenPrevious) { Cancel(); return; }
        float dt = Time.fixedDeltaTime;
        if (phase == 0)
        {
            if (elapsed >= profile.Duration)
            {
                phase = 1; elapsed = 0f; body.linearVelocity = Vector3.zero;
                return;
            }
            float end = Mathf.Min(elapsed + dt, profile.Duration);
            body.linearVelocity = (profile.Offset(end, direction) - profile.Offset(elapsed, direction)) / dt;
            elapsed = end;
        }
        else if (phase == 1)
        {
            body.linearVelocity = Vector3.zero;
            elapsed += dt;
            if (elapsed >= profile.Pause) { phase = 2; body.useGravity = true; }
        }
        else
        {
            Vector3 velocity = body.linearVelocity;
            velocity.x = velocity.z = 0f;
            body.linearVelocity = velocity;
            if (velocity.y <= 0f && IsGrounded())
            {
                Cancel();
                body.useGravity = false;
                body.isKinematic = true;
            }
        }
    }

    private bool IsGrounded()
    {
        if (bodyCollider == null) return false;
        Bounds bounds = bodyCollider.bounds;
        foreach (RaycastHit hit in Physics.RaycastAll(bounds.center, Vector3.down,
                     bounds.extents.y + .04f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.attachedRigidbody != body && hit.normal.y > .5f) return true;
        return false;
    }

    public void Cancel()
    {
        if (IsActive && body != null && !body.isKinematic) body.linearVelocity = Vector3.zero;
        IsActive = false;
    }

    private void OnDisable()
    {
        if (!IsActive) return;
        Cancel();
        if (companion != null && companion.IsFrozenPrevious)
        {
            body.useGravity = false;
            body.isKinematic = true;
        }
    }
}
