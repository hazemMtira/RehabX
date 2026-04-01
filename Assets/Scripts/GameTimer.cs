using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    public static GameTimer Instance { get; private set; }

    public TextMeshProUGUI timerText;

    private float _timeRemaining;
    private float _elapsed;
    private bool  _isRunning;

    /// <summary>Seconds elapsed since the timer last started / reset.</summary>
    public float ElapsedSeconds => _elapsed;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (GameFlowController.Instance?.CurrentConfig != null)
            _timeRemaining = GameFlowController.Instance.CurrentConfig.gameDuration;
        UpdateDisplay();
    }

    void Update()
    {
        if (!_isRunning) return;

        float dt = Time.deltaTime;
        _timeRemaining -= dt;
        _elapsed       += dt;

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            _isRunning     = false;
            UpdateDisplay();
            OnTimerEnd();
            return;
        }

        UpdateDisplay();
    }

    // ── Public controls ───────────────────────────────────────────────────

    public void StartTimer()  { _isRunning = true; }
    public void StopTimer()   { _isRunning = false; }
    public void PauseTimer()  { _isRunning = false; }

    public void ResumeTimer()
    {
        if (_timeRemaining > 0f) _isRunning = true;
    }

    /// <summary>Reset countdown (from CurrentConfig.gameDuration) and immediately start.</summary>
    public void ResetAndStart()
    {
        _elapsed = 0f;

        if (GameFlowController.Instance?.CurrentConfig != null)
            _timeRemaining = GameFlowController.Instance.CurrentConfig.gameDuration;
        else
        {
            Debug.LogWarning("[Timer] No config found — using fallback 60s");
            _timeRemaining = 60f;
        }

        _isRunning = true;
        UpdateDisplay();
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (timerText == null) return;
        int min = Mathf.FloorToInt(_timeRemaining / 60f);
        int sec = Mathf.FloorToInt(_timeRemaining % 60f);
        timerText.text = $"{min:00}:{sec:00}";
    }

    private void OnTimerEnd()
    {
        Debug.Log("[Timer] Time's up!");
        GameFlowController.Instance?.OnGameOver();
    }
}