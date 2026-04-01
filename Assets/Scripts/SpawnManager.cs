using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public LevelConfig     currentConfig;
    public Hole[]          holes;
    public MoleSpawnData[] moleSpawnTable;

    private int       _activeMoles;
    private Coroutine _spawnCoroutine;
    private bool      _roundActive;

    // ── Public API ────────────────────────────────────────────────────────

    public void StartSpawner()
{
    Debug.Log("🟢 StartSpawner CALLED");

    _roundActive = true;

    if (currentConfig == null)
        Debug.LogError("❌ currentConfig is NULL when starting spawner!");

    if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
    _spawnCoroutine = StartCoroutine(SpawnLoop());
}

    public void StopSpawner()
    {
        _roundActive = false;
        if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
        ClearAllMoles();
    }

    public void PauseSpawner()  { _roundActive = false; }
    public void ResumeSpawner() { _roundActive = true;  }

    // ── Spawn loop ────────────────────────────────────────────────────────

    IEnumerator SpawnLoop()
{
    Debug.Log("🟡 SpawnLoop STARTED");

    while (_roundActive)
    {
        if (currentConfig == null)
        {
            Debug.LogError("❌ currentConfig STILL NULL");
            yield return null;
            continue;
        }

        Debug.Log("⏳ Waiting to spawn...");
        yield return new WaitForSeconds(currentConfig.spawnInterval);

        if (_activeMoles >= currentConfig.maxActiveMoles)
        {
            Debug.Log("⚠ Max moles reached");
            continue;
        }

        Hole hole = GetFreeHole();
        if (hole == null)
        {
            Debug.Log("⚠ No free holes");
            continue;
        }

        Debug.Log("🐹 Spawning mole!");
        SpawnMole(hole);
    }
}

    void SpawnMole(Hole hole)
    {
        GameObject prefab = GetWeightedRandomPrefab();
        if (prefab == null) return;

        Mole mole = MolePoolManager.Instance.GetMole(prefab);
        hole.SetMole(mole);

        // FIX: Mole.Init receives LevelConfig — DifficultyData removed from codebase
        mole.Init(hole, this, currentConfig, prefab);
        _activeMoles++;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    GameObject GetWeightedRandomPrefab()
    {
        int totalWeight = 0;
        foreach (var entry in moleSpawnTable) totalWeight += entry.weight;

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
        var free = new List<Hole>();
        foreach (var hole in holes)
            if (hole.IsFree) free.Add(hole);

        return free.Count == 0 ? null : free[Random.Range(0, free.Count)];
    }

    public void MoleRemoved()
    {
        _activeMoles = Mathf.Max(0, _activeMoles - 1);
    }

    void ClearAllMoles()
    {
        foreach (var hole in holes)
        {
            if (!hole.IsFree)
            {
                Mole mole = hole.GetComponentInChildren<Mole>();
                if (mole != null) mole.ForceReturnToPool();
                hole.ClearHole();
            }
        }
        _activeMoles = 0;
    }
}