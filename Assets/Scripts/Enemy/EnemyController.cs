using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyController : MonoBehaviour
{
    public enum EnemyState
    {
        Waiting,
        Chasing,
        Attacking
    }

    [Header("敵データ")]
    [SerializeField] private EnemyData enemyData;

    [Header("飛行設定")]
    [SerializeField] private bool isFlying = false;
    [SerializeField] private float hoverHeight = 2.5f;

    private float hoverY;

    [Header("ターゲット設定")]
    [SerializeField] private float targetSearchInterval = 0.2f;

    [Header("帰還設定")]
    [SerializeField] private float returnDistance = 8.0f;

    private Rigidbody rb;

    private EnemyState currentState;

    private Transform currentTarget;

    private Vector3 waitingPosition;

    private float targetSearchTimer;

    // 所属している敵集団
    private EnemySpawnGroup spawnGroup;

    // 敵集団からプレイヤーを感知しているか
    private bool hasDetectedPlayer;

    public EnemyState CurrentState => currentState;

    public Transform CurrentTarget => currentTarget;

    public Vector3 WaitingPosition => waitingPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (isFlying)
        {
            rb.useGravity = false;
        }

        currentState = EnemyState.Waiting;
    }

    private void Start()
    {
        // 最初の位置を帰還先として保存
        waitingPosition = transform.position;

        if (isFlying)
        {
            hoverY =
                waitingPosition.y + hoverHeight;

            Vector3 position =
                transform.position;

            position.y = hoverY;

            rb.position = position;
        }
    }

    private void Update()
    {
        if (enemyData == null)
            return;

        if (isFlying)
        {
            MaintainHover();
        }

        // まだ集団から感知されていない
        if (!hasDetectedPlayer)
        {
            currentTarget = null;
            currentState = EnemyState.Waiting;

            UpdateWaiting();

            return;
        }

        // =====================================================
        // 帰還距離チェック
        // =====================================================

        float distanceFromHome =
            HorizontalDistance(
                transform.position,
                waitingPosition
            );

        if (distanceFromHome > returnDistance)
        {
            ReturnToWaitingPosition();

            return;
        }

        // =====================================================
        // ターゲット更新
        // =====================================================

        targetSearchTimer -= Time.deltaTime;

        if (targetSearchTimer <= 0.0f)
        {
            targetSearchTimer =
                targetSearchInterval;

            UpdateTarget();
        }

        // =====================================================
        // 状態更新
        // =====================================================

        switch (currentState)
        {
            case EnemyState.Waiting:
                UpdateWaiting();
                break;

            case EnemyState.Chasing:
                UpdateChasing();
                break;

            case EnemyState.Attacking:
                UpdateAttacking();
                break;
        }
    }

    // =========================================================
    // 敵集団
    // =========================================================

    public void SetSpawnGroup(
        EnemySpawnGroup group)
    {
        spawnGroup = group;
    }

    /// <summary>
    /// 敵集団からプレイヤー感知を開始する
    /// </summary>
    public void DetectPlayer()
    {
        hasDetectedPlayer = true;

        targetSearchTimer = 0.0f;

        UpdateTarget();
    }

    /// <summary>
    /// 敵集団からプレイヤーが範囲外に出た
    /// </summary>
    public void LosePlayerDetection()
    {
        hasDetectedPlayer = false;

        currentTarget = null;

        currentState =
            EnemyState.Waiting;

        StopMoving();
    }

    // =========================================================
    // ターゲット
    // =========================================================

    private void UpdateTarget()
    {
        if (!hasDetectedPlayer)
        {
            currentTarget = null;
            currentState = EnemyState.Waiting;

            return;
        }

        // 現在のターゲットがまだ有効ならそのまま
        if (currentTarget != null)
        {
            if (currentTarget.gameObject.activeInHierarchy)
            {
                return;
            }

            currentTarget = null;
        }

        Transform playerTarget =
            FindPlayer();

        Transform companionTarget =
            FindCompanion();

        // どちらもいない
        if (playerTarget == null &&
            companionTarget == null)
        {
            currentTarget = null;

            currentState =
                EnemyState.Waiting;

            return;
        }

        // プレイヤーだけ
        if (playerTarget != null &&
            companionTarget == null)
        {
            currentTarget =
                playerTarget;

            currentState =
                EnemyState.Chasing;

            return;
        }

        // 仲間だけ
        if (playerTarget == null &&
            companionTarget != null)
        {
            currentTarget =
                companionTarget;

            currentState =
                EnemyState.Chasing;

            return;
        }

        // 両方いる場合は近い方
        float playerDistance =
            HorizontalDistance(
                transform.position,
                playerTarget.position
            );

        float companionDistance =
            HorizontalDistance(
                transform.position,
                companionTarget.position
            );

        if (playerDistance <= companionDistance)
        {
            currentTarget =
                playerTarget;
        }
        else
        {
            currentTarget =
                companionTarget;
        }

        currentState =
            EnemyState.Chasing;
    }

    private Transform FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
            return null;

        if (!playerObject.activeInHierarchy)
            return null;

        return playerObject.transform;
    }

    private Transform FindCompanion()
    {
        CompanionManager manager =
            CompanionManager.Instance;

        if (manager == null)
            return null;

        CompanionController companion =
            manager.ActiveCompanion;

        if (companion == null)
            return null;

        if (!companion.IsDeployed)
            return null;

        if (!companion.gameObject.activeInHierarchy)
            return null;

        return companion.transform;
    }

    // =========================================================
    // 待機
    // =========================================================

    private void UpdateWaiting()
    {
        float distance =
            HorizontalDistance(
                transform.position,
                waitingPosition
            );

        if (distance > 0.1f)
        {
            MoveTowards(waitingPosition);
        }
        else
        {
            StopMoving();
        }
    }

    // =========================================================
    // 追跡
    // =========================================================

    private void UpdateChasing()
    {
        if (!hasDetectedPlayer)
        {
            ReturnToWaitingPosition();

            return;
        }

        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;

            currentState =
                EnemyState.Waiting;

            StopMoving();

            return;
        }

        float distance =
            HorizontalDistance(
                transform.position,
                currentTarget.position
            );

        // 攻撃範囲に入った
        if (distance <= enemyData.AttackRange)
        {
            currentState =
                EnemyState.Attacking;

            StopMoving();

            return;
        }

        MoveTowards(
            currentTarget.position
        );
    }

    // =========================================================
    // 攻撃
    // =========================================================

    private void UpdateAttacking()
    {
        if (!hasDetectedPlayer)
        {
            ReturnToWaitingPosition();

            return;
        }

        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;

            currentState =
                EnemyState.Waiting;

            StopMoving();

            return;
        }

        float distance =
            HorizontalDistance(
                transform.position,
                currentTarget.position
            );

        // 攻撃範囲から出た
        if (distance > enemyData.AttackRange)
        {
            currentState =
                EnemyState.Chasing;

            return;
        }

        StopMoving();
    }

    // =========================================================
    // 帰還
    // =========================================================

    private void ReturnToWaitingPosition()
    {
        currentTarget = null;

        currentState =
            EnemyState.Waiting;

        MoveTowards(
            waitingPosition
        );

        float distance =
            HorizontalDistance(
                transform.position,
                waitingPosition
            );

        if (distance <= 0.2f)
        {
            StopMoving();

            // 帰還完了
            hasDetectedPlayer = false;

            currentState =
                EnemyState.Waiting;
        }
    }

    // =========================================================
    // 移動
    // =========================================================

    private void MoveTowards(
        Vector3 targetPosition)
    {
        if (enemyData == null)
            return;

        Vector3 direction =
            targetPosition -
            transform.position;

        // 水平方向だけで移動方向を決める
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            StopMoving();

            return;
        }

        direction.Normalize();

        Vector3 velocity =
            direction *
            enemyData.MoveSpeed;

        if (isFlying)
        {
            float heightDifference =
                hoverY -
                rb.position.y;

            velocity.y =
                Mathf.Clamp(
                    heightDifference * 2f,
                    -enemyData.MoveSpeed,
                    enemyData.MoveSpeed
                );
        }
        else
        {
            velocity.y =
                rb.linearVelocity.y;
        }

        rb.linearVelocity =
            velocity;

        transform.rotation =
            Quaternion.LookRotation(
                direction
            );
    }

    // =========================================================
    // 停止
    // =========================================================

    private void StopMoving()
    {
        if (rb == null)
            return;

        Vector3 velocity =
            rb.linearVelocity;

        velocity.x = 0.0f;
        velocity.z = 0.0f;

        if (!isFlying)
        {
            velocity.y =
                rb.linearVelocity.y;
        }

        rb.linearVelocity =
            velocity;
    }

    // =========================================================
    // ターゲット解除
    // =========================================================

    public void ClearTarget()
    {
        currentTarget = null;

        currentState =
            EnemyState.Waiting;

        StopMoving();
    }

    // =========================================================
    // 飛行
    // =========================================================

    private void MaintainHover()
    {
        if (rb == null ||
            enemyData == null)
            return;

        float heightDifference =
            hoverY -
            rb.position.y;

        Vector3 velocity =
            rb.linearVelocity;

        velocity.y =
            Mathf.Clamp(
                heightDifference * 2f,
                -enemyData.MoveSpeed,
                enemyData.MoveSpeed
            );

        rb.linearVelocity =
            velocity;
    }

    // =========================================================
    // 距離
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
    // デバッグ
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (enemyData == null)
            return;

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            enemyData.AttackRange
        );
    }
}