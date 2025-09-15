using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Required for TMP_InputField

public class SceneChanger : MonoBehaviour
{
    [Header("UI References")]
    public Button myButton;             // Assign your button in Inspector
    public TMP_InputField sessionInput; // Assign your TMP InputField in Inspector

    [Header("Scene Settings")]
    public string sceneName;            // Enter the scene name in Inspector
    public string gameName;             // Optional: game name to store in SessionManager

    void Start()
    {
        if (myButton == null)
        {
            Debug.LogError("Button not assigned in Inspector!");
            return;
        }

        // Add listener to the button
        myButton.onClick.AddListener(OnButtonClicked);
    }

    void OnButtonClicked()
    {
        // Read value from TMP_InputField
        string enteredSessionId = sessionInput != null ? sessionInput.text : null;

        if (string.IsNullOrEmpty(enteredSessionId))
        {
            Debug.LogWarning("No session ID entered!");
        }
        else
        {
            // Store session ID in SessionManager
            if (SessionManager.Instance != null)
            {
                SessionManager.Instance.sessionId = enteredSessionId;
                Debug.Log($"Session ID set to: {enteredSessionId}");
            }
            else
            {
                Debug.LogError("SessionManager instance not found!");
            }
        }

        // Optionally, store a selected game name too
        if (SessionManager.Instance != null)
        {
            SessionManager.Instance.selectedGameName = gameName;
        }

        // Change the scene
        SceneManager.LoadScene(sceneName);
    }
}
