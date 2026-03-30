/*using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
//a verifier 
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    [Header("Gameplay objects")]
    public GameObject bubbleSpawner;
    public GameObject scoreUI;
    public GameObject timerUI;
    public GameObject gameManager;

    [Header("UI")]
    public TextMeshProUGUI scoreText;

    [Header("Controllers")]
    public GameTimer gameTimer;

    [Header("Hand roots")]
    public GameObject leftHandRoot;
    public GameObject rightHandRoot;

    [Header("Level configs (index 0 = level 1)")]
    public LevelConfig[] levelConfigs;

    [Header("Param schema (describes custom params to the dashboard)")]
    public ParamSchema[] paramSchema;

    public int         Level         { get; private set; }
    public bool        AutoMode      { get; private set; }
    public LevelConfig CurrentConfig { get; private set; }
    public BubbleManager Spawner     { get; private set; }
    public bool        IsResetting   { get; private set; } = false;

    private bool _usingRuntimeConfig = false;
    private bool _levelPassed        = false;

    // ─────────────────────────────────────────────────────────────────────
    #region Unity lifecycle

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        Spawner = FindObjectOfType<BubbleManager>();
        ScoreManager.Instance.OnScoreChanged += HandleScoreChanged;
        UpdateScoreDisplay(0, 0);
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= HandleScoreChanged;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Session lifecycle

    public void PrepareForSession()
    {
        _usingRuntimeConfig = false;
        if (bubbleSpawner) bubbleSpawner.SetActive(true);
        if (scoreUI)       scoreUI.SetActive(true);
        if (timerUI)       timerUI.SetActive(true);
        if (gameManager)   gameManager.SetActive(true);
        if (leftHandRoot)  leftHandRoot.SetActive(false);
        if (rightHandRoot) rightHandRoot.SetActive(false);
        bubbleSpawner?.GetComponent<BubbleManager>()?.HardStart("PrepareForSession");
    }

    public void SetAutoMode(bool enabled) { AutoMode = enabled; }

    public void SetLevel(int newLevel)
    {
        Level         = Mathf.Clamp(newLevel, 1, levelConfigs?.Length ?? 1);
        _levelPassed  = false;
        CurrentConfig = GetConfig(Level);
        _usingRuntimeConfig = false;
        if (!AutoMode) ScoreManager.Instance.ResetAll();
        else           ScoreManager.Instance.ResetLevelScore();
        var sp = FindObjectOfType<BubbleManager>();
        if (sp != null) sp.allowSpawning = true;
    }

    /// <summary>
    /// Builds a runtime LevelConfig from the flat dictionary the dashboard
    /// sends in custom_params. Handles all bubble-specific parameters.
    /// Unknown keys are silently ignored so new params don't break old APKs.
    /// </summary>
    public void ApplyCustomConfig(Dictionary<string, object> p)
    {
        var cfg = ScriptableObject.CreateInstance<LevelConfig>();
        cfg.level = Level;

        // ── Progression ───────────────────────────────────────────────────
        cfg.targetScore = GetInt  (p, "targetScore", CurrentConfig?.targetScore ?? 15);
        cfg.totalTime   = GetFloat(p, "totalTime",   CurrentConfig?.totalTime   ?? 300f);

        // ── Spawn positioning ─────────────────────────────────────────────
        cfg.spawnDistance = GetFloat(p, "spawnDistance", CurrentConfig?.spawnDistance ?? 2f);

        // horizontalSpread is the symmetric half-width: [-spread, +spread]
        float hSpread = GetFloat(p, "horizontalSpread",
            CurrentConfig != null ? CurrentConfig.horizontalRange.y : 0.5f);
        cfg.horizontalRange = new Vector2(-hSpread, hSpread);

        float vMin = GetFloat(p, "verticalMin",
            CurrentConfig != null ? CurrentConfig.verticalRange.x : 0.5f);
        float vMax = GetFloat(p, "verticalMax",
            CurrentConfig != null ? CurrentConfig.verticalRange.y : 1.5f);
        cfg.verticalRange = new Vector2(vMin, vMax);

        // ── Spawn timing ──────────────────────────────────────────────────
        cfg.spawnInterval = GetFloat(p, "spawnInterval", CurrentConfig?.spawnInterval ?? 2f);
        cfg.poolSize      = GetInt  (p, "poolSize",      CurrentConfig?.poolSize      ?? 20);

        // ── Lifetime ──────────────────────────────────────────────────────
        cfg.minLifetime = GetFloat(p, "minLifetime", CurrentConfig?.minLifetime ?? 2f);
        cfg.maxLifetime = GetFloat(p, "maxLifetime", CurrentConfig?.maxLifetime ?? 5f);

        // ── Accessibility ─────────────────────────────────────────────────
        cfg.bubbleScale = GetFloat(p, "bubbleScale", CurrentConfig?.bubbleScale ?? 1f);

        CurrentConfig       = cfg;
        _usingRuntimeConfig = true;

        // Apply bubble scale immediately if spawner is active
        ApplyBubbleScale(cfg.bubbleScale);

        Debug.Log($"[GFC] Custom config applied — spawn:{cfg.spawnInterval:F2}s  " +
                  $"lifetime:{cfg.minLifetime:F1}-{cfg.maxLifetime:F1}s  " +
                  $"spread:{hSpread:F2}  h:{vMin:F2}-{vMax:F2}  scale:{cfg.bubbleScale:F2}");
    }

    /// <summary>
    /// Sets the localScale of the bubble prefab root so all spawned bubbles
    /// use the age-appropriate size without changing the prefab asset.
    /// </summary>
    void ApplyBubbleScale(float scale)
    {
        if (scale <= 0f || Mathf.Approximately(scale, 1f)) return;

        var spawner = FindObjectOfType<BubbleManager>();
        if (spawner == null || spawner.bubblePrefab == null) return;

        // Scale already-active bubbles in pool
        foreach (var b in FindObjectsOfType<BubblePopper>())
            b.transform.localScale = Vector3.one * scale;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Param dict helpers

    static float GetFloat(Dictionary<string, object> d, string key, float fallback)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is float  f)  return f;
        if (v is int    i)  return i;
        if (v is double db) return (float)db;
        float.TryParse(v.ToString(),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float r);
        return r == 0 ? fallback : r;
    }

    static int GetInt(Dictionary<string, object> d, string key, int fallback)
        => (int)GetFloat(d, key, fallback);

    static bool GetBool(Dictionary<string, object> d, string key, bool fallback)
    {
        if (!d.TryGetValue(key, out var v) || v == null) return fallback;
        if (v is bool b) return b;
        return v.ToString() == "1" || v.ToString().ToLower() == "true";
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Score

    void HandleScoreChanged(int total, int thisLevel)
    {
        UpdateScoreDisplay(total, thisLevel);
        if (!_levelPassed && CurrentConfig != null && thisLevel >= CurrentConfig.targetScore)
        {
            _levelPassed = true;
            OnLevelCompleted();
        }
    }

    void UpdateScoreDisplay(int total, int thisLevel)
    {
        if (scoreText != null) scoreText.text = "Score: " + thisLevel;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Level complete

    void OnLevelCompleted()
    {
        FindObjectOfType<BubbleManager>()?.StopSpawner();

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

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Game over

    public void OnGameOver()
    {
        FindObjectOfType<BubbleManager>()?.StopSpawner();
        SupabaseGameBridge.Instance?.NotifyLevelResult(true,
            () => SupabaseGameBridge.Instance?.NotifySessionEnd());
        StartCoroutine(ResetToLevelSelection());
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Auto advance

    IEnumerator AutoNextLevel()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        Level         = Mathf.Clamp(Level + 1, 1, levelConfigs?.Length ?? 1);
        _levelPassed  = false;
        CurrentConfig = GetConfig(Level);
        ScoreManager.Instance.ResetLevelScore();
        SupabaseGameBridge.Instance?.NotifyLevelAdvance(Level);

        if (bubbleSpawner) bubbleSpawner.SetActive(true);
        if (scoreUI)       scoreUI.SetActive(true);
        if (timerUI)       timerUI.SetActive(true);
        if (gameManager)   gameManager.SetActive(true);

        var sp = bubbleSpawner?.GetComponent<BubbleManager>();
        if (sp != null) StartCoroutine(DelayBeforeStart(sp, 0.75f));
    }

    IEnumerator DelayBeforeStart(BubbleManager spawner, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        spawner.allowSpawning = true;
        spawner.HardStart("AutoNextLevel");
        GameTimer.Instance?.ResetAndStart();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Reset

    public IEnumerator ResetToLevelSelection()
    {
        IsResetting = true;
        yield return new WaitForSecondsRealtime(2f);

        GameTimer.Instance?.StopTimer();
        FindObjectOfType<BubbleManager>()?.StopSpawner();
        if (bubbleSpawner) bubbleSpawner.SetActive(false);
        if (scoreUI)       scoreUI.SetActive(false);
        if (timerUI)       timerUI.SetActive(false);
        if (gameManager)   gameManager.SetActive(false);
        if (leftHandRoot)  leftHandRoot.SetActive(true);
        if (rightHandRoot) rightHandRoot.SetActive(true);
        ScoreManager.Instance.ResetAll();

        if (_usingRuntimeConfig && CurrentConfig != null)
        {
            Destroy(CurrentConfig);
            _usingRuntimeConfig = false;
        }

        Level = 0; _levelPassed = false; AutoMode = false; CurrentConfig = null;
        IsResetting = false;
        Debug.Log("[GFC] ✅ Reset complete.");
    }

    LevelConfig GetConfig(int level)
    {
        if (levelConfigs == null || levelConfigs.Length == 0) return null;
        return levelConfigs[Mathf.Clamp(level - 1, 0, levelConfigs.Length - 1)];
    }

    #endregion
}*/