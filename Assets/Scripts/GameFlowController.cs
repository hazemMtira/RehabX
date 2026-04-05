using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    private ElbowAngleGestureDetector gestureDetector;

    [Header("Experience")]
    public ExperienceConfig experienceConfig;

    [Header("Gameplay objects")]
    public GameObject moleSpawner;
    public GameObject scoreUI;
    public GameObject timerUI;
    public GameObject gameManager;

    [Header("UI")]
    public TextMeshProUGUI scoreText;
    public GameTimer       gameTimer;

    [Header("Hand roots")]
    public GameObject leftHandRoot;
    public GameObject rightHandRoot;

    [Header("Level configs (index 0 = level 1)")]
    public LevelConfig[] levelConfigs;

    [Header("Param schema (describes custom params to the dashboard)")]
    public ParamSchema[] paramSchema;

    [Header("KPI schema (metrics sent back to dashboard after each session)")]
    public KpiSchema[] kpiSchema;

    // ── Runtime state ─────────────────────────────────────────────────────

    public string      ActiveHand    { get; private set; }
    public int         Level         { get; private set; }
    public bool        AutoMode      { get; private set; }
    public LevelConfig CurrentConfig { get; private set; }
    public bool        IsResetting   { get; private set; }

    private readonly Dictionary<string, object> _kpiData = new Dictionary<string, object>();
    private bool _usingRuntimeConfig;
    private bool _levelPassed;

    // ── Cached references ─────────────────────────────────────────────────

    private SpawnManager _spawner;
    private SpawnManager Spawner =>
        _spawner != null ? _spawner : (_spawner = FindFirstObjectByType<SpawnManager>());

    // ─────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        UpdateScoreDisplay(0, 0);
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    // ── Session lifecycle ─────────────────────────────────────────────────

    public void PrepareForSession()
    {
        Debug.Log($"[GFC] PrepareForSession — frame={Time.frameCount} time={Time.time:F2}s");

        _kpiData.Clear();
        _usingRuntimeConfig = false;

        if (moleSpawner) moleSpawner.SetActive(true);
        if (scoreUI)     scoreUI.SetActive(true);
        if (timerUI)     timerUI.SetActive(true);
        if (gameManager) gameManager.SetActive(true);

        if (leftHandRoot)  leftHandRoot.SetActive(false);
        if (rightHandRoot) rightHandRoot.SetActive(false);

        if (gestureDetector != null) gestureDetector.ResetMaxWristSpeed();
    }

    public void SetAutoMode(bool enabled) { AutoMode = enabled; }

    public void SetLevel(int newLevel)
    {
        Level        = Mathf.Clamp(newLevel, 1, levelConfigs?.Length ?? 1);
        _levelPassed = false;
        CurrentConfig = GetConfig(Level);
        _usingRuntimeConfig = false;

        if (!AutoMode) ScoreManager.Instance.ResetAll();
        else           ScoreManager.Instance.ResetLevelScore();

        if (Spawner != null)
            Spawner.currentConfig = CurrentConfig;
    }

    public void StartGameplay()
    {
        if (Spawner == null)
        {
            Debug.LogError("❌ SpawnManager not found!");
            return;
        }

        if (CurrentConfig == null)
        {
            Debug.LogError("❌ CurrentConfig is NULL!");
            return;
        }

        // Push required score to the in-game UI
        var ui = FindFirstObjectByType<GameUIBuilder>();
        if (ui != null) ui.SetTargetScore(CurrentConfig.requiredScore);

        Spawner.currentConfig = CurrentConfig;
        Spawner.StartSpawner();
        GameTimer.Instance?.ResetAndStart();

        Debug.Log("🚀 Gameplay started.");
    }

    // ── Interaction mode ──────────────────────────────────────────────────

    public void SetInteractionMode(string hand)
    {
        Debug.Log($"[GFC] SetInteractionMode — hand={hand} frame={Time.frameCount} time={Time.time:F2}s");
        ActiveHand = hand;
        ApplyHandRoots(hand);
    }

    private void ApplyHandRoots(string hand)
    {
        if (leftHandRoot)  leftHandRoot.SetActive(hand == "left");
        if (rightHandRoot) rightHandRoot.SetActive(hand == "right");

        if (hand == "left" && leftHandRoot != null)
            gestureDetector = leftHandRoot.GetComponentInChildren<ElbowAngleGestureDetector>(true);
        else if (hand == "right" && rightHandRoot != null)
            gestureDetector = rightHandRoot.GetComponentInChildren<ElbowAngleGestureDetector>(true);
        else
            gestureDetector = null;

        if (gestureDetector != null)
        {
            gestureDetector.ForceReinitialize();
            Debug.Log($"[GFC] Detector reinitialized: {gestureDetector.name}");
        }

        Debug.Log("GestureDetector = " + gestureDetector);
    }

    // ── KPI recording ─────────────────────────────────────────────────────

    public void RecordKpi(string key, object value)
    {
        _kpiData[key] = value;
        Debug.Log($"[GFC] KPI recorded: {key} = {value}");
    }

    public Dictionary<string, object> CollectKpiData()
    {
        if (HasKpi("score") && !_kpiData.ContainsKey("score"))
            _kpiData["score"] = ScoreManager.Instance.ScoreThisLevel;

        if (HasKpi("duration") && !_kpiData.ContainsKey("duration"))
            _kpiData["duration"] = Mathf.RoundToInt(GameTimer.Instance?.ElapsedSeconds ?? 0);

        if (HasKpi("maxStrikeSpeed") && !_kpiData.ContainsKey("maxStrikeSpeed") && gestureDetector != null)
            _kpiData["maxStrikeSpeed"] = gestureDetector.GetMaxWristSpeed();

        Debug.Log($"[KPI] Final keys: {string.Join(", ", _kpiData.Keys)}");

        return new Dictionary<string, object>(_kpiData);
    }

    private bool HasKpi(string key)
    {
        if (kpiSchema == null) return false;
        foreach (var k in kpiSchema) if (k != null && k.key == key) return true;
        return false;
    }

    // ── Custom config ─────────────────────────────────────────────────────

    public void ApplyCustomConfig(Dictionary<string, object> p)
    {
        var cfg   = ScriptableObject.CreateInstance<LevelConfig>();
        cfg.level = Level;

        cfg.requiredScore  = GetInt  (p, "requiredScore",  CurrentConfig?.requiredScore  ?? 15);
        cfg.gameDuration   = GetFloat(p, "gameDuration",   CurrentConfig?.gameDuration   ?? 60f);
        cfg.spawnInterval  = GetFloat(p, "spawnInterval",  CurrentConfig?.spawnInterval  ?? 1.25f);
        cfg.moleLifetime   = GetFloat(p, "moleLifetime",   CurrentConfig?.moleLifetime   ?? 8f);
        cfg.moleSpeed      = GetFloat(p, "moleSpeed",      CurrentConfig?.moleSpeed      ?? 2.5f);
        cfg.maxActiveMoles = GetInt  (p, "maxActiveMoles", CurrentConfig?.maxActiveMoles ?? 3);
        cfg.levelPosition  = GetVector3(p, "levelPosition", CurrentConfig?.levelPosition ?? Vector3.zero);

        CurrentConfig       = cfg;
        _usingRuntimeConfig = true;

        if (Spawner != null) Spawner.currentConfig = CurrentConfig;

        Debug.Log($"[GFC] Custom config applied: requiredScore={cfg.requiredScore} " +
                  $"gameDuration={cfg.gameDuration} spawnInterval={cfg.spawnInterval} " +
                  $"moleLifetime={cfg.moleLifetime} moleSpeed={cfg.moleSpeed} " +
                  $"maxActiveMoles={cfg.maxActiveMoles}");
    }

    // ── Param helpers ─────────────────────────────────────────────────────

    static float GetFloat(Dictionary<string, object> d, string key, float fallback)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is float  f)  return f;
        if (v is int    i)  return i;
        if (v is double db) return (float)db;
        float.TryParse(v.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result);
        return result == 0 ? fallback : result;
    }

    static int GetInt(Dictionary<string, object> d, string key, int fallback)
        => (int)GetFloat(d, key, fallback);

    static Vector3 GetVector3(Dictionary<string, object> d, string key, Vector3 fallback)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is Vector3 vec) return vec;

        if (v is Dictionary<string, object> dict)
        {
            float x = dict.ContainsKey("x") ? float.Parse(dict["x"].ToString()) : 0;
            float y = dict.ContainsKey("y") ? float.Parse(dict["y"].ToString()) : 0;
            float z = dict.ContainsKey("z") ? float.Parse(dict["z"].ToString()) : 0;
            return new Vector3(x, y, z);
        }

        var parts = v.ToString().Split(',');
        if (parts.Length == 3)
        {
            float.TryParse(parts[0], out float x);
            float.TryParse(parts[1], out float y);
            float.TryParse(parts[2], out float z);
            return new Vector3(x, y, z);
        }

        return fallback;
    }

    // ── Score ─────────────────────────────────────────────────────────────

    void HandleScoreChanged(int total, int thisLevel)
    {
        UpdateScoreDisplay(total, thisLevel);
        _kpiData["score"] = thisLevel;

        if (!_levelPassed && CurrentConfig != null && thisLevel >= CurrentConfig.requiredScore)
        {
            _levelPassed = true;
            OnLevelCompleted();
        }
    }

    void UpdateScoreDisplay(int total, int thisLevel)
    {
        if (scoreText != null) scoreText.text = "Score: " + thisLevel;
    }

    // ── Level complete ────────────────────────────────────────────────────

    void OnLevelCompleted()
    {
        Spawner?.StopSpawner();

        _kpiData["score"]    = ScoreManager.Instance.ScoreThisLevel;
        _kpiData["duration"] = Mathf.RoundToInt(GameTimer.Instance?.ElapsedSeconds ?? 0);

        if (HasKpi("maxStrikeSpeed") && gestureDetector != null)
            _kpiData["maxStrikeSpeed"] = gestureDetector.GetMaxWristSpeed();

        if (AutoMode && Level < (levelConfigs?.Length ?? 1) && !_usingRuntimeConfig)
        {
            SupabaseGameBridge.Instance?.NotifyLevelResult(false);
            StartCoroutine(AutoNextLevel());
        }
        else
        {
            SupabaseGameBridge.Instance?.NotifyLevelResult(false,
                () => SupabaseGameBridge.Instance?.NotifySessionEnd());
            StartCoroutine(ResetToLevelSelection());
        }
    }

    // ── Game over ─────────────────────────────────────────────────────────

    public void OnGameOver()
    {
        Spawner?.StopSpawner();

        _kpiData["score"]    = ScoreManager.Instance.ScoreThisLevel;
        _kpiData["duration"] = Mathf.RoundToInt(GameTimer.Instance?.ElapsedSeconds ?? 0);

        if (HasKpi("maxStrikeSpeed") && gestureDetector != null)
            _kpiData["maxStrikeSpeed"] = gestureDetector.GetMaxWristSpeed();

        SupabaseGameBridge.Instance?.NotifyLevelResult(true,
            () => SupabaseGameBridge.Instance?.NotifySessionEnd());

        StartCoroutine(ResetToLevelSelection());
    }

    // ── Auto advance ──────────────────────────────────────────────────────

    IEnumerator AutoNextLevel()
    {
        yield return new WaitForSecondsRealtime(1.5f);

        Level         = Mathf.Clamp(Level + 1, 1, levelConfigs?.Length ?? 1);
        _levelPassed  = false;
        CurrentConfig = GetConfig(Level);
        ScoreManager.Instance.ResetLevelScore();
        _kpiData.Clear();

        SupabaseGameBridge.Instance?.NotifyLevelAdvance(Level);

        if (moleSpawner) moleSpawner.SetActive(true);
        if (scoreUI)     scoreUI.SetActive(true);
        if (timerUI)     timerUI.SetActive(true);
        if (gameManager) gameManager.SetActive(true);

        if (Spawner != null) StartCoroutine(DelayBeforeStart(Spawner, 0.75f));
    }

    IEnumerator DelayBeforeStart(SpawnManager spawner, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        spawner.currentConfig = CurrentConfig;
        spawner.StartSpawner();
        GameTimer.Instance?.ResetAndStart();
    }

    // ── Reset ─────────────────────────────────────────────────────────────

    public IEnumerator ResetToLevelSelection()
    {
        IsResetting = true;
        yield return new WaitForSecondsRealtime(2f);

        GameTimer.Instance?.StopTimer();
        Spawner?.StopSpawner();

        if (moleSpawner) moleSpawner.SetActive(false);
        if (scoreUI)     scoreUI.SetActive(false);
        if (timerUI)     timerUI.SetActive(false);
        if (gameManager) gameManager.SetActive(false);

        if (leftHandRoot)  leftHandRoot.SetActive(true);
        if (rightHandRoot) rightHandRoot.SetActive(true);

        ScoreManager.Instance.ResetAll();
        _kpiData.Clear();

        if (_usingRuntimeConfig && CurrentConfig != null)
        {
            Destroy(CurrentConfig);
            _usingRuntimeConfig = false;
        }

        Level         = 0;
        _levelPassed  = false;
        AutoMode      = false;
        CurrentConfig = null;
        ActiveHand    = null;
        IsResetting   = false;

        Debug.Log("[GFC] Reset complete.");
    }

    LevelConfig GetConfig(int level)
    {
        if (levelConfigs == null || levelConfigs.Length == 0) return null;
        return levelConfigs[Mathf.Clamp(level - 1, 0, levelConfigs.Length - 1)];
    }
}