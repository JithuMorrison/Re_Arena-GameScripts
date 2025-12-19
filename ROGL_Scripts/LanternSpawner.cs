using UnityEngine;
using System.Collections;

public class LanternSpawner : MonoBehaviour
{
    public LightController lightController;
    public GameObject spawnPrefab;
    public float spawnRate = 2f;

    public Transform[] spawnPoints;   // All spawn point transforms
    private bool[] occupied;          // Tracks if a spawn point is taken

    void Start()
    {
        // Initialize occupancy array
        occupied = new bool[spawnPoints.Length];

        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnRate);

            // Global limit
            if (LanternThrow.activeLanterns >= 5)
                continue;

            if (spawnPrefab == null || spawnPoints.Length == 0)
                continue;

            // Spawn only if any green light
            if (lightController != null && lightController.AnyGreen())
            {
                TrySpawn();
            }
        }
    }

    void TrySpawn()
    {
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (!occupied[i])   // Only use empty point
            {
                Transform point = spawnPoints[i];

                GameObject obj = Instantiate(
                    spawnPrefab,
                    point.position,
                    point.rotation
                );

                occupied[i] = true;

                // Tell the lantern which spawner + index it belongs to
                RegisterLantern(obj, i);

                break;  // Spawn one per cycle
            }
        }
    }

    public void RegisterLantern(GameObject obj, int index)
    {
        LanternThrow lt = obj.GetComponent<LanternThrow>();
        if (lt != null)
        {
            lt.spawner = this;
            lt.spawnIndex = index;
        }
    }

    // Called by LanternThrow when the lantern is destroyed
    public void FreeSpawnPoint(int index)
    {
        if (index >= 0 && index < occupied.Length)
        {
            occupied[index] = false;
        }
    }
}
