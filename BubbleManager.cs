using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BubbleManager : MonoBehaviour
{
    [Header("References")]
    public GameObject bubblePrefab;   // Assign a prefab with Bubble script + Collider + Rigidbody (isKinematic = true)
    public Transform player;          // Reference to player transform

    [Header("PPO Parameters")]
    public float spawnAreaSize = 3f;
    public float bubbleSpeed = 1f;
    public float bubbleLifetime = 5f;
    public float spawnHeight = 2f;
    public int numBubbles = 5;
    public float bubbleSize = 0.5f;
    public bool guidanceOn = false;

    private List<GameObject> activeBubbles = new List<GameObject>();
    public int totalbubbles = 0;

    void Start()
    {
        // ✅ Auto-find player if not assigned in Inspector
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    void Update()
    {
        // Maintain number of bubbles
        if (activeBubbles.Count < numBubbles)
        {
            SpawnBubble();
            totalbubbles++;
        }
    }

    void SpawnBubble()
    {
        if (player == null) return; // ✅ prevents null error

        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnAreaSize, spawnAreaSize),
            spawnHeight,
            Random.Range(-spawnAreaSize, spawnAreaSize)
        );

        Vector3 spawnPos = player.position + randomOffset;

        GameObject bubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);
        bubble.transform.localScale = Vector3.one * bubbleSize;

        // ✅ Use GetComponent (don’t add a second script)
        Bubble bubbleScript = bubble.GetComponent<Bubble>();
        if (bubbleScript != null)
        {
            bubbleScript.Init(this, player, bubbleSpeed, bubbleLifetime, guidanceOn);
        }

        activeBubbles.Add(bubble);
    }

    public void OnBubblePopped(GameObject bubble)
    {
        if (activeBubbles.Contains(bubble))
        {
            activeBubbles.Remove(bubble);
            Destroy(bubble);
        }
    }

    public void UpdateEnvironment(List<float> action)
    {
        // ✅ Update fields, not local variables
        spawnAreaSize = 0.5f;//action[0];
        bubbleSpeed = 0.5f;//action[1];
        bubbleLifetime = action[2];
        spawnHeight = 0f;//action[3];
        numBubbles = Mathf.RoundToInt(action[4]);
        bubbleSize = 0.4f;//action[5];
        guidanceOn = false;//action[6] > 0.5f;

        Debug.Log($"Environment updated: Speed={bubbleSpeed}, Size={bubbleSize}, Count={numBubbles}");
    }
}
