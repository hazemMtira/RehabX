using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public DifficultyData currentDifficulty;
    public Hole[] holes;
    public MoleSpawnData[] moleSpawnTable;
    private int activeMoles;
    private Coroutine spawnCoroutine;
    private bool roundActive = false;

    public void StartSpawning()
    {
        roundActive = true;
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        roundActive = false;
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);

        ClearAllMoles();
    }

    IEnumerator SpawnLoop()
    {
        while (roundActive)
        {
            yield return new WaitForSeconds(currentDifficulty.spawnInterval);

            if (activeMoles >= currentDifficulty.maxActiveMoles)
                continue;

            Hole hole = GetFreeHole();
            if (hole == null)
                continue;

            SpawnMole(hole);
        }
    }

    void SpawnMole(Hole hole)
{
    GameObject prefab = GetWeightedRandomPrefab();
    if (prefab == null) return;

    Mole mole = MolePoolManager.Instance.GetMole(prefab);
    hole.SetMole(mole);
    mole.Init(hole, this, currentDifficulty, prefab);
    activeMoles++;
}

    GameObject GetWeightedRandomPrefab()
    {
        int totalWeight = 0;
        foreach (var entry in moleSpawnTable)
            totalWeight += entry.weight;

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        foreach (var entry in moleSpawnTable)
        {
            cumulative += entry.weight;
            if (roll < cumulative) return entry.prefab;
        }
        return null;
    }

    Hole GetFreeHole()
    {
        List<Hole> free = new List<Hole>();
        foreach (var hole in holes)
            if (hole.IsFree) free.Add(hole);

        if (free.Count == 0) return null;
        return free[Random.Range(0, free.Count)];
    }

    public void MoleRemoved()
    {
        activeMoles = Mathf.Max(0, activeMoles - 1);
    }

    void ClearAllMoles()
    {
        foreach (var hole in holes)
        {
            if (!hole.IsFree)
            {
                Mole mole = hole.GetComponentInChildren<Mole>();
                if (mole != null)
                {
                    mole.ForceReturnToPool();
                }
                hole.ClearHole();
            }
        }
        activeMoles = 0;
    }
}