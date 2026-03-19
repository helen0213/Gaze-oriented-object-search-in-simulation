using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GazeVisualizer : MonoBehaviour
{
    public CombinedGaze combinedGaze;
    public float maxDistance = 10f;

    private LineRenderer line;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    void Start()
    {
        if (line != null)
        {
            line.positionCount = 2;
            line.startWidth = 0.01f;
            line.endWidth = 0.01f;
            line.useWorldSpace = true;
        }
    }

    void Update()
    {
        if (combinedGaze == null || line == null)
            return;

        Ray ray = combinedGaze.CombinedRay;

        line.SetPosition(0, ray.origin);
        line.SetPosition(1, ray.origin + ray.direction * maxDistance);
    }
}