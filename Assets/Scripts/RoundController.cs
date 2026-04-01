using UnityEngine;
using UnityEngine.SceneManagement;

public class RoundController : MonoBehaviour
{
    [Header("References")]
    public SpawnManager spawnManager;
    public ScoreManager scoreManager;
    //public XRManager    xrManager;

    [Header("Gesture Detectors")]
    public ElbowAngleGestureDetector leftHandDetector;
    public ElbowAngleGestureDetector rightHandDetector;

    [Header("UI")]
    public GameUIBuilder _gameUI;

    // ── Private state ─────────────────────────────────────────────────────

    LevelConfig _config;
    bool  _roundActive;
    bool  _isPaused;
    float _elapsed;
    float _maxStrikeSpeed;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    void Start() => InitRound();

    void InitRound()
    {
    _config = GameFlowController.Instance?.CurrentConfig;
//
    if (_config == null)
        {
            Debug.LogError("[RoundController] No LevelConfig available from GameFlowController.");
            return;
        }

        StartRound();
    }

    // ── Round flow ────────────────────────────────────────────────────────

    public void StartRound()
    {
        _roundActive    = true;
        _isPaused       = false;
        _elapsed        = 0f;
        _maxStrikeSpeed = 0f;

        ApplyHandSetting();

        var detector = GetActiveDetector();
        if (detector != null) detector.ResetMaxWristSpeed();

        GameManager.Instance.SetGameState(GameState.Playing);

        scoreManager.ResetLevelScore();

        // FIX: signature matches Action<int, int>
        scoreManager.OnScoreChanged += EvaluateScore;

        if (_gameUI != null) _gameUI.UpdateScore(0);

        spawnManager.currentConfig = _config;
        spawnManager.StartSpawner();

        if (AudioManager.Instance != null) AudioManager.Instance.StartMusic();
       // if (xrManager != null)            xrManager.SwapToGameplay();

        Debug.Log("[RoundController] Round started.");
    }

    void Update()
    {
        if (!_roundActive || _isPaused) return;

        _elapsed += Time.deltaTime;
        UpdateTimerUI(_elapsed);

        if (_elapsed >= _config.gameDuration)
            EndRound(true);
    }

    // ── Hand control ──────────────────────────────────────────────────────

    void ApplyHandSetting()
    {
        var hand = GameFlowController.Instance?.ActiveHand;

        bool useLeft  = hand == "left"  || hand == "both" || string.IsNullOrEmpty(hand);
        bool useRight = hand == "right" || hand == "both" || string.IsNullOrEmpty(hand);

        if (leftHandDetector  != null) leftHandDetector.gameObject.SetActive(useLeft);
        if (rightHandDetector != null) rightHandDetector.gameObject.SetActive(useRight);
    }

    ElbowAngleGestureDetector GetActiveDetector()
    {
        var hand = GameFlowController.Instance?.ActiveHand;
        return hand == "left" ? leftHandDetector : rightHandDetector;
    }

    // ── Timer UI ──────────────────────────────────────────────────────────

    void UpdateTimerUI(float elapsed)
    {
        if (_gameUI == null || _config == null) return;
        float remaining = Mathf.Max(0f, _config.gameDuration - elapsed);
        _gameUI.UpdateTimer(remaining);
    }

    // ── Score ─────────────────────────────────────────────────────────────

    // FIX: two-arg signature to match Action<int, int>
    void EvaluateScore(int total, int thisLevel)
    {
        if (!_roundActive) return;
        if (thisLevel >= _config.requiredScore)
            EndRound(false);
    }

    // ── Max strike speed ──────────────────────────────────────────────────

    public void RecordStrikeSpeed(float speed)
    {
        if (speed > _maxStrikeSpeed) _maxStrikeSpeed = speed;
    }

    // ── End round ─────────────────────────────────────────────────────────

    void EndRound(bool timeout)
    {
        if (!_roundActive) return;

        _roundActive   = false;
        _isPaused      = false;
        Time.timeScale = 1f;

        spawnManager.StopSpawner();

        // FIX: unsubscribe matching signature
        scoreManager.OnScoreChanged -= EvaluateScore;

        if (AudioManager.Instance != null) AudioManager.Instance.StopMusic();

        // FIX: ScoreManager has no .Score property — correct property is .ScoreThisLevel
        int   finalScore    = scoreManager.ScoreThisLevel;
        float finalDuration = _elapsed;

        GameManager.Instance.SetGameState(GameState.GameOver);
        //if (xrManager != null) xrManager.SwapToEndGame();

        Debug.Log($"[RoundController] Round ended. Timeout={timeout} Score={finalScore} Duration={finalDuration:F1}s MaxSpeed={_maxStrikeSpeed:F2}");

        var bridge    = SupabaseGameBridge.Instance;
        var sessionId = SupabaseGameBridge.Instance?.CurrentSessionId;

        if (bridge != null && !string.IsNullOrEmpty(sessionId))
        {
            GameFlowController.Instance?.RecordKpi("maxStrikeSpeed", _maxStrikeSpeed);
            bridge.NotifyLevelResult(timeout, GoToLoadingScene);
        }
        else
        {
            GoToLoadingScene();
        }

        GameManager.Instance.HandleRoundEnd(finalScore, Mathf.FloorToInt(finalDuration), timeout);
    }

    // ── Scene transition ──────────────────────────────────────────────────

    void GoToLoadingScene()
    {
        SceneManager.LoadScene("LoadingScene");
    }
}