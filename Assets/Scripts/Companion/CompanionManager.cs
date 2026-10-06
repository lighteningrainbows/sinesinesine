using UnityEngine;
using UnityEngine.InputSystem;

public class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance
    {
        get;
        private set;
    }

    [Header("プレイヤー")]
    [SerializeField] private Transform player;

    [Header("所持している味方")]
    [SerializeField] private CompanionController[] companions;

    [Header("現在出撃中")]
    [SerializeField] private int activeCompanionIndex = 0;

    // 1つ前に使っていた味方
    private CompanionController previousCompanion;

    public CompanionController ActiveCompanion
    {
        get
        {
            if (companions == null ||
                companions.Length == 0)
                return null;

            if (activeCompanionIndex < 0 ||
                activeCompanionIndex >=
                companions.Length)
                return null;

            return companions[
                activeCompanionIndex
            ];
        }
    }

    public CompanionController PreviousCompanion
        => previousCompanion;

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

    private void Start()
    {
        InitializeCompanions();
    }

    private void Update()
    {
        HandleSwitchInput();
    }

    // 初期化
    private void InitializeCompanions()
    {
        if (companions == null ||
            companions.Length == 0)
            return;

        activeCompanionIndex =
            Mathf.Clamp(
                activeCompanionIndex,
                0,
                companions.Length - 1
            );

        for (int i = 0;
             i < companions.Length;
             i++)
        {
            CompanionController companion =
                companions[i];

            if (companion == null)
                continue;

            companion.SetPlayer(player);

            if (i ==
                activeCompanionIndex)
            {
                companion.gameObject
                    .SetActive(true);

                companion.Deploy();
            }
            else
            {
                companion.Standby();

                companion.gameObject
                    .SetActive(false);
            }
        }

        previousCompanion = null;
    }

    // 交代入力
    private void HandleSwitchInput()
    {
        // LB
        if (Gamepad.current != null &&
            Gamepad.current.leftShoulder
                .wasPressedThisFrame)
        {
            SwitchPrevious();
        }

        // RB
        if (Gamepad.current != null &&
            Gamepad.current.rightShoulder
                .wasPressedThisFrame)
        {
            SwitchNext();
        }

        // キーボード Q / E
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey
                .wasPressedThisFrame)
            {
                SwitchPrevious();
            }

            if (Keyboard.current.eKey
                .wasPressedThisFrame)
            {
                SwitchNext();
            }
        }
    }

    public void SwitchNext()
    {
        if (companions == null ||
            companions.Length <= 1)
            return;

        int nextIndex =
            activeCompanionIndex + 1;

        if (nextIndex >=
            companions.Length)
        {
            nextIndex = 0;
        }

        SwitchTo(nextIndex);
    }

    public void SwitchPrevious()
    {
        if (companions == null ||
            companions.Length <= 1)
            return;

        int previousIndex =
            activeCompanionIndex - 1;

        if (previousIndex < 0)
        {
            previousIndex =
                companions.Length - 1;
        }

        SwitchTo(previousIndex);
    }

    // 実際の交代
    public void SwitchTo(int index)
    {
        if (companions == null ||
            companions.Length == 0)
            return;

        if (index < 0 ||
            index >= companions.Length)
            return;

        if (index ==
            activeCompanionIndex)
            return;

        CompanionController oldActive =
            ActiveCompanion;

        CompanionController newActive =
            companions[index];

        if (newActive == null)
            return;

        // 以前残していた味方を消す
        if (previousCompanion != null &&
            previousCompanion != oldActive &&
            previousCompanion != newActive)
        {
            previousCompanion
                .ReleaseFrozenState();

            previousCompanion
                .Standby();

            previousCompanion
                .gameObject
                .SetActive(false);
        }

        // 今まで操作していた味方をその場に残す
        if (oldActive != null)
        {
            oldActive
                .FreezeAsPreviousCompanion();

            oldActive
                .gameObject
                .SetActive(true);

            previousCompanion =
                oldActive;
        }

        // 新しい味方
        activeCompanionIndex =
            index;

        newActive.gameObject
            .SetActive(true);

        newActive
            .ReleaseFrozenState();

        newActive
            .SetPlayer(player);

        newActive
            .Deploy();

        Debug.Log(
            $"味方交代 : {newActive.gameObject.name}"
        );
    }

    public int GetActiveCompanionIndex()
    {
        return activeCompanionIndex;
    }

    public CompanionController GetCompanion(
        int index)
    {
        if (companions == null)
            return null;

        if (index < 0 ||
            index >= companions.Length)
            return null;

        return companions[index];
    }

    public int GetCompanionCount()
    {
        if (companions == null)
            return 0;

        return companions.Length;
    }
}