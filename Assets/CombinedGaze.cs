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

    void Update()
    {
        bool leftValid = leftEye != null &&
                         leftEye.Confidence >= confidenceThreshold &&
                         leftEye.transform.forward.sqrMagnitude > 0.0001f;

        bool rightValid = rightEye != null &&
                          rightEye.Confidence >= confidenceThreshold &&
                          rightEye.transform.forward.sqrMagnitude > 0.0001f;

        if (!leftValid && !rightValid)
        {
            UsingEyeTracking = false;

            if (fallbackHead != null)
            {
                CombinedRay = new Ray(fallbackHead.position, fallbackHead.forward);
            }

            if (debug)
            {
                Debug.DrawRay(
                    CombinedRay.origin,
                    CombinedRay.direction * 5f,
                    Color.yellow
                );
            }

            return;
        }

        UsingEyeTracking = true;

        Vector3 origin;
        Vector3 direction;

        if (leftValid && rightValid)
        {
            origin = (leftEye.transform.position + rightEye.transform.position) * 0.5f;
            direction = (leftEye.transform.forward + rightEye.transform.forward).normalized;
        }
        else if (leftValid)
        {
            origin = leftEye.transform.position;
            direction = leftEye.transform.forward.normalized;
        }
        else
        {
            origin = rightEye.transform.position;
            direction = rightEye.transform.forward.normalized;
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
}