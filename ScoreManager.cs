using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;
using TMPro;   // ✅ Import TextMeshPro namespace

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public TMP_Text scoreText;   // ✅ assign a TMP_Text from Canvas
    private int score = 0;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        UpdateUI();
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + score;
        }
    }

    public int GetScore()
    {
        return score;
    }
}
