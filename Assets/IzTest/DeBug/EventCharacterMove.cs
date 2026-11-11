using System.Collections;
using UnityEngine;

public class EventCharacterMove : MonoBehaviour
{
    [Header("動かすキャラクター")]
    [SerializeField] private Transform character;

    [Header("移動先")]
    [SerializeField] private Transform targetPoint;

    [Header("移動設定")]
    [SerializeField] private float moveSpeed = 2.0f;
    [SerializeField] private float rotateSpeed = 8.0f;

    private bool isMoving = false;

    // 外部から呼び出す

    [ContextMenu("Test Move")]
    private void TestMove()
    {
        StartMove();
    }

    public void StartMove()
    {
        if (character == null || targetPoint == null)
        {
            Debug.LogWarning("Character または TargetPoint が設定されていません");
            return;
        }

        if (!isMoving)
        {
            StartCoroutine(MoveCharacter());
        }
    }

    private IEnumerator MoveCharacter()
    {
        isMoving = true;

        while (Vector3.Distance(character.position, targetPoint.position) > 0.05f)
        {
            Vector3 direction =
                targetPoint.position - character.position;

            // 移動方向を向く
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction.normalized);

                character.rotation = Quaternion.Slerp(
                    character.rotation,
                    targetRotation,
                    rotateSpeed * Time.deltaTime
                );
            }

            // 移動
            character.position = Vector3.MoveTowards(
                character.position,
                targetPoint.position,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        // 最後に位置をピッタリ合わせる
        character.position = targetPoint.position;

        // 移動先の向きにも合わせる
        character.rotation = targetPoint.rotation;

        isMoving = false;
    }

    public bool IsMoving()
    {
        return isMoving;
    }
}