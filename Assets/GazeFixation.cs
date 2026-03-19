using UnityEngine;

public class GazeFixation : MonoBehaviour
{
    public GazeTargetDetector detector;

    public float fixationTime = 0.4f;

    public bool debug = true;

    public GameObject ConfirmedTarget { get; private set; }

    private GameObject lastTarget;
    private float timer = 0f;
    private bool hasConfirmedCurrent = false;
    private GameObject lastLoggedFixation;

    void Update()
    {
        if (detector == null)
            return;

        GameObject current = detector.CurrentTarget;

        if (current == null)
        {
            ResetFixation();
            return;
        }

        if (current == lastTarget)
        {
            if (!hasConfirmedCurrent)
            {
                timer += Time.deltaTime;

                if (timer >= fixationTime)
                {
                    ConfirmedTarget = current;
                    hasConfirmedCurrent = true;

                    if (debug && lastLoggedFixation != current)
                    {
                        Debug.Log("🎯 FIXATED: " + current.name);
                        lastLoggedFixation = current;
                    }
                }
            }
        }
        else
        {
            lastTarget = current;
            timer = 0f;
            hasConfirmedCurrent = false;
            ConfirmedTarget = null;
        }
    }

    void ResetFixation()
    {
        lastTarget = null;
        timer = 0f;
        hasConfirmedCurrent = false;
        ConfirmedTarget = null;
        lastLoggedFixation = null;
    }
}