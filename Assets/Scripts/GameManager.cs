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

    [Header("UI")]
    public GameObject gameplayCanvas;
    public GameObject endGameCanvas;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI finalTimeText;

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // Start directly in Playing (no menu logic)
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

    // NEW: finalTime is now int
    public void HandleRoundEnd(int finalScore, int finalTime, bool timeout)
    {
        if (gameplayCanvas != null)
            gameplayCanvas.SetActive(false);

        if (endGameCanvas != null)
            endGameCanvas.SetActive(true);

        if (finalScoreText != null)
            finalScoreText.text = "Score: " + finalScore;

        if (finalTimeText != null)
            finalTimeText.text = "Time: " + finalTime + "s"; // <--- Display as integer seconds
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMainMenu()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogWarning("Main Menu scene name not set in GameManager.");
    }
}