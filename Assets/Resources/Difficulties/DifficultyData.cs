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

    
    /*public Dictionary<string, object> ExportParams() => new Dictionary<string, object>
    {
        { "targetScore",      targetScore                },
        { "totalTime",        totalTime                  },
        { "spawnDistance",    spawnDistance              },
        { "horizontalSpread", horizontalRange.y          }, // symmetric: [-x, x]
        { "verticalMin",      verticalRange.x            },
        { "verticalMax",      verticalRange.y            },
        { "spawnInterval",    spawnInterval              },
        { "poolSize",         poolSize                   },
        { "minLifetime",      minLifetime                },
        { "maxLifetime",      maxLifetime                },
        { "bubbleScale",      bubbleScale                },
    };*/
}