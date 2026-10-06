using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CompanionController : MonoBehaviour
{
    public enum CompanionState
    {
        Standby,
        Following,
        Chasing,
        Attacking,
        Frozen
    }

    [Header("ロボットデータ")]
    [SerializeField] private RobotData robotData;

    public RobotData RobotData => robotData;

    [Header("追従対象")]
    [SerializeField] private Transform player;

    [Header("飛行設定")]
    [SerializeField] private bool isFlying = false;
    [SerializeField] private float hoverHeight = 2.5f;

    private float hoverY;
    private bool originalUseGravity;

    [Header("敵検索")]
    [SerializeField] private float searchRadius = 10.0f;

    [Header("追従設定")]
    [SerializeField] private float followDistance = 2.5f;
    [SerializeField] private float warpDistance = 15.0f;

    private Rigidbody rb;

    private CompanionState currentState;
    private Transform currentTarget;

    private bool isDeployed;

    // ロックオンによる強制ターゲット
    private Transform lockedTarget;

    // 交代前の味方として固定されているか
    private bool isFrozenPrevious;

    public bool IsDeployed => isDeployed;
    public bool IsFrozenPrevious => isFrozenPrevious;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        originalUseGravity = rb.useGravity;

        currentState = CompanionState.Standby;
        isDeployed = false;
        isFrozenPrevious = false;
    }

    private void Update()
    {
        // 前回の味方として固定中
        if (isFrozenPrevious)
        {
            StopMoving();
            return;
        }

        // 待機中ならAIを動かさない
        if (!isDeployed)
            return;

        if (player == null)
            return;

        // ロックオン優先
        if (lockedTarget != null)
        {
            if (lockedTarget.gameObject.activeInHierarchy)
            {
                if (currentTarget != lockedTarget)
                {
                    currentTarget = lockedTarget;
                    currentState = CompanionState.Chasing;
                }
            }
            else
            {
                lockedTarget = null;
                currentTarget = null;
            }
        }

        // ロックオンがない場合だけ通常索敵
        if (lockedTarget == null)
        {
            FindTarget();
        }

        if (isFlying)
        {
            MaintainHover();
        }

        switch (currentState)
        {
            case CompanionState.Following:
                UpdateFollowing();
                break;

            case CompanionState.Chasing:
                UpdateChasing();
                break;

            case CompanionState.Attacking:
                UpdateAttacking();
                break;

            case CompanionState.Frozen:
                StopMoving();
                break;
        }
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    // 出撃
    public void Deploy()
    {
        if (player == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} : Playerが設定されていません。"
            );

            return;
        }

        isFrozenPrevious = false;
        isDeployed = true;

        currentTarget = null;
        lockedTarget = null;

        currentState = CompanionState.Following;

        // Rigidbodyを通常状態に戻す
        rb.isKinematic = false;

        // プレイヤーの後ろに出現
        Vector3 spawnPosition =
            player.position -
            player.forward * followDistance;

        if (isFlying)
        {
            hoverY =
                player.position.y +
                hoverHeight;

            spawnPosition.y = hoverY;

            rb.useGravity = false;
        }
        else
        {
            spawnPosition.y =
                transform.position.y;

            rb.useGravity =
                originalUseGravity;
        }

        rb.position = spawnPosition;

        StopMoving();

        Debug.Log(
            $"{gameObject.name} : 出撃"
        );
    }

    // 通常待機
    public void Standby()
    {
        isDeployed = false;
        isFrozenPrevious = false;

        currentTarget = null;
        lockedTarget = null;

        currentState =
            CompanionState.Standby;

        StopMoving();

        rb.isKinematic = false;
        rb.useGravity = originalUseGravity;

        Debug.Log(
            $"{gameObject.name} : 待機"
        );
    }

    // 交代前の味方をその場に固定
    public void FreezeAsPreviousCompanion()
    {
        isDeployed = false;
        isFrozenPrevious = true;

        currentTarget = null;
        lockedTarget = null;

        currentState =
            CompanionState.Frozen;

        StopMoving();

        // 完全固定
        // Colliderは消さないので足場として使用可能
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.useGravity = false;
        rb.isKinematic = true;

        Debug.Log(
            $"{gameObject.name} : その場に停止"
        );
    }

    // 固定状態解除
    public void ReleaseFrozenState()
    {
        isFrozenPrevious = false;

        rb.isKinematic = false;

        if (isFlying)
        {
            rb.useGravity = false;
        }
        else
        {
            rb.useGravity =
                originalUseGravity;
        }
    }

    // ロックオン
    public void SetLockedTarget(
        Transform target)
    {
        if (!isDeployed)
            return;

        if (isFrozenPrevious)
            return;

        lockedTarget = target;
        currentTarget = target;

        if (target != null)
        {
            currentState =
                CompanionState.Chasing;

            Debug.Log(
                $"{gameObject.name} : " +
                $"ロックオン対象 → {target.name}"
            );
        }
        else
        {
            currentState =
                CompanionState.Following;
        }
    }

    public void ClearLockedTarget()
    {
        lockedTarget = null;
        currentTarget = null;

        if (isDeployed &&
            !isFrozenPrevious)
        {
            currentState =
                CompanionState.Following;
        }
    }

    // 通常索敵
    private void FindTarget()
    {
        if (currentTarget != null)
            return;

        Collider[] colliders =
            Physics.OverlapSphere(
                transform.position,
                searchRadius,
                LayerMask.GetMask("Enemy")
            );

        if (colliders.Length == 0)
        {
            currentState =
                CompanionState.Following;

            return;
        }

        float closestDistance =
            float.MaxValue;

        Transform closestEnemy = null;

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            float distance =
                HorizontalDistance(
                    transform.position,
                    collider.transform.position
                );

            if (distance <
                closestDistance)
            {
                closestDistance =
                    distance;

                closestEnemy =
                    collider.transform;
            }
        }

        if (closestEnemy != null)
        {
            currentTarget =
                closestEnemy;

            currentState =
                CompanionState.Chasing;
        }
    }

    // プレイヤー追従
    private void UpdateFollowing()
    {
        float distance =
            HorizontalDistance(
                transform.position,
                player.position
            );

        if (distance > warpDistance)
        {
            WarpToPlayer();
            return;
        }

        if (distance <= followDistance)
        {
            StopMoving();
            return;
        }

        MoveTowards(player.position);
    }

    // 敵追跡
    private void UpdateChasing()
    {
        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            // ロックオン対象が倒された
            if (lockedTarget == currentTarget)
            {
                lockedTarget = null;
            }

            currentTarget = null;

            currentState =
                CompanionState.Following;

            StopMoving();

            return;
        }

        if (robotData == null)
        {
            StopMoving();
            return;
        }

        float distance =
            HorizontalDistance(
                transform.position,
                currentTarget.position
            );

        if (distance <=
            robotData.AttackRange)
        {
            currentState =
                CompanionState.Attacking;

            StopMoving();

            return;
        }

        MoveTowards(
            currentTarget.position
        );
    }

    // 攻撃中
    private void UpdateAttacking()
    {
        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            if (lockedTarget ==
                currentTarget)
            {
                lockedTarget = null;
            }

            currentTarget = null;

            currentState =
                CompanionState.Following;

            StopMoving();

            return;
        }

        if (robotData == null)
        {
            StopMoving();
            return;
        }

        float distance =
            HorizontalDistance(
                transform.position,
                currentTarget.position
            );

        if (distance >
            robotData.AttackRange)
        {
            currentState =
                CompanionState.Chasing;

            return;
        }

        StopMoving();
    }

    // 移動
    private void MoveTowards(
        Vector3 targetPosition)
    {
        if (robotData == null)
            return;

        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <=
            0.01f)
        {
            StopMoving();
            return;
        }

        direction.Normalize();

        Vector3 velocity =
            direction *
            robotData.MoveSpeed;

        if (isFlying)
        {
            float heightDifference =
                hoverY -
                rb.position.y;

            velocity.y =
                Mathf.Clamp(
                    heightDifference * 2f,
                    -robotData.MoveSpeed,
                    robotData.MoveSpeed
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

    // 停止
    private void StopMoving()
    {
        if (rb == null)
            return;

        Vector3 velocity =
            rb.linearVelocity;

        velocity.x = 0.0f;
        velocity.z = 0.0f;

        rb.linearVelocity =
            velocity;
    }

    // ワープ
    private void WarpToPlayer()
    {
        if (player == null)
            return;

        Vector3 position =
            player.position -
            player.forward *
            followDistance;

        if (isFlying)
        {
            hoverY =
                player.position.y +
                hoverHeight;

            position.y = hoverY;
        }
        else
        {
            position.y =
                transform.position.y;
        }

        rb.position = position;

        StopMoving();
    }

    // 現在のターゲット
    public Transform GetCurrentTarget()
    {
        return currentTarget;
    }

    public CompanionState GetCurrentState()
    {
        return currentState;
    }

    public void ClearTarget()
    {
        currentTarget = null;

        // ロックオン中なら次のUpdateで
        // ロックオン対象へ戻る
        if (lockedTarget != null)
        {
            currentState =
                CompanionState.Chasing;

            return;
        }

        if (isFrozenPrevious)
        {
            currentState =
                CompanionState.Frozen;
        }
        else if (isDeployed)
        {
            currentState =
                CompanionState.Following;
        }
        else
        {
            currentState =
                CompanionState.Standby;
        }
    }

    // 飛行高度維持
    private void MaintainHover()
    {
        if (rb == null ||
            robotData == null)
            return;

        float heightDifference =
            hoverY -
            rb.position.y;

        Vector3 velocity =
            rb.linearVelocity;

        velocity.y =
            Mathf.Clamp(
                heightDifference * 2f,
                -robotData.MoveSpeed,
                robotData.MoveSpeed
            );

        rb.linearVelocity =
            velocity;
    }

    // 水平距離
    private float HorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            searchRadius
        );
    }
}