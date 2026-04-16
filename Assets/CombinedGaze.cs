using UnityEngine;

public class CombinedGaze : MonoBehaviour
{
    public OVREyeGaze leftEye;
    public OVREyeGaze rightEye;
    public Transform fallbackHead;
    public float confidenceThreshold = 0.5f;
    public bool debug = true;

    public Ray CombinedRay { get; private set; }
    public bool UsingEyeTracking { get; private set; }
    public bool EyeTrackingPermissionGranted { get; private set; }
    public bool EyeTrackingSupported { get; private set; }

    private OVRPlugin.EyeGazesState eyeGazesState;

    void Awake()
    {
        CombinedRay = BuildFallbackRay();
    }

    void Update()
    {
        EyeTrackingSupported = OVRPlugin.eyeTrackingSupported;
        EyeTrackingPermissionGranted =
            OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking);

        if (!EyeTrackingSupported || !EyeTrackingPermissionGranted || Camera.main == null)
        {
            UseFallbackRay();
            return;
        }

        if (!OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState))
        {
            UseFallbackRay();
            return;
        }

        bool leftValid = TryGetEyePose(OVRPlugin.Eye.Left, out Vector3 leftOrigin, out Vector3 leftDirection);
        bool rightValid = TryGetEyePose(OVRPlugin.Eye.Right, out Vector3 rightOrigin, out Vector3 rightDirection);

        if (!leftValid && !rightValid)
        {
            UseFallbackRay();
            return;
        }

        UsingEyeTracking = true;

        Vector3 origin;
        Vector3 direction;

        if (leftValid && rightValid)
        {
            origin = (leftOrigin + rightOrigin) * 0.5f;
            direction = (leftDirection + rightDirection).normalized;
        }
        else if (leftValid)
        {
            origin = leftOrigin;
            direction = leftDirection;
        }
        else
        {
            origin = rightOrigin;
            direction = rightDirection;
        }

        CombinedRay = new Ray(origin, direction);

        if (debug)
        {
            Debug.DrawRay(
                CombinedRay.origin,
                CombinedRay.direction * 5f,
                Color.green
            );
        }
    }

    private bool TryGetEyePose(OVRPlugin.Eye eye, out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.zero;
        direction = Vector3.forward;

        OVRPlugin.EyeGazeState eyeGaze = eyeGazesState.EyeGazes[(int)eye];
        if (!eyeGaze.IsValid || eyeGaze.Confidence < confidenceThreshold)
        {
            return false;
        }

        OVRPose pose = eyeGaze.Pose.ToOVRPose().ToWorldSpacePose(Camera.main);
        direction = pose.orientation * Vector3.forward;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        origin = pose.position;
        direction.Normalize();
        return true;
    }

    private void UseFallbackRay()
    {
        UsingEyeTracking = false;
        CombinedRay = BuildFallbackRay();

        if (debug)
        {
            Debug.DrawRay(
                CombinedRay.origin,
                CombinedRay.direction * 5f,
                Color.yellow
            );
        }
    }

    private Ray BuildFallbackRay()
    {
        if (fallbackHead != null)
        {
            return new Ray(fallbackHead.position, fallbackHead.forward);
        }

        Transform source = Camera.main != null ? Camera.main.transform : transform;
        return new Ray(source.position, source.forward);
    }
}
