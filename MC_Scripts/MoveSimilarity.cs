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

    [Header("Settings")]
    public float maxDistance = 1.5f;   // Recommended 1.0 – 2.0

    // Current similarity value (0-1)
    private float currentSimilarity = 0f;

    void Update()
    {
        currentSimilarity = CalculateSimilarity();

        if (similarityText != null)
        {
            int percent = Mathf.RoundToInt(currentSimilarity * 100f);
            similarityText.text = "Similarity: " + percent + "%";
        }
    }

    /// <summary>
    /// Returns similarity value between 0 and 1 (1 = identical pose)
    /// </summary>
    public float GetSimilarity()
    {
        return currentSimilarity;
    }

    /// <summary>
    /// Calculate similarity based on body part positions
    /// </summary>
    private float CalculateSimilarity()
    {
        float scoreHip = ComparePoints(hipA, hipB);
        float scoreLH = ComparePoints(leftHandA, leftHandB);
        float scoreRH = ComparePoints(rightHandA, rightHandB);
        float scoreLL = ComparePoints(leftLegA, leftLegB);
        float scoreRL = ComparePoints(rightLegA, rightLegB);

        // Weighted average (can adjust weights based on importance)
        float finalScore = (
            scoreHip * 0.25f +    // Hip is foundation
            scoreLH * 0.2f +      // Hands
            scoreRH * 0.2f +
            scoreLL * 0.175f +    // Legs
            scoreRL * 0.175f
        );

        return finalScore;
    }

    /// <summary>
    /// Compare relative positions of two body parts
    /// </summary>
    private float ComparePoints(Transform a, Transform b)
    {
        if (a == null || b == null || hipA == null || hipB == null)
            return 0f;

        // Use relative positioning (normalized by hip position)
        Vector3 relA = a.position - hipA.position;
        Vector3 relB = b.position - hipB.position;

        float dist = Vector3.Distance(relA, relB);

        // Convert distance to similarity score (0-1)
        float similarity = 1f - Mathf.Clamp01(dist / maxDistance);
        
        return similarity;
    }

    /// <summary>
    /// Get detailed breakdown of similarity per body part
    /// </summary>
    public void GetSimilarityBreakdown(out float hip, out float leftHand, out float rightHand, 
                                       out float leftLeg, out float rightLeg)
    {
        hip = ComparePoints(hipA, hipB);
        leftHand = ComparePoints(leftHandA, leftHandB);
        rightHand = ComparePoints(rightHandA, rightHandB);
        leftLeg = ComparePoints(leftLegA, leftLegB);
        rightLeg = ComparePoints(rightLegA, rightLegB);
    }

    /// <summary>
    /// Check if all required transforms are assigned
    /// </summary>
    public bool IsValid()
    {
        return hipA != null && hipB != null &&
               leftHandA != null && leftHandB != null &&
               rightHandA != null && rightHandB != null &&
               leftLegA != null && leftLegB != null &&
               rightLegA != null && rightLegB != null;
    }

    void OnValidate()
    {
        if (!IsValid())
        {
            Debug.LogWarning("MoveSimilarity: Not all body part transforms are assigned!");
        }
    }
}
