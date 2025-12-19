using UnityEngine;
using System.Collections;

public class LightController : MonoBehaviour
{
    public enum LightState { Red, Orange, Green }

    [System.Serializable]
    public class LightInfo
    {
        public string lightName;
        public Renderer renderer;
        public LightState state;
    }

    public LightInfo[] lights;
    public float minTime = 1f;
    public float maxTime = 3f;

    void Start()
    {
        foreach (var l in lights) StartCoroutine(ChangeRoutine(l));
    }

    IEnumerator ChangeRoutine(LightInfo li)
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minTime, maxTime));
            li.state = (LightState)Random.Range(0, 3);
            UpdateColor(li);
        }
    }

    void UpdateColor(LightInfo li)
    {
        switch (li.state)
        {
            case LightState.Red: li.renderer.material.color = Color.red; break;
            case LightState.Orange: li.renderer.material.color = new Color(1, .5f, 0); break;
            case LightState.Green: li.renderer.material.color = Color.green; break;
        }
    }

    // Used by other scripts
    public bool AnyGreen()
    {
        foreach (var l in lights)
            if (l.state == LightState.Green) return true;
        return false;
    }
}
