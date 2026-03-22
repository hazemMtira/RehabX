using System.Collections.Generic;
using UnityEngine;

public class MolePoolManager : MonoBehaviour
{
    public static MolePoolManager Instance;

    private Dictionary<GameObject, Queue<Mole>> poolDictionary =
        new Dictionary<GameObject, Queue<Mole>>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public Mole GetMole(GameObject prefab)
    {
        if (!poolDictionary.ContainsKey(prefab))
            poolDictionary[prefab] = new Queue<Mole>();

        if (poolDictionary[prefab].Count > 0)
        {
            Mole mole = poolDictionary[prefab].Dequeue();
            mole.gameObject.SetActive(true);
            return mole;
        }
        else
        {
            GameObject obj = Instantiate(prefab);
            return obj.GetComponentInChildren<Mole>();
        }
    }

    public void ReturnMole(GameObject prefab, Mole mole)
    {
        mole.gameObject.SetActive(false);
        poolDictionary[prefab].Enqueue(mole);
    }
}
    