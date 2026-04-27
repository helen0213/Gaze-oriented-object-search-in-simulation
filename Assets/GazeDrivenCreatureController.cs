using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using ithappy.Animals_FREE;

[DefaultExecutionOrder(1000)]
[RequireComponent(typeof(CreatureMover))]
public class GazeDrivenCreatureController : MonoBehaviour
{
    [System.Serializable]
    private class ActionMessage
    {
        public string action;
        public string robot_state;
    }

    private static bool loggedMenuWait;
    private GameObject lastMovementLoggedTarget;
    private bool loggedNoTarget;
    private float menuResumeGraceTimer;

    [Header("Gaze Source")]
    public GazeFixation fixation;
    public GazeTargetDetector detector;
    public bool requireFixation = true;

    [Header("Movement")]
    public Transform creatureRoot;
    public float stopDistance = 1.2f;
    public float pickupArrivalPadding = 0.4f;
    public float slowDownDistance = 2.5f;
    public bool runToTarget = false;
    public bool faceTargetWhileIdle = true;

    [Header("Action Gate")]
    public bool moveOnlyOnPickUpAction = true;
    public bool faceGazeTargetBeforePickUp = true;
    public string mappingFilePath = "runtime_data/scene_object.json";

    [Header("Debug")]
    public bool debug;

    private CreatureMover mover;
    private GameObject currentMoveTarget;
    private GameObject lastPomdpSelectedTarget;
    private readonly Dictionary<string, string> objKeyToName = new Dictionary<string, string>();
    private readonly Dictionary<string, GameObject> nameToObject = new Dictionary<string, GameObject>();
    private string loadedMappingPath;
    private System.DateTime loadedMappingWriteTimeUtc;
    private float lastMoveLogTime = -999f;

    private void OnEnable()
    {
        PythonSender.RawMessageReceived += OnRawMessageReceived;
    }

    private void OnDisable()
    {
        PythonSender.RawMessageReceived -= OnRawMessageReceived;
    }

    private void Awake()
    {
        Debug.Log("[GazeDrivenCreatureController] Awake on " + gameObject.name);
        EnsureMover();
        EnsureCreatureRoot();
        LoadMapping();
    }

    private void Update()
    {
        EnsureMover();
        EnsureCreatureRoot();

        if (SimulationMenuBlocker.IsBlockingScene())
        {
            if (!loggedMenuWait)
            {
                Debug.Log("[GazeDrivenCreatureController] Waiting for menu confirmation. started=" + SimulatorStartMenu.HasStarted() + ", inactivityOpen=" + GazeInactivityMenu.IsMenuOpen());
                loggedMenuWait = true;
            }

            Vector3 pausedLookTarget = creatureRoot.position + creatureRoot.forward * 2f;
            mover.SetInput(Vector2.zero, pausedLookTarget, false, false);
            menuResumeGraceTimer = 0.5f;
            return;
        }

        if (loggedMenuWait)
        {
            Debug.Log("[GazeDrivenCreatureController] Menu completed. Gameplay update resumed. started=" + SimulatorStartMenu.HasStarted() + ", inactivityOpen=" + GazeInactivityMenu.IsMenuOpen());
            loggedMenuWait = false;
        }

        if (menuResumeGraceTimer > 0f)
        {
            menuResumeGraceTimer -= Time.deltaTime;
        }

        GameObject targetObject = GetTrackedTarget();
        Vector3 lookTarget = creatureRoot.position + creatureRoot.forward * 2f;
        if (moveOnlyOnPickUpAction && currentMoveTarget != null)
        {
            targetObject = currentMoveTarget;
        }

        if (targetObject == null)
        {
            if (!loggedNoTarget)
            {
                Debug.Log("[GazeDrivenCreatureController] No tracked target after menu.");
                loggedNoTarget = true;
                lastMovementLoggedTarget = null;
            }

            mover.SetInput(Vector2.zero, lookTarget, false, false);
            return;
        }

        if (moveOnlyOnPickUpAction)
        {
            if (currentMoveTarget == null)
            {
                if (lastMovementLoggedTarget != targetObject)
                {
                    Debug.Log("[GazeDrivenCreatureController] Saw target " + targetObject.name + "; waiting for robot_pick_up before moving.");
                    lastMovementLoggedTarget = targetObject;
                    loggedNoTarget = false;
                }

                Vector3 waitingLookTarget = lastPomdpSelectedTarget != null
                    ? GetTargetPoint(lastPomdpSelectedTarget)
                    : GetTargetPoint(targetObject);
                mover.SetInput(Vector2.zero, faceGazeTargetBeforePickUp ? waitingLookTarget : lookTarget, false, false);
                return;
            }
        }

        if (lastMovementLoggedTarget != targetObject)
        {
            Debug.Log("[GazeDrivenCreatureController] Tracking target " + targetObject.name);
            lastMovementLoggedTarget = targetObject;
            loggedNoTarget = false;
        }

        Vector3 destination = GetTargetPoint(targetObject);
        Vector3 flatOffset = destination - creatureRoot.position;
        flatOffset.y = 0f;

        float effectiveStopDistance = moveOnlyOnPickUpAction
            ? stopDistance + pickupArrivalPadding
            : stopDistance;

        if (flatOffset.sqrMagnitude <= effectiveStopDistance * effectiveStopDistance)
        {
            Debug.Log("[GazeDrivenCreatureController] Target is inside stop distance: " + flatOffset.magnitude.ToString("F2"));
            mover.SetInput(Vector2.zero, faceTargetWhileIdle ? destination : lookTarget, false, false);
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

        if (debug || Time.realtimeSinceStartup - lastMoveLogTime >= 1f)
        {
            lastMoveLogTime = Time.realtimeSinceStartup;
            Debug.Log("[GazeDrivenCreatureController] Moving toward " + targetObject.name + " with distance " + flatOffset.magnitude.ToString("F2"));
        }

        mover.SetInput(axis, destination, shouldRun, false);
    }

    private void OnRawMessageReceived(string message)
    {
        if (!moveOnlyOnPickUpAction)
            return;

        ActionMessage actionMsg = null;
        try
        {
            actionMsg = JsonUtility.FromJson<ActionMessage>(message);
        }
        catch (System.Exception e)
        {
            if (debug)
            {
                Debug.LogWarning("[GazeDrivenCreatureController] Failed to parse action message: " + e.Message);
            }
            return;
        }

        if (actionMsg == null || string.IsNullOrWhiteSpace(actionMsg.action))
        {
            if (actionMsg != null && !string.IsNullOrWhiteSpace(actionMsg.robot_state))
            {
                ApplyRobotState(actionMsg.robot_state);
            }
            return;
        }

        if (!string.IsNullOrWhiteSpace(actionMsg.robot_state))
        {
            ApplyRobotState(actionMsg.robot_state);
        }

        string action = actionMsg.action.Trim().ToLowerInvariant();
        if (action == "robot_pick_up")
        {
            currentMoveTarget = lastPomdpSelectedTarget;
            if (currentMoveTarget != null)
            {
                Debug.Log("[GazeDrivenCreatureController] robot_pick_up received; moving toward " + currentMoveTarget.name);
            }
            else
            {
                Debug.LogWarning("[GazeDrivenCreatureController] robot_pick_up received, but no POMDP obj target is resolved yet.");
            }
        }
        else if (action == "robot_look_at_human" || action.StartsWith("robot_look_at_obj"))
        {
            currentMoveTarget = null;
            if (action.StartsWith("robot_look_at_obj"))
            {
                ApplyPomdpObjectSelection(action);
            }
        }
    }

    private void ApplyRobotState(string robotState)
    {
        if (string.IsNullOrWhiteSpace(robotState))
            return;

        string normalized = robotState.Trim().ToLowerInvariant();
        if (normalized.StartsWith("robot_observes_obj"))
        {
            ApplyPomdpObjectSelection(normalized);
        }
    }

    private void ApplyPomdpObjectSelection(string text)
    {
        string objKey = ExtractObjectKey(text);
        GameObject target = ResolveObject(objKey);
        if (target == null)
            return;

        lastPomdpSelectedTarget = target;
        if (debug)
        {
            Debug.Log("[GazeDrivenCreatureController] POMDP selected " + objKey + " -> " + target.name);
        }
    }

    private string ExtractObjectKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        Match match = Regex.Match(text, @"(obj\d+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private GameObject ResolveObject(string objKey)
    {
        if (string.IsNullOrEmpty(objKey))
            return null;

        string objectName = PythonSender.GetLatestPomdpObjectName(objKey);
        if (string.IsNullOrWhiteSpace(objectName))
        {
            RefreshMappingIfChanged();
            objKeyToName.TryGetValue(objKey, out objectName);
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            if (debug)
            {
                Debug.LogWarning("[GazeDrivenCreatureController] No object mapping for " + objKey + ".");
            }
            return null;
        }

        if (nameToObject.TryGetValue(objectName, out GameObject cached) && cached != null)
            return cached;

        GameObject go = GameObject.Find(objectName);
        if (go == null)
        {
            Debug.LogWarning("[GazeDrivenCreatureController] Mapped object not found in scene: " + objectName);
            return null;
        }

        nameToObject[objectName] = go;
        return go;
    }

    private void LoadMapping()
    {
        objKeyToName.Clear();
        nameToObject.Clear();

        string path = GetExistingMappingPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            if (debug)
            {
                Debug.LogWarning("[GazeDrivenCreatureController] Mapping file not found. Checked: " + GetMappingPath());
            }
            return;
        }

        string json = File.ReadAllText(path);
        loadedMappingPath = path;
        loadedMappingWriteTimeUtc = File.GetLastWriteTimeUtc(path);
        Regex regex = new Regex("\\\"(obj\\d+)\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
        MatchCollection matches = regex.Matches(json);
        foreach (Match match in matches)
        {
            string key = match.Groups[1].Value;
            string name = match.Groups[2].Value;
            objKeyToName[key] = name;
        }

        if (debug)
        {
            Debug.Log("[GazeDrivenCreatureController] Loaded object mapping from " + path + ": " + objKeyToName.Count + " entries.");
        }
    }

    private string GetMappingPath()
    {
        if (string.IsNullOrWhiteSpace(mappingFilePath))
            return string.Empty;

        if (Path.IsPathRooted(mappingFilePath))
            return mappingFilePath;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, mappingFilePath);
    }

    private string GetExistingMappingPath()
    {
        string configuredPath = GetMappingPath();
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string defaultPath = Path.Combine(projectRoot, "runtime_data/scene_object.json");
        return File.Exists(defaultPath) ? defaultPath : string.Empty;
    }

    private void RefreshMappingIfChanged()
    {
        string path = GetExistingMappingPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        System.DateTime writeTimeUtc = File.GetLastWriteTimeUtc(path);
        if (path != loadedMappingPath || writeTimeUtc != loadedMappingWriteTimeUtc)
        {
            LoadMapping();
        }
    }

    private GameObject GetTrackedTarget()
    {
        EnsureCreatureRoot();

        if (detector != null && detector.CurrentTarget != null)
        {
            return detector.CurrentTarget;
        }

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
        EnsureCreatureRoot();

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

    private void EnsureCreatureRoot()
    {
        if (creatureRoot == null)
        {
            creatureRoot = transform;
        }
    }

    private void EnsureMover()
    {
        if (mover == null)
        {
            mover = GetComponent<CreatureMover>();
        }
    }
}
