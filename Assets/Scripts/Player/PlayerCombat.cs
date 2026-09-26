using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [Header("プレイヤーデータ")]
    [SerializeField] private PlayerData playerData;

    [Header("攻撃設定")]
    [SerializeField] private LayerMask targetLayer;

    [Header("飛行敵対応")]
    [SerializeField] private float attackHeight = 4.0f;

    private float attackTimer;

    private void Update()
    {
        attackTimer -= Time.deltaTime;

        bool attackPressed = false;

        // ゲームパッド Yボタン
        if (Gamepad.current != null &&
            Gamepad.current.buttonNorth.wasPressedThisFrame)
        {
            attackPressed = true;
        }

        // キーボード Jキー
        if (Keyboard.current != null &&
            Keyboard.current.jKey.wasPressedThisFrame)
        {
            attackPressed = true;
        }

        if (attackPressed)
            TryAttack();
    }

    private void TryAttack()
    {
        if (playerData == null)
            return;

        if (attackTimer > 0f)
            return;

        attackTimer = playerData.AttackInterval;

        Attack();
    }

    private void Attack()
    {
        if (BattleSystem.Instance == null)
        {
            Debug.LogWarning("BattleSystemが存在しません。");
            return;
        }

        Vector3 center =
            transform.position +
            transform.forward;

        Vector3 halfExtents = new Vector3(
            playerData.AttackArea.x * 0.5f,
            attackHeight * 0.5f,
            playerData.AttackArea.y * 0.5f
        );

        Collider[] targets = Physics.OverlapBox(
            center,
            halfExtents,
            transform.rotation,
            targetLayer
        );

        foreach (Collider target in targets)
        {
            BattleSystem.Instance.Attack(
                gameObject,
                target.gameObject,
                playerData.Attack,
                null
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerData == null)
            return;

        Vector3 center =
            transform.position +
            transform.forward;

        Vector3 size = new Vector3(
            playerData.AttackArea.x,
            attackHeight,
            playerData.AttackArea.y
        );

        Gizmos.matrix = Matrix4x4.TRS(
            center,
            transform.rotation,
            Vector3.one
        );

        Gizmos.DrawWireCube(
            Vector3.zero,
            size
        );
    }
}