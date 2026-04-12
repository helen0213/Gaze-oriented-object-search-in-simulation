using UnityEngine;
using ithappy.Animals_FREE;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(CreatureMover))]
public class GazeDrivenCreatureController : MonoBehaviour
{
    private static bool loggedMenuWait;

    [Header("Gaze Source")]
    public GazeFixation fixation;
    public GazeTargetDetector detector;
    public bool requireFixation = true;

    [Header("Movement")]
    public Transform creatureRoot;
    public float stopDistance = 1.2f;
    public float slowDownDistance = 2.5f;
    public bool runToTarget = false;
    public bool faceTargetWhileIdle = true;

    [Header("Debug")]
    public bool debug;

    private CreatureMover mover;

    private void Awake()
    {
        Debug.Log("[GazeDrivenCreatureController] Awake on " + gameObject.name);
        SimulatorStartMenu.EnsureCreated();
        mover = GetComponent<CreatureMover>();

        if (creatureRoot == null)
        {
            creatureRoot = transform;
        }
    }

    private void Update()
    {
        if (!SimulatorStartMenu.HasStarted())
        {
            if (!loggedMenuWait)
            {
                Debug.Log("[GazeDrivenCreatureController] Waiting for start menu confirmation.");
                loggedMenuWait = true;
            }

            Vector3 pausedLookTarget = creatureRoot.position + creatureRoot.forward * 2f;
            mover.SetInput(Vector2.zero, pausedLookTarget, false, false);
            return;
        }

        if (loggedMenuWait)
        {
            Debug.Log("[GazeDrivenCreatureController] Start menu completed. Gameplay update resumed.");
            loggedMenuWait = false;
        }

        GameObject targetObject = GetTrackedTarget();
        Vector3 lookTarget = creatureRoot.position + creatureRoot.forward * 2f;

        if (targetObject == null)
        {
            mover.SetInput(Vector2.zero, lookTarget, false, false);
            return;
        }

        Vector3 destination = GetTargetPoint(targetObject);
        Vector3 flatOffset = destination - creatureRoot.position;
        flatOffset.y = 0f;

        if (flatOffset.sqrMagnitude <= stopDistance * stopDistance)
        {
            mover.SetInput(Vector2.zero, faceTargetWhileIdle ? destination : lookTarget, false, false);
            return;
        }

        Vector3 localDirection = creatureRoot.InverseTransformDirection(flatOffset.normalized);
        Vector2 axis = new Vector2(localDirection.x, localDirection.z);
        axis = Vector2.ClampMagnitude(axis, 1f);

        bool shouldRun = runToTarget && flatOffset.magnitude > slowDownDistance;

        if (debug)
        {
            Debug.DrawLine(creatureRoot.position + Vector3.up * 0.25f, destination, Color.cyan);
        }

        mover.SetInput(axis, destination, shouldRun, false);
    }

    private GameObject GetTrackedTarget()
    {
        if (requireFixation)
        {
            return fixation != null ? fixation.ConfirmedTarget : null;
        }

        if (fixation != null && fixation.ConfirmedTarget != null)
        {
            return fixation.ConfirmedTarget;
        }

        return detector != null ? detector.CurrentTarget : null;
    }

    private Vector3 GetTargetPoint(GameObject targetObject)
    {
        Collider targetCollider = targetObject.GetComponentInChildren<Collider>();
        Vector3 origin = creatureRoot.position;

        if (targetCollider != null)
        {
            Vector3 point = targetCollider.bounds.center;
            point.y = origin.y;
            return targetCollider.ClosestPoint(point);
        }

        Vector3 fallback = targetObject.transform.position;
        fallback.y = origin.y;
        return fallback;
    }
}
