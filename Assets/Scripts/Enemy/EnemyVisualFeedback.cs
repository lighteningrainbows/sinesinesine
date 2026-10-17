using UnityEngine;

// Feedback affects the model only; collision and hit boxes stay stable.
public class EnemyVisualFeedback : MonoBehaviour
{
    [SerializeField] private Transform modelRoot;
    private EnemyController controller;
    private EnemyCombat combat;
    private Vector3 restPosition;

    private void Awake()
    {
        controller = GetComponent<EnemyController>();
        combat = GetComponent<EnemyCombat>();
        if (modelRoot != null) restPosition = modelRoot.localPosition;
    }

    private void LateUpdate()
    {
        if (modelRoot == null || controller == null) return;
        float windup = combat != null ? combat.AttackProgress : 0f;
        float tilt = controller.IsStaggered ? -14f : 0f;
        modelRoot.localRotation = Quaternion.Euler(-20f * windup, 0f, tilt);
        float bob = controller.IsFlying && !controller.IsStaggered
            ? Mathf.Sin(Time.time * 4f) * 0.04f : 0f;
        modelRoot.localPosition = restPosition + Vector3.up * bob;
    }
}
