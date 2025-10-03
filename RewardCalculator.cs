using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class Transition
{
    public List<float> state;
    public int action;  // Changed to int for action_index
    public float reward;
    public List<float> next_state;
    public bool done;
    public float log_prob;
}

[System.Serializable]
public class TransitionsWrapper
{
    public List<Transition> transitions;
}

public class RewardCalculator : MonoBehaviour
{
    private string flaskTrainUrl = "http://127.0.0.1:5000/ppo_train";

    public void SendReward(List<float> state, List<float> action, float reward, List<float> nextState, bool done = false, float logProb = 0f)
    {
        // Convert action list to action index (simplified - you may need to adjust this)
        int actionIndex = GetActionIndex(action);

        Transition transition = new Transition
        {
            state = state,
            action = actionIndex,
            reward = reward,
            next_state = nextState,
            done = done,
            log_prob = logProb
        };

        TransitionsWrapper wrapper = new TransitionsWrapper
        {
            transitions = new List<Transition> { transition }
        };

        string jsonData = JsonUtility.ToJson(wrapper);
        StartCoroutine(PostReward(jsonData));
    }

    private int GetActionIndex(List<float> action)
    {
        // Convert action parameters to discrete index
        // This is a simplified version - adjust based on your action space
        if (action == null || action.Count == 0)
            return 0;

        // Example: combine action parameters into an index
        // You can make this more sophisticated based on your needs
        float sum = 0f;
        foreach (float val in action)
        {
            sum += Mathf.Abs(val);
        }
        
        return Mathf.RoundToInt(sum * 10f) % 100; // Simple hash to index
    }

    IEnumerator PostReward(string jsonData)
    {
        UnityWebRequest www = new UnityWebRequest(flaskTrainUrl, "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
        www.uploadHandler = new UploadHandlerRaw(bodyRaw);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
            Debug.Log("Training data sent successfully: " + www.downloadHandler.text);
        else
            Debug.LogError("Error sending training data: " + www.error);

        www.Dispose();
    }
}
