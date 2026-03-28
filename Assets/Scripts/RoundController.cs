using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundController : MonoBehaviour
{
    [Header("References")]
    public SpawnManager spawnManager;
    public ScoreManager scoreManager;
    public XRManager    xrManager;

    [Header("Gesture Detectors")]
    [Tooltip("Assign ElbowAngleGestureDetector on LeftHand.")]
    public ElbowAngleGestureDetector leftHandDetector;
    [Tooltip("Assign ElbowAngleGestureDetector on RightHand.")]
    public ElbowAngleGestureDetector rightHandDetector;

    [Header("UI")]
    public GameUIBuilder _gameUI;

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------

    DifficultyData _difficulty;
    bool           _roundActive;
    bool           _isPaused;
    float          _elapsed;          // manually accumulated — immune to timeScale
    float          _maxStrikeSpeed;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Start()
    {
        InitRound();
    }

    void OnDestroy()
    {
        // Always unsubscribe to avoid stale callbacks after scene unload
        if (SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.OnPaused  -= HandlePause;
            SupabaseManager.Instance.OnResumed -= HandleResume;
        }
    }

    void InitRound()
    {
        if (GameSessionSettings.Instance == null)
        {
            Debug.LogError("[RoundController] GameSessionSettings missing.");
            return;
        }

        _difficulty = GameSessionSettings.Instance.selectedDifficulty;

        if (_difficulty == null)
        {
            Debug.LogError("[RoundController] No difficulty selected.");
            return;
        }

        StartRound();
    }

    // ---------------------------------------------------------------
    // Round flow
    // ---------------------------------------------------------------

    public void StartRound()
    {
        _roundActive    = true;
        _isPaused       = false;
        _elapsed        = 0f;
        _maxStrikeSpeed = 0f;

        // Subscribe to pause / resume events from Supabase
        if (SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.OnPaused  += HandlePause;
            SupabaseManager.Instance.OnResumed += HandleResume;
        }

        // Reset peak speed tracker on detector
        var detector = GetActiveDetector();
        if (detector != null) detector.ResetMaxWristSpeed();

        // Tell Supabase the round is now active
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(GameSessionSettings.Instance.sessionId))
            SupabaseManager.Instance.SetSessionActive(GameSessionSettings.Instance.sessionId);

        GameManager.Instance.SetGameState(GameState.Playing);
        scoreManager.ResetScore();
        scoreManager.OnScoreChanged += OnScoreChangedUI;
        scoreManager.OnScoreChanged += EvaluateScore;

        if (_gameUI != null) _gameUI.UpdateScore(0);

        spawnManager.currentDifficulty = _difficulty;
        spawnManager.StartSpawning();

        if (AudioManager.Instance != null) AudioManager.Instance.StartMusic();
        if (xrManager != null) xrManager.SwapToGameplay();

        Debug.Log("[RoundController] Round started.");
    }

    void Update()
    {
        if (!_roundActive || _isPaused) return;

        // Use unscaled delta so this is resilient even if something else
        // touches timeScale, but we also guard with _isPaused above.
        _elapsed += Time.deltaTime;

        TrackMaxStrikeSpeed();
        UpdateTimerUI(_elapsed);

        if (_elapsed >= _difficulty.gameDuration)
            EndRound(true);
    }

    // ---------------------------------------------------------------
    // Pause / Resume
    // ---------------------------------------------------------------

    void HandlePause()
    {
        if (!_roundActive || _isPaused) return;

        _isPaused       = true;
        Time.timeScale  = 0f;

        spawnManager.StopSpawning();
        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

        Debug.Log("[RoundController] Round paused.");
    }

    void HandleResume()
    {
        if (!_roundActive || !_isPaused) return;

        _isPaused       = false;
        Time.timeScale  = 1f;

        spawnManager.StartSpawning();
        if (AudioManager.Instance != null) AudioManager.Instance.StartMusic();

        Debug.Log("[RoundController] Round resumed.");
    }

    // ---------------------------------------------------------------
    // Max strike speed
    // ---------------------------------------------------------------

    void TrackMaxStrikeSpeed()
    {
        var detector = GetActiveDetector();
        if (detector == null) return;

        if (detector.gestureReady)
        {
            float peak = detector.GetMaxWristSpeed();
            if (peak > _maxStrikeSpeed)
                _maxStrikeSpeed = peak;

            detector.ResetMaxWristSpeed();
        }
    }

    ElbowAngleGestureDetector GetActiveDetector()
    {
        var hand = GameSessionSettings.Instance.selectedHand;
        return hand == HandType.Left ? leftHandDetector : rightHandDetector;
    }

    // ---------------------------------------------------------------
    // Timer UI
    // ---------------------------------------------------------------

    void UpdateTimerUI(float elapsed)
    {
        if (_gameUI == null) return;
        float remaining = Mathf.Max(0f, _difficulty.gameDuration - elapsed);
        _gameUI.UpdateTimer(remaining);
    }

    // ---------------------------------------------------------------
    // Score
    // ---------------------------------------------------------------

    void EvaluateScore(int score)
    {
        if (!_roundActive) return;
        if (score >= _difficulty.requiredScore)
            EndRound(false);
    }

    void OnScoreChangedUI(int score)
    {
        if (_gameUI != null)
            _gameUI.UpdateScore(score);
    }

    // ---------------------------------------------------------------
    // End round
    // ---------------------------------------------------------------

    void EndRound(bool timeout)
    {
        if (!_roundActive) return;

        _roundActive   = false;
        _isPaused      = false;
        Time.timeScale = 1f;   // always restore before leaving

        // Unsubscribe immediately so no stale events fire during teardown
        if (SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.OnPaused  -= HandlePause;
            SupabaseManager.Instance.OnResumed -= HandleResume;
        }

        spawnManager.StopSpawning();
        scoreManager.OnScoreChanged -= EvaluateScore;
        scoreManager.OnScoreChanged -= OnScoreChangedUI;

        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

        int   finalScore    = scoreManager.Score;
        float finalDuration = _elapsed;   // use our manually tracked time, not Time.time

        GameManager.Instance.SetGameState(GameState.GameOver);
        if (xrManager != null) xrManager.SwapToEndGame();

        Debug.Log($"[RoundController] Round ended. Timeout={timeout} Score={finalScore} " +
                  $"Duration={finalDuration:F1}s MaxSpeed={_maxStrikeSpeed:F2}m/s");

        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(GameSessionSettings.Instance.sessionId))
        {
            SupabaseManager.Instance.SubmitResults(
                GameSessionSettings.Instance.sessionId,
                finalScore,
                finalDuration,
                _maxStrikeSpeed,
                onDone: GoToLoadingScene
            );
        }
        else
        {
            GoToLoadingScene();
        }

        GameManager.Instance.HandleRoundEnd(finalScore, Mathf.FloorToInt(finalDuration), timeout);
    }

    // ---------------------------------------------------------------
    // Scene transition
    // ---------------------------------------------------------------

    void GoToLoadingScene()
    {
        if (SupabaseManager.Instance != null)
            SupabaseManager.Instance.ResetForNewSession();

        SceneManager.LoadScene("LoadingScene");
    }
}