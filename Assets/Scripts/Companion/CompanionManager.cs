using UnityEngine;
using UnityEngine.InputSystem;

public class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance { get; private set; }

    [Header("プレイヤー")]
    [SerializeField] private Transform player;

    [Header("所持している仲間")]
    [SerializeField] private CompanionController[] companions;

    [Header("現在出撃中の仲間")]
    [SerializeField] private int activeCompanionIndex = 0;

    public CompanionController ActiveCompanion
    {
        get
        {
            if (companions == null ||
                companions.Length == 0)
            {
                return null;
            }

            if (activeCompanionIndex < 0 ||
                activeCompanionIndex >= companions.Length)
            {
                return null;
            }

            return companions[activeCompanionIndex];
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        InitializeCompanions();
    }

    private void Update()
    {
        HandleSwitchInput();
    }

    private void InitializeCompanions()
    {
        if (companions == null || companions.Length == 0)
        {
            Debug.LogWarning("仲間ロボットが登録されていません。");
            return;
        }

        // インデックスが範囲外なら先頭に戻す
        if (activeCompanionIndex < 0 ||
            activeCompanionIndex >= companions.Length)
        {
            activeCompanionIndex = 0;
        }

        for (int i = 0; i < companions.Length; i++)
        {
            CompanionController companion = companions[i];

            if (companion == null)
                continue;

            companion.SetPlayer(player);

            if (i == activeCompanionIndex)
            {
                // アクティブなロボットだけ表示
                companion.gameObject.SetActive(true);
                companion.Deploy();
            }
            else
            {
                // 待機ロボットは非表示・処理停止
                companion.Standby();
                companion.gameObject.SetActive(false);
            }
        }
    }

    private void HandleSwitchInput()
    {
        if (companions == null || companions.Length <= 1)
            return;

        // キーボード Q / E
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame)
                SwitchPrevious();

            if (Keyboard.current.eKey.wasPressedThisFrame)
                SwitchNext();
        }

        // ゲームパッド LB / RB
        if (Gamepad.current != null)
        {
            if (Gamepad.current.leftShoulder.wasPressedThisFrame)
                SwitchPrevious();

            if (Gamepad.current.rightShoulder.wasPressedThisFrame)
                SwitchNext();
        }
    }

    public void SwitchNext()
    {
        if (companions == null ||
            companions.Length <= 1)
        {
            return;
        }

        int nextIndex =
            (activeCompanionIndex + 1) % companions.Length;

        SwitchTo(nextIndex);
    }

    public void SwitchPrevious()
    {
        if (companions == null ||
            companions.Length <= 1)
        {
            return;
        }

        int previousIndex =
            activeCompanionIndex - 1;

        if (previousIndex < 0)
        {
            previousIndex = companions.Length - 1;
        }

        SwitchTo(previousIndex);
    }

    public void SwitchTo(int index)
    {
        if (companions == null || companions.Length == 0)
            return;

        if (index < 0 || index >= companions.Length)
            return;

        if (index == activeCompanionIndex)
            return;

        CompanionController oldCompanion =
            companions[activeCompanionIndex];

        CompanionController newCompanion =
            companions[index];

        // 現在のロボットを待機状態にして非表示
        if (oldCompanion != null)
        {
            oldCompanion.Standby();
            oldCompanion.gameObject.SetActive(false);
        }

        // アクティブなロボットの番号を更新
        activeCompanionIndex = index;

        // 新しいロボットを表示して出撃
        if (newCompanion != null)
        {
            newCompanion.gameObject.SetActive(true);
            newCompanion.SetPlayer(player);
            newCompanion.Deploy();
        }

        Debug.Log($"仲間切り替え : {activeCompanionIndex}");
    }

    public int GetActiveCompanionIndex()
    {
        return activeCompanionIndex;
    }

    public CompanionController GetCompanion(int index)
    {
        if (companions == null)
            return null;

        if (index < 0 ||
            index >= companions.Length)
        {
            return null;
        }

        return companions[index];
    }

    public int GetCompanionCount()
    {
        if (companions == null)
            return 0;

        return companions.Length;
    }
}