using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LockonManager : MonoBehaviour
{
    public static LockonManager Instance
    {
        get;
        private set;
    }

    [Header("ロックオン設定")]
    [SerializeField] private float lockOnRadius = 20.0f;

    [SerializeField] private LayerMask enemyLayer;

    private readonly List<Transform> targets =
        new List<Transform>();

    private Transform currentTarget;

    private int currentTargetIndex = -1;

    public Transform CurrentTarget
        => currentTarget;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Update()
    {
        HandleInput();

        ValidateCurrentTarget();
    }

    // 入力
    private void HandleInput()
    {
        bool leftPressed = false;
        bool rightPressed = false;
        bool downPressed = false;

        // キーボード
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey
                .wasPressedThisFrame)
            {
                leftPressed = true;
            }

            if (Keyboard.current.rightArrowKey
                .wasPressedThisFrame)
            {
                rightPressed = true;
            }

            if (Keyboard.current.downArrowKey
                .wasPressedThisFrame)
            {
                downPressed = true;
            }
        }

        // コントローラー D-Pad
        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.left
                .wasPressedThisFrame)
            {
                leftPressed = true;
            }

            if (Gamepad.current.dpad.right
                .wasPressedThisFrame)
            {
                rightPressed = true;
            }

            if (Gamepad.current.dpad.down
                .wasPressedThisFrame)
            {
                downPressed = true;
            }
        }

        // 実行
        if (downPressed)
        {
            ClearLockOn();
            return;
        }

        if (rightPressed)
        {
            SelectNextTarget();
        }

        if (leftPressed)
        {
            SelectPreviousTarget();
        }
    }

    // 右
    private void SelectNextTarget()
    {
        RefreshTargets();

        if (targets.Count == 0)
        {
            ClearLockOn();
            return;
        }

        if (currentTarget == null)
        {
            currentTargetIndex = 0;
        }
        else
        {
            int index =
                targets.IndexOf(
                    currentTarget
                );

            if (index < 0)
            {
                currentTargetIndex = 0;
            }
            else
            {
                currentTargetIndex =
                    index + 1;

                if (currentTargetIndex >=
                    targets.Count)
                {
                    currentTargetIndex = 0;
                }
            }
        }

        SetTarget(
            targets[currentTargetIndex]
        );
    }

    // 左
    private void SelectPreviousTarget()
    {
        RefreshTargets();

        if (targets.Count == 0)
        {
            ClearLockOn();
            return;
        }

        if (currentTarget == null)
        {
            currentTargetIndex =
                targets.Count - 1;
        }
        else
        {
            int index =
                targets.IndexOf(
                    currentTarget
                );

            if (index < 0)
            {
                currentTargetIndex =
                    targets.Count - 1;
            }
            else
            {
                currentTargetIndex =
                    index - 1;

                if (currentTargetIndex < 0)
                {
                    currentTargetIndex =
                        targets.Count - 1;
                }
            }
        }

        SetTarget(
            targets[currentTargetIndex]
        );
    }

    // 敵一覧更新
    private void RefreshTargets()
    {
        targets.Clear();

        Collider[] colliders =
            Physics.OverlapSphere(
                transform.position,
                lockOnRadius,
                enemyLayer
            );

        foreach (Collider collider in colliders)
        {
            if (collider == null)
                continue;

            if (!collider.gameObject
                .activeInHierarchy)
                continue;

            Transform enemy =
                collider.transform;

            // 同じ敵が複数Colliderを
            // 持っている場合の重複防止
            EnemyController controller =
                collider.GetComponentInParent<
                    EnemyController>();

            if (controller != null)
            {
                enemy =
                    controller.transform;
            }

            if (!targets.Contains(enemy))
            {
                targets.Add(enemy);
            }
        }

        // プレイヤーから近い順
        targets.Sort(
            (a, b) =>
            {
                float distanceA =
                    HorizontalDistance(
                        transform.position,
                        a.position
                    );

                float distanceB =
                    HorizontalDistance(
                        transform.position,
                        b.position
                    );

                return distanceA
                    .CompareTo(distanceB);
            }
        );
    }

    // ロックオン
    private void SetTarget(
        Transform target)
    {
        currentTarget = target;

        if (currentTarget == null)
            return;

        Debug.Log(
            $"LOCK ON : {currentTarget.name}"
        );

        CompanionManager manager =
            CompanionManager.Instance;

        if (manager == null)
            return;

        CompanionController companion =
            manager.ActiveCompanion;

        if (companion == null)
            return;

        companion.SetLockedTarget(
            currentTarget
        );
    }

    // ロックオン解除
    public void ClearLockOn()
    {
        currentTarget = null;
        currentTargetIndex = -1;

        CompanionManager manager =
            CompanionManager.Instance;

        if (manager == null)
            return;

        CompanionController companion =
            manager.ActiveCompanion;

        if (companion != null)
        {
            companion.ClearLockedTarget();
        }

        Debug.Log("LOCK ON解除");
    }

    // ターゲット生存確認
    private void ValidateCurrentTarget()
    {
        if (currentTarget == null)
            return;

        if (!currentTarget.gameObject
            .activeInHierarchy)
        {
            ClearLockOn();
            return;
        }

        float distance =
            HorizontalDistance(
                transform.position,
                currentTarget.position
            );

        if (distance > lockOnRadius)
        {
            ClearLockOn();
        }
    }

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
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            lockOnRadius
        );
    }
}