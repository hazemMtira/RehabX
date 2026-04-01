using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Level Config", fileName = "LevelConfig")]
public class LevelConfig : ScriptableObject, ILevelConfigExporter
{
    [Header("Spawn Settings")]
    public float spawnInterval;
    public int   maxActiveMoles;
    public float moleLifetime;
    public float moleSpeed;

    [Header("Round Settings")]
    public float gameDuration;
    public int   requiredScore;
    
    public Vector3 levelPosition;

    // level index (1-based), assigned at runtime or in the ScriptableObject
    public int level;

    public Dictionary<string, object> ExportParams() => new Dictionary<string, object>
    {
        { "requiredScore",  requiredScore  },
        { "gameDuration",   gameDuration   },
        { "moleSpeed",      moleSpeed      },
        { "moleLifetime",   moleLifetime   },
        { "maxActiveMoles", maxActiveMoles },
        { "spawnInterval",  spawnInterval  },
    };
}