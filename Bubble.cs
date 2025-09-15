using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bubble : MonoBehaviour
{
    private BubbleManager manager;
    private Transform player;
    private float speed;
    private float lifetime;
    private bool guidanceOn;
    private int bubbleValue; // Value of the bubble

    private float timer = 0f;
    private Vector3 randomDir;   // random movement direction

    public void Init(BubbleManager manager, Transform player, float speed, float lifetime, bool guidanceOn, int value)
    {
        this.manager = manager;
        this.player = player;
        this.speed = speed;
        this.lifetime = lifetime;
        this.guidanceOn = guidanceOn;
        this.bubbleValue = value; // Set the bubble value

        // ✅ Pick a random direction (slightly biased upwards so it feels floaty)
        randomDir = new Vector3(
            Random.Range(-0.5f, 0.5f),   // random left/right
            Random.Range(0.5f, 1f),      // always goes upward
            Random.Range(-0.5f, 0.5f)    // random forward/back
        ).normalized;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (guidanceOn && player != null)
        {
            // Guided movement towards player
            Vector3 dir = (player.position - transform.position).normalized;
            transform.position += dir * speed * Time.deltaTime;
        }
        else
        {
            // ✅ Random drifting instead of only up
            transform.position += randomDir * speed * Time.deltaTime;
        }

        if (timer >= lifetime && manager != null)
        {
            manager.OnBubblePopped(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && manager != null)
        {
            manager.OnBubblePopped(gameObject);
            manager.PlayPopSound(bubbleValue); // Play pop sound with bubble value
            Debug.Log("Bubble popped by player!");
            int extra = 0;
            if (ScoreManager.Instance.GetPrevPop() == bubbleValue){
                extra = 1;
            }
            ScoreManager.Instance.AddScore(bubbleValue+extra); // Add score based on bubble value
            ScoreManager.Instance.SetPrevPop(bubbleValue); // Update previous popped bubble value
        }
    }
}
