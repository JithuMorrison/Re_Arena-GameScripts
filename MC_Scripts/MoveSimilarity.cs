using UnityEngine;
using TMPro;

public class MoveSimilarity : MonoBehaviour
{
    [Header("Avatar A (reference animation)")]
    public Transform hipA;
    public Transform leftHandA;
    public Transform rightHandA;
    public Transform leftLegA;
    public Transform rightLegA;

    [Header("Avatar B (player / second avatar)")]
    public Transform hipB;
    public Transform leftHandB;
    public Transform rightHandB;
    public Transform leftLegB;
    public Transform rightLegB;

    [Header("UI Display")]
    public TMP_Text similarityText;

    // --------------------
    // NEWLY ADDED
    // --------------------
    public TMP_Text scoreText;
    private int score = 0;
    private bool wasAbove75 = false;
    private bool wasBelow60 = true;
    // --------------------

    [Header("Settings")]
    public float maxDistance = 1.5f;   // Recommended 1.0 – 2.0

    void Update()
    {
        float similarity = GetSimilarity();

        if (similarityText != null)
        {
            float percent = similarity * 100f;
            similarityText.text = "Similarity:" + percent;

            // -----------------------------------------
            // NEW SCORE SYSTEM (added without touching old code)
            // -----------------------------------------

            // Crossed ABOVE 75 → increase score
            if (percent >= 80f && !wasAbove75)
            {
                score++;
                wasAbove75 = true;
                wasBelow60 = false;
            }
            else if (percent < 80f) {
                wasAbove75 = false;
            }

            // Crossed BELOW 60 → reset flag, but score doesn't decrease
            if (percent <= 70f && !wasBelow60)
            {
                score = Mathf.Max(0, score-1); // score never below 0
                wasBelow60 = true;
                wasAbove75 = false;
            }
            else if (percent > 70f) {
                wasBelow60 = false;
            }

            // Update score UI
            if (scoreText != null)
            {
                scoreText.text = "Score: " + score;
            }
        }
    }

    // Returns 0–1 (1 = identical pose)
    public float GetSimilarity()
    {
        float scoreHip = ComparePoints(hipA, hipB);
        float scoreLH = ComparePoints(leftHandA, leftHandB);
        float scoreRH = ComparePoints(rightHandA, rightHandB);
        float scoreLL = ComparePoints(leftLegA, leftLegB);
        float scoreRL = ComparePoints(rightLegA, rightLegB);

        float finalScore = (scoreHip + scoreLH + scoreRH + scoreLL + scoreRL) / 5f;

        Debug.Log("FINAL SIMILARITY: " + finalScore);

        return finalScore;
    }

    private float ComparePoints(Transform a, Transform b)
    {
        if (a == null || b == null || hipA == null || hipB == null)
            return 0f;

        // ---- RELATIVE POSITIONING FIX ----
        Vector3 relA = a.position - hipA.position;
        Vector3 relB = b.position - hipB.position;

        float dist = Vector3.Distance(relA, relB);

        // Normalize to similarity
        float similarity = 1f - Mathf.Clamp01(dist / maxDistance);
        return similarity;
    }
}
