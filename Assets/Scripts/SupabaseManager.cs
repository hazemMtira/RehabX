using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Handles all communication with Supabase REST API.
/// Attach to a persistent GameObject in your Loading Scene.
/// Flow:
///   1. On Start → polls for a 'pending' session every 2 seconds
///   2. When found → loads settings into GameSessionSettings → fires OnSessionLoaded
///   3. RoundController calls SetSessionActive() when round starts
///   4. RoundController calls SubmitResults() when round ends
///   5. EndGameMenuController calls RequestRestart() when patient wants to replay
/// </summary>
public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance { get; private set; }

    // ---------------------------------------------------------------
    // Config
    // ---------------------------------------------------------------

    const string URL = "https://ojselmwfjaahqnzrapzf.supabase.co/rest/v1";
    const string KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9qc2VsbXdmamFhaHFuenJhcHpmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzMyMjg0MzUsImV4cCI6MjA4ODgwNDQzNX0.bVm9zGN0PDHRGl_zHg746Z2bL6FfHVHsYAdj2JJPgAU";

    [Header("Settings")]
    [Tooltip("How often (seconds) to poll for a pending session while waiting.")]
    public float pollInterval = 2f;

    // ---------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------

    /// <summary>Fired when a pending session is found and loaded into GameSessionSettings.</summary>
    public event Action OnSessionLoaded;

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    bool _polling = false;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

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
    // Polling
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
        StopAllCoroutines();
        Debug.Log("[Supabase] Polling stopped.");
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
        string cutoff  = DateTime.UtcNow.AddSeconds(-30).ToString("o");
        string endpoint = $"{URL}/sessions?session_status=eq.pending&order=created_at.desc&limit=1&select=*&created_at=gte.{cutoff}";

        using var req = UnityWebRequest.Get(endpoint);
        AddHeaders(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Supabase] Poll error: {req.error}");
            yield break;
        }

        string json = req.downloadHandler.text;
        Debug.Log($"[Supabase] Poll response: {json}");

        var wrapper = JsonUtility.FromJson<SessionArrayWrapper>("{\"items\":" + json + "}");

        if (wrapper == null || wrapper.items == null || wrapper.items.Length == 0)
            yield break;

        SessionData s = wrapper.items[0];
        LoadSessionIntoSettings(s);

        StopPolling();
        OnSessionLoaded?.Invoke();

        Debug.Log($"[Supabase] ✅ Session loaded: {s.id} | difficulty={s.difficulty_name} | hand={s.hand_used}");
    }

    // ---------------------------------------------------------------
    // Load session settings into GameSessionSettings
    // ---------------------------------------------------------------

    void LoadSessionIntoSettings(SessionData s)
    {
        var gs = GameSessionSettings.Instance;
        if (gs == null) { Debug.LogError("[Supabase] GameSessionSettings.Instance is null!"); return; }

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
            DifficultyData match = null;
            foreach (var p in presets)
            {
                if (string.Equals(p.name, s.difficulty_name, StringComparison.OrdinalIgnoreCase))
                { match = p; break; }
            }
            if (match != null)
                gs.selectedDifficulty = match;
            else
                Debug.LogWarning($"[Supabase] No DifficultyData preset found for '{s.difficulty_name}'.");
        }
    }

    // ---------------------------------------------------------------
    // Set session active — called by RoundController on round start
    // ---------------------------------------------------------------

    public void SetSessionActive(string sessionId, Action onDone = null)
    {
        StartCoroutine(PatchSession(sessionId,
            "{\"session_status\":\"active\",\"start_time\":\"" + DateTime.UtcNow.ToString("o") + "\"}",
            onDone));
    }

    // ---------------------------------------------------------------
    // Submit results — called by RoundController on round end
    // ---------------------------------------------------------------

    public void SubmitResults(string sessionId, int finalScore, float roundDuration, float maxStrikeSpeed, Action onDone = null)
    {
        StartCoroutine(DoSubmitResults(sessionId, finalScore, roundDuration, maxStrikeSpeed, onDone));
    }

    IEnumerator DoSubmitResults(string sessionId, int finalScore, float roundDuration, float maxStrikeSpeed, Action onDone)
    {
        // Mark session finished
        string endTime      = DateTime.UtcNow.ToString("o");
        string sessionPatch = $"{{\"session_status\":\"finished\",\"end_time\":\"{endTime}\"}}";
        yield return StartCoroutine(PatchSession(sessionId, sessionPatch, null));

        // Insert metrics
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
            Debug.LogError($"[Supabase] Failed to insert metrics: {req.error}\n{req.downloadHandler.text}");
        else
            Debug.Log($"[Supabase] ✅ Metrics submitted. Score={finalScore} Duration={roundDuration:F1}s MaxSpeed={maxStrikeSpeed:F2}m/s");

        onDone?.Invoke();
    }

    // ---------------------------------------------------------------
    // Request restart — called by GameManager when patient hits restart
    // ---------------------------------------------------------------

    public void RequestRestart(string sessionId, Action onDone = null)
    {
        Debug.Log("[Supabase] Restart requested by patient.");
        StartCoroutine(PatchSession(sessionId,
            "{\"restart_requested\":true}",
            onDone));
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    IEnumerator PatchSession(string sessionId, string jsonBody, Action onDone)
    {
        string endpoint = $"{URL}/sessions?id=eq.{sessionId}";
        using var req   = new UnityWebRequest(endpoint, "PATCH");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
        req.downloadHandler = new DownloadHandlerBuffer();
        AddHeaders(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[Supabase] PATCH error: {req.error}\n{req.downloadHandler.text}");
        else
            Debug.Log($"[Supabase] Session patched: {jsonBody}");

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

    [Serializable]
    class SessionArrayWrapper
    {
        public SessionData[] items;
    }

    [Serializable]
    class SessionData
    {
        public string id;
        public string difficulty_name;
        public string hand_used;
        public string level_name;
        public string patient_name;
        public string therapist_name;
        public int    score_needed;
        public float  round_duration_setting;
        public float  custom_spawn_interval;
        public float  custom_mole_speed;
        public float  custom_mole_lifetime;
        public int    custom_max_active_moles;
        public string session_status;
    }
}