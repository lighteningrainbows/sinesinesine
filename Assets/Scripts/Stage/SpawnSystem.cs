using UnityEngine;

public class SpawnSystem : MonoBehaviour
{
    [Header("このエリアに配置する敵集団")]
    [SerializeField] private EnemySpawnGroup[] enemyGroups;

    private bool hasStarted;

    private Transform player;

    /// <summary>
    /// プレイヤーを設定
    /// </summary>
    public void SetPlayer(
        Transform playerTransform)
    {
        player = playerTransform;
    }

    /// <summary>
    /// このエリアの敵出現を開始
    /// </summary>
    public void BeginSpawn()
    {
        if (hasStarted)
            return;

        hasStarted = true;

        if (enemyGroups == null ||
            enemyGroups.Length == 0)
        {
            Debug.LogWarning(
                $"{name} : 敵集団が設定されていません。"
            );

            return;
        }

        foreach (
            EnemySpawnGroup group
            in enemyGroups)
        {
            if (group == null)
                continue;

            // プレイヤーを集団へ渡す
            group.SetPlayer(player);

            group.BeginSpawn();
        }

        Debug.Log(
            $"{name} : このエリアの敵出現を開始しました。"
        );
    }
}