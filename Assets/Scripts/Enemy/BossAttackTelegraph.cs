using UnityEngine;

public class BossAttackTelegraph : MonoBehaviour
{
    [SerializeField] private Transform warningBox;
    private EnemyCombat combat;
    private BoxCollider body;
    private Renderer warningRenderer;
    private MaterialPropertyBlock properties;

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
        combat = GetComponent<EnemyCombat>();
        body = GetComponent<BoxCollider>();
        if (warningBox != null) warningRenderer = warningBox.GetComponent<Renderer>();
    }

    private void LateUpdate()
    {
        if (warningBox == null || combat == null) return;
        warningBox.gameObject.SetActive(combat.IsAttacking);
        if (!combat.IsAttacking) return;
        combat.GetAttackBox(out Vector3 center, out Vector3 halfExtents);
        center.y = body.bounds.min.y + .025f;
        warningBox.position = center;
        warningBox.rotation = transform.rotation;
        warningBox.localScale = new Vector3(halfExtents.x * 2f, .025f, halfExtents.z * 2f);
        if (warningRenderer == null) return;
        Color color = combat.IsStrongAttack ? new Color(1f, .12f, .08f) : new Color(1f, .7f, .1f);
        color.a = .3f + combat.AttackProgress * .35f;
        properties.SetColor("_BaseColor", color);
        warningRenderer.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        if (warningBox != null) warningBox.gameObject.SetActive(false);
    }
}
