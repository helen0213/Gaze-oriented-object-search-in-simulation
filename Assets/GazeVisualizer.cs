using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GazeVisualizer : MonoBehaviour
{
    public CombinedGaze combinedGaze;
    public float maxDistance = 10f;
    public float lineStartOffset = 0.05f;
    public Color lineColor = new Color(0.85f, 0.85f, 0.85f, 1f);

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
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (line.material != null)
                line.material.color = lineColor;
        }
    }

    void Update()
    {
        if (line == null)
            return;

        if (SimulationMenuBlocker.IsBlockingScene())
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;

        if (combinedGaze == null)
            return;

        Ray ray = combinedGaze.CombinedRay;
        Vector3 direction = ray.direction.sqrMagnitude > 0.0001f
            ? ray.direction.normalized
            : transform.forward;
        Vector3 start = ray.origin + direction * Mathf.Max(0f, lineStartOffset);
        Vector3 end = start + direction * maxDistance;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startColor = lineColor;
        line.endColor = lineColor;
        if (line.material != null)
            line.material.color = lineColor;
    }
}
