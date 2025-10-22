using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class ResultDisplay : MonoBehaviour
{
    [Header("Image References")]
    public Image resultImage;
    public Sprite startSprite; // First image (neutral/start image)
    public Sprite winSprite;
    public Sprite loseSprite;

    [Header("Scene Settings")]
    public string nextSceneName = "NextScene";
    public float firstDelay = 3f;   // Show start image duration
    public float secondDelay = 2f;  // Show win/lose duration before loading

    public GameAnalyticsManager gameAnalyticsManager;

    void Start()
    {
        if (resultImage != null)
            resultImage.gameObject.SetActive(false);
    }

    public void ShowResult(string result)
    {
        if (resultImage == null) return;
        StartCoroutine(DisplaySequence(result));
    }

    private IEnumerator DisplaySequence(string result)
    {
        resultImage.gameObject.SetActive(true);

        // Step 1: Show neutral/start image
        resultImage.sprite = startSprite;
        yield return new WaitForSeconds(firstDelay);

        // Step 2: Show win or lose sprite
        if (result.ToLower() == "win"){
            resultImage.sprite = winSprite;
            gameAnalyticsManager.OnGameEnd("win");
        }
        else if (result.ToLower() == "lose"){
            resultImage.sprite = loseSprite;
            gameAnalyticsManager.OnGameEnd("lose");
        }
        else{
            Debug.LogWarning("Invalid result parameter: use 'win' or 'lose'.");
        }

        yield return new WaitForSeconds(secondDelay);

        NuitrackManager nuitrackMgr = FindObjectOfType<NuitrackManager>();
        if (nuitrackMgr != null)
        {
            // Stop Nuitrack safely
            nuitrackMgr.StopNuitrack();
        }

        // Step 3: Load next scene
        SceneManager.LoadScene(nextSceneName);
    }
}
