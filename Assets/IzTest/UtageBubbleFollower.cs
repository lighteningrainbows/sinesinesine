using System;
using System.Collections.Generic;
using UnityEngine;
using Utage;

public class UtageBubbleFollower : MonoBehaviour
{
    [Serializable]
    public class CharacterTarget
    {
        public string characterLabel;
        public Transform headPoint;
    }

    [Header("宴の吹き出し本体")]
    [SerializeField] private RectTransform bubbleRoot;

    [Header("3D世界を映しているカメラ")]
    [SerializeField] private Camera worldCamera;

    [Header("Canvas")]
    [SerializeField] private Canvas canvas;

    [Header("頭からのズレ")]
    [SerializeField] private Vector2 offset = new Vector2(0, 80);

    [Header("キャラクター")]
    [SerializeField]
    private List<CharacterTarget> characters = new();

    private Transform currentHead;

    // 宴の On Post Change Text から呼ぶ
    public void OnPostChangeText(AdvMessageWindow window)
    {
        string label = window.CharacterLabel;

        currentHead = null;

        foreach (CharacterTarget data in characters)
        {
            if (data.characterLabel == label)
            {
                currentHead = data.headPoint;
                break;
            }
        }

        if (currentHead == null)
        {
            Debug.LogWarning(
                $"吹き出し対象が登録されていません : {label}"
            );
        }
    }

    private void LateUpdate()
    {
        if (currentHead == null)
            return;

        if (worldCamera == null)
            return;

        Vector3 screenPosition =
            worldCamera.WorldToScreenPoint(currentHead.position);

        // カメラの後ろ
        if (screenPosition.z <= 0)
            return;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            bubbleRoot.position =
                (Vector2)screenPosition + offset;
        }
        else
        {
            RectTransform canvasRect =
                canvas.transform as RectTransform;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvas.worldCamera,
                out Vector2 localPosition
            );

            bubbleRoot.anchoredPosition =
                localPosition + offset;
        }
    }
}