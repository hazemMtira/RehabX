using UnityEngine;

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
    float          _roundStartTime;
    bool           _roundActive;
    float          _maxStrikeSpeed;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Start()
    {
        InitRound();
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
        _roundStartTime = Time.time;
        _roundActive    = true;
        _maxStrikeSpeed = 0f;

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
        if (!_roundActive) return;

        TrackMaxStrikeSpeed();

        float elapsed = Time.time - _roundStartTime;
        UpdateTimerUI(elapsed);

        if (elapsed >= _difficulty.gameDuration)
            EndRound(true);
    }

    // ---------------------------------------------------------------
    // Max strike speed — only sample when a valid strike fires
    // ---------------------------------------------------------------

    void TrackMaxStrikeSpeed()
    {
        var detector = GetActiveDetector();
        if (detector == null) return;

        // Only record peak when gestureReady just fired — avoids constant polling
        if (detector.gestureReady)
        {
            float peak = detector.GetMaxWristSpeed();
            if (peak > _maxStrikeSpeed)
                _maxStrikeSpeed = peak;

            // Reset so next strike gets a fresh peak measurement
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

        _roundActive = false;
        spawnManager.StopSpawning();
        scoreManager.OnScoreChanged -= EvaluateScore;
        scoreManager.OnScoreChanged -= OnScoreChangedUI;

        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

        int   finalScore    = scoreManager.Score;
        float finalDuration = Time.time - _roundStartTime;

        GameManager.Instance.SetGameState(GameState.GameOver);
        if (xrManager != null) xrManager.SwapToEndGame();

        Debug.Log($"[RoundController] Round ended. Timeout={timeout} Score={finalScore} " +
                  $"Duration={finalDuration:F1}s MaxSpeed={_maxStrikeSpeed:F2}m/s");

        // Submit to Supabase
        if (SupabaseManager.Instance != null && !string.IsNullOrEmpty(GameSessionSettings.Instance.sessionId))
        {
            SupabaseManager.Instance.SubmitResults(
                GameSessionSettings.Instance.sessionId,
                finalScore,
                finalDuration,
                _maxStrikeSpeed
            );
        }

        GameManager.Instance.HandleRoundEnd(finalScore, Mathf.FloorToInt(finalDuration), timeout);
    }
}