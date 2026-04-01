using UnityEngine;
using System;


/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public int TotalScore { get; private set; }
    public int ScoreThisLevel { get; private set; }

    /// <summary>Fired whenever score changes. Passes (totalScore, scoreThisLevel).</summary>
    public event Action<int, int> OnScoreChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    /// <summary>Add (or subtract) points. Clamps both values to >= 0.</summary>
    public void AddScore(int value)
    {
        // Ignore negative additions when already at zero
        if (value < 0 && TotalScore <= 0) return;

        TotalScore += value;
        ScoreThisLevel += value;

        if (TotalScore < 0) TotalScore = 0;
        if (ScoreThisLevel < 0) ScoreThisLevel = 0;

        OnScoreChanged?.Invoke(TotalScore, ScoreThisLevel);
    }

    /// <summary>Called by GameFlowController at the start of each new level.</summary>
    public void ResetLevelScore()
    {
        ScoreThisLevel = 0;
        OnScoreChanged?.Invoke(TotalScore, ScoreThisLevel);
    }

    /// <summary>Called when returning to main menu (full reset).</summary>
    public void ResetAll()
    {
        TotalScore = 0;
        ScoreThisLevel = 0;
        OnScoreChanged?.Invoke(TotalScore, ScoreThisLevel);
    }
}