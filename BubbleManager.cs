using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BubbleManager : MonoBehaviour
{
    [Header("References")]
    public GameObject bubblePrefab;
    public Transform player;
    
    [Header("Spawn Locations")]
    public Transform spawnLocation1;  // Assign 3 different spawn points in Inspector
    public Transform spawnLocation2;
    public Transform spawnLocation3;

    [Header("Audio")]
    public AudioClip popSound;
    public AudioClip failSound;
    private AudioSource audioSource;

    [Header("PPO Parameters")]
    public float spawnAreaSize = 3f;
    public float bubbleSpeed = 1f;
    public float bubbleLifetime = 5f;
    public float spawnHeight = 2f;
    public int numBubbles = 5;
    public float bubbleSize = 0.5f;
    public bool guidanceOn = false;

    [Header("Color Probabilities")]
    public float positiveProb = 0.5f;  // Probability for positive colors (cyan, magenta)
    public float negativeProb = 0.5f;  // Probability for negative colors (black, white)

    private List<GameObject> activeBubbles = new List<GameObject>();
    private float spawn_rate = 0.5f;
    public int totalbubbles = 0;
    private PPOStateTracker stateTracker;

    void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false;

        stateTracker = FindObjectOfType<PPOStateTracker>();

        // Create default spawn locations if not assigned
        if (spawnLocation1 == null || spawnLocation2 == null || spawnLocation3 == null)
        {
            CreateDefaultSpawnLocations();
        }
    }

    private void CreateDefaultSpawnLocations()
    {
        GameObject spawnParent = new GameObject("SpawnLocations");
        spawnParent.transform.position = player != null ? player.position : Vector3.zero;

        if (spawnLocation1 == null)
        {
            GameObject loc1 = new GameObject("SpawnLocation1");
            loc1.transform.parent = spawnParent.transform;
            loc1.transform.localPosition = new Vector3(-2f, 0f, 0f);
            spawnLocation1 = loc1.transform;
        }

        if (spawnLocation2 == null)
        {
            GameObject loc2 = new GameObject("SpawnLocation2");
            loc2.transform.parent = spawnParent.transform;
            loc2.transform.localPosition = new Vector3(0f, 0f, 0f);
            spawnLocation2 = loc2.transform;
        }

        if (spawnLocation3 == null)
        {
            GameObject loc3 = new GameObject("SpawnLocation3");
            loc3.transform.parent = spawnParent.transform;
            loc3.transform.localPosition = new Vector3(2f, 0f, 0f);
            spawnLocation3 = loc3.transform;
        }
    }

    public void PlayPopSound(int bubbleval)
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            if (bubbleval == -1)
            {
                audioSource.clip = failSound;
            }
            else
            {
                audioSource.clip = popSound;
            }
            audioSource.Play();
        }
    }

    void Update()
    {
        if (activeBubbles.Count < numBubbles || spawn_rate > 0.5f)
        {
            SpawnBubble();
            totalbubbles++;
            spawn_rate = Mathf.Clamp(spawn_rate-0.1f, 0f, 1f);
        }
    }

    void SpawnBubble()
    {
        if (player == null) return;

        // Randomly select spawn location using weighted random choice
        Transform selectedSpawn = GetRandomSpawnLocation();

        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnAreaSize, spawnAreaSize),
            spawnHeight,
            Random.Range(-spawnAreaSize, spawnAreaSize)
        );

        Vector3 spawnPos = selectedSpawn.position + randomOffset;

        GameObject bubble = Instantiate(bubblePrefab, spawnPos, Quaternion.identity);
        bubble.transform.localScale = Vector3.one * bubbleSize;

        // Use weighted probability for color selection
        int bubbleVal = GetWeightedRandomColor();

        Renderer renderer = bubble.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color[] colors = { Color.black, Color.white, Color.cyan, Color.magenta };
            renderer.material.color = colors[bubbleVal + 1];
        }

        Bubble bubbleScript = bubble.GetComponent<Bubble>();
        if (bubbleScript != null)
        {
            bubbleScript.Init(this, player, bubbleSpeed, bubbleLifetime, guidanceOn, bubbleVal);
        }

        activeBubbles.Add(bubble);
    }

    private Transform GetRandomSpawnLocation()
    {
        // Equal probability for each spawn location
        int randomIndex = Random.Range(0, 3);
        
        switch (randomIndex)
        {
            case 0: return spawnLocation1;
            case 1: return spawnLocation2;
            case 2: return spawnLocation3;
            default: return spawnLocation2;
        }
    }

    private int GetWeightedRandomColor()
    {
        // Normalize probabilities
        float totalProb = positiveProb + negativeProb;
        float normalizedPositive = positiveProb / totalProb;
        float normalizedNegative = negativeProb / totalProb;

        float randomValue = Random.value;

        if (randomValue < normalizedPositive)
        {
            // Positive colors: cyan (1) or magenta (2)
            return Random.Range(1, 3);
        }
        else
        {
            // Negative colors: black (-1) or white (0)
            return Random.Range(-1, 1);
        }
    }

    public void OnBubblePopped(GameObject bubble)
    {
        if (activeBubbles.Contains(bubble))
        {
            activeBubbles.Remove(bubble);
            Destroy(bubble);

            // Notify state tracker
            if (stateTracker != null)
            {
                stateTracker.OnBubblePopped();
            }
        }
    }

    public void UpdateEnvironment(AdjustmentsData adjustments)
    {
        GameConfig gameconfig = SessionManager.Instance.GetGameConfig(SessionManager.Instance.selectedGameName);
        float bubbleSpeedMax = 1f;
        float bubbleLifetimeMax = 10f;
        float numBubblesMax = 5f;
        float bubbleSizeMax = 1f;

        if (gameconfig != null)
        {
            bubbleSpeedMax   = Mathf.Max(gameconfig.bubbleSpeedMax, 1f);
            bubbleLifetimeMax = Mathf.Max(gameconfig.bubbleLifetimeMax, 10f); // at least 1 second
            numBubblesMax     = Mathf.Max(gameconfig.numBubblesMax, 5f);     // at least 1 bubble
            bubbleSizeMax     = Mathf.Max(gameconfig.bubbleSizeMax, 0.2f);   // at least min size
        }

        // ✅ Define safe minimums
        float minBubbleSize    = 0.2f; 
        float minBubbleSpeed   = 0.3f; 
        float minBubbleLifetime = 1f;   // don't allow instant vanish
        int   minNumBubbles    = 1;     // always at least one bubble

        // Update bubble size
        if (adjustments.bubble_size != 0f)
        {
            bubbleSize = Mathf.Clamp(bubbleSize + adjustments.bubble_size, minBubbleSize, bubbleSizeMax);
        }

        // Update color probabilities
        positiveProb = Mathf.Clamp01(positiveProb + adjustments.positive_prob);
        negativeProb = Mathf.Clamp01(negativeProb + adjustments.negative_prob);

        // Update spawn rate (affects bubble speed)
        if (adjustments.spawn_rate != 0f)
        {
            bubbleSpeed = Mathf.Clamp(bubbleSpeed + adjustments.spawn_rate * 0.1f, minBubbleSpeed, bubbleSpeedMax);
            spawn_rate = Mathf.Clamp(spawn_rate + adjustments.spawn_rate, 0f, 1f);
        }

        // ✅ Clamp other variables so they never hit 0
        bubbleLifetime = Mathf.Clamp(bubbleLifetime, minBubbleLifetime, bubbleLifetimeMax);
        numBubbles     = Mathf.Clamp(numBubbles, minNumBubbles, (int)numBubblesMax);

        Debug.Log($"Environment updated: Speed={bubbleSpeed}, Size={bubbleSize}, " +
                $"Lifetime={bubbleLifetime}, NumBubbles={numBubbles}, " +
                $"PosProb={positiveProb}, NegProb={negativeProb}");
    }

    public int GetActiveBubbleCount()
    {
        return activeBubbles.Count;
    }
}
