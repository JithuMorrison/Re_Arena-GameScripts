using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Wrapper class to send state
[System.Serializable]
public class StateWrapper
{
    public List<float> state;
    public float fatigue;
    public float engagement;
    public float success;
}

// Wrapper class to receive action
[System.Serializable]
public class ActionWrapper
{
    public List<float> action;
}

public class PPOStateTracker : MonoBehaviour
{
    public GameObject leftHand, rightHand, leftShoulder, rightShoulder, hip, head;
    public BubbleManager bubbleManager;
    public RewardCalculator rewardCalculator;
    private string flaskUrl = "http://127.0.0.1:5000/ppo_action";

    private List<float> state = new List<float>(); // reuse list to avoid GC allocations
    private Vector3 prevLeftHandPos;
    private Vector3 prevRightHandPos;
    private Vector3 prevHipPos;
    private float lastFatigue;
    private float lastEngagement;
    private float lastSuccessRate;

    void Start()
    {
        prevLeftHandPos = leftHand != null ? leftHand.transform.position : Vector3.zero;
        prevRightHandPos = rightHand != null ? rightHand.transform.position : Vector3.zero;
        prevHipPos = hip != null ? hip.transform.position : Vector3.zero;

        StartCoroutine(UpdatePPO());
    }

    IEnumerator UpdatePPO()
    {
        while (true)
        {
            state.Clear(); // reuse list instead of creating a new one

            state = CollectState();

            // ------------------------
            // 2. Send State to Flask
            // ------------------------
            StateWrapper stateWrapper = new StateWrapper 
            { 
                state = state,
                fatigue = lastFatigue,      // assign computed value
                engagement = lastEngagement,
                success = lastSuccessRate
            };
            string jsonData = JsonUtility.ToJson(stateWrapper);

            UnityWebRequest www = new UnityWebRequest(flaskUrl, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (www.result == UnityWebRequest.Result.Success)
#else
            if (!www.isNetworkError && !www.isHttpError)
#endif
            {
                string responseText = www.downloadHandler.text;
                ActionWrapper actionData = JsonUtility.FromJson<ActionWrapper>(responseText);
                if (actionData != null && actionData.action != null && actionData.action.Count >= 7)
                {
                    List<float> action = actionData.action;

                    float spawnAreaSize = action[0];
                    float bubbleSpeedAction = action[1];
                    float bubbleLifetime = action[2];
                    float spawnHeight = action[3];
                    int numBubbles = Mathf.RoundToInt(action[4]);
                    float bubbleSize = action[5];
                    bool guidanceOn = action[6] > 0.5f;

                    Debug.Log("PPO Actions received: " + string.Join(",", action));

                    if (bubbleManager != null)
                    {
                        bubbleManager.UpdateEnvironment(action);
                    }

                    List<float> nextState = CollectState(); // you can reuse your CollectState logic

                    // (3) Compute reward (this can be your own logic)
                    float reward = CalculateReward(nextState,action);

                    // (4) Send experience to Flask PPO trainer
                    if (rewardCalculator != null)
                        rewardCalculator.SendReward(new List<float>(state), action, reward, nextState, true);
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

            // ------------------------
            // 3. Dispose UnityWebRequest to avoid memory leak
            // ------------------------
            www.Dispose();

            yield return new WaitForSeconds(25f); // 10 Hz
        }
    }

    private List<float> CollectState()
    {
        List<float> s = new List<float>();

        ScoreManager score = ScoreManager.Instance;
        GameConfig config = SessionManager.Instance.GetGameConfig(SessionManager.Instance.selectedGameName);

        float deltaTime = Mathf.Max(Time.deltaTime, 0.02f);

        // 1. Hand & arm positions
        float maxHandHeight = (leftHand != null && rightHand != null && hip != null) 
            ? Mathf.Max(leftHand.transform.position.y, rightHand.transform.position.y) - hip.transform.position.y 
            : 0f;

        float armExtension = (leftHand != null && leftShoulder != null) 
            ? Vector3.Distance(leftHand.transform.position, leftShoulder.transform.position) 
            : 0f;

        float stepLength = (hip != null) ? Vector3.Distance(hip.transform.position, hip.transform.position) : 0f;

        float leftHandVel = (leftHand != null) 
            ? Vector3.Distance(leftHand.transform.position, prevLeftHandPos) / deltaTime 
            : 0f;

        float rightHandVel = (rightHand != null) 
            ? Vector3.Distance(rightHand.transform.position, prevRightHandPos) / deltaTime 
            : 0f;

        float handSpeed = (leftHandVel + rightHandVel) * 0.5f;
        float jerk = Mathf.Abs(leftHandVel - rightHandVel);
        float smoothness = 1f / (1f + jerk);

        s.Add(maxHandHeight);   // 0
        s.Add(armExtension);    // 1
        s.Add(stepLength);      // 2
        s.Add(handSpeed);       // 3
        s.Add(smoothness);      // 4

        // 2. Bubble environment
        Vector3 bubblePos = new Vector3(0.5f, 1f, 0.5f);
        float bubbleSpeed = bubbleManager != null ? bubbleManager.bubbleSpeed : 1.0f;
        float spawnArea = bubbleManager != null ? bubbleManager.spawnAreaSize : 1.0f;

        s.Add(bubblePos.x);     // 5
        s.Add(bubblePos.y);     // 6
        s.Add(bubblePos.z);     // 7
        s.Add(bubbleSpeed);     // 8
        s.Add(spawnArea);       // 9

        // 3. User metrics (guaranteed values)
        int errors = (bubbleManager != null && score != null) ? bubbleManager.totalbubbles - score.GetScore() : 0;
        float elapsedTime = Time.time;
        float maxSessionTime = 180f;

        lastFatigue = ComputeFatigue(new List<float> { leftHandVel, rightHandVel }, errors, elapsedTime, maxSessionTime);

        int tasksCompleted = score != null ? score.GetScore() : 1;
        int totalTasks = bubbleManager != null ? bubbleManager.totalbubbles : 1;
        float reactionSpeed = elapsedTime / Mathf.Max(1, tasksCompleted);
        float maxReactionSpeed = 3.0f;

        lastEngagement = ComputeEngagement(tasksCompleted, totalTasks, elapsedTime, maxSessionTime, reactionSpeed, maxReactionSpeed);

        int correctMovements = score != null ? score.GetScore() : 1;
        int totalMovements = bubbleManager != null ? bubbleManager.totalbubbles : 1;
        float maxSmoothness = 1.0f;

        lastSuccessRate = ComputeSuccess(correctMovements, totalMovements, smoothness, maxSmoothness);

        // Add final 3 floats: successRate, fatigue, level
        s.Add(lastSuccessRate);  // 10
        s.Add(lastFatigue);      // 11

        // Level based on difficulty (optional)
        float level = 1f;
        if (config != null)
        {
            if (config.difficulty == "Easy") level = 0.5f;
            else if (config.difficulty == "Medium") level = 1.0f;
            else if (config.difficulty == "Hard") level = 1.5f;
        }
        s.Add(level);             // 12 -> total list size = 13 if counting from 0

        // Make sure list always has exactly 12 values for reward calculation
        while (s.Count < 12)
            s.Add(0f);

        return s;
    }

    private float CalculateReward(List<float> nextState, List<float> actions)
    {
        float reward = 0f;

        GameConfig config = SessionManager.Instance.GetGameConfig(SessionManager.Instance.selectedGameName);
        if (config == null)
        {
            Debug.LogWarning("No config found for game: " + SessionManager.Instance.selectedGameName);
            return -1f; // penalty if no config
        }

        // ✅ Step 2: Extract actions
        float spawnAreaSize = actions[0];
        float bubbleSpeed = actions[1];
        float bubbleLifetime = actions[2];
        float spawnHeight = actions[3];
        int numBubbles = Mathf.RoundToInt(actions[4]);
        float bubbleSize = actions[5];

        // ✅ Step 3: Derive min/max based on therapist's max values
        float spawnAreaMin = 0f;
        float spawnAreaMax = config.spawnAreaMax;

        float bubbleSpeedMin = 0f;
        float bubbleSpeedMax = config.bubbleSpeedMax;

        float bubbleLifetimeMin = 1f;
        float bubbleLifetimeMax = config.bubbleLifetimeMax;

        float spawnHeightMin = 0f;
        float spawnHeightMax = config.spawnHeightMax;

        int numBubblesMin = 1;
        int numBubblesMax = config.numBubblesMax;

        float bubbleSizeMin = 0f;
        float bubbleSizeMax = config.bubbleSizeMax;

        // ✅ Step 4: Check ranges
        reward += CheckRange(spawnAreaSize, spawnAreaMin, spawnAreaMax, 1f);
        reward += CheckRange(bubbleSpeed, bubbleSpeedMin, bubbleSpeedMax, 1f);
        reward += CheckRange(bubbleLifetime, bubbleLifetimeMin, bubbleLifetimeMax, 1f);
        reward += CheckRange(spawnHeight, spawnHeightMin, spawnHeightMax, 1f);
        reward += CheckRange(numBubbles, numBubblesMin, numBubblesMax, 1f);
        reward += CheckRange(bubbleSize, bubbleSizeMin, bubbleSizeMax, 1f);

        // ✅ Step 5: Add user performance factors
        float handSpeed = nextState[3];
        float successRate = nextState[10];
        float fatigue = nextState[11];

        reward += (successRate * 2.0f);
        reward += (handSpeed * 0.5f);
        reward -= (fatigue * 1.0f);

        return reward;
    }

    private float CheckRange(float value, float min, float max, float weight)
    {
        if (value >= min && value <= max)
            return +1f * weight;   // reward
        else
            return -1f * weight;   // penalty
    }

    private float ComputeFatigue(List<float> jointVelocities, int errors, float elapsedTime, float maxTime)
    {
        if (jointVelocities.Count == 0) return 0f;

        float avgVelocity = 0f;
        float maxVelocity = float.MinValue;
        foreach (var v in jointVelocities)
        {
            avgVelocity += v;
            if (v > maxVelocity) maxVelocity = v;
        }
        avgVelocity /= jointVelocities.Count;
        float velocityScore = avgVelocity / Mathf.Max(0.0001f, maxVelocity);

        float errorScore = (float)errors / Mathf.Max(1, bubbleManager != null ? bubbleManager.totalbubbles : 1);
        float timeScore = elapsedTime / Mathf.Max(0.0001f, maxTime);

        float fatigue = Mathf.Min(1.0f, 0.5f * errorScore + 0.3f * (1 - velocityScore) + 0.2f * timeScore);
        return fatigue;
    }

    private float ComputeEngagement(int tasksCompleted, int totalTasks, float activeTime, float sessionTime, float reactionSpeed, float maxReactionSpeed)
    {
        float taskRatio = (float)tasksCompleted / Mathf.Max(1, totalTasks);
        float activeRatio = activeTime / Mathf.Max(1f, sessionTime);
        float reactionRatio = reactionSpeed / Mathf.Max(1f, maxReactionSpeed);

        float engagement = Mathf.Min(1.0f, 0.5f * taskRatio + 0.3f * activeRatio + 0.2f * reactionRatio);
        return engagement;
    }

    private float ComputeSuccess(int correctMovements, int totalMovements, float smoothness, float maxSmoothness)
    {
        float accuracyScore = (float)correctMovements / Mathf.Max(1, totalMovements);
        float smoothnessScore = smoothness / Mathf.Max(1f, maxSmoothness);

        float success = Mathf.Min(1.0f, 0.6f * accuracyScore + 0.4f * smoothnessScore);
        return success;
    }
}
