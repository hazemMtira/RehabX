using UnityEngine;

[CreateAssetMenu(fileName = "NewDifficulty", menuName = "Rehab/Difficulty")]
public class DifficultyData : ScriptableObject
{
    [Header("Spawn Settings")]
    public float spawnInterval;
    public int maxActiveMoles;
    public float moleLifetime;
    public float moleSpeed;

    [Header("Round Settings")]
    public float gameDuration;
    public int requiredScore;
}