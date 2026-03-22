using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuController : MonoBehaviour
{
    [Header("Difficulty Options")]
    public DifficultyData easy;
    public DifficultyData medium;
    public DifficultyData hard;

    [Header("Stage Options")]
    public List<string> stageNames;

    [Header("Movement Detection UI")]
    public Toggle movementDetectionToggle;

    private GameSessionSettings Settings => GameSessionSettings.Instance;

    bool EnsureSettings()
    {
        if (Settings == null)
        {
            Debug.LogError("GameSessionSettings instance missing – cannot apply menu selections.");
            return false;
        }
        return true;
    }

    void Start()
    {
        if (!EnsureSettings()) return;

        // Sync toggle with saved value
        if (movementDetectionToggle != null)
        {
            movementDetectionToggle.isOn = Settings.movementDetectionEnabled;

            movementDetectionToggle.onValueChanged.AddListener(OnMovementToggleChanged);
        }
    }

    void OnDestroy()
    {
        if (movementDetectionToggle != null)
            movementDetectionToggle.onValueChanged.RemoveListener(OnMovementToggleChanged);
    }

    // ------------------------------
    // Difficulty
    // ------------------------------
    public void SelectEasy() => Settings.selectedDifficulty = easy;
    public void SelectMedium() => Settings.selectedDifficulty = medium;
    public void SelectHard() => Settings.selectedDifficulty = hard;

    // ------------------------------
    // Stage
    // ------------------------------
    public void SelectStageByIndex(int index)
    {
        if (!EnsureSettings()) return;

        if (index < 0 || index >= stageNames.Count)
        {
            Debug.LogError("Invalid stage index");
            return;
        }

        Settings.selectedStageName = stageNames[index];
    }

    // ------------------------------
    // Hands
    // ------------------------------
    public void SelectLeftHand() => Settings.selectedHand = HandType.Left;
    public void SelectRightHand() => Settings.selectedHand = HandType.Right;
    public void SelectBothHands() => Settings.selectedHand = HandType.Both;

    // ------------------------------
    // Movement Detection
    // ------------------------------
    void OnMovementToggleChanged(bool value)
    {
        if (!EnsureSettings()) return;

        Settings.movementDetectionEnabled = value;

        Debug.Log("Movement Detection set to: " + value);
    }

    // ------------------------------
    // Start Game
    // ------------------------------
    public void StartGame()
    {
        if (!EnsureSettings()) return;

        if (string.IsNullOrEmpty(Settings.selectedStageName))
        {
            Debug.LogError("No stage selected!");
            return;
        }

        SceneManager.LoadScene(Settings.selectedStageName);
    }
}