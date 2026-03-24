using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Manages this headset's unique identity.
/// On first launch: generates a UUID, asks therapist to name it, registers in Supabase.
/// On subsequent launches: reads stored ID from PlayerPrefs.
///
/// SETUP:
///   1. Attach to a persistent GameObject in LoadingScene
///   2. Wire up the registration UI in the inspector (shown only on first launch)
///   3. HeadsetId is available as HeadsetManager.Instance.HeadsetId
/// </summary>
public class HeadsetManager : MonoBehaviour
{
    public static HeadsetManager Instance { get; private set; }

    const string URL          = "https://ojselmwfjaahqnzrapzf.supabase.co/rest/v1";
    const string KEY          = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Im9qc2VsbXdmamFhaHFuenJhcHpmIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzMyMjg0MzUsImV4cCI6MjA4ODgwNDQzNX0.bVm9zGN0PDHRGl_zHg746Z2bL6FfHVHsYAdj2JJPgAU";
    const string PREFS_ID_KEY = "headset_uuid";
    const string PREFS_NM_KEY = "headset_name";

    // ---------------------------------------------------------------
    // Public state
    // ---------------------------------------------------------------

    public string HeadsetId   { get; private set; }
    public string HeadsetName { get; private set; }
    public bool   IsRegistered { get; private set; }

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
        // Check if already registered
        if (PlayerPrefs.HasKey(PREFS_ID_KEY))
        {
            HeadsetId   = PlayerPrefs.GetString(PREFS_ID_KEY);
            HeadsetName = PlayerPrefs.GetString(PREFS_NM_KEY, "Unknown Headset");
            IsRegistered = true;
            Debug.Log($"[HeadsetManager] Loaded existing ID: {HeadsetId} ({HeadsetName})");
        }
        else
        {
            // First launch — generate new ID
            HeadsetId    = Guid.NewGuid().ToString();
            HeadsetName  = DetectHeadsetModel();
            IsRegistered = false;
            Debug.Log($"[HeadsetManager] New headset detected. ID={HeadsetId} Model={HeadsetName}");

            // Auto-register with Supabase
            StartCoroutine(RegisterHeadset());
        }
    }

    // ---------------------------------------------------------------
    // Headset model detection
    // ---------------------------------------------------------------

    string DetectHeadsetModel()
    {
        string deviceName = SystemInfo.deviceModel.ToLower();
        string deviceType = XRDeviceName().ToLower();

        if (deviceName.Contains("quest 3") || deviceType.Contains("quest 3"))
            return "Meta Quest 3";
        if (deviceName.Contains("quest pro") || deviceType.Contains("quest pro"))
            return "Meta Quest Pro";
        if (deviceName.Contains("quest 2") || deviceType.Contains("quest 2"))
            return "Meta Quest 2";
        if (deviceName.Contains("quest") || deviceType.Contains("quest"))
            return "Meta Quest";

        // Fallback
        return string.IsNullOrEmpty(deviceName) ? "Unknown Meta Headset" : deviceName;
    }

    string XRDeviceName()
    {
        var xrDisplaySubsystems = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(xrDisplaySubsystems);
        return xrDisplaySubsystems.Count > 0 ? SystemInfo.deviceModel : "";
    }

    // ---------------------------------------------------------------
    // Registration
    // ---------------------------------------------------------------

    IEnumerator RegisterHeadset()
    {
        // Use a friendly default name — therapist can rename from dashboard later
        string friendlyName = $"{HeadsetName} ({HeadsetId.Substring(0, 8)})";

        string body = $"{{" +
            $"\"id\":\"{HeadsetId}\"," +
            $"\"name\":\"{friendlyName}\"," +
            $"\"model\":\"{HeadsetName}\"" +
            $"}}";

        using var req = new UnityWebRequest($"{URL}/headsets", "POST");
        req.uploadHandler   = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        AddHeaders(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[HeadsetManager] Registration failed: {req.error}");
        }
        else
        {
            // Save to PlayerPrefs permanently
            PlayerPrefs.SetString(PREFS_ID_KEY, HeadsetId);
            PlayerPrefs.SetString(PREFS_NM_KEY, friendlyName);
            PlayerPrefs.Save();

            HeadsetName  = friendlyName;
            IsRegistered = true;

            Debug.Log($"[HeadsetManager] ✅ Headset registered: {HeadsetId} as '{friendlyName}'");
        }
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    void AddHeaders(UnityWebRequest req)
    {
        req.SetRequestHeader("apikey",        KEY);
        req.SetRequestHeader("Authorization", $"Bearer {KEY}");
        req.SetRequestHeader("Content-Type",  "application/json");
        req.SetRequestHeader("Prefer",        "return=representation");
    }

    /// <summary>
    /// Call this to reset registration (e.g. for testing).
    /// Will re-register on next launch.
    /// </summary>
    public void ClearRegistration()
    {
        PlayerPrefs.DeleteKey(PREFS_ID_KEY);
        PlayerPrefs.DeleteKey(PREFS_NM_KEY);
        PlayerPrefs.Save();
        Debug.Log("[HeadsetManager] Registration cleared.");
    }
}