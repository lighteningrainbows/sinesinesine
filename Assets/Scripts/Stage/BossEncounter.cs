using UnityEngine;
using UnityEngine.Events;

// One authored encounter, independent of ordinary enemy groups and their respawn timer.
public class BossEncounter : MonoBehaviour
{
    [SerializeField] private EnemyController bossPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform player;
    [SerializeField] private Vector2 areaSize = Vector2.one * 30f;
    [SerializeField] private bool spawnOnEnter = true;
    [SerializeField] private UnityEvent onEncounterStarted = new UnityEvent();
    [SerializeField] private UnityEvent onEncounterCleared = new UnityEvent();
    private EnemyHealth bossHealth;
    private bool alerted;

    public Transform Player => player;
    public EnemyController ActiveBoss { get; private set; }
    public bool HasStarted { get; private set; }
    public bool IsCleared { get; private set; }
    public Vector2 AreaSize => areaSize;
    public int RewardExperience => bossHealth != null && bossHealth.EnemyData != null
        ? bossHealth.EnemyData.Experience : 0;

    public bool IsInsideArea(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return Mathf.Abs(local.x) <= areaSize.x * .5f &&
               Mathf.Abs(local.z) <= areaSize.y * .5f;
    }

    public bool ContainsTarget(Transform target)
    {
        return EnemyController.IsLivingTarget(target) && IsInsideArea(target.position);
    }

    private void Update()
    {
        if (IsCleared) return;
        if (!HasStarted)
        {
            if (spawnOnEnter && player != null && ContainsTarget(player)) BeginEncounter();
            return;
        }
        if (ActiveBoss == null || bossHealth == null || bossHealth.CurrentHP <= 0)
        {
            IsCleared = true;
            alerted = false;
            if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Exploration);
            onEncounterCleared.Invoke();
            return;
        }
        CompanionController ally = CompanionManager.Instance != null
            ? CompanionManager.Instance.ActiveCompanion : null;
        bool occupied = ContainsTarget(player) || (ally != null && ContainsTarget(ally.transform));
        if (occupied == alerted) return;
        alerted = occupied;
        if (alerted) ActiveBoss.DetectPlayer();
        else ActiveBoss.LosePlayerDetection();
    }

    // Can also be called from a story event with Spawn On Enter disabled.
    public void BeginEncounter()
    {
        if (HasStarted || IsCleared || bossPrefab == null || spawnPoint == null) return;
        ActiveBoss = Instantiate(bossPrefab, spawnPoint.position, spawnPoint.rotation);
        bossHealth = ActiveBoss.GetComponent<EnemyHealth>();
        ActiveBoss.SetBossEncounter(this);
        HasStarted = true;
        alerted = false;
        if (GameManager.Instance != null) GameManager.Instance.ChangeState(GameState.Battle);
        onEncounterStarted.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .35f, .1f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, .1f, areaSize.y));
    }
}
