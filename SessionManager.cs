using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 🔹 This persists across scenes
public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    [Header("Session Data")]
    public string sessionId;
    public UserResponse loadedResponse;   // Full response from server
    public string selectedGameName;       // Currently selected game

    // Event to notify listeners when session data is ready
    public event Action OnResponseReady;

    void Awake()
    {
        // Singleton pattern - ensure only one exists
        if (Instance != null && Instance != this)
        {
            Debug.Log("Destroying duplicate SessionManager");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // persist across scenes
        Debug.Log("SessionManager initialized and set to persist across scenes");
    }

    // ✅ Helper to set response once loaded
    public void SetResponse(UserResponse response, string sessionIdValue = null)
    {
        Debug.Log("Setting session response...");
        
        loadedResponse = response;

        if (!string.IsNullOrEmpty(sessionIdValue))
            sessionId = sessionIdValue;

        // Debug logging
        if (response != null)
        {
            Debug.Log($"✅ Session data set successfully. User: {response.user?.name}");
            Debug.Log($"✅ Games count: {response.games?.Count ?? 0}");
            Debug.Log($"✅ GameConfigs count: {response.gameConfigs?.Count ?? 0}");
            
            if (response.gameConfigs != null)
            {
                foreach (var config in response.gameConfigs)
                {
                    Debug.Log($"   - {config.Key}: enabled={config.Value.enabled}");
                }
            }
        }
        else
        {
            Debug.LogError("❌ Attempted to set null response!");
        }

        // Notify listeners that session data is ready
        OnResponseReady?.Invoke();
        Debug.Log("OnResponseReady event invoked");
    }

    // ✅ Get games list quickly
    public List<GameData> GetGames()
    {
        if (loadedResponse != null && loadedResponse.games != null)
            return loadedResponse.games;
        return new List<GameData>();
    }

    // ✅ Get gameConfigs dictionary quickly
    public Dictionary<string, GameConfig> GetGameConfigs()
    {
        if (loadedResponse != null && loadedResponse.gameConfigs != null)
            return loadedResponse.gameConfigs;
        return new Dictionary<string, GameConfig>();
    }

    // ✅ Get specific game config
    public GameConfig GetGameConfig(string gameName)
    {
        var configs = GetGameConfigs();
        if (configs.ContainsKey(gameName))
            return configs[gameName];
        return null;
    }

    // ✅ Check if a game is enabled
    public bool IsGameEnabled(string gameName)
    {
        var config = GetGameConfig(gameName);
        return config != null && config.enabled;
    }

    // ✅ Get user data
    public UserData1 GetUser()
    {
        if (loadedResponse != null && loadedResponse.user != null)
            return loadedResponse.user;
        return null;
    }

    // ✅ Check if session is ready
    public bool IsSessionReady()
    {
        return loadedResponse != null && 
               loadedResponse.success && 
               loadedResponse.gameConfigs != null;
    }

    // ✅ Clear session data (useful for logout)
    public void ClearSession()
    {
        Debug.Log("Clearing session data...");
        sessionId = null;
        loadedResponse = null;
        selectedGameName = null;
    }

    // Debug method to print session info
    [ContextMenu("Debug Session Info")]
    public void DebugSessionInfo()
    {
        Debug.Log("=== SESSION DEBUG INFO ===");
        Debug.Log($"Session ID: {sessionId}");
        Debug.Log($"Is Ready: {IsSessionReady()}");
        
        if (loadedResponse != null)
        {
            Debug.Log($"Success: {loadedResponse.success}");
            Debug.Log($"User: {loadedResponse.user?.name} ({loadedResponse.user?.userCode})");
            Debug.Log($"Games Count: {loadedResponse.games?.Count ?? 0}");
            Debug.Log($"Game Configs Count: {loadedResponse.gameConfigs?.Count ?? 0}");
            
            if (loadedResponse.gameConfigs != null)
            {
                foreach (var kvp in loadedResponse.gameConfigs)
                {
                    Debug.Log($"  {kvp.Key}: enabled={kvp.Value.enabled}, difficulty={kvp.Value.difficulty}");
                }
            }
        }
        else
        {
            Debug.Log("loadedResponse is null");
        }
        Debug.Log("=== END SESSION DEBUG ===");
    }
}
