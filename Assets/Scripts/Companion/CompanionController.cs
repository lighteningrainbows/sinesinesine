using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CompanionController : MonoBehaviour
{
    public enum CompanionState
    {
        Standby,
        Following,
        Chasing,
        Attacking
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

    public bool IsDeployed => isDeployed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        originalUseGravity = rb.useGravity;

        currentState = CompanionState.Standby;
        isDeployed = false;
    }

    private void Update()
    {
        // 待機中ならAIを動かさない
        if (!isDeployed)
            return;

        if (player == null)
            return;

        FindTarget();

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
        }
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    public void Deploy()
    {
        if (player == null)
        {
            Debug.LogWarning(
                $"{gameObject.name} : Playerが設定されていません。"
            );

            return;
        }

        isDeployed = true;

        currentTarget = null;
        currentState = CompanionState.Following;

        // プレイヤーの後ろに出現
        Vector3 spawnPosition =
            player.position -
            player.forward * followDistance;

        if (isFlying)
        {
            hoverY = player.position.y + hoverHeight;
            spawnPosition.y = hoverY;

            rb.useGravity = false;
        }
        else
        {
            spawnPosition.y = transform.position.y;
        }

        rb.position = spawnPosition;

        StopMoving();

        Debug.Log(
            $"{gameObject.name} : 出撃"
        );
    }

    public void Standby()
    {
        isDeployed = false;

        currentTarget = null;
        currentState = CompanionState.Standby;

        StopMoving();

        rb.useGravity = originalUseGravity;

        Debug.Log(
            $"{gameObject.name} : 待機"
        );
    }

    private void FindTarget()
    {
        if (currentTarget != null)
            return;

        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            searchRadius,
            LayerMask.GetMask("Enemy")
        );

        if (colliders.Length == 0)
        {
            currentState = CompanionState.Following;
            return;
        }

        float closestDistance = float.MaxValue;

        Transform closestEnemy = null;

        foreach (Collider collider in colliders)
        {
            float distance = HorizontalDistance(
                transform.position,
                collider.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = collider.transform;
            }
        }

        if (closestEnemy != null)
        {
            currentTarget = closestEnemy;

            currentState =
                CompanionState.Chasing;
        }
    }

    private void UpdateFollowing()
    {
        float distance = HorizontalDistance(
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

    private void UpdateChasing()
    {
        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;
            currentState = CompanionState.Following;
            StopMoving();

            return;
        }

        if (robotData == null)
        {
            StopMoving();
            return;
        }

        float distance = HorizontalDistance(
            transform.position,
            currentTarget.position
        );

        if (distance <= robotData.AttackRange)
        {
            currentState = CompanionState.Attacking;
            StopMoving();
            return;
        }

        MoveTowards(currentTarget.position);
    }

    private void UpdateAttacking()
    {
        // ターゲットが消滅・非アクティブになった
        if (currentTarget == null ||
            !currentTarget.gameObject.activeInHierarchy)
        {
            currentTarget = null;
            currentState = CompanionState.Following;
            StopMoving();

            return;
        }

        if (robotData == null)
        {
            StopMoving();
            return;
        }

        float distance = HorizontalDistance(
            transform.position,
            currentTarget.position
        );

        // 攻撃範囲から出た
        if (distance > robotData.AttackRange)
        {
            currentState = CompanionState.Chasing;
            return;
        }

        StopMoving();
    }

    private void MoveTowards(Vector3 targetPosition)
    {
        if (robotData == null)
            return;

        Vector3 direction = targetPosition - transform.position;

        // 水平方向への移動
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            StopMoving();
            return;
        }

        direction.Normalize();

        Vector3 velocity = direction * robotData.MoveSpeed;

        if (isFlying)
        {
            // 高度を維持するための上下移動
            float heightDifference = hoverY - rb.position.y;

            velocity.y = Mathf.Clamp(
                heightDifference * 2f,
                -robotData.MoveSpeed,
                robotData.MoveSpeed
            );
        }
        else
        {
            velocity.y = rb.linearVelocity.y;
        }

        rb.linearVelocity = velocity;

        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void StopMoving()
    {
        if (rb == null)
            return;

        Vector3 velocity =
            rb.linearVelocity;

        velocity.x = 0.0f;
        velocity.z = 0.0f;

        rb.linearVelocity = velocity;
    }

    private void WarpToPlayer()
    {
        if (player == null)
            return;

        Vector3 position =
            player.position -
            player.forward * followDistance;

        position.y =
            transform.position.y;

        rb.position = position;

        StopMoving();
    }

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

        if (isDeployed)
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

    private void MaintainHover()
    {
        if (rb == null)
            return;

        float heightDifference = hoverY - rb.position.y;

        Vector3 velocity = rb.linearVelocity;

        velocity.y = Mathf.Clamp(
            heightDifference * 2f,
            -robotData.MoveSpeed,
            robotData.MoveSpeed
        );

        rb.linearVelocity = velocity;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            searchRadius
        );
    }
}