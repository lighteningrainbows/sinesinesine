using UnityEngine;

public class StageArea : MonoBehaviour
{
    [Header("エリア設定")]
    [SerializeField]
    private Vector2 areaSize =
        new Vector2(30f, 30f);

    [Header("エリア内の敵出現管理")]
    [SerializeField] private SpawnSystem spawnSystem;

    [Header("エリア侵入判定")]
    [SerializeField] private Transform player;

    private bool playerEntered;

    public Vector2 AreaSize => areaSize;

    public SpawnSystem SpawnSystem =>
        spawnSystem;

    private void Update()
    {
        if (player == null)
            return;

        if (playerEntered)
            return;

        if (IsInsideArea(player.position))
        {
            playerEntered = true;

            EnterArea();
        }
    }

    /// <summary>
    /// 指定された位置がエリア内にあるか判定
    /// </summary>
    public bool IsInsideArea(
        Vector3 position)
    {
        Vector3 center =
            transform.position;

        float halfX =
            areaSize.x * 0.5f;

        float halfZ =
            areaSize.y * 0.5f;

        return
            position.x >= center.x - halfX &&
            position.x <= center.x + halfX &&
            position.z >= center.z - halfZ &&
            position.z <= center.z + halfZ;
    }

    /// <summary>
    /// プレイヤーがエリアに入った
    /// </summary>
    private void EnterArea()
    {
        Debug.Log(
            $"{name} : プレイヤーがエリアに入りました。"
        );

        if (spawnSystem == null)
        {
            Debug.LogWarning(
                $"{name} : SpawnSystemが設定されていません。"
            );

            return;
        }

        // プレイヤーをSpawnSystemへ渡す
        spawnSystem.SetPlayer(player);

        // このエリアの敵を出現させる
        spawnSystem.BeginSpawn();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Vector3 size =
            new Vector3(
                areaSize.x,
                0.1f,
                areaSize.y
            );

        Gizmos.DrawWireCube(
            transform.position,
            size
        );
    }
}