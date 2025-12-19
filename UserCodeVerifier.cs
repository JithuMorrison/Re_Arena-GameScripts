using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

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
                    // Parse the response
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

                    // Save session data (SessionManager assumed present in your project)
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
        if (string.IsNullOrEmpty(jsonResponse)) return null;

        // 1) Parse basic fields (success, user, games) using JsonUtility
        ApiResponse api = null;
        try
        {
            api = JsonUtility.FromJson<ApiResponse>(jsonResponse);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("JsonUtility.FromJson failed for ApiResponse: " + ex.Message);
        }

        // 2) Build the UserResponse
        UserResponse response = new UserResponse();
        response.success = api != null && api.success;
        response.user = api != null ? api.user : null;
        response.games = api != null ? api.games : null;

        // 3) Parse gameConfigs using MiniJSON (works with dynamic keys)
        response.gameConfigs = ParseGameConfigs(jsonResponse);

        return response;
    }

    /// <summary>
    /// Parses gameConfigs section into a Dictionary<string, GameConfig>
    /// using MiniJSON to handle dynamic keys and nested objects.
    /// </summary>
    private Dictionary<string, GameConfig> ParseGameConfigs(string jsonResponse)
    {
        var result = new Dictionary<string, GameConfig>();

        try
        {
            // Deserialize the full JSON into an object dictionary
            var top = MiniJSON.Json.Deserialize(jsonResponse) as Dictionary<string, object>;
            if (top == null)
            {
                Debug.LogWarning("Top-level JSON is not an object.");
                return result;
            }

            if (!top.ContainsKey("gameConfigs"))
            {
                Debug.Log("No gameConfigs key found in response.");
                return result;
            }

            var gameConfigsRaw = top["gameConfigs"] as Dictionary<string, object>;
            if (gameConfigsRaw == null)
            {
                Debug.LogWarning("gameConfigs is not an object.");
                return result;
            }

            foreach (var kv in gameConfigsRaw)
            {
                try
                {
                    string gameKey = kv.Key;
                    object rawValue = kv.Value;

                    // Serialize the nested config object back to JSON string
                    string configJson = MiniJSON.Json.Serialize(rawValue);

                    // Use JsonUtility to create a typed GameConfig
                    GameConfig config = JsonUtility.FromJson<GameConfig>(configJson);

                    if (config == null)
                    {
                        Debug.LogWarning($"Failed to JsonUtility.FromJson for {gameKey}. Raw JSON: {configJson}");
                        continue;
                    }

                    // For safety, set game_name if missing
                    if (string.IsNullOrEmpty(config.game_name))
                        config.game_name = gameKey;

                    // For nested lights probabilities, JsonUtility should have parsed into the LightsGreenProb field
                    result[gameKey] = config;

                    Debug.Log($"Parsed gameConfig [{gameKey}] -> game_name={config.game_name}, enabled={config.enabled}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Error parsing gameConfigs entry '{kv.Key}': {e.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Exception in ParseGameConfigs: " + ex.Message);
        }

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
public class LightsGreenProb
{
    public float lh;
    public float ll;
    public float rl;
    public float rh;
}

[Serializable]
public class GameConfig
{
    // Match the JSON keys returned by the server as closely as possible
    public string difficulty;
    public bool enabled;
    public string game_name;

    // bubble_game fields (names exactly as in server JSON)
    public float bubbleLifetime;
    public float bubbleLifetime_max;
    public float bubbleSize;
    public float bubbleSize_max;
    public float bubbleSpeedAction;
    public float bubbleSpeed_max;
    public float spawnAreaSize;
    public float spawnAreaSize_max;
    public float spawnHeight;
    public float spawnHeight_max;
    public int max_bubbles;
    public int numBubbles;
    public int numBubbles_max;
    public int target_score;

    // lights_green_prob nested object
    public LightsGreenProb lights_green_prob;

    // get_set_repeat fields
    public int action_time_delay;
    public int num_actions;
    public int similarity_max;
    public int similarity_min;

    // fallback / optional fields used in manual parsing previously
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
    // gameConfigs intentionally omitted here because we parse it with MiniJSON
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

#region MINIJSON (Lightweight JSON serializer/deserializer)
// The following MiniJSON implementation is public-domain / permissive and commonly used in Unity projects.
// It supports Deserialize -> object (Dictionary<string,object>, List<object>, string, double, bool, null)
// and Serialize to produce JSON string from objects composed of primitives, lists and dictionaries.
//
// I included a compact version here. If your project already has a JSON utility (SimpleJSON, Newtonsoft.Json, etc.),
// you can remove this and use that instead.
public static class MiniJSON
{
    public static object JsonDeserialize(string json)
    {
        return Json.Deserialize(json);
    }

    public static string JsonSerialize(object obj)
    {
        return Json.Serialize(obj);
    }

    // The actual implementation below is the classic "MiniJSON" found in many Unity repos.
    // Note: I renamed entry points to Json.Deserialize / Json.Serialize for convenience.
    public static class Json
    {
        public static object Deserialize(string json)
        {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }

        sealed class Parser
        {
            const string WORD_BREAK = "{}[],:\" \t\n\r";

            enum TOKEN
            {
                NONE,
                CURLY_OPEN,
                CURLY_CLOSE,
                SQUARED_OPEN,
                SQUARED_CLOSE,
                COLON,
                COMMA,
                STRING,
                NUMBER,
                TRUE,
                FALSE,
                NULL
            };

            StringReader json;

            Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }

            public static object Parse(string jsonString)
            {
                var instance = new Parser(jsonString);
                return instance.ParseValue();
            }

            void EatWhitespace()
            {
                while (char.IsWhiteSpace((char)json.Peek()))
                    json.Read();
            }

            public object ParseValue()
            {
                EatWhitespace();
                int c = json.Peek();
                if (c == -1) return null;

                char ch = (char)c;
                switch (ch)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case 't':
                        json.Read(); json.Read(); json.Read(); json.Read();
                        return true;
                    case 'f':
                        json.Read(); json.Read(); json.Read(); json.Read(); json.Read();
                        return false;
                    case 'n':
                        json.Read(); json.Read(); json.Read(); json.Read();
                        return null;
                    default:
                        return ParseNumber();
                }
            }

            Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> table = new Dictionary<string, object>();
                json.Read(); // {

                while (true)
                {
                    EatWhitespace();
                    if ((char)json.Peek() == '}')
                    {
                        json.Read();
                        break;
                    }

                    string name = ParseString();
                    EatWhitespace();

                    // colon
                    json.Read();
                    EatWhitespace();

                    object value = ParseValue();
                    table[name] = value;

                    EatWhitespace();
                    int next = json.Peek();
                    if (next == ',') { json.Read(); continue; }
                    if (next == '}') { json.Read(); break; }
                }
                return table;
            }

            List<object> ParseArray()
            {
                List<object> array = new List<object>();
                json.Read(); // [

                while (true)
                {
                    EatWhitespace();
                    if ((char)json.Peek() == ']') { json.Read(); break; }
                    object value = ParseValue();
                    array.Add(value);
                    EatWhitespace();
                    int next = json.Peek();
                    if (next == ',') { json.Read(); continue; }
                    if (next == ']') { json.Read(); break; }
                }

                return array;
            }

            string ParseString()
            {
                System.Text.StringBuilder s = new System.Text.StringBuilder();
                json.Read(); // opening quote

                while (true)
                {
                    int c = json.Read();
                    if (c == -1) break;
                    char ch = (char)c;
                    if (ch == '"') break;
                    if (ch == '\\')
                    {
                        int esc = json.Read();
                        if (esc == -1) break;
                        char ech = (char)esc;
                        switch (ech)
                        {
                            case '"': s.Append('"'); break;
                            case '\\': s.Append('\\'); break;
                            case '/': s.Append('/'); break;
                            case 'b': s.Append('\b'); break;
                            case 'f': s.Append('\f'); break;
                            case 'n': s.Append('\n'); break;
                            case 'r': s.Append('\r'); break;
                            case 't': s.Append('\t'); break;
                            case 'u':
                                {
                                    char[] hex = new char[4];
                                    json.Read(hex, 0, 4);
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                }
                                break;
                        }
                    }
                    else
                    {
                        s.Append(ch);
                    }
                }

                return s.ToString();
            }

            object ParseNumber()
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                while (true)
                {
                    int c = json.Peek();
                    if (c == -1) break;
                    char ch = (char)c;
                    if ("0123456789+-.eE".IndexOf(ch) != -1)
                    {
                        sb.Append(ch);
                        json.Read();
                    }
                    else
                        break;
                }

                string num = sb.ToString();
                if (num.IndexOf('.') != -1 || num.IndexOf('e') != -1 || num.IndexOf('E') != -1)
                {
                    if (double.TryParse(num, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double d))
                        return d;
                    return 0.0;
                }
                else
                {
                    if (long.TryParse(num, out long l))
                        return l;
                    return 0L;
                }
            }

            sealed class StringReader : IDisposable
            {
                readonly string s;
                int index;

                public StringReader(string s)
                {
                    this.s = s;
                    index = 0;
                }

                public int Peek()
                {
                    if (index >= s.Length) return -1;
                    return s[index];
                }

                public int Read()
                {
                    if (index >= s.Length) return -1;
                    return s[index++];
                }

                public int Read(char[] buffer, int offset, int count)
                {
                    int i = 0;
                    while (i < count && index < s.Length)
                    {
                        buffer[offset + i] = s[index++];
                        i++;
                    }
                    return i;
                }

                public void Dispose() { }
            }
        }

        sealed class Serializer
        {
            StringBuilder builder;

            Serializer()
            {
                builder = new StringBuilder();
            }

            public static string Serialize(object obj)
            {
                var instance = new Serializer();
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }

            void SerializeValue(object obj)
            {
                if (obj == null) { builder.Append("null"); return; }

                if (obj is string) { SerializeString((string)obj); return; }
                if (obj is bool) { builder.Append((bool)obj ? "true" : "false"); return; }
                if (obj is long || obj is int || obj is short || obj is byte) { builder.Append(obj.ToString()); return; }
                if (obj is double || obj is float || obj is decimal) { builder.Append(Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture)); return; }

                if (obj is Dictionary<string, object> dict) { SerializeObject(dict); return; }
                if (obj is IDictionary idict)
                {
                    var d = new Dictionary<string, object>();
                    foreach (DictionaryEntry e in idict) d[e.Key.ToString()] = e.Value;
                    SerializeObject(d);
                    return;
                }
                if (obj is IEnumerable<object> listObj) { SerializeArray(listObj); return; }
                if (obj is IList ilist)
                {
                    var temp = new List<object>();
                    foreach (var v in ilist) temp.Add(v);
                    SerializeArray(temp);
                    return;
                }

                // fallback: try to serialize as object using reflection on public fields/properties
                SerializeString(obj.ToString());
            }

            void SerializeObject(Dictionary<string, object> dict)
            {
                builder.Append('{');
                bool first = true;
                foreach (var kv in dict)
                {
                    if (!first) builder.Append(',');
                    SerializeString(kv.Key);
                    builder.Append(':');
                    SerializeValue(kv.Value);
                    first = false;
                }
                builder.Append('}');
            }

            void SerializeArray(IEnumerable<object> array)
            {
                builder.Append('[');
                bool first = true;
                foreach (var obj in array)
                {
                    if (!first) builder.Append(',');
                    SerializeValue(obj);
                    first = false;
                }
                builder.Append(']');
            }

            void SerializeString(string str)
            {
                builder.Append('\"');
                foreach (var c in str)
                {
                    switch (c)
                    {
                        case '\"': builder.Append("\\\""); break;
                        case '\\': builder.Append("\\\\"); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            int codepoint = Convert.ToInt32(c);
                            if ((codepoint >= 32 && codepoint <= 126))
                                builder.Append(c);
                            else
                                builder.Append("\\u" + Convert.ToString(codepoint, 16).PadLeft(4, '0'));
                            break;
                    }
                }
                builder.Append('\"');
            }
        }
    }
}
#endregion
