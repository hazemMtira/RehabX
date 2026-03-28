using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public enum GameState
{
    Menu,
    Playing,
    GameOver,
    Paused
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public GameState CurrentState { get; private set; }

    [Header("UI — Gameplay")]
    public GameObject gameplayCanvas;

    [Header("UI — End Game")]
    public GameObject endGameCanvas;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI finalTimeText;
    public TextMeshProUGUI gameOverText;       // "Round Complete!" or "Time's Up!"

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        CurrentState = GameState.Playing;
    }

    void Start()
    {
        if (gameplayCanvas != null)
            gameplayCanvas.SetActive(true);

        if (endGameCanvas != null)
            endGameCanvas.SetActive(false);
    }

    public void SetGameState(GameState state)
    {
        CurrentState = state;
    }

    public void HandleRoundEnd(int finalScore, int finalTime, bool timeout)
    {
        if (gameplayCanvas != null)
            gameplayCanvas.SetActive(false);

        if (endGameCanvas != null)
            endGameCanvas.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = timeout ? "Time's Up!" : "Round Complete!";

        if (finalScoreText != null)
            finalScoreText.text = "SCORE: " + finalScore;

        if (finalTimeText != null)
        {
            int mins = finalTime / 60;
            int secs = finalTime % 60;
            finalTimeText.text = mins > 0
                ? $"TIME: {mins}m {secs:00}s"
                : $"TIME: {secs}s";
        }
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}