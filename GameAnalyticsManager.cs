using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// Serializable classes for JSON payload
[System.Serializable]
public class GameData1
{
    public int score;
    public float leftHandMaxFromHip;
    public float rightHandMaxFromHip;
    public float leftLegMax;
    public float rightLegMax;
}

[System.Serializable]
public class GamePayload
{
    public GameData1 gameData;
    public string status;
}

public class GameAnalyticsManager : MonoBehaviour
{
    [Header("Player Transforms")]
    public Transform hip;          // Reference hip Transform
    public Transform leftHand;
    public Transform rightHand;
    public Transform leftLeg;
    public Transform rightLeg;

    [Header("UI")]
    public Button submitButton;
    public string sceneName;

    [Header("API Settings")]
    public string apiUrl = "http://localhost:5000/api/session"; // base URL without sessionId

    [Header("Analytics")]
    public float leftHandMaxFromHip;
    public float rightHandMaxFromHip;
    public float leftLegMax;
    public float rightLegMax;

    void Start()
    {
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);
    }

    void Update()
    {
        if (hip == null) return;

        // Calculate distances from hip
        if (leftHand != null)
        {
            float dist = Vector3.Distance(leftHand.position, hip.position);
            if (dist > leftHandMaxFromHip) leftHandMaxFromHip = dist;
        }

        if (rightHand != null)
        {
            float dist = Vector3.Distance(rightHand.position, hip.position);
            if (dist > rightHandMaxFromHip) rightHandMaxFromHip = dist;
        }

        if (leftLeg != null)
        {
            float dist = Vector3.Distance(leftLeg.position, hip.position);
            if (dist > leftLegMax) leftLegMax = dist;
        }

        if (rightLeg != null)
        {
            float dist = Vector3.Distance(rightLeg.position, hip.position);
            if (dist > rightLegMax) rightLegMax = dist;
        }
    }

    void OnSubmitClicked()
    {
        if (SessionManager.Instance == null || string.IsNullOrEmpty(SessionManager.Instance.sessionId))
        {
            Debug.LogError("No session ID found!");
            return;
        }

        string sessionId = SessionManager.Instance.sessionId;
        string urlWithId = apiUrl + "/" + sessionId; // append sessionId to URL

        // Get score from ScoreManager
        int currentScore = ScoreManager.Instance != null ? ScoreManager.Instance.GetScore() : 0;

        // Create payload
        GameData1 data = new GameData1
        {
            score = currentScore,
            leftHandMaxFromHip = leftHandMaxFromHip,
            rightHandMaxFromHip = rightHandMaxFromHip,
            leftLegMax = leftLegMax,
            rightLegMax = rightLegMax
        };

        GamePayload payload = new GamePayload
        {
            gameData = data,
            status = "active"
        };

        string json = JsonUtility.ToJson(payload);
        StartCoroutine(SendGameData(urlWithId, json));
    }

    IEnumerator SendGameData(string url, string json)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "PUT"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"Error sending game data: {request.error}");
            }
            else
            {
                Debug.Log($"Game data sent successfully: {request.downloadHandler.text}");
                SceneManager.LoadScene(sceneName);
            }
        }
    }
}
