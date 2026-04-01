using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class SupabaseGameBridge : MonoBehaviour
{
    public static SupabaseGameBridge Instance { get; private set; }

    const string BASE_URL = "https://xrtyqkpeawirpcubajyw.supabase.co/rest/v1";

    [Header("Supabase")]
    public string KEY;

    const string PREFS_ID_KEY = "headset_uuid";
    const float  HEARTBEAT_INTERVAL = 5f;

    [Header("UI")]
    public TextMeshProUGUI loadingText;

    // ── Public state ──────────────────────────────────────────────────────
    public string HeadsetId        { get; private set; }
    public string HeadsetName      { get; private set; }
    public bool   IsGameInProgress { get; private set; }
    public string CurrentSessionId { get; private set; }

    private bool  _isPaused    = false;
    private bool  _stopHandled = false;
    private float _sessionStart;
    private bool  _initialized = false;

    // ─────────────────────────────────────────────────────────────────────
    #region Unity lifecycle

    void Awake()
    {
        System.Net.ServicePointManager.ServerCertificateValidationCallback = (_, __, ___, ____) => true;
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() => StartCoroutine(Init());

    void OnEnable()
    {
        if (!_initialized) return;
        StopAllCoroutines();
        StartCoroutine(HeartbeatLoop());
        StartCoroutine(SessionPollLoop());
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Init

    IEnumerator Init()
    {
        yield return null;

        HeadsetId   = PlayerPrefs.HasKey(PREFS_ID_KEY)
            ? PlayerPrefs.GetString(PREFS_ID_KEY)
            : NewUuid();
        HeadsetName = DetectHeadsetModel();

        ShowLoading("Connecting…");
        yield return UpsertHeadset();
        yield return WaitForGameFlowController();
        yield return SyncExperienceToSupabase();

        _initialized = true;
        HideLoading();
        StartCoroutine(HeartbeatLoop());
        StartCoroutine(SessionPollLoop());
    }

    IEnumerator WaitForGameFlowController(float timeout = 5f)
    {
        float elapsed = 0f;
        while (GameFlowController.Instance == null && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        if (GameFlowController.Instance == null)
            Debug.LogWarning("[Bridge] GameFlowController not found after " + timeout + "s — schema sync skipped.");
        else
            Debug.Log("[Bridge] GameFlowController ready after " + elapsed.ToString("F2") + "s.");
    }

    string NewUuid()
    {
        var id = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(PREFS_ID_KEY, id);
        PlayerPrefs.Save();
        return id;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Headset registration & heartbeat

    IEnumerator UpsertHeadset()
    {
        string body = Json(KV("headset_id", HeadsetId), KV("name", HeadsetName),
                           KV("model", HeadsetName), KV("status", "online"),
                           KV("last_seen", Now()));
        using var req = Post($"{BASE_URL}/headsets", body);
        req.SetRequestHeader("Prefer", "resolution=merge-duplicates");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("[Bridge] UpsertHeadset: " + req.error);
            yield return new WaitForSecondsRealtime(3f);
            yield return UpsertHeadset();
        }
    }

    IEnumerator HeartbeatLoop()
    {
        while (true)
        {
            string status = IsGameInProgress ? (_isPaused ? "paused" : "in_game") : "online";
            using var req = Patch($"{BASE_URL}/headsets?headset_id=eq.{HeadsetId}",
                Json(KV("status", status), KV("last_seen", Now())));
            yield return req.SendWebRequest();
            yield return new WaitForSecondsRealtime(HEARTBEAT_INTERVAL);
        }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Experience schema sync

    IEnumerator SyncExperienceToSupabase()
{
    var gfc = GameFlowController.Instance;
    if (gfc == null)
    {
        Debug.LogWarning("[Bridge] No GameFlowController — skipping experience sync.");
        yield break;
    }

    // ── Read ExperienceConfig (with sensible fallbacks) ────────────────
    var expCfg = gfc.experienceConfig;

    string packageId   = Application.identifier;
    string displayName = (expCfg != null && !string.IsNullOrEmpty(expCfg.experienceName))
                            ? expCfg.experienceName
                            : Application.productName;
    string icon        = expCfg != null ? expCfg.icon        : "🎮";
    string description = expCfg != null ? expCfg.description : "";
    string category    = expCfg != null ? expCfg.CategoryKey  : "Other";
    string handMode    = expCfg != null ? expCfg.HandModeKey  : "single";
    string fingerMode  = expCfg != null ? expCfg.FingerModeKey: "all";

    string levelsJson  = BuildLevelsJson(gfc.levelConfigs);
    string schemaJson  = BuildParamSchemaJson(gfc.paramSchema);
    string kpiJson     = BuildKpiSchemaJson(gfc.kpiSchema);

    Debug.Log($"[Bridge] Syncing experience: {displayName} | {category} | hand={handMode} finger={fingerMode}");

    // ── Check for existing row ─────────────────────────────────────────
    using var findReq = Get($"{BASE_URL}/experiences?package_id=eq.{packageId}&limit=1");
    yield return findReq.SendWebRequest();

    if (findReq.result != UnityWebRequest.Result.Success)
    {
        Debug.LogWarning("[Bridge] SyncExperience — GET failed: " + findReq.error);
        yield break;
    }

    string responseBody = findReq.downloadHandler.text;

    if (responseBody == "[]" || string.IsNullOrEmpty(responseBody))
    {
        // ── Create new row ─────────────────────────────────────────────
        string create =
            $"{{\"name\":\"{EscJson(displayName)}\"," +
            $"\"package_id\":\"{EscJson(packageId)}\"," +
            $"\"icon\":\"{EscJson(icon)}\"," +
            $"\"description\":\"{EscJson(description)}\"," +
            $"\"category\":\"{EscJson(category)}\"," +
            $"\"hand_mode\":\"{EscJson(handMode)}\"," +
            $"\"finger_mode\":\"{EscJson(fingerMode)}\"," +
            $"\"levels\":{levelsJson}," +
            $"\"param_schema\":{schemaJson}," +
            $"\"kpi_schema\":{kpiJson}}}";

        using var createReq = Post($"{BASE_URL}/experiences", create);
        createReq.SetRequestHeader("Prefer", "return=minimal");
        yield return createReq.SendWebRequest();
        Debug.Log("[Bridge] ✅ Experience created with full config.");
    }
    else
    {
        // ── Patch auto-synced fields only ──────────────────────────────
        // Never overwrite name/icon/description — clinician may have
        // edited those in the dashboard. Only sync what Unity owns.
        string patch =
            $"{{\"category\":\"{EscJson(category)}\"," +
            $"\"hand_mode\":\"{EscJson(handMode)}\"," +
            $"\"finger_mode\":\"{EscJson(fingerMode)}\"," +
            $"\"levels\":{levelsJson}," +
            $"\"param_schema\":{schemaJson}," +
            $"\"kpi_schema\":{kpiJson}}}";

        using var patchReq = Patch($"{BASE_URL}/experiences?package_id=eq.{packageId}", patch);
        yield return patchReq.SendWebRequest();
        Debug.Log("[Bridge] ✅ Experience synced — category + hand/finger + schema.");
    }
}

    // ── Schema serialisers ────────────────────────────────────────────────

    string BuildParamSchemaJson(ParamSchema[] schemas)
    {
        if (schemas == null || schemas.Length == 0) return "[]";
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < schemas.Length; i++)
        {
            if (i > 0) sb.Append(",");
            var s = schemas[i];
            string typeStr = s.type == ParamSchema.ParamType.Float ? "range" : s.type.ToString().ToLower();
            sb.Append("{");
            sb.Append($"\"key\":\"{EscJson(s.key)}\",");
            sb.Append($"\"label\":\"{EscJson(s.label)}\",");
            sb.Append($"\"type\":\"{typeStr}\",");
            sb.Append($"\"unit\":\"{EscJson(s.unit)}\",");
            sb.Append($"\"defaultVal\":{s.defaultVal.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}");

            if (s.type == ParamSchema.ParamType.Select)
            {
                sb.Append(",\"options\":[");
                for (int j = 0; j < s.options.Length; j++)
                {
                    if (j > 0) sb.Append(",");
                    sb.Append($"\"{EscJson(s.options[j])}\"");
                }
                sb.Append("]");
            }
            else if (s.type != ParamSchema.ParamType.Bool)
            {
                sb.Append($",\"min\":{s.min.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($",\"max\":{s.max.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($",\"step\":{s.step.ToString("G", System.Globalization.CultureInfo.InvariantCulture)}");
            }
            sb.Append("}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// Serialises the KpiSchema[] array into a JSON array that the dashboard
    /// can read to know what metrics to display after a session.
    /// </summary>
    string BuildKpiSchemaJson(KpiSchema[] schemas)
    {
        if (schemas == null || schemas.Length == 0) return "[]";
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < schemas.Length; i++)
        {
            if (i > 0) sb.Append(",");
            var k = schemas[i];
            sb.Append("{");
            sb.Append($"\"key\":\"{EscJson(k.key)}\",");
            sb.Append($"\"label\":\"{EscJson(k.label)}\",");
            sb.Append($"\"unit\":\"{EscJson(k.unit)}\",");
            sb.Append($"\"type\":\"{k.type.ToString().ToLower()}\",");
            sb.Append($"\"higherIsBetter\":{(k.higherIsBetter ? "true" : "false")}");
            sb.Append("}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    string BuildLevelsJson(LevelConfig[] configs)
    {
        if (configs == null || configs.Length == 0) return "[]";
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < configs.Length; i++)
        {
            if (i > 0) sb.Append(",");
            var cfg = configs[i];
            var exporter = cfg as ILevelConfigExporter;
            var dict = exporter != null ? exporter.ExportParams() : new Dictionary<string, object>();
            sb.Append($"{{\"level\":{cfg.level},\"label\":\"{EscJson(LevelLabel(cfg.level))}\"");
            foreach (var kv in dict) { sb.Append(","); sb.Append($"\"{EscJson(kv.Key)}\":{SerialiseValue(kv.Value)}"); }
            sb.Append("}");
        }
        sb.Append("]");
        return sb.ToString();
    }

    string LevelLabel(int level)
    {
        switch (level) { case 1: return "Easy"; case 2: return "Medium"; case 3: return "Hard"; case 4: return "Expert"; default: return "Level " + level; }
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Session polling

    IEnumerator SessionPollLoop()
    {
        while (true)
        {
            yield return CheckSessionState();
            yield return new WaitForSecondsRealtime(IsGameInProgress ? 2f : 1.5f);
        }
    }

    void HandleSessionState(Session s)
    {
        switch (s.status)
        {
            case "active":  if (_isPaused || !IsGameInProgress) ResumeGame(); break;
            case "paused":  if (!_isPaused) PauseGame(); break;
            case "stopped": if (!_stopHandled) { _stopHandled = true; StopGame(); } break;
        }
    }

    IEnumerator CheckForPendingSession()
    {
        using var req = Get($"{BASE_URL}/game_sessions?headset_id=eq.{HeadsetId}&status=eq.pending&limit=1");
        req.timeout = 10;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;
        string raw = req.downloadHandler.text;
        if (raw == "[]" || string.IsNullOrEmpty(raw)) yield break;
        string customParamsRaw = ExtractJsonbField(raw, "custom_params");
        var wrap = JsonUtility.FromJson<Wrapper>("{\"items\":" + raw + "}");
        if (wrap?.items == null || wrap.items.Length == 0) yield break;
        wrap.items[0].custom_params = customParamsRaw;
        yield return ClaimSession(wrap.items[0]);
    }

    IEnumerator CheckSessionState()
    {
        var gfc = GameFlowController.Instance;
        if (string.IsNullOrEmpty(CurrentSessionId))
        {
            if (!IsGameInProgress && (gfc == null || !gfc.IsResetting))
                yield return CheckForPendingSession();
            yield break;
        }
        using var req = Get($"{BASE_URL}/game_sessions?id=eq.{CurrentSessionId}&limit=1");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;
        string raw = req.downloadHandler.text;
        string customParamsRaw = ExtractJsonbField(raw, "custom_params");
        var wrap = JsonUtility.FromJson<Wrapper>("{\"items\":" + raw + "}");
        if (wrap?.items == null || wrap.items.Length == 0) yield break;
        wrap.items[0].custom_params = customParamsRaw;
        HandleSessionState(wrap.items[0]);
    }

    IEnumerator ClaimSession(Session s)
    {
        if (IsGameInProgress) yield break;
        var gfc = GameFlowController.Instance;
        if (gfc != null && gfc.IsResetting) yield break;

        using var req = Patch($"{BASE_URL}/game_sessions?id=eq.{s.id}&status=eq.pending",
            Json(KV("status", "active")));
        req.SetRequestHeader("Prefer", "return=representation");
        yield return req.SendWebRequest();

        string body = req.downloadHandler.text;
        if (body == "[]" || string.IsNullOrEmpty(body)) yield break;

        CurrentSessionId = s.id;
        IsGameInProgress = true;
        _isPaused        = false;
        _stopHandled     = false;
        _sessionStart    = Time.realtimeSinceStartup;

        Debug.Log($"[Bridge] ✅ Claimed session {s.id} → {s.patient_name}");
        StartGame(s);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Game control

    void StartGame(Session s)
    {
        Time.timeScale = 1f;
        var gfc = GameFlowController.Instance;
        if (gfc == null) { HideLoading(); return; }

        gfc.PrepareForSession();
        gfc.SetAutoMode(s.auto_mode);

        Debug.Log($"[Bridge] custom_params='{s.custom_params}' level={s.level}");
        var paramDict = ParseCustomParams(s.custom_params);
        Debug.Log($"[Bridge] parsed {paramDict.Count} params");

        if (paramDict.Count > 0)
        {
            int baseLevel = s.level > 0 ? s.level : 1;
            gfc.SetLevel(baseLevel);
            gfc.ApplyCustomConfig(paramDict);
        }
        else
        {
            gfc.SetLevel(s.level > 0 ? s.level : 1);
        }

        gfc.StartGameplay();
        // ── Interaction mode (hand / finger) ──────────────────────────────
        // hand: "left" | "right" | "both" | null → null means no selection required
        // finger: "thumb" | "index" | … | null
        gfc.SetInteractionMode(
            string.IsNullOrEmpty(s.hand)   ? null : s.hand.ToLower()
        );


        HideLoading();
        Debug.Log($"[Bridge] 🎮 Started L{s.level} | {s.hand} | {s.finger} | auto={s.auto_mode} | params={s.custom_params}");
    }

    void ResumeGame()
    {
        _isPaused = false; IsGameInProgress = true;
        Time.timeScale = 1f;
        GameTimer.Instance?.ResumeTimer();
        FindObjectOfType<SpawnManager>()?.ResumeSpawner();
    }

    void PauseGame()
    {
        _isPaused = true; Time.timeScale = 0f;
        GameTimer.Instance?.PauseTimer();
        FindObjectOfType<SpawnManager>()?.PauseSpawner();
    }

    void StopGame()
    {
        Time.timeScale = 1f; _isPaused = false;
        IsGameInProgress = false; CurrentSessionId = null;
        GameTimer.Instance?.StopTimer();
        FindObjectOfType<SpawnManager>()?.StopSpawner();
        ShowLoading("Loading…");
        GameFlowController.Instance?.StartCoroutine(
            GameFlowController.Instance.ResetToLevelSelection());
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Results — with KPI data

    public void NotifyLevelResult(bool gameOver, Action cb = null)
        => StartCoroutine(ResultRoutine(gameOver, cb));

    IEnumerator ResultRoutine(bool gameOver, Action cb)
    {
        ShowLoading();

        // ── Collect all KPI data from the game ────────────────────────────
        var gfc     = GameFlowController.Instance;
        var kpiDict = gfc?.CollectKpiData() ?? new Dictionary<string, object>();

        int   score    = ScoreManager.Instance.ScoreThisLevel;
        float duration = Time.realtimeSinceStartup - _sessionStart;

        // Build the kpi_data JSONB payload
        string kpiJson = BuildKpiDataJson(kpiDict);

        // Use a raw JSON body so kpi_data is sent as JSONB (not a JSON string)
        string body =
            $"{{\"status\":\"{(gameOver ? "failed" : "completed")}\"," +
            $"\"score\":\"{score}\"," +
            $"\"duration_seconds\":\"{duration:F1}\"," +
            $"\"kpi_data\":{kpiJson}}}";

        using var req = Patch($"{BASE_URL}/game_sessions?id=eq.{CurrentSessionId}", body);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning("[Bridge] ResultRoutine PATCH error: " + req.error);
        else
            Debug.Log($"[Bridge] ✅ Result sent — KPIs: {kpiJson}");

        cb?.Invoke();
    }

    /// <summary>
    /// Serialises the KPI dictionary to a JSON object suitable for a JSONB column.
    /// Values retain their native types (int, float, bool, string).
    /// </summary>
    string BuildKpiDataJson(Dictionary<string, object> data)
    {
        var sb = new System.Text.StringBuilder("{");
        bool first = true;
        foreach (var kv in data)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append($"\"{EscJson(kv.Key)}\":{SerialiseValue(kv.Value)}");
        }
        sb.Append("}");
        return sb.ToString();
    }

    public void NotifySessionEnd() => StartCoroutine(EndRoutine());

    IEnumerator EndRoutine()
    {
        yield return Patch($"{BASE_URL}/game_sessions?id=eq.{CurrentSessionId}",
            Json(KV("status", "completed")));
        IsGameInProgress = false; CurrentSessionId = null;
        _isPaused = false; _stopHandled = false;
        ShowLoading("Waiting for next session…");
    }

    public void NotifyLevelAdvance(int newLevel) =>
        StartCoroutine(PatchCoroutine($"{BASE_URL}/game_sessions?id=eq.{CurrentSessionId}",
            Json(KV("level", newLevel.ToString()))));

    IEnumerator PatchCoroutine(string url, string body)
    {
        using var req = Patch(url, body);
        yield return req.SendWebRequest();
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Param parsing

    public static Dictionary<string, object> ParseCustomParams(string json)
    {
        var result = new Dictionary<string, object>();
        if (string.IsNullOrEmpty(json) || json == "null" || json == "{}") return result;
        json = json.Trim();
        if (json.StartsWith("{")) json = json.Substring(1, json.Length - 2);
        int i = 0;
        while (i < json.Length)
        {
            while (i < json.Length && (json[i] == ',' || json[i] == ' ' || json[i] == '\n' || json[i] == '\r' || json[i] == '\t')) i++;
            if (i >= json.Length) break;
            if (json[i] != '"') { i++; continue; }
            string key = ReadJsonString(json, ref i);
            while (i < json.Length && json[i] != ':') i++;
            i++;
            while (i < json.Length && json[i] == ' ') i++;
            object val = ReadJsonValue(json, ref i);
            if (key != null) result[key] = val;
        }
        return result;
    }

    static string ReadJsonString(string s, ref int i)
    {
        if (i >= s.Length || s[i] != '"') return null;
        i++;
        var sb = new System.Text.StringBuilder();
        while (i < s.Length && s[i] != '"')
        {
            if (s[i] == '\\') { i++; if (i < s.Length) sb.Append(s[i]); }
            else sb.Append(s[i]);
            i++;
        }
        i++;
        return sb.ToString();
    }

    static object ReadJsonValue(string s, ref int i)
    {
        if (i >= s.Length) return null;
        char c = s[i];
        if (c == '"') return ReadJsonString(s, ref i);
        if (c == 't') { i += 4; return true; }
        if (c == 'f') { i += 5; return false; }
        if (c == 'n') { i += 4; return null; }
        int start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+')) i++;
        string numStr = s.Substring(start, i - start);
        if (numStr.Contains('.') || numStr.Contains('e') || numStr.Contains('E'))
        {
            if (float.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fval)) return fval;
        }
        else { if (int.TryParse(numStr, out int ival)) return ival; }
        return 0;
    }

    // ── JSONB field extractor ─────────────────────────────────────────────

    static string ExtractJsonbField(string jsonArray, string fieldName)
    {
        string search = $"\"{fieldName}\":";
        int keyIdx = jsonArray.IndexOf(search, StringComparison.Ordinal);
        if (keyIdx < 0) return null;
        int valStart = keyIdx + search.Length;
        while (valStart < jsonArray.Length && jsonArray[valStart] == ' ') valStart++;
        if (valStart >= jsonArray.Length) return null;
        char opener = jsonArray[valStart];
        if (opener == 'n') return null;
        if (opener == '"') return null;
        if (opener != '{' && opener != '[') return null;
        char closer = opener == '{' ? '}' : ']';
        int depth = 0, end = valStart;
        bool inStr = false;
        while (end < jsonArray.Length)
        {
            char ch = jsonArray[end];
            if (ch == '\\' && inStr) { end += 2; continue; }
            if (ch == '"') { inStr = !inStr; end++; continue; }
            if (!inStr)
            {
                if (ch == '{' || ch == '[') depth++;
                else if (ch == '}' || ch == ']') { depth--; if (depth == 0) { end++; break; } }
            }
            end++;
        }
        return jsonArray.Substring(valStart, end - valStart);
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region HTTP helpers

    UnityWebRequest Get(string url)
    {
        var r = UnityWebRequest.Get(url);
        AddHeaders(r); r.timeout = 10; return r;
    }

    UnityWebRequest Post(string url, string body)
    {
        var r = new UnityWebRequest(url, "POST");
        r.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        r.downloadHandler = new DownloadHandlerBuffer();
        AddHeaders(r); r.timeout = 10; return r;
    }

    UnityWebRequest Patch(string url, string body)
    {
        var r = new UnityWebRequest(url, "PATCH");
        r.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        r.downloadHandler = new DownloadHandlerBuffer();
        AddHeaders(r); r.timeout = 10; return r;
    }

    IEnumerator Patch(string url, string body, Action done = null)
    {
        using var r = Patch(url, body);
        yield return r.SendWebRequest();
        done?.Invoke();
    }

    void AddHeaders(UnityWebRequest r)
    {
        r.SetRequestHeader("apikey",        KEY);
        r.SetRequestHeader("Authorization", $"Bearer {KEY}");
        r.SetRequestHeader("Content-Type",  "application/json");
    }

    // String-only Json builder (for simple payloads)
    static string Json(params (string, string)[] fields)
    {
        var s = "{";
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0) s += ",";
            s += $"\"{fields[i].Item1}\":\"{fields[i].Item2}\"";
        }
        return s + "}";
    }

    static (string, string) KV(string k, string v) => (k, v);
    static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    static string EscJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    static string SerialiseValue(object v)
    {
        if (v == null) return "null";
        if (v is bool b)   return b ? "true" : "false";
        if (v is int   i)  return i.ToString();
        if (v is float f)  return f.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        if (v is double d) return d.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        return $"\"{EscJson(v.ToString())}\"";
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region UI helpers

    void ShowLoading(string msg = "Loading…")
    { if (loadingText == null) return; loadingText.gameObject.SetActive(true); loadingText.text = msg; }

    void HideLoading()
    { if (loadingText == null) return; loadingText.gameObject.SetActive(false); }

    string DetectHeadsetModel()
    {
        string d = SystemInfo.deviceModel.ToLower();
        if (d.Contains("quest 3")) return "Meta Quest 3";
        if (d.Contains("quest 2")) return "Meta Quest 2";
        return SystemInfo.deviceModel;
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────
    #region Data models

    [Serializable] class Wrapper { public Session[] items; }

    [Serializable]
    class Session
    {
        public string id;
        public string patient_name;
        public int    level;
        public string status;
        public string hand;
        public string finger;
        public bool   auto_mode;
        public string custom_params;   // flat JSON blob from dashboard
    }

    #endregion
}