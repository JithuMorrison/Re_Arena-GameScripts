using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

// Wrapper classes for communication
[System.Serializable]
public class MCStateWrapper
{
    public List<float> state;
    public float fatigue;
    public float engagement;
    public float success;
}

[System.Serializable]
public class MCActionWrapper
{
    public List<float> action;
    public MCAdjustments adjustments;
    public float log_prob;
}

[System.Serializable]
public class MCAdjustments
{
    public float upper_threshold_change;
    public float lower_threshold_change;
    public float gap_change;
    public float difficulty_change;
}

[System.Serializable]
public class MCTransitionWrapper
{
    public List<MCTransition> transitions;
    public int batch_size;
    public int epochs;
}

[System.Serializable]
public class MCTransition
{
    public List<float> state;
    public List<float> action;
    public float reward;
    public List<float> next_state;
    public bool done;
    public float log_prob;
}

[System.Serializable]
public class MCSessionLogWrapper
{
    public float time;
    public float similarity_current;
    public float similarity_avg_5s;
    public int score;
    public int difficulty_level;
    public float upper_threshold;
    public float lower_threshold;
    public float gap_between_actions;
    public string current_animation;
    public float fatigue;
    public float engagement;
    public float success_rate;
    public float time_above_threshold;
    public float time_below_threshold;
}

public class MCPPOStateTracker : MonoBehaviour
{
    [Header("Game Components")]
    public MoveSimilarity moveSimilarity;
    public RandomAnimationPlayer animationPlayer;
    public TMP_Text scoreText;
    public ResultDisplay resultDisplay;

    [Header("PPO Settings")]
    public float updateInterval = 5f;
    public bool trainingMode = true;

    [Header("Threshold Settings")]
    public float upperThreshold = 80f;  // Score increases when above this for 1s
    public float lowerThreshold = 70f;  // Score decreases when below this for 2s

    [Header("Animation Difficulty")]
    public int currentDifficulty = 0;  // 0=Easy, 1=Medium, 2=Hard
    
    [System.Serializable]
    public class AnimationSet
    {
        public string difficultyName;
        public string[] animationNames;
    }
    
    public AnimationSet[] difficultyLevels = new AnimationSet[3];

    private string flaskUrl = "http://127.0.0.1:5000/mc_action";
    private string trainUrl = "http://127.0.0.1:5000/mc_train";
    private string loggingUrl = "http://127.0.0.1:5000/store_mc_session";

    // Scoring system
    private int score = 0;
    private float timeAboveThreshold = 0f;
    private float timeBelowThreshold = 0f;
    private bool lastAboveUpper = false;
    private bool lastBelowLower = false;

    // State tracking
    private Queue<float> similarityHistory = new Queue<float>();
    private const int historySize = 5;
    private float sessionStartTime = 0f;
    private string currentAnimation = "";

    // Experience replay
    private List<float> prevState;
    private List<float> prevAction;
    private List<MCTransition> experienceBuffer = new List<MCTransition>();

    // Metrics
    private float lastFatigue;
    private float lastEngagement;
    private float lastSuccessRate;
    private int totalAttempts = 0;
    private int successfulAttempts = 0;
    private int lastScore = 0;

    void Start()
    {
        // Validate components
        if (moveSimilarity == null)
        {
            Debug.LogError("MCPPOStateTracker: MoveSimilarity not assigned!");
            enabled = false;
            return;
        }

        if (animationPlayer == null)
        {
            Debug.LogError("MCPPOStateTracker: RandomAnimationPlayer not assigned!");
            enabled = false;
            return;
        }

        // Initialize difficulty levels if not set
        if (difficultyLevels == null || difficultyLevels.Length != 3)
        {
            difficultyLevels = new AnimationSet[3];
            difficultyLevels[0] = new AnimationSet { difficultyName = "Easy", animationNames = new string[0] };
            difficultyLevels[1] = new AnimationSet { difficultyName = "Medium", animationNames = new string[0] };
            difficultyLevels[2] = new AnimationSet { difficultyName = "Hard", animationNames = new string[0] };
        }

        sessionStartTime = Time.time;
        StartCoroutine(PPOUpdateLoop());
    }

    void Update()
    {
        UpdateScoringSystem();
        UpdateSimilarityHistory();
    }

    void UpdateScoringSystem()
    {
        float similarity = moveSimilarity.GetSimilarity() * 100f;

        // Check if above upper threshold
        if (similarity >= upperThreshold)
        {
            timeAboveThreshold += Time.deltaTime;
            timeBelowThreshold = 0f;

            // Score increases after being above threshold for 1 second
            if (timeAboveThreshold >= 1f && !lastAboveUpper)
            {
                score++;
                successfulAttempts++;
                lastAboveUpper = true;
                lastBelowLower = false;
                Debug.Log($"✅ Score increased to {score} (Similarity: {similarity:F1}%)");
            }
        }
        else
        {
            if (lastAboveUpper)
            {
                lastAboveUpper = false;
                timeAboveThreshold = 0f;
            }
        }

        // Check if below lower threshold
        if (similarity <= lowerThreshold)
        {
            timeBelowThreshold += Time.deltaTime;
            timeAboveThreshold = 0f;

            // Score decreases after being below threshold for 2 seconds
            if (timeBelowThreshold >= 2f && !lastBelowLower)
            {
                score = Mathf.Max(0, score - 1);
                totalAttempts++;
                lastBelowLower = true;
                lastAboveUpper = false;
                Debug.Log($"❌ Score decreased to {score} (Similarity: {similarity:F1}%)");
            }
        }
        else
        {
            if (lastBelowLower)
            {
                lastBelowLower = false;
                timeBelowThreshold = 0f;
            }
        }

        // Update score display
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
        IsEpisodeDone();
    }

    void UpdateSimilarityHistory()
    {
        float similarity = moveSimilarity.GetSimilarity();
        similarityHistory.Enqueue(similarity);

        if (similarityHistory.Count > historySize)
        {
            similarityHistory.Dequeue();
        }
    }

    IEnumerator PPOUpdateLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);

            // Get current state
            List<float> currentState = GetStateVector();

            Debug.Log($"MC PPO State: Similarity Avg={GetAverageSimilarity():F2}, Score={score}, Difficulty={currentDifficulty}");

            // Log session data
            StartCoroutine(SendSessionLog(currentState));

            // Send state to PPO and get action
            MCStateWrapper stateWrapper = new MCStateWrapper
            {
                state = currentState,
                fatigue = lastFatigue,
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

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseText = www.downloadHandler.text;
                MCActionWrapper actionData = JsonUtility.FromJson<MCActionWrapper>(responseText);

                if (actionData != null && actionData.adjustments != null)
                {
                    Debug.Log($"MC PPO Action received - Log Prob: {actionData.log_prob:F3}");
                    Debug.Log($"Adjustments - Upper: {actionData.adjustments.upper_threshold_change:F2}, " +
                              $"Lower: {actionData.adjustments.lower_threshold_change:F2}, " +
                              $"Gap: {actionData.adjustments.gap_change:F2}, " +
                              $"Difficulty: {actionData.adjustments.difficulty_change:F2}");

                    // Apply adjustments
                    ApplyAdjustments(actionData.adjustments);

                    // Calculate reward
                    float reward = CalculateReward(currentState);

                    // Store experience if we have a previous state
                    if (prevState != null && prevState.Count > 0)
                    {
                        bool done = IsEpisodeDone();

                        MCTransition transition = new MCTransition
                        {
                            state = prevState,
                            action = prevAction,
                            reward = reward,
                            next_state = currentState,
                            done = done,
                            log_prob = actionData.log_prob
                        };

                        experienceBuffer.Add(transition);

                        // Train periodically
                        if (experienceBuffer.Count >= 5 && trainingMode)
                        {
                            StartCoroutine(TrainPPO());
                        }

                        if (done)
                        {
                            Debug.Log("Episode ended. Resetting...");
                            ResetEpisode();
                        }
                    }

                    // Store current state and action
                    prevState = new List<float>(currentState);
                    prevAction = actionData.action;
                }
            }
            else
            {
                Debug.LogError("MC PPO Action Error: " + www.error);
            }

            www.Dispose();

            // Update metrics
            UpdateMetrics();
            lastScore = score;
        }
    }

    IEnumerator TrainPPO()
    {
        MCTransitionWrapper transitionWrapper = new MCTransitionWrapper
        {
            transitions = new List<MCTransition>(experienceBuffer),
            batch_size = 64,
            epochs = 10
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
            Debug.Log("MC PPO Training successful: " + www.downloadHandler.text);
            experienceBuffer.Clear();
        }
        else
        {
            Debug.LogError("MC PPO Training Error: " + www.error);
        }

        www.Dispose();
    }

    IEnumerator SendSessionLog(List<float> currentState)
    {
        MCSessionLogWrapper log = new MCSessionLogWrapper
        {
            time = Time.time - sessionStartTime,
            similarity_current = moveSimilarity.GetSimilarity(),
            similarity_avg_5s = GetAverageSimilarity(),
            score = score,
            difficulty_level = currentDifficulty,
            upper_threshold = upperThreshold,
            lower_threshold = lowerThreshold,
            gap_between_actions = animationPlayer.gapBetweenActions,
            current_animation = currentAnimation,
            fatigue = lastFatigue,
            engagement = lastEngagement,
            success_rate = lastSuccessRate,
            time_above_threshold = timeAboveThreshold,
            time_below_threshold = timeBelowThreshold
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
        // State: [similarity_history (5 values), current_score, difficulty_level]
        List<float> state = new List<float>();

        // Add similarity history (pad if needed)
        float[] historyArray = similarityHistory.ToArray();
        for (int i = 0; i < historySize; i++)
        {
            if (i < historyArray.Length)
                state.Add(historyArray[i]);
            else
                state.Add(0f);
        }

        // Add current score (normalized to 0-1 range, assuming max score ~50)
        state.Add(Mathf.Clamp01(score / 50f));

        // Add difficulty level (normalized to 0-1 range)
        state.Add(currentDifficulty / 2f);

        return state;
    }

    void ApplyAdjustments(MCAdjustments adjustments)
    {
        // Adjust upper threshold
        upperThreshold = Mathf.Clamp(upperThreshold + adjustments.upper_threshold_change, 70f, 95f);

        // Adjust lower threshold
        lowerThreshold = Mathf.Clamp(lowerThreshold + adjustments.lower_threshold_change, 50f, 80f);

        // Ensure lower < upper
        if (lowerThreshold >= upperThreshold)
        {
            lowerThreshold = upperThreshold - 5f;
        }

        // Adjust gap between animations
        if (animationPlayer != null)
        {
            animationPlayer.gapBetweenActions = Mathf.Clamp(
                animationPlayer.gapBetweenActions + adjustments.gap_change, 
                2f, 
                10f
            );
        }

        // Adjust difficulty based on action value
        int newDifficulty = currentDifficulty;
        if (adjustments.difficulty_change > 0.3f)
        {
            newDifficulty = Mathf.Min(2, currentDifficulty + 1);
        }
        else if (adjustments.difficulty_change < -0.3f)
        {
            newDifficulty = Mathf.Max(0, currentDifficulty - 1);
        }

        if (newDifficulty != currentDifficulty)
        {
            currentDifficulty = newDifficulty;
            UpdateAnimationDifficulty();
        }

        Debug.Log($"Thresholds: Upper={upperThreshold:F1}, Lower={lowerThreshold:F1}, " +
                  $"Gap={animationPlayer.gapBetweenActions:F1}s, Difficulty={GetDifficultyName()}");
    }

    void UpdateAnimationDifficulty()
    {
        if (animationPlayer != null && difficultyLevels[currentDifficulty].animationNames.Length > 0)
        {
            animationPlayer.boolNames = difficultyLevels[currentDifficulty].animationNames;
            Debug.Log($"Switched to {GetDifficultyName()} animations");
        }
    }

    string GetDifficultyName()
    {
        return difficultyLevels[currentDifficulty].difficultyName;
    }

    float GetAverageSimilarity()
    {
        if (similarityHistory.Count == 0) return 0f;
        
        float sum = 0f;
        foreach (float sim in similarityHistory)
        {
            sum += sim;
        }
        return sum / similarityHistory.Count;
    }

    float CalculateReward(List<float> state)
    {
        float reward = 0f;

        // Reward for score improvement
        int scoreDelta = score - lastScore;
        reward += scoreDelta * 3f;

        // Reward for maintaining high similarity
        float avgSimilarity = GetAverageSimilarity();
        if (avgSimilarity > 0.8f)
        {
            reward += 2f;
        }
        else if (avgSimilarity > 0.7f)
        {
            reward += 1f;
        }
        else if (avgSimilarity < 0.5f)
        {
            reward -= 1f;
        }

        // Penalty for fatigue
        reward -= lastFatigue * 2f;

        // Bonus for engagement
        reward += lastEngagement * 1.5f;

        // Bonus for high success rate
        reward += lastSuccessRate * 1f;

        return reward;
    }

   bool IsEpisodeDone()
    {
        float elapsedTime = Time.time - sessionStartTime;

        // Win condition
        if (score >= 30)
        {
            if (resultDisplay)
                resultDisplay.ShowResult("win");
            StopAllCoroutines(); // Stop PPO loop
            return true;
        }

        // Lose condition - time limit (3 minutes)
        if (elapsedTime >= 180f)
        {
            if (resultDisplay)
                resultDisplay.ShowResult("lose");
            StopAllCoroutines(); // Stop PPO loop
            return true;
        }

        return false;
    }

    void ResetEpisode()
    {
        score = 0;
        lastScore = 0;
        timeAboveThreshold = 0f;
        timeBelowThreshold = 0f;
        lastAboveUpper = false;
        lastBelowLower = false;
        similarityHistory.Clear();
        experienceBuffer.Clear();
        prevState = null;
        prevAction = null;
        totalAttempts = 0;
        successfulAttempts = 0;
        sessionStartTime = Time.time;
    }

    void UpdateMetrics()
    {
        float elapsedTime = Time.time - sessionStartTime;
        float avgSimilarity = GetAverageSimilarity();

        // Fatigue based on time and performance
        lastFatigue = Mathf.Clamp01((elapsedTime / 180f) * 0.5f + (1f - avgSimilarity) * 0.5f);

        // Engagement based on similarity and score progress
        float scoreProgress = Mathf.Clamp01(score / 30f);
        lastEngagement = Mathf.Clamp01(avgSimilarity * 0.5f + scoreProgress * 0.3f + (1f - lastFatigue) * 0.2f);

        // Success rate
        lastSuccessRate = totalAttempts > 0 ? (float)successfulAttempts / totalAttempts : 0.5f;
    }

    void OnValidate()
    {
        if (moveSimilarity == null) Debug.LogWarning("MCPPOStateTracker: MoveSimilarity not assigned!");
        if (animationPlayer == null) Debug.LogWarning("MCPPOStateTracker: RandomAnimationPlayer not assigned!");
    }
}
