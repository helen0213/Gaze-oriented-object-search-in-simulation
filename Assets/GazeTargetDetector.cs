using UnityEngine;

public class GazeTargetDetector : MonoBehaviour
{
    public CombinedGaze combinedGaze;

    public LayerMask targetLayer;
    public float maxDistance = 20f;

    public bool enableHighlight = true;
    public Color highlightColor = Color.red;

    public bool debug = true;

    public GameObject CurrentTarget { get; private set; }
    public RaycastHit CurrentHit { get; private set; }

    private GameObject lastLoggedTarget;
    private Renderer lastHighlightedRenderer;
    private Color lastOriginalColor;

    void Update()
    {
        if (combinedGaze == null)
            return;

        Ray ray = combinedGaze.CombinedRay;

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, targetLayer))
        {
            GameObject targetRoot = ResolveTargetRoot(hit.collider.gameObject);

            if (targetRoot != null)
            {
                CurrentHit = hit;
                CurrentTarget = targetRoot;

                if (debug && lastLoggedTarget != targetRoot)
                {
                    Debug.Log("👁 Looking at: " + targetRoot.name);
                    lastLoggedTarget = targetRoot;
                }

                if (enableHighlight)
                {
                    Highlight(targetRoot);
                }

                return;
            }
        }

        if (debug && lastLoggedTarget != null)
        {
            Debug.Log("👁 Looking at: NONE");
            lastLoggedTarget = null;
        }

        ClearHighlight();
        CurrentTarget = null;
    }

    GameObject ResolveTargetRoot(GameObject hitObject)
    {
        Transform t = hitObject.transform;
        Transform lastOnTargetLayer = null;

        while (t != null)
        {
            if (((1 << t.gameObject.layer) & targetLayer.value) != 0)
            {
                lastOnTargetLayer = t;
            }

            t = t.parent;
        }

        return lastOnTargetLayer != null ? lastOnTargetLayer.gameObject : null;
    }

    void Highlight(GameObject target)
    {
        Renderer r = target.GetComponentInChildren<Renderer>();
        if (r == null)
            return;

        if (lastHighlightedRenderer == r)
            return;

        ClearHighlight();

        lastHighlightedRenderer = r;
        lastOriginalColor = r.material.color;
        r.material.color = highlightColor;
    }

    void ClearHighlight()
    {
        if (lastHighlightedRenderer != null)
        {
            lastHighlightedRenderer.material.color = lastOriginalColor;
            lastHighlightedRenderer = null;
        }
    }
}