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
    private bool milestonePlayed = false;

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

        if (!milestonePlayed && score >= SessionManager.Instance.GetGameConfig(SessionManager.Instance.selectedGameName).target_score && milestoneSound != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(milestoneSound);
            milestonePlayed = true; // ✅ ensures it plays only once
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
