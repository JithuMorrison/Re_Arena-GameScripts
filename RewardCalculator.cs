using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class Experience
{
    public List<float> state;
    public List<float> action;
    public float reward;
    public List<float> next_state;
    public bool done;  // ✅ Added done field
}

// Wrapper to match Flask endpoint
[System.Serializable]
public class ExperienceWrapper
{
    public List<Experience> transitions;
}

public class RewardCalculator : MonoBehaviour
{
    private string flaskTrainUrl = "http://127.0.0.1:5000/ppo_train";

    public void SendReward(List<float> state, List<float> action, float reward, List<float> nextState, bool done = false)
    {
        Experience exp = new Experience
        {
            state = state,
            action = action,
            reward = reward,
            next_state = nextState,
            done = done  // ✅ Pass done status
        };

        ExperienceWrapper wrapper = new ExperienceWrapper
        {
            transitions = new List<Experience> { exp }  // wrap single experience in a list
        };

        string jsonData = JsonUtility.ToJson(wrapper);
        StartCoroutine(PostReward(jsonData));
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
            Debug.Log("Reward sent successfully.");
        else
            Debug.LogError("Error sending reward: " + www.error);

        www.Dispose();
    }
}
