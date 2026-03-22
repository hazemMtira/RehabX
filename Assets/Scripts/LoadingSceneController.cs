using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Attach to a GameObject in your Loading Scene.
/// Shows instructions while waiting for a Supabase session.
/// Once session is received, shows a 5-second countdown then loads the game scene.
/// 
/// SETUP:
///   1. Create a new scene called "LoadingScene" and add it to Build Settings (index 0)
///   2. Your Game scene should be in Build Settings too (index 1 or named)
///   3. Create a World Space Canvas as child of Main Camera
///   4. Assign the TMP text fields in the inspector
///   5. Make sure SupabaseManager is in this scene (or carried over via DontDestroyOnLoad)
/// </summary>
public class LoadingSceneController : MonoBehaviour
{
    [Header("Status Text")]
    [Tooltip("Main status line — shows 'Waiting...' then 'Get Ready!'")]
    public TextMeshProUGUI statusText;

    [Tooltip("Shows the countdown number 5..4..3..2..1")]
    public TextMeshProUGUI countdownText;

    [Tooltip("Subtitle under status — shows session info once received")]
    public TextMeshProUGUI subtitleText;

    [Header("Instruction Panels")]
    [Tooltip("Parent GameObject containing all instruction cards — always visible")]
    public GameObject instructionsRoot;

    [Header("Scene Transition")]
    [Tooltip("Name of the game scene to load after countdown")]
    public string gameSceneName = "GameScene";

    [Tooltip("Countdown duration in seconds after session is received")]
    public float countdownDuration = 5f;

    // ---------------------------------------------------------------
    // Private state
    // ---------------------------------------------------------------

    bool _sessionReceived = false;

    // ---------------------------------------------------------------
    // Unity lifecycle
    // ---------------------------------------------------------------

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        // Initial UI state
        SetStatus("Waiting for session...", "Put on your headset and ask your therapist to start the session.");
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        // Listen for session
        if (SupabaseManager.Instance != null)
        {
            SupabaseManager.Instance.OnSessionLoaded += OnSessionLoaded;
        }
        else
        {
            // No SupabaseManager — skip straight to game (editor fallback)
            Debug.LogWarning("[LoadingScene] No SupabaseManager found — skipping to game scene.");
            StartCoroutine(CountdownAndLoad());
        }
    }

    void OnDestroy()
    {
        if (SupabaseManager.Instance != null)
            SupabaseManager.Instance.OnSessionLoaded -= OnSessionLoaded;
    }

    // ---------------------------------------------------------------
    // Session received
    // ---------------------------------------------------------------

    void OnSessionLoaded()
    {
        if (_sessionReceived) return;
        _sessionReceived = true;

        var gs = GameSessionSettings.Instance;
        string difficulty = gs.selectedDifficulty != null ? gs.selectedDifficulty.name : "—";
        string hand       = gs.selectedHand.ToString();

        SetStatus("Session Ready!", $"Difficulty: {difficulty}   |   Hand: {hand}");

        StartCoroutine(CountdownAndLoad());
    }

    // ---------------------------------------------------------------
    // Countdown
    // ---------------------------------------------------------------

    IEnumerator CountdownAndLoad()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(true);

        float remaining = countdownDuration;

        while (remaining > 0f)
        {
            if (countdownText != null)
                countdownText.text = Mathf.CeilToInt(remaining).ToString();

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        if (countdownText != null) countdownText.text = "GO!";
        yield return new WaitForSeconds(0.5f);

        // Load game scene
        string sceneName = string.IsNullOrEmpty(GameSessionSettings.Instance?.selectedStageName)
            ? gameSceneName
            : GameSessionSettings.Instance.selectedStageName;

        SceneManager.LoadScene(sceneName);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    void SetStatus(string title, string subtitle)
    {
        if (statusText  != null) statusText.text  = title;
        if (subtitleText != null) subtitleText.text = subtitle;
    }
}
