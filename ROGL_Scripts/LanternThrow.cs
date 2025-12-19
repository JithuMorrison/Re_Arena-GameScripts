using UnityEngine;

public class LanternThrow : MonoBehaviour
{
    Rigidbody rb;

    bool attached = false;
    Transform attachTarget;
    bool scored = false;

    // Player body parts
    public Transform leftHand;
    public Transform rightHand;
    public Transform leftLeg;
    public Transform rightLeg;

    // Light controller reference
    public LightController lightController;

    public static int activeLanterns = 0;

    // NEW: reference to spawner + spawn index
    public LanternSpawner spawner;
    public int spawnIndex = -1;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        activeLanterns++; 
    }

    void OnDestroy()
    {
        activeLanterns--;

        // Free spawn point on destruction
        if (spawner != null && spawnIndex != -1)
        {
            spawner.FreeSpawnPoint(spawnIndex);
        }
    }

    void OnCollisionEnter(Collision c)
    {
        Transform hit = c.transform;

        if (!attached)
        {
            if (hit.IsChildOf(leftHand))       attachTarget = leftHand;
            else if (hit.IsChildOf(rightHand)) attachTarget = rightHand;
            else if (hit.IsChildOf(leftLeg))   attachTarget = leftLeg;
            else if (hit.IsChildOf(rightLeg))  attachTarget = rightLeg;

            if (attachTarget != null)
                Attach();
        }

        if (!scored && attached && c.collider.CompareTag("ScorePlane"))
        {
            ScoreAccordingToLight();
        }
    }

    void Attach()
    {
        attached = true;
        transform.SetParent(attachTarget);
        transform.localPosition = Vector3.zero;
    }

    void ScoreAccordingToLight()
    {
        int index = -1;

        if (attachTarget == leftHand) index = 0;
        else if (attachTarget == leftLeg) index = 1;
        else if (attachTarget == rightLeg) index = 2;
        else if (attachTarget == rightHand) index = 3;

        if (index == -1) return;

        var light = lightController.lights[index];

        int scoreDelta = 0;

        switch (light.state)
        {
            case LightController.LightState.Green:  scoreDelta = -1; break;
            case LightController.LightState.Red:    scoreDelta = 1; break;
            case LightController.LightState.Orange: scoreDelta = 0; break;
        }

        ROGLScoreManager.instance.AddScore(scoreDelta);

        scored = true;
        Destroy(gameObject);
    }
}
