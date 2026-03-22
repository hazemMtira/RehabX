using UnityEngine;

public enum HandType { Left, Right, Both }

/// <summary>
/// Persists across scenes. Holds the current session configuration
/// loaded from Supabase by SupabaseManager.
/// </summary>
public class GameSessionSettings : MonoBehaviour
{
    public static GameSessionSettings Instance { get; private set; }

    [Header("Session (set by SupabaseManager)")]
    public string         sessionId;
    public string         patientName;
    public string         therapistName;

    [Header("Game Settings")]
    public DifficultyData selectedDifficulty;
    public HandType       selectedHand = HandType.Right;
    public bool           movementDetectionEnabled = true;
    public string         selectedStageName;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}