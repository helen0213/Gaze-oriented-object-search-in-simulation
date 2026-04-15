using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class OVRMenuRayDriver : MonoBehaviour
{
    private const OVRInput.Button ClickButton = OVRInput.Button.SecondaryIndexTrigger;
    private const float DefaultRayLength = 3.5f;
    private static readonly string[] RightControllerNames =
    {
        "RightControllerInHandAnchor",
        "RightControllerAnchor",
        "RightHandOnControllerAnchor",
        "RightHandAnchor"
    };

    private static readonly string[] LeftControllerNames =
    {
        "LeftControllerInHandAnchor",
        "LeftControllerAnchor",
        "LeftHandOnControllerAnchor",
        "LeftHandAnchor"
    };

    private OVRInputModule ovrInputModule;
    private InputSystemUIInputModule inputSystemModule;
    private bool xrMenuInputEnabled;
    private Transform visualTarget;
    private LineRenderer lineRenderer;
    private Transform rayEndVisual;
    private bool hasSeenControllerActivity;

    public void Configure(EventSystem eventSystem)
    {
        if (eventSystem == null)
        {
            return;
        }

        ovrInputModule = eventSystem.GetComponent<OVRInputModule>();
        if (ovrInputModule == null)
        {
            ovrInputModule = eventSystem.gameObject.AddComponent<OVRInputModule>();
        }

        inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        ovrInputModule.joyPadClickButton = ClickButton;
        ovrInputModule.enabled = false;

        EnsureVisuals();
        SetVisualsActive(false);
    }

    public void SetXRMenuInputEnabled(bool enabled)
    {
        xrMenuInputEnabled = enabled;
        hasSeenControllerActivity = false;

        if (ovrInputModule == null)
        {
            return;
        }

        ovrInputModule.enabled = enabled;

        if (inputSystemModule != null)
        {
            inputSystemModule.enabled = !enabled;
        }

        SetVisualsActive(enabled);
    }

    public void SetVisualTarget(Transform target)
    {
        visualTarget = target;
    }

    private void Update()
    {
        if (!xrMenuInputEnabled || ovrInputModule == null)
        {
            return;
        }

        Transform controllerTransform = GetPreferredControllerTransform(out bool controllerIsActive);
        if (controllerIsActive)
        {
            hasSeenControllerActivity = true;
        }

        if (!hasSeenControllerActivity)
        {
            ovrInputModule.rayTransform = null;
            SetVisualsActive(false);
            return;
        }

        ovrInputModule.rayTransform = controllerTransform;
        UpdateVisuals(controllerTransform);
    }

    private static Transform GetPreferredControllerTransform(out bool controllerIsActive)
    {
        if (IsControllerActivelyPressing(OVRInput.Controller.RTouch))
        {
            controllerIsActive = true;
            Transform rightPressed = FindControllerTransform(RightControllerNames);
            if (rightPressed != null)
            {
                return rightPressed;
            }
        }

        if (IsControllerActivelyPressing(OVRInput.Controller.LTouch))
        {
            controllerIsActive = true;
            Transform leftPressed = FindControllerTransform(LeftControllerNames);
            if (leftPressed != null)
            {
                return leftPressed;
            }
        }

        controllerIsActive = false;

        Transform right = FindControllerTransform(RightControllerNames);
        if (right != null)
        {
            return right;
        }

        return FindControllerTransform(LeftControllerNames);
    }

    private static bool IsControllerActivelyPressing(OVRInput.Controller controller)
    {
        return OVRInput.Get(OVRInput.Button.One, controller)
            || OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller)
            || OVRInput.Get(OVRInput.Button.SecondaryIndexTrigger, controller)
            || OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller) > 0.5f
            || OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger, controller) > 0.5f;
    }

    private static Transform FindControllerTransform(params string[] candidateNames)
    {
        for (int i = 0; i < candidateNames.Length; i++)
        {
            GameObject candidate = GameObject.Find(candidateNames[i]);
            if (candidate != null)
            {
                return candidate.transform;
            }
        }

        return null;
    }

    private void EnsureVisuals()
    {
        if (lineRenderer != null && rayEndVisual != null)
        {
            return;
        }

        GameObject lineObject = new GameObject("OVRMenuPointerLine");
        lineObject.hideFlags = HideFlags.HideInHierarchy;
        lineObject.transform.SetParent(transform, false);

        lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.startWidth = 0.0035f;
        lineRenderer.endWidth = 0.002f;
        Material lineMaterial = CreateRuntimeMaterial(
            "Sprites/Default",
            "Universal Render Pipeline/Unlit",
            "Unlit/Color");
        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
        lineRenderer.startColor = new Color(0.4f, 0.95f, 1f, 0.95f);
        lineRenderer.endColor = new Color(1f, 1f, 1f, 0.95f);

        GameObject endObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        endObject.name = "OVRMenuPointerDot";
        endObject.hideFlags = HideFlags.HideInHierarchy;
        endObject.transform.SetParent(transform, false);
        endObject.transform.localScale = Vector3.one * 0.015f;

        Collider endCollider = endObject.GetComponent<Collider>();
        if (endCollider != null)
        {
            Destroy(endCollider);
        }

        MeshRenderer endRenderer = endObject.GetComponent<MeshRenderer>();
        Material endMaterial = CreateRuntimeMaterial(
            "Unlit/Color",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default");
        if (endMaterial != null)
        {
            endRenderer.sharedMaterial = endMaterial;
            endRenderer.sharedMaterial.color = new Color(1f, 1f, 1f, 0.95f);
        }

        rayEndVisual = endObject.transform;
    }

    private static Material CreateRuntimeMaterial(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                return new Material(shader);
            }
        }

        Debug.LogError("[OVRMenuRayDriver] Could not find any runtime shader for menu ray visuals.");
        return null;
    }

    private void SetVisualsActive(bool active)
    {
        if (lineRenderer != null)
        {
            lineRenderer.enabled = active;
        }

        if (rayEndVisual != null)
        {
            rayEndVisual.gameObject.SetActive(active);
        }
    }

    private void UpdateVisuals(Transform controllerTransform)
    {
        if (controllerTransform == null || lineRenderer == null || rayEndVisual == null)
        {
            SetVisualsActive(false);
            return;
        }

        if (!lineRenderer.enabled)
        {
            SetVisualsActive(true);
        }

        Vector3 rayOrigin = controllerTransform.position;
        Vector3 rayDirection = controllerTransform.forward;
        Vector3 rayEnd = rayOrigin + rayDirection * DefaultRayLength;

        if (visualTarget != null)
        {
            Plane canvasPlane = new Plane(-visualTarget.forward, visualTarget.position);
            Ray ray = new Ray(rayOrigin, rayDirection);
            if (canvasPlane.Raycast(ray, out float hitDistance) && hitDistance > 0f)
            {
                rayEnd = ray.GetPoint(Mathf.Min(hitDistance, DefaultRayLength));
            }
        }

        lineRenderer.SetPosition(0, rayOrigin);
        lineRenderer.SetPosition(1, rayEnd);
        rayEndVisual.position = rayEnd;
    }
}
