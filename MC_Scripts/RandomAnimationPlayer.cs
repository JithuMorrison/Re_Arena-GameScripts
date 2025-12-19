using UnityEngine;

public class RandomAnimationPlayer : MonoBehaviour
{
    [Header("Animator of the Avatar")]
    public Animator anim;

    [Header("Animator Bool Parameters to Trigger Randomly")]
    public string[] boolNames;

    [Header("Gap Between Actions (seconds)")]
    public float gapBetweenActions = 5f;

    bool isRunning = false;
    bool isFrozen = false;

    void Start()
    {
        if (anim == null)
            anim = GetComponent<Animator>();

        StartCoroutine(LoopActions());
    }

    System.Collections.IEnumerator LoopActions()
    {
        while (true)
        {
            yield return TriggerRandomBool();
            yield return new WaitForSeconds(gapBetweenActions);
        }
    }

    System.Collections.IEnumerator TriggerRandomBool()
    {
        if (boolNames.Length == 0)
            yield break;

        int r = Random.Range(0, boolNames.Length);
        string selectedBool = boolNames[r];

        anim.SetBool(selectedBool, true);
        Debug.Log("Set TRUE: " + selectedBool);

        float timer = 0f;
        float duration = 1f;  // animation active time

        // Instead of WaitForSeconds, we manually count time
        while (timer < duration)
        {
            if (!isFrozen)
                timer += Time.deltaTime; // only count when not frozen

            yield return null;
        }

        anim.SetBool(selectedBool, false);
        Debug.Log("Set FALSE: " + selectedBool);
    }

    // ------------------------
    // ⭐ FREEZE & UNFREEZE
    // ------------------------

    public void FreezeAnimation()
    {
        anim.speed = 0f;
        isFrozen = true;
        Debug.Log("ANIMATION FROZEN");
    }

    public void UnfreezeAnimation()
    {
        anim.speed = 1f;
        isFrozen = false;
        Debug.Log("ANIMATION UNFROZEN");
    }
}
