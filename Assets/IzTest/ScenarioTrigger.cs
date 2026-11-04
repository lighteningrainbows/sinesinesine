using UnityEngine;
using Utage;

public class ScenarioTrigger : MonoBehaviour
{
    public enum TriggerCondition
    {
        SceneStart,     //シーン開始時
        PlayerEnter,    //プレイヤーがエリアに入った時
        Manual          //他のスクリプトから呼ぶ
    }

    [Header("宴")]
    [SerializeField] private AdvEngine advEngine;

    [Header("発生条件")]
    [SerializeField] private TriggerCondition condition;

    [Header("再生するシナリオ")]
    [SerializeField] private string scenarioName;

    private void Start()
    {
        if (condition == TriggerCondition.SceneStart)
        {
            PlayScenario();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (condition != TriggerCondition.PlayerEnter)
            return;

        if (!other.CompareTag("Player"))
            return;

        PlayScenario();
    }

    public void PlayScenario()
    {
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

        advEngine.JumpScenario(scenarioName);
    }
}