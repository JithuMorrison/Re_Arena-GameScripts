using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

// Main verifier class
public class UserCodeVerifier : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField userCodeInput;
    public TMP_Text statusText;
    public string nextSceneName = "GameSelection";

    // Store the games list globally
    public static List<string> gamesList = new List<string>();

    // Called by Button OnClick
    public void OnVerifyButtonClick()
    {
        string userCode = userCodeInput.text.Trim();
        Debug.Log("Verify button clicked");

        if (string.IsNullOrEmpty(userCode))
        {
            if (statusText != null)
                statusText.text = "Please enter a user code.";
            return;
        }

        StartCoroutine(VerifyUserCode(userCode));
    }

    private IEnumerator VerifyUserCode(string userCode)
    {
        string url = "http://localhost:5000/usercodeunity";
        Debug.Log("Starting verification...");

        // Create JSON body
        string jsonData = "{\"userCode\":\"" + userCode + "\"}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                if (statusText != null)
                    statusText.text = "Error: " + www.error;
                Debug.LogError("Network Error: " + www.error);
            }
            else
            {
                string jsonResponse = www.downloadHandler.text;
                Debug.Log("Response received: " + jsonResponse);

                try
                {
                    // Parse the response manually since Unity JsonUtility doesn't handle dictionaries
                    UserResponse response = ParseUserResponse(jsonResponse);

                    if (response == null || !response.success)
                    {
                        if (statusText != null)
                            statusText.text = "Authentication failed.";
                        yield break;
                    }

                    // Collect game names for UI
                    gamesList = new List<string>();
                    if (response.games != null)
                    {
                        foreach (var game in response.games)
                        {
                            gamesList.Add(game.display_name);
                        }
                    }

                    // Save session data
                    SessionManager.Instance.SetResponse(response, "SESSION_" + Guid.NewGuid().ToString());

                    if (gamesList.Count > 0)
                    {
                        if (statusText != null) statusText.text = "Success! Loading next scene...";
                        SceneManager.LoadScene(nextSceneName);
                    }
                    else
                    {
                        if (statusText != null) statusText.text = "No games found for this user.";
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("JSON parse error: " + e.Message + "\n" + e.StackTrace);
                    if (statusText != null)
                        statusText.text = "Invalid response from server.";
                }
            }
        }
    }

    private UserResponse ParseUserResponse(string jsonResponse)
    {
        // Use Unity's JsonUtility to parse the basic structure
        var jsonObject = JsonUtility.FromJson<ApiResponse>(jsonResponse);
        
        if (jsonObject == null)
        {
            Debug.LogError("Failed to parse JSON response");
            return null;
        }

        UserResponse response = new UserResponse();
        response.success = jsonObject.success;
        response.user = jsonObject.user;
        response.games = jsonObject.games;

        // Parse gameConfigs manually since Unity JsonUtility doesn't support Dictionary
        response.gameConfigs = ParseGameConfigs(jsonResponse);

        return response;
    }

    private Dictionary<string, GameConfig> ParseGameConfigs(string jsonResponse)
    {
        Dictionary<string, GameConfig> gameConfigs = new Dictionary<string, GameConfig>();

        try
        {
            // Find the gameConfigs section in the JSON
            int startIndex = jsonResponse.IndexOf("\"gameConfigs\":");
            if (startIndex == -1) return gameConfigs;

            startIndex = jsonResponse.IndexOf("{", startIndex);
            int braceCount = 1;
            int currentIndex = startIndex + 1;

            // Find the matching closing brace
            while (braceCount > 0 && currentIndex < jsonResponse.Length)
            {
                if (jsonResponse[currentIndex] == '{') braceCount++;
                else if (jsonResponse[currentIndex] == '}') braceCount--;
                currentIndex++;
            }

            string gameConfigsJson = jsonResponse.Substring(startIndex, currentIndex - startIndex);
            
            // Parse each game config
            ParseGameConfigsFromJson(gameConfigsJson, gameConfigs);
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing gameConfigs: " + e.Message);
        }

        return gameConfigs;
    }

    private void ParseGameConfigsFromJson(string gameConfigsJson, Dictionary<string, GameConfig> gameConfigs)
    {
        // Remove outer braces
        gameConfigsJson = gameConfigsJson.Trim().Substring(1, gameConfigsJson.Trim().Length - 2);

        // Split by game entries (this is a simplified parser)
        string[] parts = gameConfigsJson.Split(new string[] { "\",\"" }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (string part in parts)
        {
            try
            {
                // Extract game name and config
                string cleanPart = part.Trim().Trim(',').Trim('"');
                int colonIndex = cleanPart.IndexOf("\":");
                
                if (colonIndex > 0)
                {
                    string gameName = cleanPart.Substring(0, colonIndex).Trim('"');
                    string configJson = cleanPart.Substring(colonIndex + 2).Trim();
                    
                    // Add missing braces if needed
                    if (!configJson.StartsWith("{"))
                    {
                        configJson = "{" + configJson;
                    }
                    if (!configJson.EndsWith("}"))
                    {
                        configJson = configJson + "}";
                    }

                    GameConfig config = JsonUtility.FromJson<GameConfig>(configJson);
                    if (config != null)
                    {
                        gameConfigs[gameName] = config;
                        Debug.Log($"Parsed config for {gameName}: enabled={config.enabled}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to parse game config part: " + e.Message);
            }
        }

        // Fallback: manual parsing for known games
        if (gameConfigs.Count == 0)
        {
            ParseGameConfigsManually(gameConfigsJson, gameConfigs);
        }
    }

    private void ParseGameConfigsManually(string json, Dictionary<string, GameConfig> gameConfigs)
    {
        if (string.IsNullOrEmpty(json)) return;

        // Remove whitespace for easier parsing
        string cleanJson = json.Replace("\n", "").Replace("\r", "").Replace(" ", "");

        // Extract bubble_game block
        string bubbleKey = "\"bubble_game\":{";
        int bubbleStart = cleanJson.IndexOf(bubbleKey);
        if (bubbleStart >= 0)
        {
            int bubbleEnd = cleanJson.IndexOf("}", bubbleStart) + 1;
            string bubbleBlock = cleanJson.Substring(bubbleStart + bubbleKey.Length - 1, bubbleEnd - (bubbleStart + bubbleKey.Length - 1));

            GameConfig bubbleConfig = new GameConfig();
            bubbleConfig.game_name = "bubble_game";
            bubbleConfig.enabled = bubbleBlock.Contains("\"enabled\":true") || bubbleBlock.Contains("\"enabled\":true");
            bubbleConfig.difficulty = ExtractStringValue(bubbleBlock, "difficulty");
            bubbleConfig.spawnAreaMax     = ExtractFloatValue(bubbleBlock, "spawn_area_max");
            bubbleConfig.bubbleSpeedMax   = ExtractFloatValue(bubbleBlock, "bubble_speed_max");
            bubbleConfig.bubbleLifetimeMax= ExtractFloatValue(bubbleBlock, "bubble_lifetime_max");
            bubbleConfig.spawnHeightMax   = ExtractFloatValue(bubbleBlock, "spawn_height_max");
            bubbleConfig.numBubblesMax    = ExtractIntValue(bubbleBlock, "num_bubbles_max");
            bubbleConfig.bubbleSizeMax    = ExtractFloatValue(bubbleBlock, "bubble_size_max");
            bubbleConfig.target_score = ExtractIntValue(bubbleBlock, "target_score");

            bubbleConfig.guidanceEnabled  = bubbleBlock.Contains("\"guidance_enabled\":true");

            gameConfigs["bubble_game"] = bubbleConfig;
            Debug.Log($"Parsed bubble_game: {bubbleBlock}");
        }

        // Extract memory_match block
        string memoryKey = "\"memory_match\":{";
        int memoryStart = cleanJson.IndexOf(memoryKey);
        if (memoryStart >= 0)
        {
            int memoryEnd = cleanJson.IndexOf("}", memoryStart) + 1;
            string memoryBlock = cleanJson.Substring(memoryStart + memoryKey.Length - 1, memoryEnd - (memoryStart + memoryKey.Length - 1));

            GameConfig memoryConfig = new GameConfig();
            memoryConfig.game_name = "memory_match";
            memoryConfig.enabled = memoryBlock.Contains("\"enabled\":true") || memoryBlock.Contains("\"enabled\": true");
            memoryConfig.difficulty = ExtractStringValue(memoryBlock, "difficulty");
            memoryConfig.grid_size = ExtractStringValue(memoryBlock, "grid_size");
            memoryConfig.time_limit = ExtractIntValue(memoryBlock, "time_limit");

            gameConfigs["memory_match"] = memoryConfig;
            Debug.Log($"Parsed memory_match: {memoryBlock}");
        }
    }

    private string ExtractStringValue(string json, string key)
    {
        string pattern = "\"" + key + "\":\"";
        int startIndex = json.IndexOf(pattern);
        if (startIndex == -1) return "";
        
        startIndex += pattern.Length;
        int endIndex = json.IndexOf("\"", startIndex);
        if (endIndex == -1) return "";
        
        return json.Substring(startIndex, endIndex - startIndex);
    }

    private float ExtractFloatValue(string json, string key)
    {
        string pattern = "\"" + key + "\":";
        int startIndex = json.IndexOf(pattern);
        if (startIndex == -1) return 0f;

        startIndex += pattern.Length;
        int endIndex = json.IndexOfAny(new char[] { ',', '}' }, startIndex);
        if (endIndex == -1) endIndex = json.Length;

        string valueStr = json.Substring(startIndex, endIndex - startIndex).Trim();
        if (float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        return 0f;
    }

    private int ExtractIntValue(string json, string key)
    {
        string pattern = "\"" + key + "\":";
        int startIndex = json.IndexOf(pattern);
        if (startIndex == -1) return 0;
        
        startIndex += pattern.Length;
        int endIndex = startIndex;
        while (endIndex < json.Length && (char.IsDigit(json[endIndex]) || json[endIndex] == '-'))
        {
            endIndex++;
        }
        
        string numberStr = json.Substring(startIndex, endIndex - startIndex);
        int result;
        int.TryParse(numberStr, out result);
        return result;
    }
}

#region DATA CLASSES

[Serializable]
public class UserData1
{
    public string id;
    public string name;
    public string userCode;
    public string userType;
}

[Serializable]
public class GameConfig
{
    public string difficulty;
    public bool enabled;
    public string game_name;
    public int target_score;
    public float spawnAreaMax;
    public float bubbleSpeedMax;
    public float bubbleLifetimeMax;
    public float spawnHeightMax;
    public int numBubblesMax;
    public float bubbleSizeMax;

    public bool guidanceEnabled;

    // Optional fields (like in memory_match)
    public string grid_size;
    public int time_limit;
}

[Serializable]
public class ConfigurableFieldOption
{
    public string name;
    public string type;
    public string @default;
    public int min;
    public int max;
    public List<string> options;
    public string nestedFieldsJson;
}

[Serializable]
public class GameData
{
    public string _id;
    public string name;
    public string display_name;
    public string description;
    public string category;
    public bool available;
    public string createdAt;
    public List<ConfigurableFieldOption> configurable_fields;
}

// Intermediate parsing class that matches the JSON structure Unity can handle
[Serializable]
public class ApiResponse
{
    public bool success;
    public UserData1 user;
    public List<GameData> games;
    // Note: gameConfigs is parsed manually due to Dictionary limitations
}

// Final usable response class
[Serializable]
public class UserResponse
{
    public bool success;
    public UserData1 user;
    public List<GameData> games;
    public Dictionary<string, GameConfig> gameConfigs;
}

#endregion
