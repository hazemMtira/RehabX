using UnityEngine;

public enum HandType { Left, Right, Both }

public class GameSessionSettings : MonoBehaviour
{
    public static GameSessionSettings Instance { get; private set; }

    [Header("Session (TEMP - for compatibility)")]
    public string sessionId;
    public string patientName;
    public string therapistName;

    [Header("Game Settings")]
    public DifficultyData selectedDifficulty;
    public HandType selectedHand = HandType.Right;

    // ✅ RESTORED (needed by other scripts)
    public bool movementDetectionEnabled = true;
    public string selectedStageName;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}