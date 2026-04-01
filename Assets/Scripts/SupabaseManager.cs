using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class SupabaseManager : MonoBehaviour
{
    public static SupabaseManager Instance;

    const string BASE_URL = "https://xrtyqkpeawirpcubajyw.supabase.co/rest/v1";
    const string API_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InhydHlxa3BlYXdpcnBjdWJhanl3Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzQzODQ1NTMsImV4cCI6MjA4OTk2MDU1M30.TbjePoft5BVQuZZ3zb880dCLYhT7Us0LFTZ-TlOnGsE"; // keep yours

    string headsetId;

    string currentSessionId;
    bool gameRunning = false;
    bool isPaused = false;
    bool isPolling = false;

    public Action OnSessionLoaded;
    public Action OnPaused;
    public Action OnResumed;

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        headsetId = SystemInfo.deviceUniqueIdentifier;
    }

    void Start()
    {
        StartCoroutine(RegisterHeadset());
        StartCoroutine(SessionLoop());
    }

    // ─────────────────────────────
    // MAIN LOOP
    // ─────────────────────────────
    IEnumerator SessionLoop()
    {
        while (true)
        {
            if (string.IsNullOrEmpty(currentSessionId))
                yield return CheckForNewSession();
            else
                yield return CheckSessionUpdates();

            yield return new WaitForSeconds(2f);
        }
    }

    // ─────────────────────────────
    // REGISTER HEADSET
    // ─────────────────────────────
    IEnumerator RegisterHeadset()
    {
        string json = $"{{\"headset_id\":\"{headsetId}\",\"name\":\"VR Headset\",\"model\":\"{SystemInfo.deviceModel}\"}}";

        UnityWebRequest req = new UnityWebRequest($"{BASE_URL}/headsets", "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("apikey", API_KEY);
        req.SetRequestHeader("Authorization", "Bearer " + API_KEY);
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Prefer", "resolution=merge-duplicates");

        yield return req.SendWebRequest();
    }

    // ─────────────────────────────
    // CHECK NEW SESSION
    // ─────────────────────────────
    IEnumerator CheckForNewSession()
    {
        string url = $"{BASE_URL}/game_sessions?headset_id=eq.{headsetId}&status=eq.pending&limit=1";

        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", API_KEY);
        req.SetRequestHeader("Authorization", "Bearer " + API_KEY);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            yield break;

        string json = req.downloadHandler.text;
        if (json == "[]") yield break;

        SessionWrapper wrapper = JsonUtility.FromJson<SessionWrapper>("{\"items\":" + json + "}");
        var s = wrapper.items[0];

        currentSessionId = s.id;

        GameSessionSettings.Instance.sessionId = s.id;
        GameSessionSettings.Instance.patientName = s.patient_name;
        GameSessionSettings.Instance.selectedHand = ParseHand(s.hand);

        Debug.Log("🎮 SESSION RECEIVED");

        yield return SetSessionStatus("active");

        gameRunning = true;

        OnSessionLoaded?.Invoke();
    }

    // ─────────────────────────────
    // CHECK SESSION UPDATES (PAUSE / RESUME / STOP)
    // ─────────────────────────────
    IEnumerator CheckSessionUpdates()
    {
        string url = $"{BASE_URL}/game_sessions?id=eq.{currentSessionId}&limit=1";

        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("apikey", API_KEY);
        req.SetRequestHeader("Authorization", "Bearer " + API_KEY);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
            yield break;

        string json = req.downloadHandler.text;

        SessionWrapper wrapper = JsonUtility.FromJson<SessionWrapper>("{\"items\":" + json + "}");
        var s = wrapper.items[0];

        switch (s.status)
        {
            case "paused":
                if (!isPaused)
                {
                    isPaused = true;
                    OnPaused?.Invoke();
                    Debug.Log("⏸ Paused from dashboard");
                }
                break;

            case "active":
                if (isPaused)
                {
                    isPaused = false;
                    OnResumed?.Invoke();
                    Debug.Log("▶ Resumed from dashboard");
                }
                break;

            case "stopped":
                Debug.Log("🛑 Stopped from dashboard");
                HandleStop();
                break;
        }
    }

    void HandleStop()
    {
        currentSessionId = null;
        gameRunning = false;
        isPaused = false;

        Time.timeScale = 1f;

        UnityEngine.SceneManagement.SceneManager.LoadScene("LoadingScene");
    }

    // ─────────────────────────────
    // UPDATE STATUS
    // ─────────────────────────────
    IEnumerator SetSessionStatus(string status)
    {
        string url = $"{BASE_URL}/game_sessions?id=eq.{currentSessionId}";
        string json = $"{{\"status\":\"{status}\"}}";

        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("apikey", API_KEY);
        req.SetRequestHeader("Authorization", "Bearer " + API_KEY);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
    }

    // ─────────────────────────────
    // SEND RESULTS
    // ─────────────────────────────
    public void SubmitResults(string sessionId, int score, float duration, float speed, Action onDone)
    {
        StartCoroutine(SendResults(sessionId, score, duration, onDone));
    }

    IEnumerator SendResults(string id, int score, float duration, Action onDone)
    {
        string url = $"{BASE_URL}/game_sessions?id=eq.{id}";

        string json = $"{{\"status\":\"completed\",\"score\":{score},\"duration_seconds\":{duration}}}";

        UnityWebRequest req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();

        req.SetRequestHeader("apikey", API_KEY);
        req.SetRequestHeader("Authorization", "Bearer " + API_KEY);
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        Debug.Log("RESULTS SENT");

        onDone?.Invoke();
    }

    public void ResetForNewSession()
    {
        currentSessionId = null;
    }

    // ─────────────────────────────
    // HELPERS
    // ─────────────────────────────
    HandType ParseHand(string hand)
    {
        if (hand == "left") return HandType.Left;
        if (hand == "right") return HandType.Right;
        return HandType.Both;
    }

    [Serializable]
    class SessionWrapper
    {
        public Session[] items;
    }

    [Serializable]
    class Session
    {
        public string id;
        public string patient_name;
        public string hand;
        public string status; // ✅ FIXED
    }
}