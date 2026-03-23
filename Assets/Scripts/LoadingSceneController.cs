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
///   1. Scene named "LoadingScene" at Build Settings index 0
///   2. Assign TMP text fields in the inspector
///   3. SupabaseManager and GameSessionSettings must exist in this scene
/// </summary>
public class LoadingSceneController : MonoBehaviour
{
    [Header("Status Text")]
    [Tooltip("Main status line — shows 'Waiting...' then 'Session Ready!'")]
    public TextMeshProUGUI statusText;

    [Tooltip("Shows the countdown number 5..4..3..2..1")]
    public TextMeshProUGUI countdownText;

    [Tooltip("Subtitle under status — shows session info once received")]
    public TextMeshProUGUI subtitleText;

    [Header("Scene Transition")]
    [Tooltip("Name of the game scene to load after countdown")]
    public string gameSceneName = "FieldScene";

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
        // Reset UI
        SetStatus("Waiting for session...", "Ask your therapist to start the session.");
        if (countdownText != null) countdownText.gameObject.SetActive(false);

        if (SupabaseManager.Instance != null)
        {
            // Subscribe to session loaded event
            SupabaseManager.Instance.OnSessionLoaded += OnSessionLoaded;

            // Always reset and restart polling — handles both first launch and restart flow
            SupabaseManager.Instance.ResetForNewSession();
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

        var gs         = GameSessionSettings.Instance;
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

        // Load correct scene — use stage name from session if available
        string sceneName = !string.IsNullOrEmpty(GameSessionSettings.Instance?.selectedStageName)
            ? GameSessionSettings.Instance.selectedStageName
            : gameSceneName;

        SceneManager.LoadScene(sceneName);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    void SetStatus(string title, string subtitle)
    {
        if (statusText   != null) statusText.text   = title;
        if (subtitleText != null) subtitleText.text  = subtitle;
    }
}