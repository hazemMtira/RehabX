using UnityEngine;
using TMPro;
using System;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int Score { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI scoreText;

    public event Action<int> OnScoreChanged;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        ResetScore();
    }

    public void AddScore(int amount)
    {
        if (GameManager.Instance.CurrentState != GameState.Playing)
            return;

        Score += amount;
        UpdateUI();
        OnScoreChanged?.Invoke(Score);
    }

    public void ResetScore()
    {
        Score = 0;
        UpdateUI();
        OnScoreChanged?.Invoke(Score);
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = Score.ToString();
    }
}