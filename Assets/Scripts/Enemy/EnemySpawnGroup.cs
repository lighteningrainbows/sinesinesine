using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnGroup : MonoBehaviour
{
    [Header("出現する敵のPrefab")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("敵の出現位置")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("出現数")]
    [SerializeField] private int minEnemyCount = 1;
    [SerializeField] private int maxEnemyCount = 3;

    [Header("再出現までの時間")]
    [SerializeField] private float respawnSeconds = 30f;

    [Header("プレイヤー・仲間感知")]
    [SerializeField] private float detectionRadius = 4.0f;

    private readonly List<GameObject> spawnedEnemies = new();

    private bool isWaitingForRespawn;
    private bool hasStarted;

    private Transform player;

    private bool hasDetectedPlayer;

    public void SetPlayer(Transform targetPlayer)
    {
        player = targetPlayer;
    }

    public void BeginSpawn()
    {
        if (hasStarted)
            return;

        hasStarted = true;

        SpawnEnemies();

        StartCoroutine(MonitorGroup());
    }

    private void Update()
    {
        if (!hasStarted)
            return;

        if (isWaitingForRespawn)
            return;

        // プレイヤーかCompanionのどちらかが
        // 集団の4m以内にいるか確認
        bool targetInsideGroup =
            IsPlayerInsideGroup() ||
            IsCompanionInsideGroup();

        // =====================================================
        // まだ感知していない
        // =====================================================

        if (!hasDetectedPlayer)
        {
            if (targetInsideGroup)
            {
                DetectPlayer();
            }

            return;
        }

        // =====================================================
        // プレイヤーもCompanionも4mより外
        // =====================================================

        if (!targetInsideGroup)
        {
            LosePlayerDetection();
        }
    }

    // =========================================================
    // プレイヤーが4m以内か
    // =========================================================

    private bool IsPlayerInsideGroup()
    {
        if (player == null)
            return false;

        if (!player.gameObject.activeInHierarchy)
            return false;

        float distance =
            HorizontalDistance(
                transform.position,
                player.position
            );

        return distance <= detectionRadius;
    }

    // =========================================================
    // 現在のCompanionが4m以内か
    // =========================================================

    private bool IsCompanionInsideGroup()
    {
        CompanionManager manager =
            CompanionManager.Instance;

        if (manager == null)
            return false;

        CompanionController companion =
            manager.ActiveCompanion;

        if (companion == null)
            return false;

        if (!companion.IsDeployed)
            return false;

        if (!companion.gameObject.activeInHierarchy)
            return false;

        float distance =
            HorizontalDistance(
                transform.position,
                companion.transform.position
            );

        return distance <= detectionRadius;
    }

    // =========================================================
    // 感知開始
    // =========================================================

    private void DetectPlayer()
    {
        hasDetectedPlayer = true;

        Debug.Log(
            $"{name} : プレイヤーまたはCompanionを感知しました。"
        );

        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy == null)
                continue;

            if (!enemy.activeInHierarchy)
                continue;

            EnemyController controller =
                enemy.GetComponent<EnemyController>();

            if (controller == null)
                continue;

            controller.SetSpawnGroup(this);

            controller.DetectPlayer();
        }
    }

    // =========================================================
    // 感知解除
    // =========================================================

    private void LosePlayerDetection()
    {
        hasDetectedPlayer = false;

        Debug.Log(
            $"{name} : プレイヤーとCompanionが感知範囲外へ出ました。敵が帰還します。"
        );

        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy == null)
                continue;

            if (!enemy.activeInHierarchy)
                continue;

            EnemyController controller =
                enemy.GetComponent<EnemyController>();

            if (controller == null)
                continue;

            controller.LosePlayerDetection();
        }
    }

    // =========================================================
    // 敵スポーン
    // =========================================================

    private void SpawnEnemies()
    {
        if (enemyPrefabs == null ||
            enemyPrefabs.Length == 0)
        {
            Debug.LogWarning(
                $"{name} : 敵Prefabが設定されていません。"
            );

            return;
        }

        if (spawnPoints == null ||
            spawnPoints.Length == 0)
        {
            Debug.LogWarning(
                $"{name} : SpawnPointが設定されていません。"
            );

            return;
        }

        spawnedEnemies.Clear();

        int minCount =
            Mathf.Clamp(
                minEnemyCount,
                1,
                spawnPoints.Length
            );

        int maxCount =
            Mathf.Clamp(
                maxEnemyCount,
                minCount,
                spawnPoints.Length
            );

        int count =
            Random.Range(
                minCount,
                maxCount + 1
            );

        List<Transform> availablePoints =
            new List<Transform>(spawnPoints);

        for (int i = 0; i < count; i++)
        {
            int pointIndex =
                Random.Range(
                    0,
                    availablePoints.Count
                );

            Transform point =
                availablePoints[pointIndex];

            availablePoints.RemoveAt(pointIndex);

            GameObject prefab =
                enemyPrefabs[
                    Random.Range(
                        0,
                        enemyPrefabs.Length
                    )
                ];

            GameObject enemy =
                Instantiate(
                    prefab,
                    point.position,
                    point.rotation
                );

            spawnedEnemies.Add(enemy);

            EnemyController controller =
                enemy.GetComponent<EnemyController>();

            if (controller != null)
            {
                controller.SetSpawnGroup(this);

                if (hasDetectedPlayer)
                {
                    controller.DetectPlayer();
                }
            }
        }

        Debug.Log(
            $"{name} : 敵を{count}体スポーンしました。"
        );
    }

    // =========================================================
    // 全滅監視
    // =========================================================

    private IEnumerator MonitorGroup()
    {
        while (true)
        {
            yield return
                new WaitForSeconds(0.5f);

            if (isWaitingForRespawn)
                continue;

            if (spawnedEnemies.Count == 0)
                continue;

            bool allDefeated = true;

            foreach (GameObject enemy in spawnedEnemies)
            {
                if (enemy != null &&
                    enemy.activeInHierarchy)
                {
                    allDefeated = false;
                    break;
                }
            }

            if (allDefeated)
            {
                isWaitingForRespawn = true;

                Debug.Log(
                    $"{name} : 集団全滅。{respawnSeconds}秒後に再出現します。"
                );

                yield return
                    new WaitForSeconds(
                        respawnSeconds
                    );

                hasDetectedPlayer = false;

                SpawnEnemies();

                isWaitingForRespawn = false;
            }
        }
    }

    // =========================================================
    // 水平方向の距離
    // =========================================================

    private float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    // =========================================================
    // Gizmo
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        // 集団の感知範囲
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRadius
        );

        // SpawnPoint
        if (spawnPoints == null)
            return;

        Gizmos.color = Color.red;

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.DrawWireSphere(
                    point.position,
                    0.4f
                );
            }
        }
    }
}