using UnityEngine;
using Utage;

public class UtageTest : MonoBehaviour
{
    public AdvEngine advEngine;

    void Start()
    {
        advEngine.JumpScenario("Start");
    }
}