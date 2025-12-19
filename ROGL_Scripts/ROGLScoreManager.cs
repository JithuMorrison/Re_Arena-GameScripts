using UnityEngine;
using TMPro; // Required for TextMeshPro

public class ROGLScoreManager : MonoBehaviour
{
    public static ROGLScoreManager instance;

    int score = 0;

    // Reference to the TMP text
    [SerializeField] private TMP_Text scoreText;

    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        // Initialize the score text
        UpdateScoreText();
    }

    public void AddScore(int x)
    {
        score += x;
        Debug.Log("Score : " + score);
        UpdateScoreText();
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }
}
