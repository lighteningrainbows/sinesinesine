using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Utage;

public class ScenarioTrigger : MonoBehaviour
{
    public enum TriggerCondition
    {
        SceneStart,
        PlayerEnter,
        Interact,
        Manual
    }

    [Header("宴")]
    [SerializeField] private AdvEngine advEngine;

    [Header("発生条件")]
    [SerializeField] private TriggerCondition condition;

    [Header("再生するシナリオ")]
    [SerializeField] private string scenarioName;

    [Header("プレイヤー判定")]
    [SerializeField] private string playerTag = "Player";

    [Header("調べる操作")]
    [SerializeField] private InputActionReference interactAction;

    [Header("イベント中に停止するプレイヤー操作")]
    [SerializeField] private Behaviour[] playerControlScripts;

    [Header("イベント設定")]
    [SerializeField] private bool playOnce = false;

    private bool playerInRange;
    private bool hasPlayed;
    private bool isScenarioPlaying;

    // イベント開始前のON/OFF状態を保存
    private bool[] previousControlStates;

    private void Awake()
    {
        if (advEngine == null)
        {
            advEngine = FindFirstObjectByType<AdvEngine>();
        }
    }

    private void Start()
    {
        if (condition == TriggerCondition.SceneStart)
        {
            PlayScenario();
        }
    }

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.Disable();
        }
    }

    private void Update()
    {
        if (condition != TriggerCondition.Interact)
            return;

        if (!playerInRange)
            return;

        if (isScenarioPlaying)
            return;

        if (interactAction == null)
            return;

        if (interactAction.action.WasPressedThisFrame())
        {
            PlayScenario();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (condition == TriggerCondition.PlayerEnter)
        {
            PlayScenario();
        }

        if (condition == TriggerCondition.Interact)
        {
            playerInRange = true;

            Debug.Log("調べることができます");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (condition == TriggerCondition.Interact)
        {
            playerInRange = false;
        }
    }

    public void PlayScenario()
    {
        if (isScenarioPlaying)
            return;

        if (playOnce && hasPlayed)
            return;

        if (advEngine == null)
        {
            Debug.LogError("AdvEngineが設定されていません");
            return;
        }

        if (string.IsNullOrEmpty(scenarioName))
        {
            Debug.LogError("Scenario Nameが設定されていません");
            return;
        }

        StartCoroutine(PlayScenarioCoroutine());
    }

    private IEnumerator PlayScenarioCoroutine()
    {
        isScenarioPlaying = true;

        //プレイヤー操作停止
        LockPlayerControls();

        //宴シナリオ開始
        advEngine.JumpScenario(scenarioName);

        //宴のシナリオが終わるまで待つ
        while (!advEngine.IsEndOrPauseScenario)
        {
            yield return null;
        }

        //プレイヤー操作を元に戻す
        UnlockPlayerControls();

        hasPlayed = true;
        isScenarioPlaying = false;
    }

    private void LockPlayerControls()
    {
        previousControlStates =
            new bool[playerControlScripts.Length];

        for (int i = 0; i < playerControlScripts.Length; i++)
        {
            if (playerControlScripts[i] == null)
                continue;

            //元の状態を保存
            previousControlStates[i] =
                playerControlScripts[i].enabled;

            //操作停止
            playerControlScripts[i].enabled = false;
        }
    }

    private void UnlockPlayerControls()
    {
        if (previousControlStates == null)
            return;

        for (int i = 0; i < playerControlScripts.Length; i++)
        {
            if (playerControlScripts[i] == null)
                continue;

            //イベント前の状態に戻す
            playerControlScripts[i].enabled =
                previousControlStates[i];
        }
    }
}