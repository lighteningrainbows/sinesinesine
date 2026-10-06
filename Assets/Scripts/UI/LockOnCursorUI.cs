using UnityEngine;

public class LockOnCursorUI : MonoBehaviour
{
    [Header("カーソル")]
    [SerializeField] private RectTransform cursor;

    [Header("使用するカメラ")]
    [SerializeField] private Camera targetCamera;

    [Header("敵からの表示位置")]
    [SerializeField]
    private Vector3 worldOffset = new Vector3(0f, 2.0f, 0f);

    private void Start()
    {
        // カメラ未設定ならMainCameraを使用
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // 最初は非表示
        if (cursor != null)
        {
            cursor.gameObject.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (cursor == null)
            return;

        if (targetCamera == null)
            return;

        if (LockonManager.Instance == null)
        {
            HideCursor();
            return;
        }

        Transform target =
            LockonManager.Instance.CurrentTarget;

        // ロックオンしていない
        if (target == null)
        {
            HideCursor();
            return;
        }

        // 敵が非表示・撃破済み
        if (!target.gameObject.activeInHierarchy)
        {
            HideCursor();
            return;
        }

        // 敵の頭上のワールド座標
        Vector3 worldPosition =
            target.position +
            worldOffset;

        // ワールド座標 → 画面座標
        Vector3 screenPosition =
            targetCamera.WorldToScreenPoint(
                worldPosition
            );

        // カメラの後ろにいる
        if (screenPosition.z <= 0f)
        {
            HideCursor();
            return;
        }

        // カーソル表示
        if (!cursor.gameObject.activeSelf)
        {
            cursor.gameObject.SetActive(true);
        }

        cursor.position =
            screenPosition;
    }

    private void HideCursor()
    {
        if (cursor != null &&
            cursor.gameObject.activeSelf)
        {
            cursor.gameObject.SetActive(false);
        }
    }
}