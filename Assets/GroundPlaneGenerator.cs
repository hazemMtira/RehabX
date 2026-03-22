using UnityEngine;

[ExecuteAlways]
public class GroundPlaneGenerator : MonoBehaviour
{
    [Header("Size")]
    public int width = 10;
    public int length = 10;
    public float tileSize = 1f;

    [Header("Prefabs")]
    public GameObject ground01; // MIDDLE
    public GameObject ground02; // OUTER
    public GameObject ground03; // INNER

    [Header("Editor")]
    public bool generate;

    void Update()
    {
        if (!generate) return;

        generate = false;
        ClearMap();
        GenerateMap();
    }

    void ClearMap()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            DestroyImmediate(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }
    }

    void GenerateMap()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                GameObject prefab;

                bool outer =
                    x == 0 || x == width - 1 ||
                    z == 0 || z == length - 1;

                bool inner =
                    x == 1 || x == width - 2 ||
                    z == 1 || z == length - 2;

                if (outer)
                    prefab = ground02;
                else if (inner)
                    prefab = ground03;
                else
                    prefab = ground01;

                Vector3 pos = new Vector3(x * tileSize, 0, z * tileSize);
                GameObject tile = Instantiate(prefab, pos, Quaternion.identity, transform);
                tile.name = $"Tile_{x}_{z}";
            }
        }
    }
}
