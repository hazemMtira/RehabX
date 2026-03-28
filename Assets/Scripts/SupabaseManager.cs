using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles all Supabase REST API communication.
/// Polls for session_cancelled and session_paused flags during active rounds.
/// </summary>
public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance { get; private set; }

    const string URL = "https://ojselmwfjaahqnzrapzf.supabase.co/rest/v1";
    const string KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9qc2VsbXdmamFhaHFuenJhcHpmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzMyMjg0MzUsImV4cCI6MjA4ODgwNDQzNX0.bVm9zGN0PDHRGl_zHg746Z2bL6FfHVHsYAdj2JJPgAU";

    [Header("Settings")]
    public float pollInterval               = 2f;
    public float cancellationCheckInterval  = 3f;
    public float pauseCheckInterval         = 2f;   // how often to poll for pause during active round
    public string loadingSceneName          = "LoadingScene";

    public event Action OnSessionLoaded;

    // ── Pause / Resume events ──────────────────────────────────────
    /// <summary>Fired when the therapist pauses the session from the dashboard.</summary>
    public event Action OnPaused;
    /// <summary>Fired when the therapist resumes the session from the dashboard.</summary>
    public event Action OnResumed;

    bool _polling    = false;
    bool _monitoring = false;  // cancellation monitor
    bool _pauseMonitoring = false;
    bool _isPaused   = false;  // last known pause state — avoids repeat events

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartPolling();
    }

    // ---------------------------------------------------------------
    // Polling for pending session
    // ---------------------------------------------------------------

    public void StartPolling()
    {
        if (_polling) return;
        _polling = true;
        StartCoroutine(PollForPendingSession());
        Debug.Log("[Supabase] Started polling for pending session...");
    }

    public void StopPolling()
    {
        _polling = false;
        StopCoroutine(nameof(PollForPendingSession));
        Debug.Log("[Supabase] Polling stopped.");
    }

    public void ResetForNewSession()
    {
        _polling         = false;
        _monitoring      = false;
        _pauseMonitoring = false;
        _isPaused        = false;
        StopAllCoroutines();

        if (GameSessionSettings.Instance != null)
        {
            GameSessionSettings.Instance.sessionId          = null;
            GameSessionSettings.Instance.selectedDifficulty = null;
        }

        StartPolling();
        Debug.Log("[Supabase] Reset — polling for new session.");
    }

    IEnumerator PollForPendingSession()
    {
        while (_polling)
        {
            yield return StartCoroutine(FetchPendingSession());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    IEnumerator FetchPendingSession()
    {
        if (HeadsetManager.Instance == null || !HeadsetManager.Instance.IsRegistered)
            yield break;

        string headsetId = HeadsetManager.Instance.HeadsetId;
        string cutoff    = DateTime.UtcNow.AddSeconds(-30).ToString("o");

        string endpoint = $"{URL}/sessions" +
            $"?session_status=eq.pending" +
            $"&headset_id=eq.{headsetId}" +
            $"&session_cancelled=eq.false" +
            $"&order=created_at.desc" +
            $"&limit=1&select=*" +
            $"&created_at=gte.{cutoff}";

        using var req = UnityWebRequest.Get(endpoint);
        AddHeaders(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Supabase] Poll error: {req.error}");
            yield break;
        }

        string json  = req.downloadHandler.text;
        var wrapper  = JsonUtility.FromJson<SessionArrayWrapper>("{\"items\":" + json + "}");

        if (wrapper == null || wrapper.items == null || wrapper.items.Length == 0)
            yield break;

        SessionData s = wrapper.items[0];
        LoadSessionIntoSettings(s);

        StopPolling();
        OnSessionLoaded?.Invoke();

        Debug.Log($"[Supabase] ✅ Session loaded: {s.id} | difficulty={s.difficulty_name} | hand={s.hand_used}");
    }

    // ---------------------------------------------------------------
    // Cancellation monitoring — runs during active round
    // ---------------------------------------------------------------

    public void StartCancellationMonitoring(string sessionId)
    {
        if (_monitoring) return;
        _monitoring = true;
        StartCoroutine(MonitorCancellation(sessionId));
        Debug.Log("[Supabase] Started cancellation monitoring.");
    }

    public void StopCancellationMonitoring()
    {
        _monitoring = false;
        Debug.Log("[Supabase] Cancellation monitoring stopped.");
    }

    IEnumerator MonitorCancellation(string sessionId)
    {
        while (_monitoring)
        {
            yield return new WaitForSecondsRealtime(cancellationCheckInterval);

            string endpoint = $"{URL}/sessions?id=eq.{sessionId}&select=session_cancelled";
            using var req   = UnityWebRequest.Get(endpoint);
            AddHeaders(req);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) continue;

            string json = req.downloadHandler.text;
            var wrapper = JsonUtility.FromJson<CancelArrayWrapper>("{\"items\":" + json + "}");

            if (wrapper?.items == null || wrapper.items.Length == 0) continue;

            if (wrapper.items[0].session_cancelled)
            {
                Debug.Log("[Supabase] ⚠ Session cancelled by therapist — returning to loading screen.");
                _monitoring = false;
                StopPauseMonitoring();
                ReturnToLoadingScene();
                yield break;
            }
        }
    }

    void ReturnToLoadingScene()
    {
        if (GameSessionSettings.Instance != null)
            GameSessionSettings.Instance.sessionId = null;

        // Make sure timescale is restored before leaving the scene
        Time.timeScale = 1f;

        SceneManager.LoadScene(loadingSceneName);
    }

    // ---------------------------------------------------------------
    // Pause monitoring — runs during active round
    // ---------------------------------------------------------------

    /// <summary>
    /// Start polling Supabase for session_paused changes.
    /// Call this at the same time as StartCancellationMonitoring.
    /// Subscribe to OnPaused / OnResumed to react in your game manager.
    /// </summary>
    public void StartPauseMonitoring(string sessionId)
    {
        if (_pauseMonitoring) return;
        _pauseMonitoring = true;
        _isPaused        = false;
        StartCoroutine(MonitorPause(sessionId));
        Debug.Log("[Supabase] Started pause monitoring.");
    }

    public void StopPauseMonitoring()
    {
        _pauseMonitoring = false;
        Debug.Log("[Supabase] Pause monitoring stopped.");
    }

    IEnumerator MonitorPause(string sessionId)
    {
        while (_pauseMonitoring)
        {
            // WaitForSecondsRealtime ignores Time.timeScale — essential so this
            // coroutine keeps polling even while the game is paused (timeScale = 0)
            yield return new WaitForSecondsRealtime(pauseCheckInterval);

            string endpoint = $"{URL}/sessions?id=eq.{sessionId}&select=session_paused";
            using var req   = UnityWebRequest.Get(endpoint);
            AddHeaders(req);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success) continue;

            string json = req.downloadHandler.text;
            var wrapper = JsonUtility.FromJson<PauseArrayWrapper>("{\"items\":" + json + "}");

            if (wrapper?.items == null || wrapper.items.Length == 0) continue;

            bool serverPaused = wrapper.items[0].session_paused;

            // Only fire events on transitions to avoid spamming
            if (serverPaused && !_isPaused)
            {
                _isPaused = true;
                Debug.Log("[Supabase] ⏸ Session paused by therapist.");
                OnPaused?.Invoke();
            }
            else if (!serverPaused && _isPaused)
            {
                _isPaused = false;
                Debug.Log("[Supabase] ▶ Session resumed by therapist.");
                OnResumed?.Invoke();
            }
        }
    }

    // ---------------------------------------------------------------
    // Session lifecycle
    // ---------------------------------------------------------------

    public void SetSessionActive(string sessionId, Action onDone = null)
    {
        StartCoroutine(PatchSession(sessionId,
            "{\"session_status\":\"active\",\"start_time\":\"" + DateTime.UtcNow.ToString("o") + "\"}",
            onDone));

        // Start both monitors when the round goes active
        StartCancellationMonitoring(sessionId);
        StartPauseMonitoring(sessionId);
    }

    public void SubmitResults(string sessionId, int finalScore, float roundDuration,
        float maxStrikeSpeed, Action onDone = null)
    {
        StopCancellationMonitoring();
        StopPauseMonitoring();

        // Always restore timescale on finish in case we were paused
        Time.timeScale = 1f;

        StartCoroutine(DoSubmitResults(sessionId, finalScore, roundDuration, maxStrikeSpeed, onDone));
    }

    IEnumerator DoSubmitResults(string sessionId, int finalScore, float roundDuration,
        float maxStrikeSpeed, Action onDone)
    {
        string endTime      = DateTime.UtcNow.ToString("o");
        string sessionPatch = $"{{\"session_status\":\"finished\",\"end_time\":\"{endTime}\"}}";
        yield return StartCoroutine(PatchSession(sessionId, sessionPatch, null));

        string metricsBody = $"{{" +
            $"\"session_id\":\"{sessionId}\"," +
            $"\"final_score\":{finalScore}," +
            $"\"round_duration\":{roundDuration.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"max_strike_speed\":{maxStrikeSpeed.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}" +
            $"}}";

        using var req = new UnityWebRequest($"{URL}/session_metrics", "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(metricsBody));
        req.downloadHandler = new DownloadHandlerBuffer();
        AddHeaders(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[Supabase] Failed to insert metrics: {req.error}");
        else
            Debug.Log($"[Supabase] ✅ Metrics submitted.");

        onDone?.Invoke();
    }


    public void CancelSession(string sessionId, Action onDone = null)
    {
        StartCoroutine(PatchSession(sessionId,
            "{\"session_cancelled\":true,\"session_status\":\"finished\"}", onDone));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    void LoadSessionIntoSettings(SessionData s)
    {
        var gs = GameSessionSettings.Instance;
        if (gs == null) return;

        gs.sessionId         = s.id;
        gs.patientName       = s.patient_name;
        gs.therapistName     = s.therapist_name;
        gs.selectedStageName = s.level_name;
        gs.selectedHand      = s.hand_used.ToLower() == "left" ? HandType.Left : HandType.Right;

        if (s.difficulty_name == "Custom")
        {
            var custom = ScriptableObject.CreateInstance<DifficultyData>();
            custom.name           = "Custom";
            custom.gameDuration   = s.round_duration_setting > 0 ? s.round_duration_setting : 60f;
            custom.requiredScore  = s.score_needed > 0 ? s.score_needed : 10;
            custom.spawnInterval  = s.custom_spawn_interval > 0 ? s.custom_spawn_interval : 1.5f;
            custom.moleSpeed      = s.custom_mole_speed > 0 ? s.custom_mole_speed : 1f;
            custom.moleLifetime   = s.custom_mole_lifetime > 0 ? s.custom_mole_lifetime : 3f;
            custom.maxActiveMoles = s.custom_max_active_moles > 0 ? s.custom_max_active_moles : 3;
            gs.selectedDifficulty = custom;
        }
        else
        {
            var presets = Resources.LoadAll<DifficultyData>("Difficulties");
            foreach (var p in presets)
            {
                if (string.Equals(p.name, s.difficulty_name, StringComparison.OrdinalIgnoreCase))
                { gs.selectedDifficulty = p; break; }
            }
        }
    }

    IEnumerator PatchSession(string sessionId, string jsonBody, Action onDone)
    {
        string endpoint = $"{URL}/sessions?id=eq.{sessionId}";
        using var req   = new UnityWebRequest(endpoint, "PATCH");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
        req.downloadHandler = new DownloadHandlerBuffer();
        AddHeaders(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[Supabase] PATCH error: {req.error}");
        else
            Debug.Log($"[Supabase] Patched: {jsonBody}");

        onDone?.Invoke();
    }

    void AddHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("apikey",        KEY);
        req.SetRequestHeader("Authorization", $"Bearer {KEY}");
        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("Prefer",        "return=representation");
    }

    // ---------------------------------------------------------------
    // JSON data classes
    // ---------------------------------------------------------------

    [Serializable] class SessionArrayWrapper { public SessionData[] items; }
    [Serializable] class CancelArrayWrapper  { public CancelData[]  items; }
    [Serializable] class PauseArrayWrapper   { public PauseData[]   items; }

    [Serializable]
    class SessionData
    {
        public string id;
        public string difficulty_name;
        public string hand_used;
        public string level_name;
        public string patient_name;
        public string therapist_name;
        public string headset_id;
        public int    score_needed;
        public float  round_duration_setting;
        public float  custom_spawn_interval;
        public float  custom_mole_speed;
        public float  custom_mole_lifetime;
        public int    custom_max_active_moles;
        public string session_status;
        public bool   session_cancelled;
        public bool   session_paused;
    }

    [Serializable]
    class CancelData
    {
        public bool session_cancelled;
    }

    [Serializable]
    class PauseData
    {
        public bool session_paused;
    }
}