using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;
using TMPro;   // ✅ Import TextMeshPro namespace

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Audio")]
    public AudioClip milestoneSound;   // 🎵 assign in Inspector
    private AudioSource audioSource;

    public TMP_Text scoreText;   // ✅ assign a TMP_Text from Canvas
    private int score = 0;
    private int prevpop = 0;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        UpdateUI();
    }

    public void AddScore(int amount)
    {
        score += amount;
        UpdateUI();

        if (score == 15 && milestoneSound != null)
        {
            audioSource.Stop();  // ⛔ stop any playing sound
            audioSource.PlayOneShot(milestoneSound); // ▶️ play milestone sound once
        }
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

    public int GetPrevPop()
    {
        return prevpop;
    }

    public void SetPrevPop(int val)
    {
        prevpop = val;
    }
}
