using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Wrapper class to send state
[System.Serializable]
public class StateWrapper
{
    public List<int> state;  // Changed to int array for state representation
    public int score;
    public float fatigue;
    public float engagement;
}

// Wrapper class to receive action
[System.Serializable]
public class ActionWrapper
{
    public int action_index;
    public AdjustmentsData adjustments;
    public float log_prob;
}

[System.Serializable]
public class AdjustmentsData
{
    public float bubble_size;
    public float negative_prob;
    public float positive_prob;
    public float spawn_rate;
}

public class PPOStateTracker : MonoBehaviour
{
    public GameObject leftHand, rightHand, leftShoulder, rightShoulder, hip, head;
    public BubbleManager bubbleManager;
    public RewardCalculator rewardCalculator;
    private string flaskUrl = "http://127.0.0.1:5000/ppo_action";

    // State tracking variables
    private float stateCheckTimer = 0f;
    private float stateCheckInterval = 2f; // Check state every 2 seconds
    private int bubblesPopped2s = 0;
    private int bubblesPopped5s = 0;
    private int bubblesPopped10s = 0;
    private float timer2s = 0f;
    private float timer5s = 0f;
    private float timer10s = 0f;
    private float sessionStartTime = 0f;

    private List<float> prevState = new List<float>();
    private List<float> prevAction = new List<float>();
    private float lastFatigue;
    private float lastEngagement;
    private float lastRewardTime = 0f;
    private const float rewardDelay = 10f; // 10 second delay for state changes

    void Start()
    {
        sessionStartTime = Time.time;
        StartCoroutine(UpdatePPO());
    }

    void Update()
    {
        // Update timers
        timer2s += Time.deltaTime;
        timer5s += Time.deltaTime;
        timer10s += Time.deltaTime;

        // Reset counters based on time windows
        if (timer2s >= 2f)
        {
            bubblesPopped2s = 0;
            timer2s = 0f;
        }
        if (timer5s >= 5f)
        {
            bubblesPopped5s = 0;
            timer5s = 0f;
        }
        if (timer10s >= 10f)
        {
            bubblesPopped10s = 0;
            timer10s = 0f;
        }
    }

    public void OnBubblePopped()
    {
        bubblesPopped2s++;
        bubblesPopped5s++;
        bubblesPopped10s++;
    }

    IEnumerator UpdatePPO()
    {
        while (true)
        {
            // Get current state representation
            List<int> stateArray = GetStateArray();
            int currentScore = ScoreManager.Instance != null ? ScoreManager.Instance.GetScore() : 0;

            Debug.Log("State Array: " + string.Join(",", stateArray));

            // Send State to Flask
            StateWrapper stateWrapper = new StateWrapper 
            { 
                state = stateArray,
                score = currentScore,
                fatigue = lastFatigue,
                engagement = lastEngagement
            };
            string jsonData = JsonUtility.ToJson(stateWrapper);

            UnityWebRequest www = new UnityWebRequest(flaskUrl, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                ActionWrapper actionData = JsonUtility.FromJson<ActionWrapper>(responseText);
                
                if (actionData != null && actionData.adjustments != null)
                {
                    Debug.Log($"PPO Action received - Index: {actionData.action_index}, LogProb: {actionData.log_prob}");
                    Debug.Log($"Adjustments - BubbleSize: {actionData.adjustments.bubble_size}, " +
                              $"NegProb: {actionData.adjustments.negative_prob}, " +
                              $"PosProb: {actionData.adjustments.positive_prob}, " +
                              $"SpawnRate: {actionData.adjustments.spawn_rate}");

                    if (bubbleManager != null)
                    {
                        bubbleManager.UpdateEnvironment(actionData.adjustments);
                    }

                    // Calculate reward and send to trainer (after 10s delay)
                    float reward = CalculateReward(stateArray, currentScore);
                    Debug.Log("Reward: " + reward);

                    // Only send reward if enough time has passed since last send
                    if (prevState.Count > 0 && (Time.time - lastRewardTime) >= rewardDelay)
                    {
                        List<int> nextStateArray = GetStateArray();
                        bool done = (currentScore >= 20 || (Time.time - sessionStartTime) >= 120f || currentScore < 0);
                        
                        Debug.Log("Sending reward after 10s delay - State likely changed");
                        rewardCalculator.SendReward(
                            prevState, 
                            prevAction, 
                            reward, 
                            ConvertIntListToFloat(nextStateArray), 
                            done,
                            actionData.log_prob
                        );
                        
                        lastRewardTime = Time.time;
                    }
                    else if (prevState.Count == 0)
                    {
                        // First iteration, just initialize lastRewardTime
                        lastRewardTime = Time.time;
                    }

                    // Store current state and action for next iteration
                    prevState = ConvertIntListToFloat(stateArray);
                    prevAction = new List<float> { 
                        actionData.adjustments.bubble_size,
                        actionData.adjustments.negative_prob,
                        actionData.adjustments.positive_prob,
                        actionData.adjustments.spawn_rate
                    };
                }
                else
                {
                    Debug.LogWarning("Invalid action received from Flask.");
                }
            }
            else
            {
                Debug.LogError("Error sending PPO state: " + www.error);
            }

            www.Dispose();

            // Update fatigue and engagement
            UpdateMetrics();

            yield return new WaitForSeconds(25f); // Check every 5 seconds
        }
    }

    private List<int> GetStateArray()
    {
        // State array: [Win, Lose, Overwhelmed, Fast(2s), Balanced(5s), Slow(5s), Stagnant(10s)]
        List<int> state = new List<int> { 0, 0, 0, 0, 0, 0, 0 };

        ScoreManager score = ScoreManager.Instance;
        int currentScore = score != null ? score.GetScore() : 0;
        float elapsedTime = Time.time - sessionStartTime;
        int activeBubbles = bubbleManager != null ? bubbleManager.GetActiveBubbleCount() : 0;

        // Priority order: Win > Lose > Overwhelmed > Time-based states

        // 1. Win state (score >= 20)
        if (currentScore >= 20)
        {
            state[0] = 1;
            return state;
        }

        // 2. Lose state (time > 2 mins OR negative score)
        if (elapsedTime >= 120f || currentScore < 0)
        {
            state[1] = 1;
            return state;
        }

        // 3. Overwhelmed state (>10 bubbles on screen)
        if (activeBubbles > 10)
        {
            state[2] = 1;
            return state;
        }

        // 4. Time-based popping states (check in order: 2s, 5s, 10s)
        
        // Fast popping (10+ bubbles in 2s)
        if (bubblesPopped2s >= 10)
        {
            state[3] = 1;
            return state;
        }

        // Balanced (5 bubbles in 5s)
        if (bubblesPopped5s >= 5)
        {
            state[4] = 1;
            return state;
        }

        // Slow popping (2 bubbles in 5s)
        if (bubblesPopped5s >= 2)
        {
            state[5] = 1;
            return state;
        }

        // Stagnant (0 bubbles in 10s)
        if (bubblesPopped10s == 0 && timer10s >= 10f)
        {
            state[6] = 1;
            return state;
        }

        // Default: return slow popping if nothing else matches
        state[5] = 1;
        return state;
    }

    private void UpdateMetrics()
    {
        ScoreManager score = ScoreManager.Instance;
        int currentScore = score != null ? score.GetScore() : 0;
        int totalBubbles = bubbleManager != null ? bubbleManager.totalbubbles : 1;
        float elapsedTime = Time.time - sessionStartTime;

        // Simple fatigue calculation based on time and errors
        int errors = totalBubbles - currentScore;
        lastFatigue = Mathf.Clamp01(errors / Mathf.Max(1f, totalBubbles) * 0.5f + elapsedTime / 120f * 0.5f);

        // Engagement based on completion rate and time
        float completionRate = currentScore / Mathf.Max(1f, totalBubbles);
        lastEngagement = Mathf.Clamp01(completionRate * 0.7f + (1f - lastFatigue) * 0.3f);
    }

    private float CalculateReward(List<int> stateArray, int currentScore)
    {
        float reward = 0f;

        // Reward based on state
        if (stateArray[0] == 1) // Win
            reward += 10f;
        else if (stateArray[1] == 1) // Lose
            reward -= 10f;
        else if (stateArray[2] == 1) // Overwhelmed
            reward -= 2f;
        else if (stateArray[3] == 1) // Fast popping
            reward += 3f;
        else if (stateArray[4] == 1) // Balanced
            reward += 5f;
        else if (stateArray[5] == 1) // Slow
            reward += 1f;
        else if (stateArray[6] == 1) // Stagnant
            reward -= 3f;

        // Additional reward for score improvement
        reward += currentScore * 0.1f;

        // Penalty for fatigue
        reward -= lastFatigue * 2f;

        // Bonus for engagement
        reward += lastEngagement * 1.5f;

        return reward;
    }

    private List<float> ConvertIntListToFloat(List<int> intList)
    {
        List<float> floatList = new List<float>();
        foreach (int val in intList)
        {
            floatList.Add((float)val);
        }
        return floatList;
    }
}
