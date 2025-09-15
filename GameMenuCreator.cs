using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using TMPro;

[System.Serializable]
public class GameSlot
{
    public string gameName;   // Name of the game (matches API)
    public RawImage icon;     // UI slot (parent), button is child
}

public class GameMenuCreator : MonoBehaviour
{
    [Header("Game Slots")]
    public List<GameSlot> gameSlots;

    [Header("Colors")]
    public Color enabledColor = Color.white;
    public Color disabledColor = Color.grey;

    void Start()
    {
        // Try to setup immediately if session is already ready
        if (SessionManager.Instance != null && SessionManager.Instance.loadedResponse != null)
        {
            SetupMenu();
        }
        else
        {
            // Wait a frame and try again, or wait for the event
            StartCoroutine(WaitForSessionData());
        }
    }

    void OnEnable()
    {
        // Subscribe to session ready event
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnResponseReady += SetupMenu;
    }

    void OnDisable()
    {
        if (SessionManager.Instance != null)
            SessionManager.Instance.OnResponseReady -= SetupMenu;
    }

    IEnumerator WaitForSessionData()
    {
        // Wait for session manager and data to be ready
        float timeout = 5f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (SessionManager.Instance != null && 
                SessionManager.Instance.loadedResponse != null && 
                SessionManager.Instance.loadedResponse.gameConfigs != null)
            {
                SetupMenu();
                yield break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        Debug.LogWarning("⚠️ Timeout waiting for session data. Setting up with default disabled state.");
        SetupMenuWithDefaults();
    }

    void SetupMenu()
    {
        var session = SessionManager.Instance;
        if (session == null || session.loadedResponse == null || session.loadedResponse.gameConfigs == null)
        {
            Debug.LogError("❌ No session or gameConfigs found!");
            SetupMenuWithDefaults();
            return;
        }

        Debug.Log("✅ Session and gameConfigs found. Setting up menu...");

        Dictionary<string, GameConfig> gameConfigs = session.loadedResponse.gameConfigs;

        foreach (var slot in gameSlots)
        {
            bool isEnabled = false;
            
            if (gameConfigs.ContainsKey(slot.gameName))
            {
                isEnabled = gameConfigs[slot.gameName].enabled;
                Debug.Log($"Game: {slot.gameName}, Enabled: {isEnabled}");
            }
            else
            {
                Debug.LogWarning($"⚠️ Game '{slot.gameName}' not found in gameConfigs!");
            }

            SetupGameSlot(slot, isEnabled);
        }
    }

    void SetupMenuWithDefaults()
    {
        Debug.Log("Setting up menu with default disabled state...");
        
        foreach (var slot in gameSlots)
        {
            SetupGameSlot(slot, false);
        }
    }

    void SetupGameSlot(GameSlot slot, bool isEnabled)
    {
        if (slot.icon == null)
        {
            Debug.LogWarning($"⚠️ Icon is null for game slot: {slot.gameName}");
            return;
        }

        // Update icon color
        slot.icon.color = isEnabled ? enabledColor : disabledColor;

        // Enable/disable child Button
        Button childButton = slot.icon.GetComponentInChildren<Button>();
        if (childButton != null)
        {
            childButton.interactable = isEnabled;

            // Assign OnClick only if enabled
            childButton.onClick.RemoveAllListeners();
            if (isEnabled)
            {
                string gameName = slot.gameName; // capture for closure
                childButton.onClick.AddListener(() => OnGameSelected(gameName));
            }
        }
        else
        {
            Debug.LogWarning($"⚠️ No Button component found in children of {slot.gameName} icon!");
        }

        // Enable/disable child TMP_InputField
        TMP_InputField childInput = slot.icon.GetComponentInChildren<TMP_InputField>();
        if (childInput != null)
        {
            childInput.interactable = isEnabled;
        }
        else
        {
            Debug.LogWarning($"⚠️ No TMP_InputField component found in children of {slot.gameName} icon!");
        }
    }

    void OnGameSelected(string gameName)
    {
        Debug.Log("▶ Selected Game: " + gameName);
    }

    // Public method for manual refresh (useful for testing)
    [ContextMenu("Refresh Menu")]
    public void RefreshMenu()
    {
        SetupMenu();
    }
}
