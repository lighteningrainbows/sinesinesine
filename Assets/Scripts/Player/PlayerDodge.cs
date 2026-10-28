using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerDodge : MonoBehaviour
{
    public enum DodgePhase { Idle, Moving, Falling, Recovering }

    [SerializeField, Min(0f)] private float dodgeDistance = 2.5f;
    [SerializeField, Min(.01f)] private float motionDuration = .5f;
    [SerializeField, Min(0f)] private float landingRecovery = .2f;
    [SerializeField, Range(.01f, 1f)] private float lateralThreshold = .35f;
    [Header("Temporary hop animation (height is not specified in the workbook)")]
    [SerializeField, Min(0f)] private float hopHeight = .15f;
    [SerializeField] private bool showFeedback = true;

    private Rigidbody body;
    private Collider bodyCollider;
    private PlayerHealth health;
    private Vector3 direction;
    private float elapsed;
    private bool savedGravity;
    private bool startedAirborne;
    private MeshRenderer originalRenderer;
    private GameObject animatedModel;
    private bool rendererWasEnabled;

    public DodgePhase Phase { get; private set; }
    public bool ControlsMovement => Phase != DodgePhase.Idle;
    public bool IsDodging => ControlsMovement;
    public bool IsMoving => Phase == DodgePhase.Moving;
    public float Distance => dodgeDistance;
    public float MotionDuration => motionDuration;
    public float LandingRecovery => landingRecovery;
    public float LateralThreshold => lateralThreshold;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        health = GetComponent<PlayerHealth>();
        originalRenderer = GetComponent<MeshRenderer>();
    }

    // Dodge has no invulnerability or cooldown. Only a fresh input after recovery is required.
    public bool RequestDodge(float horizontalInput)
    {
        if (!isActiveAndEnabled || ControlsMovement || body.isKinematic ||
            Mathf.Abs(horizontalInput) < lateralThreshold ||
            (health != null && health.CurrentHP <= 0)) return false;
        PlayerCombat combat = GetComponent<PlayerCombat>();
        if (combat != null) combat.CancelCombo();
        savedGravity = body.useGravity;
        startedAirborne = !IsGrounded();
        direction = body.rotation * Vector3.right * Mathf.Sign(horizontalInput);
        direction.y = 0f;
        direction.Normalize();
        elapsed = 0f;
        Phase = DodgePhase.Moving;
        body.useGravity = false;
        body.linearVelocity = Vector3.zero;
        BeginFeedback();
        return true;
    }

    private void FixedUpdate()
    {
        if (!ControlsMovement) return;
        if (health != null && health.CurrentHP <= 0) { CancelDodge(); return; }
        float dt = Time.fixedDeltaTime;
        if (Phase == DodgePhase.Moving)
        {
            // Finish on the following tick so the last displacement is actually simulated.
            if (elapsed >= motionDuration) BeginFalling();
            else
            {
                float end = Mathf.Min(elapsed + dt, motionDuration);
                float distance = dodgeDistance * (end - elapsed) / motionDuration;
                if (IsBlocked(direction, distance) ||
                    (HopOffset(end) > HopOffset(elapsed) &&
                     IsBlocked(Vector3.up, HopOffset(end) - HopOffset(elapsed))))
                {
                    BeginFalling();
                }
                else
                {
                    body.linearVelocity = direction * (distance / dt) +
                        Vector3.up * ((HopOffset(end) - HopOffset(elapsed)) / dt);
                    elapsed = end;
                    return;
                }
            }
        }
        StopHorizontal();
        if (Phase == DodgePhase.Falling)
        {
            if (body.linearVelocity.y <= 0f && IsGrounded())
            {
                Phase = DodgePhase.Recovering;
                elapsed = 0f;
            }
        }
        else if (Phase == DodgePhase.Recovering)
        {
            if (!IsGrounded()) { BeginFalling(); return; }
            elapsed += dt;
            if (elapsed >= landingRecovery) FinishDodge();
        }
    }

    private float HopOffset(float time)
    {
        float t = Mathf.Clamp01(time / motionDuration);
        return 4f * hopHeight * t * (1f - t);
    }

    private bool IsBlocked(Vector3 travelDirection, float distance)
    {
        if (distance <= 0f) return false;
        foreach (RaycastHit obstacle in body.SweepTestAll(travelDirection, distance + .02f,
                     QueryTriggerInteraction.Ignore))
        {
            if (travelDirection.y == 0f && obstacle.normal.y > .5f) continue;
            if (Vector3.Dot(obstacle.normal, travelDirection) < -.01f) return true;
        }
        return false;
    }

    private bool IsGrounded()
    {
        if (bodyCollider == null) return false;
        Bounds bounds = bodyCollider.bounds;
        foreach (RaycastHit ground in Physics.RaycastAll(bounds.center, Vector3.down,
                     bounds.extents.y + .04f, ~0, QueryTriggerInteraction.Ignore))
            if (ground.collider.attachedRigidbody != body && ground.normal.y > .5f) return true;
        return false;
    }

    private void OnCollisionEnter(Collision collision) => CheckBlockingContact(collision);
    private void OnCollisionStay(Collision collision) => CheckBlockingContact(collision);

    private void CheckBlockingContact(Collision collision)
    {
        if (!IsMoving) return;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y < .5f && Vector3.Dot(normal, direction) < -.1f)
            {
                BeginFalling();
                return;
            }
        }
    }

    private void BeginFalling()
    {
        Phase = DodgePhase.Falling;
        elapsed = 0f;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        // An aerial dodge releases controls as soon as its motion ends, including on collision.
        if (startedAirborne) FinishDodge();
    }

    private void StopHorizontal()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private void FinishDodge()
    {
        StopHorizontal();
        body.useGravity = savedGravity;
        Phase = DodgePhase.Idle;
        EndFeedback();
    }

    public void CancelDodge()
    {
        if (!ControlsMovement) return;
        if (body != null)
        {
            body.useGravity = savedGravity;
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        }
        Phase = DodgePhase.Idle;
        EndFeedback();
    }

    private void BeginFeedback()
    {
        if (!showFeedback || originalRenderer == null) return;
        MeshFilter mesh = GetComponent<MeshFilter>();
        if (mesh == null || mesh.sharedMesh == null) return;
        if (animatedModel == null)
        {
            animatedModel = new GameObject("DodgeModel");
            animatedModel.transform.SetParent(transform, false);
            animatedModel.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            animatedModel.AddComponent<MeshRenderer>().sharedMaterials = originalRenderer.sharedMaterials;
        }
        rendererWasEnabled = originalRenderer.enabled;
        originalRenderer.enabled = false;
        animatedModel.SetActive(rendererWasEnabled);
    }

    private void LateUpdate()
    {
        if (ControlsMovement && health != null && health.CurrentHP <= 0) CancelDodge();
        if (animatedModel == null || !ControlsMovement) return;
        float tilt = IsMoving ? Mathf.Sin(elapsed / motionDuration * Mathf.PI) * 18f : 0f;
        float side = Vector3.Dot(direction, transform.right);
        animatedModel.transform.localRotation = Quaternion.Euler(0, 0, -side * tilt);
    }

    private void EndFeedback()
    {
        if (animatedModel == null) return;
        originalRenderer.enabled = rendererWasEnabled;
        animatedModel.SetActive(false);
    }

    private void OnGUI()
    {
        if (!showFeedback || !ControlsMovement) return;
        GUI.Box(new Rect(Screen.width * .5f - 80, Screen.height - 65, 160, 38),
            Phase == DodgePhase.Recovering ? "RECOVER" : "DODGE");
    }

    private void OnDisable() => CancelDodge();
}
