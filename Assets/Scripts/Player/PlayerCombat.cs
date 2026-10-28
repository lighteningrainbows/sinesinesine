using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerDodge))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private PlayerData playerData;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float attackHeight = 4f;
    [Header("Combo timing (seconds)")]
    [SerializeField, Min(.1f)] private float normalDuration = .32f;
    [SerializeField, Min(.1f)] private float finishDuration = .42f;
    [SerializeField, Range(.1f, .9f)] private float impactFraction = .5f;
    [SerializeField, Min(.1f)] private float chainWindow = .65f;
    [SerializeField, Min(0f)] private float finishRecovery = .45f;
    [Header("Movement and aim assist")]
    [SerializeField, Min(0f)] private float stepDistance = .5f;
    [SerializeField] private Vector3 assistSize = new Vector3(2f, 4f, 2f);
    [SerializeField] private float airLiftDistance = .2f;
    [SerializeField] private float airLiftDuration = .2f;
    [SerializeField] private bool showFeedback = true;

    private Rigidbody body;
    private Collider bodyCollider;
    private PlayerHealth health;
    private PlayerDodge dodge;
    private float elapsed, remainingStep, chainUntil, readyAt;
    private bool swinging, lifting, queued, hit, airborne, savedGravity;
    private int comboStep;
    private Vector3 attackDirection;
    private readonly HashSet<Component> damagedTargets = new HashSet<Component>();
    private LineRenderer trail;
    private Material trailMaterial;

    public bool IsAttacking => swinging || lifting;
    public bool ControlsMovement => IsAttacking;
    public int ComboStep => comboStep;
    public bool HasBufferedAttack => queued;
    public float AttackProgress => swinging ? Mathf.Clamp01(elapsed / SwingDuration) : 0f;
    private float SwingDuration => comboStep == 3 ? finishDuration : normalDuration;
    private int TargetMask => targetLayer.value | LayerMask.GetMask("Enemy", "Companion");

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        health = GetComponent<PlayerHealth>();
        dodge = GetComponent<PlayerDodge>();
    }

    private void Update()
    {
        if (health != null && health.CurrentHP <= 0) { CancelCombo(); return; }
        if (!IsAttacking && comboStep > 0 && Time.time > chainUntil) comboStep = 0;
        if ((Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)) RequestAttack();
        UpdateFeedback();
    }

    // One button press starts one swing. Only one follow-up may be buffered per swing.
    public bool RequestAttack()
    {
        if (!isActiveAndEnabled || playerData == null || Time.time < readyAt ||
            (dodge != null && dodge.ControlsMovement) ||
            (health != null && health.CurrentHP <= 0)) return false;
        if (IsAttacking)
        {
            if (comboStep >= 3 || queued) return false;
            queued = true;
            return true;
        }
        if (Time.time > chainUntil) comboStep = 0;
        BeginSwing();
        return true;
    }

    private void BeginSwing()
    {
        comboStep++;
        swinging = true;
        lifting = queued = hit = false;
        elapsed = 0f;
        savedGravity = body.useGravity;
        airborne = !IsGrounded();
        if (airborne) body.useGravity = false;
        AimAtTarget();
        attackDirection = body.rotation * Vector3.forward;
        remainingStep = comboStep == 3 ? 0f : stepDistance;
        StopHorizontal();
        if (airborne) body.linearVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!IsAttacking) return;
        if (health != null && health.CurrentHP <= 0) { CancelCombo(); return; }
        float dt = Time.fixedDeltaTime;
        StopHorizontal();
        if (airborne) body.linearVelocity = Vector3.zero;
        if (lifting)
        {
            if (elapsed >= airLiftDuration) { CompleteSwing(); return; }
            float liftStep = Mathf.Min(dt, Mathf.Max(0f, airLiftDuration - elapsed));
            float distance = airLiftDistance * liftStep / Mathf.Max(.01f, airLiftDuration);
            body.linearVelocity = Vector3.up * SafeDistance(Vector3.up, distance) / dt;
            elapsed += dt;
            return;
        }
        float impactAt = SwingDuration * impactFraction;
        if (!hit && remainingStep > 0f)
        {
            float distance = Mathf.Min(remainingStep, stepDistance * dt / Mathf.Max(.01f, impactAt));
            distance = SafeDistance(attackDirection, distance);
            Vector3 velocity = body.linearVelocity;
            velocity += attackDirection * (distance / dt);
            body.linearVelocity = velocity;
            remainingStep -= distance;
        }
        // The last movement step is simulated before testing the hit box.
        if (!hit && elapsed >= impactAt)
        {
            StopHorizontal();
            Attack();
            hit = true;
        }
        elapsed += dt;
        if (elapsed >= SwingDuration)
        {
            if (airborne && airLiftDuration > 0f)
            {
                swinging = false; lifting = true; elapsed = 0f;
            }
            else CompleteSwing();
        }
    }

    private void CompleteSwing()
    {
        bool next = queued && comboStep < 3;
        swinging = lifting = queued = false;
        body.useGravity = savedGravity;
        if (airborne) body.linearVelocity = Vector3.zero;
        StopHorizontal();
        chainUntil = Time.time + chainWindow;
        if (comboStep == 3)
        {
            comboStep = 0;
            readyAt = Time.time + finishRecovery;
        }
        if (next) BeginSwing();
    }

    public void CancelCombo()
    {
        if (IsAttacking && body != null)
        {
            body.useGravity = savedGravity;
            body.linearVelocity = Vector3.zero;
        }
        swinging = lifting = queued = false;
        comboStep = 0;
        chainUntil = readyAt = 0f;
        if (trail != null) trail.enabled = false;
    }

    private bool IsGrounded()
    {
        if (bodyCollider == null) return false;
        Bounds bounds = bodyCollider.bounds;
        foreach (RaycastHit ground in Physics.RaycastAll(bounds.center, Vector3.down,
                     bounds.extents.y + .08f, ~0, QueryTriggerInteraction.Ignore))
            if (ground.collider.attachedRigidbody != body && ground.normal.y > .5f) return true;
        return false;
    }

    private float SafeDistance(Vector3 direction, float distance)
    {
        foreach (RaycastHit obstacle in body.SweepTestAll(direction, distance + .04f,
                     QueryTriggerInteraction.Ignore))
            if (Vector3.Dot(obstacle.normal, direction) < -.01f)
                distance = Mathf.Min(distance, Mathf.Max(0f, obstacle.distance - .04f));
        return distance;
    }

    private Component ValidTarget(Collider collider)
    {
        EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
        if (enemy != null && enemy.CurrentHP > 0) return enemy;
        CompanionController ally = collider.GetComponentInParent<CompanionController>();
        if (ally != null && (ally.IsFrozenPrevious || ally.IsDeployed))
        {
            IDamageable stats = ally.GetComponent<IDamageable>();
            if (stats == null || stats.CurrentHP > 0) return ally;
        }
        return null;
    }

    private void AimAtTarget()
    {
        Vector3 center = body.position + body.rotation * Vector3.forward * (assistSize.z * .5f);
        Component best = null;
        Vector3 bestPosition = Vector3.zero;
        float bestDistance = float.PositiveInfinity;
        foreach (Collider candidate in Physics.OverlapBox(center, assistSize * .5f,
                     body.rotation, TargetMask, QueryTriggerInteraction.Ignore))
        {
            Component target = ValidTarget(candidate);
            if (target == null || !HasClearPath(candidate, target)) continue;
            Vector3 position = candidate.bounds.center;
            float distance = (position - center).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = target;
                bestPosition = position;
            }
        }
        if (best == null) return;
        // Collider poses already reflect physics moves; Transform can update a frame later.
        Vector3 direction = bestPosition - body.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > .001f) body.rotation = Quaternion.LookRotation(direction);
    }

    private void Attack()
    {
        if (playerData == null) return;
        CombatKnockback reaction = comboStep == 3 ? CombatKnockback.Finish : CombatKnockback.Normal;
        Vector3 size = new Vector3(playerData.AttackArea.x, attackHeight, playerData.AttackArea.y);
        damagedTargets.Clear();
        Vector3 direction = swinging ? attackDirection : body.rotation * Vector3.forward;
        foreach (Collider collider in Physics.OverlapBox(body.position + direction,
                     size * .5f, body.rotation, TargetMask, QueryTriggerInteraction.Ignore))
        {
            Component target = ValidTarget(collider);
            if (target == null || !HasClearPath(collider, target) || !damagedTargets.Add(target)) continue;
            if (target is CompanionController ally)
            {
                FrozenCompanionKnockback knockback = ally.GetComponent<FrozenCompanionKnockback>();
                if (knockback == null) knockback = ally.gameObject.AddComponent<FrozenCompanionKnockback>();
                knockback.ReceiveHit(transform.position, reaction);
            }
            else if (BattleSystem.Instance != null)
                BattleSystem.Instance.Attack(gameObject, target.gameObject,
                    playerData.Attack * (comboStep == 3 ? 2 : 1), null, reaction);
        }
    }

    private void StopHorizontal()
    {
        Vector3 velocity = body.linearVelocity;
        velocity.x = velocity.z = 0f;
        body.linearVelocity = velocity;
    }

    private bool HasClearPath(Collider collider, Component target)
    {
        Vector3 origin = bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        Vector3 delta = collider.bounds.center - origin;
        foreach (RaycastHit obstacle in Physics.RaycastAll(origin, delta.normalized,
                     delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (obstacle.collider.attachedRigidbody == body) continue;
            if (obstacle.collider.transform.IsChildOf(target.transform)) continue;
            return false;
        }
        return true;
    }

    private void UpdateFeedback()
    {
        if (!showFeedback || !swinging)
        {
            if (trail != null) trail.enabled = false;
            return;
        }
        if (trail == null)
        {
            var effect = new GameObject("ComboSwipe");
            effect.transform.SetParent(transform, false);
            trail = effect.AddComponent<LineRenderer>();
            trail.useWorldSpace = false;
            trail.positionCount = 12;
            trail.widthMultiplier = .07f;
            trailMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            trail.sharedMaterial = trailMaterial;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        trail.enabled = true;
        Color color = comboStep == 3 ? new Color(1f, .45f, .08f) : Color.cyan;
        trailMaterial.SetColor("_BaseColor", color);
        float progress = AttackProgress;
        float sign = comboStep == 2 ? -1f : 1f;
        for (int i = 0; i < trail.positionCount; i++)
        {
            float angle = (-70f + 140f * progress - i * 4f) * sign * Mathf.Deg2Rad;
            float radius = comboStep == 3 ? 1.3f : 1f;
            trail.SetPosition(i, new Vector3(Mathf.Sin(angle) * radius, .15f,
                Mathf.Cos(angle) * radius));
        }
    }

    private void OnGUI()
    {
        if (!showFeedback || comboStep == 0) return;
        GUI.Box(new Rect(Screen.width - 245, Screen.height - 85, 225, 55),
            (comboStep == 1 ? "[1]   2   FINISH" : comboStep == 2 ? "1   [2]   FINISH" : "1   2   [FINISH]") +
            "\nJ / Y" + (queued ? "  +" : ""));
    }

    private void OnDisable() => CancelCombo();
    private void OnDestroy() { if (trailMaterial != null) Destroy(trailMaterial); }

    private void OnDrawGizmosSelected()
    {
        if (playerData == null) return;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(Vector3.forward, new Vector3(playerData.AttackArea.x, attackHeight, playerData.AttackArea.y));
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(Vector3.forward * assistSize.z * .5f, assistSize);
    }
}
