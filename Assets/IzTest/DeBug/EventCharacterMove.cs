using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EventCharacterMove : MonoBehaviour
{
    [Header("動かすキャラクター")]
    [SerializeField]
    private NavMeshAgent character;

    [Header("移動先")]
    [SerializeField]
    private Transform targetPoint;

    [Header("自動移動中に停止する操作スクリプト")]
    [SerializeField]
    private Behaviour[] controlScripts;

    [Header("CharacterController")]
    [SerializeField]
    private CharacterController characterController;

    [Header("到着後")]
    [SerializeField]
    private bool useTargetRotation = true;

    private bool isMoving = false;

    //イベント開始前の状態を保存
    private bool[] previousControlStates;
    private bool previousCharacterControllerState;

    // テスト用

    [ContextMenu("Test Move")]
    private void TestMove()
    {
        StartMove();
    }

    // 移動開始

    public void StartMove()
    {
        if (isMoving)
            return;

        if (character == null)
        {
            Debug.LogError(
                "EventCharacterMove : CharacterのNavMeshAgentが設定されていません"
            );
            return;
        }

        if (targetPoint == null)
        {
            Debug.LogError(
                "EventCharacterMove : TargetPointが設定されていません"
            );
            return;
        }

        if (!character.enabled)
        {
            Debug.LogError(
                "EventCharacterMove : NavMeshAgentが無効になっています"
            );
            return;
        }

        if (!character.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "EventCharacterMove : キャラクターが非アクティブです"
            );
            return;
        }

        if (!character.isOnNavMesh)
        {
            Debug.LogError(
                "EventCharacterMove : キャラクターがNavMesh上にいません"
            );
            return;
        }

        StartCoroutine(MoveCharacter());
    }

    // NavMesh移動

    private IEnumerator MoveCharacter()
    {
        isMoving = true;

        //現在の操作状態を保存して停止
        LockControls();

        character.isStopped = false;
        character.updatePosition = true;
        character.updateRotation = true;

        //移動開始
        bool success =
            character.SetDestination(targetPoint.position);

        if (!success)
        {
            Debug.LogError(
                "EventCharacterMove : 移動先を設定できませんでした"
            );

            FinishMove();
            yield break;
        }

        //SetDestination直後の経路計算待ち
        yield return null;

        while (character.pathPending)
        {
            yield return null;
        }

        //経路確認
        if (character.pathStatus ==
            NavMeshPathStatus.PathInvalid)
        {
            Debug.LogError(
                "EventCharacterMove : 移動できる経路がありません"
            );

            FinishMove();
            yield break;
        }

        //到着を待つ

        while (true)
        {
            if (!character.pathPending)
            {
                if (character.remainingDistance
                    <= character.stoppingDistance)
                {
                    if (!character.hasPath ||
                        character.velocity.sqrMagnitude
                        < 0.01f)
                    {
                        break;
                    }
                }
            }

            yield return null;
        }

        character.ResetPath();

        //最後の向き

        if (useTargetRotation)
        {
            Vector3 targetEuler =
                targetPoint.eulerAngles;

            character.transform.rotation =
                Quaternion.Euler(
                    0f,
                    targetEuler.y,
                    0f
                );
        }

        Debug.Log(
            $"{character.name} のイベント移動完了"
        );

        FinishMove();
    }

    //プレイヤー操作停止

    private void LockControls()
    {
        //操作スクリプト
        previousControlStates =
            new bool[controlScripts.Length];

        for (int i = 0;
             i < controlScripts.Length;
             i++)
        {
            if (controlScripts[i] == null)
                continue;

            previousControlStates[i] =
                controlScripts[i].enabled;

            controlScripts[i].enabled = false;
        }

        //CharacterController
        if (characterController != null)
        {
            previousCharacterControllerState =
                characterController.enabled;

            characterController.enabled = false;
        }
    }

    // 元の状態へ戻す

    private void UnlockControls()
    {
        //CharacterController
        if (characterController != null)
        {
            characterController.enabled =
                previousCharacterControllerState;
        }

        //操作スクリプト
        if (previousControlStates != null)
        {
            for (int i = 0;
                 i < controlScripts.Length;
                 i++)
            {
                if (controlScripts[i] == null)
                    continue;

                controlScripts[i].enabled =
                    previousControlStates[i];
            }
        }
    }

    //移動終了処理

    private void FinishMove()
    {
        if (character != null &&
            character.enabled &&
            character.isOnNavMesh)
        {
            character.isStopped = true;
            character.ResetPath();
        }

        UnlockControls();

        isMoving = false;
    }

    //外部確認用

    public bool IsMoving()
    {
        return isMoving;
    }
}