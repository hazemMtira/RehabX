using UnityEngine;

[System.Serializable]
public class MoleSpawnData
{
    public GameObject prefab;
    [Range(0, 100)]
    public int weight = 10;
}