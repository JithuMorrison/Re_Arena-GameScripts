using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Wrapper classes for communication
[System.Serializable]
public class DQNStateWrapper
{
    public List<float> state;
    public float fatigue;
    public float engagement;
    public bool training;
}

[System.Serializable]
public class DQNActionWrapper
{
    public int action_index;
    public DQNAdjustments adjustments;
    public float q_value;
    public float epsilon;
}

[System.Serializable]
public class DQNAdjustments
{
    public List<int> light_states;  // -1 = no change, 0=Red, 1=Orange, 2=Green
    public float light_speed_change;
    public float spawn_rate_change;
}

[System.Serializable]
public class DQNTransitionWrapper
{
    public List<DQNTransition> transitions;
    public int num_updates;
    public bool update_target;
}

[System.Serializable]
public class DQNTransition
{
    public List<float> state;
    public int action;
    public float reward;
    public List<float> next_state;
    public bool done;
}

[System.Serializable]
public class ROGLSessionLogWrapper
{
    public float time;
    public List<float> state;

    public LimbData leftHand;
    public LimbData rightHand;
    public LimbData leftLeg;
    public LimbData rightLeg;

    public int light0_state;
    public int light1_state;
    public int light2_state;
    public int light3_state;

    public int active_lanterns;
    public int score;
    public float fatigue;
    public float engagement;
}

[System.Serializable]
public class LimbData
{
    public float x, y, z;
    public int active;  // 0 or 1

    public LimbData(Vector3 v, bool isActive)
    {
        x = v.x;
        y = v.y;
        z = v.z;
        active = isActive ? 1 : 0;
    }
}

public class DQNStateTracker : MonoBehaviour
{
    [Header("Body Parts")]
    public Transform leftHand;
    public Transform rightHand;
    public Transform leftLeg;
    public Transform rightLeg;

    [Header("Game Components")]
    public LightController lightController;
    public LanternSpawner lanternSpawner;
    public ROGLScoreManager scoreManager;
    public ResultDisplay resultDisplay;   

    private bool gameEnded = false;

    [Header("DQN Settings")]
    public float updateInterval = 5f;  // Time between DQN queries (will be adjusted by DQN)
    public bool trainingMode = true;
    public int targetUpdateFrequency = 10;  // Update target network every N training cycles

    private string flaskUrl = "http://127.0.0.1:5000/dqn_action";
    private string trainUrl = "http://127.0.0.1:5000/dqn_train";
    private string loggingUrl = "http://127.0.0.1:5000/store_rogl_session";

    // State tracking
    private Dictionary<Transform, bool> limbActivity = new Dictionary<Transform, bool>();
    private float sessionStartTime = 0f;
    private float lastUpdateTime = 0f;
    private int trainingCycleCount = 0;

    // Experience replay
    private List<float> prevState;
    private int prevAction;
    private List<DQNTransition> experienceBuffer = new List<DQNTransition>();

    // Metrics
    private float lastFatigue;
    private float lastEngagement;
    private int lastScore = 0;

    private Coroutine dqnLoopCoroutine;

    void Start()
    {
        // Validate all required components
        bool hasErrors = false;
        
        if (leftHand == null) { Debug.LogError("DQNStateTracker: leftHand Transform not assigned!"); hasErrors = true; }
        if (rightHand == null) { Debug.LogError("DQNStateTracker: rightHand Transform not assigned!"); hasErrors = true; }
        if (leftLeg == null) { Debug.LogError("DQNStateTracker: leftLeg Transform not assigned!"); hasErrors = true; }
        if (rightLeg == null) { Debug.LogError("DQNStateTracker: rightLeg Transform not assigned!"); hasErrors = true; }
        if (lightController == null) { Debug.LogError("DQNStateTracker: LightController not assigned!"); hasErrors = true; }
        if (lanternSpawner == null) { Debug.LogError("DQNStateTracker: LanternSpawner not assigned!"); hasErrors = true; }
        if (scoreManager == null) { Debug.LogError("DQNStateTracker: ROGLScoreManager not assigned!"); hasErrors = true; }
        
        if (lightController != null && (lightController.lights == null || lightController.lights.Length < 4))
        {
            Debug.LogError("DQNStateTracker: LightController must have at least 4 lights configured!");
            hasErrors = true;
        }

        if (hasErrors)
        {
            Debug.LogError("DQNStateTracker: Cannot start due to missing references. Please assign all required components in the Inspector.");
            enabled = false;
            return;
        }

        sessionStartTime = Time.time;
        lastUpdateTime = Time.time;
        InitializeLimbTracking();
        dqnLoopCoroutine = StartCoroutine(DQNUpdateLoop());
    }

    void InitializeLimbTracking()
    {
        limbActivity[leftHand] = false;
        limbActivity[rightHand] = false;
        limbActivity[leftLeg] = false;
        limbActivity[rightLeg] = false;
    }

    void Update()
    {
        // Track limb activity based on attached lanterns
        UpdateLimbActivity();
    }

    void UpdateLimbActivity()
    {
        // Reset all to inactive
        limbActivity[leftHand] = false;
        limbActivity[rightHand] = false;
        limbActivity[leftLeg] = false;
        limbActivity[rightLeg] = false;

        // Check all active lanterns
        LanternThrow[] lanterns = FindObjectsOfType<LanternThrow>();
        foreach (var lantern in lanterns)
        {
            Transform parent = lantern.transform.parent;
            if (parent != null && limbActivity.ContainsKey(parent))
            {
                limbActivity[parent] = true;
            }
        }
    }

    IEnumerator DQNUpdateLoop()
    {
        while (!gameEnded)
        {
            // Wait for the update interval (dynamically adjusted by DQN)
            yield return new WaitForSeconds(updateInterval);

            // Get current state
            List<float> currentState = GetStateVector();
            int currentScore = scoreManager != null ? scoreManager.GetScore() : 0;

            Debug.Log("DQN State: " + string.Join(", ", currentState));

            // Log session data
            StartCoroutine(SendSessionLog(currentState));

            // Send state to DQN and get action
            DQNStateWrapper stateWrapper = new DQNStateWrapper
            {
                state = currentState,
                fatigue = lastFatigue,
                engagement = lastEngagement,
                training = trainingMode
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
                DQNActionWrapper actionData = JsonUtility.FromJson<DQNActionWrapper>(responseText);

                if (actionData != null && actionData.adjustments != null)
                {
                    Debug.Log($"DQN Action: {actionData.action_index}, Q-Value: {actionData.q_value:F3}, Epsilon: {actionData.epsilon:F3}");

                    // Apply adjustments to environment
                    ApplyAdjustments(actionData.adjustments);

                    // Calculate reward
                    float reward = CalculateReward(currentState, currentScore);

                    // Store experience if we have a previous state
                    if (prevState != null && prevState.Count > 0)
                    {
                        bool done = IsEpisodeDone(currentScore);
                        
                        DQNTransition transition = new DQNTransition
                        {
                            state = prevState,
                            action = prevAction,
                            reward = reward,
                            next_state = currentState,
                            done = done
                        };

                        experienceBuffer.Add(transition);

                        // Train periodically
                        if (experienceBuffer.Count >= 5 && trainingMode)
                        {
                            StartCoroutine(TrainDQN());
                        }

                        if (done)
                        {
                            Debug.Log("Episode ended. Resetting...");
                            // Reset episode
                            prevState = null;
                            experienceBuffer.Clear();
                        }
                    }

                    // Store current state and action for next iteration
                    prevState = new List<float>(currentState);
                    prevAction = actionData.action_index;
                }
            }
            else
            {
                Debug.LogError("DQN Action Error: " + www.error);
            }

            www.Dispose();

            // Update metrics
            UpdateMetrics();
            lastScore = currentScore;
            lastUpdateTime = Time.time;
        }
    }

    IEnumerator TrainDQN()
    {
        trainingCycleCount++;
        bool updateTarget = (trainingCycleCount % targetUpdateFrequency == 0);

        DQNTransitionWrapper transitionWrapper = new DQNTransitionWrapper
        {
            transitions = new List<DQNTransition>(experienceBuffer),
            num_updates = 5,
            update_target = updateTarget
        };

        string jsonData = JsonUtility.ToJson(transitionWrapper);
        UnityWebRequest www = new UnityWebRequest(trainUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            Debug.Log("DQN Training successful: " + www.downloadHandler.text);
            experienceBuffer.Clear();
        }
        else
        {
            Debug.LogError("DQN Training Error: " + www.error);
        }

        www.Dispose();
    }

    IEnumerator SendSessionLog(List<float> currentState)
    {
        // Check for null references before creating log
        if (lightController == null || lightController.lights == null || lightController.lights.Length < 4)
        {
            Debug.LogError("Cannot send session log: LightController or lights not properly set up");
            yield break;
        }

        if (leftHand == null || rightHand == null || leftLeg == null || rightLeg == null)
        {
            Debug.LogError("Cannot send session log: Body part transforms not assigned");
            yield break;
        }

        ROGLSessionLogWrapper log = new ROGLSessionLogWrapper
        {
            time = Time.time - sessionStartTime,
            state = currentState,

            leftHand = new LimbData(leftHand.position, limbActivity[leftHand]),
            rightHand = new LimbData(rightHand.position, limbActivity[rightHand]),
            leftLeg = new LimbData(leftLeg.position, limbActivity[leftLeg]),
            rightLeg = new LimbData(rightLeg.position, limbActivity[rightLeg]),

            light0_state = (int)lightController.lights[0].state,
            light1_state = (int)lightController.lights[1].state,
            light2_state = (int)lightController.lights[2].state,
            light3_state = (int)lightController.lights[3].state,

            active_lanterns = LanternThrow.activeLanterns,
            score = scoreManager != null ? scoreManager.GetScore() : 0,
            fatigue = lastFatigue,
            engagement = lastEngagement
        };

        string json = JsonUtility.ToJson(log);
        UnityWebRequest www = new UnityWebRequest(loggingUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
            Debug.LogError("Logging Error: " + www.error);

        www.Dispose();
    }

    List<float> GetStateVector()
    {
        // State: [leftHand_active, leftLeg_active, rightLeg_active, rightHand_active,
        //         light0_state, light1_state, light2_state, light3_state,
        //         active_lanterns_normalized, score_normalized]
        
        List<float> state = new List<float>();

        // Limb activity (0 or 1) - with null checks
        state.Add(limbActivity.ContainsKey(leftHand) && limbActivity[leftHand] ? 1f : 0f);
        state.Add(limbActivity.ContainsKey(leftLeg) && limbActivity[leftLeg] ? 1f : 0f);
        state.Add(limbActivity.ContainsKey(rightLeg) && limbActivity[rightLeg] ? 1f : 0f);
        state.Add(limbActivity.ContainsKey(rightHand) && limbActivity[rightHand] ? 1f : 0f);

        // Light states (0=Red, 1=Orange, 2=Green)
        if (lightController != null && lightController.lights != null && lightController.lights.Length >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                state.Add((float)lightController.lights[i].state);
            }
        }
        else
        {
            Debug.LogError("LightController or lights array is null/invalid!");
            // Add default values
            for (int i = 0; i < 4; i++)
            {
                state.Add(0f);
            }
        }

        // Active lanterns normalized (0-1 range, max 5)
        state.Add(Mathf.Clamp01(LanternThrow.activeLanterns / 5f));

        // Score normalized (-1 to 1, assuming score range -20 to 20)
        int currentScore = 0;
        if (scoreManager != null)
        {
            currentScore = scoreManager.GetScore();
        }
        else
        {
            Debug.LogWarning("ScoreManager is null!");
        }
        state.Add(Mathf.Clamp(currentScore / 20f, -1f, 1f));

        return state;
    }

    void ApplyAdjustments(DQNAdjustments adjustments)
    {
        if (lightController == null)
        {
            Debug.LogError("Cannot apply adjustments: LightController is null");
            return;
        }

        if (lightController.lights == null || lightController.lights.Length < 4)
        {
            Debug.LogError("Cannot apply adjustments: lights array is invalid");
            return;
        }

        // Apply light state changes
        if (adjustments.light_states != null && adjustments.light_states.Count >= 4)
        {
            for (int i = 0; i < 4; i++)
            {
                if (adjustments.light_states[i] != -1)
                {
                    lightController.lights[i].state = (LightController.LightState)adjustments.light_states[i];
                    lightController.UpdateColor(lightController.lights[i]);
                }
            }
        }

        // Adjust light change speed
        if (Mathf.Abs(adjustments.light_speed_change) > 0.01f)
        {
            lightController.minTime = Mathf.Clamp(lightController.minTime + adjustments.light_speed_change, 0.5f, 5f);
            lightController.maxTime = Mathf.Clamp(lightController.maxTime + adjustments.light_speed_change, 1f, 8f);
            
            // Update interval is the average of min and max
            updateInterval = (lightController.minTime + lightController.maxTime) / 2f;
            Debug.Log($"Light speed adjusted: min={lightController.minTime:F2}, max={lightController.maxTime:F2}");
        }

        // Adjust spawn rate
        if (lanternSpawner != null && Mathf.Abs(adjustments.spawn_rate_change) > 0.01f)
        {
            lanternSpawner.spawnRate = Mathf.Clamp(lanternSpawner.spawnRate + adjustments.spawn_rate_change, 0.5f, 5f);
            Debug.Log($"Spawn rate adjusted: {lanternSpawner.spawnRate:F2}");
        }
    }

    float CalculateReward(List<float> state, int currentScore)
    {
        float reward = 0f;

        // Reward for score change (primary signal)
        // Now that scoring is fixed: Green hits = positive, Red hits = negative
        int scoreDelta = currentScore - lastScore;
        reward += scoreDelta * 2f;  // Strong signal for score changes

        // Penalty for too many active lanterns (overwhelmed)
        if (LanternThrow.activeLanterns > 3)
        {
            reward -= (LanternThrow.activeLanterns - 3) * 0.5f;
        }

        // Bonus for keeping some lanterns active (engagement)
        if (LanternThrow.activeLanterns >= 1 && LanternThrow.activeLanterns <= 3)
        {
            reward += 0.5f;  // Sweet spot for engagement
        }

        // Reward for balanced limb usage
        int activeLimbCount = 0;
        foreach (var active in limbActivity.Values)
        {
            if (active) activeLimbCount++;
        }
        if (activeLimbCount >= 2 && activeLimbCount <= 3)
        {
            reward += 1f;  // Encourage using multiple limbs
        }

        // Penalty for fatigue
        reward -= lastFatigue * 1.5f;

        // Bonus for engagement
        reward += lastEngagement * 1f;

        // Small time penalty (encourage efficiency but not rushing)
        float elapsedTime = Time.time - sessionStartTime;
        reward -= elapsedTime * 0.005f;  // Reduced from 0.01f

        return reward;
    }

    bool IsEpisodeDone(int currentScore)
    {
        if (gameEnded) return true;
        float elapsedTime = Time.time - sessionStartTime;

        // Win condition
        if (currentScore >= 20)
        {
            gameEnded = true;
            if (resultDisplay)
                resultDisplay.ShowResult("win");
            return true;
        }

        // Lose condition - time limit
        if (elapsedTime >= 120f)
        {
            gameEnded = true;
            if (resultDisplay)
                resultDisplay.ShowResult("lose");
            return true;
        }

        // Lose condition - negative score
        if (currentScore <= -10)
        {
            gameEnded = true;
            if (resultDisplay)
                resultDisplay.ShowResult("lose");
            return true;
        }

        return false;
    }

    void UpdateMetrics()
    {
        int currentScore = scoreManager != null ? scoreManager.GetScore() : 0;
        float elapsedTime = Time.time - sessionStartTime;

        // Fatigue based on time and errors
        int totalLanterns = Mathf.Max(1, LanternThrow.activeLanterns + Mathf.Abs(currentScore));
        float errorRate = Mathf.Max(0, -currentScore) / (float)totalLanterns;
        lastFatigue = Mathf.Clamp01(errorRate * 0.6f + (elapsedTime / 120f) * 0.4f);

        // Engagement based on activity and score progress
        float scoreProgress = Mathf.Clamp01(currentScore / 20f);
        int activeLimbs = 0;
        foreach (var active in limbActivity.Values)
        {
            if (active) activeLimbs++;
        }
        float limbEngagement = Mathf.Clamp01(activeLimbs / 4f);
        lastEngagement = Mathf.Clamp01(scoreProgress * 0.5f + limbEngagement * 0.3f + (1f - lastFatigue) * 0.2f);
    }

    // Validation method to check if all required components are assigned
    void OnValidate()
    {
        if (leftHand == null) Debug.LogWarning("DQNStateTracker: leftHand Transform not assigned!");
        if (rightHand == null) Debug.LogWarning("DQNStateTracker: rightHand Transform not assigned!");
        if (leftLeg == null) Debug.LogWarning("DQNStateTracker: leftLeg Transform not assigned!");
        if (rightLeg == null) Debug.LogWarning("DQNStateTracker: rightLeg Transform not assigned!");
        if (lightController == null) Debug.LogWarning("DQNStateTracker: LightController not assigned!");
        if (lanternSpawner == null) Debug.LogWarning("DQNStateTracker: LanternSpawner not assigned!");
        if (scoreManager == null) Debug.LogWarning("DQNStateTracker: ROGLScoreManager not assigned!");
    }
}
