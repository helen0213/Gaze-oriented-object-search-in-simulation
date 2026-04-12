using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using ithappy.Animals_FREE;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(CreatureMover))]
public class ActionDrivenCreatureController : MonoBehaviour
{
    [Serializable]
    private class ActionMessage
    {
        public string type;
        public string action;
        public string step;
        public string robot_state;
        public string human_state;
        public float max_belief_prob;
        public string max_belief_state;
    }
    [Header("Targets")]
    public Transform creatureRoot;
    public Transform cameraRig;
    public string mappingFilePath = "/Users/liusimin/Desktop/H2R/Gaze-POMDP/runtime_data/scene_object.json";

    [Header("Movement")]
    public float stopDistance = 1.2f;
    public float slowDownDistance = 2.5f;
    public bool runToTarget = false;
    public bool faceTargetWhileIdle = true;

    [Header("Debug")]
    public bool debug;

    private CreatureMover mover;
    private readonly Dictionary<string, string> objKeyToName = new Dictionary<string, string>();
    private readonly Dictionary<string, Transform> nameToTransform = new Dictionary<string, Transform>();

    private Transform currentLookTarget;
    private Transform currentMoveTarget;
    private Transform lastObservedTarget;

    private void Awake()
    {
        SimulatorStartMenu.EnsureCreated();
        mover = GetComponent<CreatureMover>();

        if (creatureRoot == null)
        {
            creatureRoot = transform;
        }

        if (cameraRig == null)
        {
            GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
            if (rig != null)
            {
                cameraRig = rig.transform;
            }
        }

        GazeDrivenCreatureController gaze = GetComponent<GazeDrivenCreatureController>();
        if (gaze != null)
        {
            gaze.enabled = false;
        }

        LoadMapping();
    }

    private void OnEnable()
    {
        PythonSender.RawMessageReceived += OnRawMessageReceived;
    }

    private void OnDisable()
    {
        PythonSender.RawMessageReceived -= OnRawMessageReceived;
    }

    private void Update()
    {
        if (!SimulatorStartMenu.HasStarted())
        {
            mover.SetInput(Vector2.zero, LookForwardPoint(), false, false);
            return;
        }

        if (currentMoveTarget != null)
        {
            Vector3 destination = GetTargetPoint(currentMoveTarget);
            Vector3 flatOffset = destination - creatureRoot.position;
            flatOffset.y = 0f;

            if (flatOffset.sqrMagnitude <= stopDistance * stopDistance)
            {
                mover.SetInput(Vector2.zero, faceTargetWhileIdle ? destination : LookForwardPoint(), false, false);
                currentMoveTarget = null;
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
            return;
        }

        if (currentLookTarget != null)
        {
            Vector3 destination = GetTargetPoint(currentLookTarget);
            mover.SetInput(Vector2.zero, destination, false, false);
            return;
        }

        mover.SetInput(Vector2.zero, LookForwardPoint(), false, false);
    }

    private Vector3 LookForwardPoint()
    {
        return creatureRoot.position + creatureRoot.forward * 2f;
    }

    private void OnRawMessageReceived(string message)
    {
        ActionMessage actionMsg = null;
        try
        {
            actionMsg = JsonUtility.FromJson<ActionMessage>(message);
        }
        catch (Exception e)
        {
            if (debug)
            {
                Debug.LogWarning("Failed to parse action message: " + e.Message);
            }
            return;
        }

        if (actionMsg == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(actionMsg.robot_state))
        {
            ApplyRobotState(actionMsg.robot_state);
        }

        string action = actionMsg.action;
        if (string.IsNullOrWhiteSpace(action)) return;

        string normalized = action.Trim().ToLowerInvariant();

        if (normalized == "robot_look_at_human")
        {
            currentMoveTarget = null;
            currentLookTarget = cameraRig;
            return;
        }

        if (normalized.StartsWith("robot_look_at_obj"))
        {
            string objKey = ExtractObjectKey(normalized);
            Transform target = ResolveObject(objKey);
            if (target != null)
            {
                currentMoveTarget = null;
                currentLookTarget = target;
                lastObservedTarget = target;
            }
            return;
        }

        if (normalized == "robot_pick_up")
        {
            if (lastObservedTarget != null)
            {
                currentMoveTarget = lastObservedTarget;
                currentLookTarget = lastObservedTarget;
            }
            return;
        }
    }

    private void ApplyRobotState(string robotState)
    {
        if (string.IsNullOrWhiteSpace(robotState)) return;

        string normalized = robotState.Trim().ToLowerInvariant();
        if (normalized.StartsWith("robot_observes_obj"))
        {
            string objKey = ExtractObjectKey(normalized);
            Transform target = ResolveObject(objKey);
            if (target != null)
            {
                currentMoveTarget = null;
                currentLookTarget = target;
                lastObservedTarget = target;
            }
        }
    }

    private string ExtractObjectKey(string normalizedAction)
    {
        Match match = Regex.Match(normalizedAction, @"(obj\d+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private Transform ResolveObject(string objKey)
    {
        if (string.IsNullOrEmpty(objKey)) return null;

        if (!objKeyToName.TryGetValue(objKey, out string objectName))
        {
            return null;
        }

        if (nameToTransform.TryGetValue(objectName, out Transform cached) && cached != null)
        {
            return cached;
        }

        GameObject go = GameObject.Find(objectName);
        if (go == null) return null;

        nameToTransform[objectName] = go.transform;
        return go.transform;
    }

    private Vector3 GetTargetPoint(Transform target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        Vector3 origin = creatureRoot.position;

        if (targetCollider != null)
        {
            Vector3 point = targetCollider.bounds.center;
            point.y = origin.y;
            return targetCollider.ClosestPoint(point);
        }

        Vector3 fallback = target.position;
        fallback.y = origin.y;
        return fallback;
    }

    private void LoadMapping()
    {
        objKeyToName.Clear();

        if (string.IsNullOrWhiteSpace(mappingFilePath))
        {
            return;
        }

        if (!File.Exists(mappingFilePath))
        {
            Debug.LogWarning("Mapping file not found: " + mappingFilePath);
            return;
        }

        string json = File.ReadAllText(mappingFilePath);
        Regex regex = new Regex("\\\"(obj\\\\d+)\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
        MatchCollection matches = regex.Matches(json);
        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string name = match.Groups[2].Value;
            objKeyToName[key] = name;
        }

        if (debug)
        {
            Debug.Log("Loaded object mapping: " + objKeyToName.Count + " entries");
        }
    }
}
