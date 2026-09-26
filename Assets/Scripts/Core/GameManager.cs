using UnityEngine;

/// <summary>
/// ゲーム全体の状態を管理するクラス。
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }

    private void Awake()
    {
        // 既にGameManagerが存在する場合は自分を削除
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // シーンをまたいでも残す
        DontDestroyOnLoad(gameObject);

        // 最初の状態
        CurrentState = GameState.Boot;
    }

    /// <summary>
    /// ゲーム状態を変更する。
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (CurrentState == newState)
        {
            return;
        }

        Debug.Log(
            $"GameState : {CurrentState} → {newState}"
        );

        CurrentState = newState;
    }
}

/// <summary>
/// ゲーム全体の状態。
/// </summary>
public enum GameState
{
    Boot,
    Title,
    Exploration,
    Battle,
    Result,
    Pause,
    GameOver
}