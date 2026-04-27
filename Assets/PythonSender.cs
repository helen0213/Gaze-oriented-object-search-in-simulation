using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Replace this namespace/class usage if your WebSocket package uses different names.
using NativeWebSocket;

[Serializable]
public class TestMessage
{
    public string type;
    public int seq;
    public float unityTime;
    public string text;
    public bool usingEyeTracking;
    public Vector3 gazeOrigin;
    public Vector3 gazeDirection;
    public SceneObjectInfo[] sceneObjects;
    public PomdpObjectByIndex pomdpObjectByIndex;
}

[Serializable]
public class SceneObjectInfo
{
    public string name;
    public Vector3 position;
}

[Serializable]
public class PomdpObjectByIndex
{
    public string obj1;
    public string obj2;
    public string obj3;
}

public class PythonSender : MonoBehaviour
{
    public static event Action<string> RawMessageReceived;
    private static PomdpObjectByIndex latestPomdpObjectByIndex;

    private WebSocket websocket;
    private int seq = 0;
    private bool isConnected = false;
    [Header("WebSocket")]
    [Tooltip("Used in Unity Editor and desktop builds.")]
    public string editorWebSocketUrl = "ws://127.0.0.1:8000/ws";
    [Tooltip("Used on Android/device builds. Set this to your computer's LAN IP by running 'ipconfig getifaddr en0', e.g. ws://192.168.1.42:8000/ws.")]
    public string androidWebSocketUrl = "ws://10.83.156.8:8000/ws";
    [Header("Gaze Source")]
    public CombinedGaze combinedGaze;
    [Header("Scene Objects")]
    public bool sendSceneObjects = true;
    public GazeTargetDetector gazeTargetDetector;
    public bool includeInactiveColliders = true;
    public bool requireNonEmptySceneObjects = true;
    public string[] allowedSceneObjectNames = { "Chicken_001", "Pinguin_001", "Tiger_001" };
    public string[] excludedSceneObjectNames = { "Dog_001" };
    public bool writeRuntimeSceneObjectMapping = true;
    public string runtimeSceneObjectMappingPath = "runtime_data/scene_object.json";
    [Header("Debug")]
    public bool debugOutgoingGaze = true;
    public float outgoingGazeDebugInterval = 1f;
    // Desired send rate (messages per second).
    public float targetHz = 30f;
    // Time between sends (seconds).
    private float sendInterval = 0f;
    // Accumulator for unscaled time to pace sends.
    private float sendAccumulator = 0f;
    private SceneObjectInfo[] lastNonEmptySceneObjects;
    private float lastOutgoingGazeDebugTime = -999f;

    async void Start()
    {
        // Initialize interval from inspector value before connecting.
        sendInterval = 1f / targetHz;
        string websocketUrl = GetWebSocketUrl();
        Debug.Log("[PythonSender] Connecting to " + websocketUrl);

        if (Application.platform == RuntimePlatform.Android &&
            (websocketUrl.Contains("127.0.0.1") || websocketUrl.Contains("localhost")))
        {
            Debug.LogWarning("[PythonSender] Android build is using loopback URL. " +
                             "Set androidWebSocketUrl to your computer LAN IP (e.g. ws://192.168.x.x:8000/ws) " +
                             "or run adb reverse tcp:8000 tcp:8000.");
        }

        // Create the WebSocket client.
        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () =>
        {
            // Mark connection state so Update can send.
            Debug.Log("Connected to Python");
            isConnected = true;
        };

        websocket.OnError += (e) =>
        {
            // Surface WebSocket errors in the console.
            Debug.LogError("WebSocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            // Stop sending if the connection closes.
            Debug.Log("WebSocket closed");
            isConnected = false;
        };

        websocket.OnMessage += (bytes) =>
        {
            // Log incoming messages from Python (actions).
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Received from Python: " + message);
            RawMessageReceived?.Invoke(message);
        };

        // Connect to the Python server (async).
        await websocket.Connect();
    }

    async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            // Gracefully close the WebSocket on exit.
            await websocket.Close();
        }
    }

    void OnValidate()
    {
        // Keep interval in sync if targetHz is edited in the Inspector.
        if (targetHz <= 0f) targetHz = 1f;
        sendInterval = 1f / targetHz;
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            // Required for NativeWebSocket to process incoming messages.
            websocket.DispatchMessageQueue();
        }
#endif

        // Only send when connected.
        if (!isConnected || websocket == null) return;
        if (SimulationMenuBlocker.IsBlockingScene())
        {
            // Mirror gaze collision/highlight gating while start/inactivity menus are active.
            sendAccumulator = 0f;
            return;
        }

        // Accumulate real time to maintain an approximate fixed send rate.
        sendAccumulator += Time.unscaledDeltaTime;
        if (sendAccumulator < sendInterval) return;
        sendAccumulator -= sendInterval;
        float now = Time.realtimeSinceStartup;

        // Build the test message payload.
        TestMessage msg = new TestMessage
        {
            type = "test",
            seq = seq++,
            unityTime = now,
            text = "Hello from Unity"
        };

        Vector3 gazeOrigin;
        Vector3 gazeDirection;

        if (combinedGaze != null)
        {
            Ray gazeRay = combinedGaze.CombinedRay;
            msg.usingEyeTracking = combinedGaze.UsingEyeTracking;
            gazeOrigin = gazeRay.origin;
            gazeDirection = NormalizeOrFallback(gazeRay.direction, Camera.main != null ? Camera.main.transform : transform);
        }
        else
        {
            Transform fallback = Camera.main != null ? Camera.main.transform : transform;
            msg.usingEyeTracking = false;
            gazeOrigin = fallback.position;
            gazeDirection = NormalizeOrFallback(fallback.forward, fallback);
        }

        msg.gazeOrigin = gazeOrigin;
        msg.gazeDirection = gazeDirection;

        if (sendSceneObjects)
        {
            var list = BuildSceneObjectsSnapshot();

            if (list.Count == 0 && lastNonEmptySceneObjects != null && lastNonEmptySceneObjects.Length > 0)
            {
                // Keep payload non-empty if live discovery temporarily fails.
                msg.sceneObjects = lastNonEmptySceneObjects;
                msg.pomdpObjectByIndex = BuildPomdpMap(lastNonEmptySceneObjects);
            }
            else if (list.Count > 0)
            {
                msg.sceneObjects = list.ToArray();
                lastNonEmptySceneObjects = msg.sceneObjects;
                msg.pomdpObjectByIndex = BuildPomdpMap(msg.sceneObjects);
            }

            if (requireNonEmptySceneObjects && (msg.sceneObjects == null || msg.sceneObjects.Length == 0))
            {
                Debug.LogWarning("[PythonSender] Skipping send because sceneObjects is empty.");
                return;
            }

            WriteRuntimeSceneObjectMapping(msg.sceneObjects, msg.pomdpObjectByIndex);
        }

        Debug.DrawRay(gazeOrigin, gazeDirection * 10f, Color.red);
        LogOutgoingGazeDebug(now, gazeOrigin, gazeDirection, msg);

        // Serialize and send to Python.
        string json = JsonUtility.ToJson(msg);
        websocket.SendText(json);
    }

    private Vector3 NormalizeOrFallback(Vector3 direction, Transform fallback)
    {
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        return fallback != null ? fallback.forward.normalized : Vector3.forward;
    }

    private void LogOutgoingGazeDebug(float now, Vector3 gazeOrigin, Vector3 gazeDirection, TestMessage msg)
    {
        if (!debugOutgoingGaze)
            return;

        if (now - lastOutgoingGazeDebugTime < Mathf.Max(outgoingGazeDebugInterval, 0.1f))
            return;

        lastOutgoingGazeDebugTime = now;

        int sceneObjectCount = msg.sceneObjects != null ? msg.sceneObjects.Length : 0;
        string hitText = "hit=None";

        float maxDistance = gazeTargetDetector != null ? gazeTargetDetector.maxDistance : 20f;
        int layerMask = gazeTargetDetector != null ? gazeTargetDetector.targetLayer.value : Physics.DefaultRaycastLayers;
        if (Physics.Raycast(gazeOrigin, gazeDirection, out RaycastHit hit, maxDistance, layerMask))
        {
            hitText = "hit=" + hit.collider.gameObject.name
                + " point=" + hit.point.ToString("F3")
                + " dist=" + hit.distance.ToString("F3");
        }

        Debug.Log("[PythonSender] Outgoing gaze "
            + "eye=" + msg.usingEyeTracking
            + " origin=" + gazeOrigin.ToString("F3")
            + " direction=" + gazeDirection.ToString("F3")
            + " objects=" + sceneObjectCount
            + " " + hitText);
    }

    private System.Collections.Generic.List<SceneObjectInfo> BuildSceneObjectsSnapshot()
    {
        if (gazeTargetDetector != null)
        {
            Transform[] tracked = gazeTargetDetector.GetRandomizedAnimals();
            if (tracked != null && tracked.Length > 0)
            {
                var trackedList = new System.Collections.Generic.List<SceneObjectInfo>(tracked.Length);
                for (int i = 0; i < tracked.Length; i++)
                {
                    Transform t = tracked[i];
                    if (t == null) continue;
                    if (!IsAllowedSceneObject(t.name)) continue;
                    if (IsExcludedSceneObject(t.name)) continue;

                    trackedList.Add(new SceneObjectInfo
                    {
                        name = t.name,
                        position = t.position
                    });
                }

                if (trackedList.Count > 0)
                    return trackedList;
            }
        }

        Collider[] colliders = includeInactiveColliders
            ? Resources.FindObjectsOfTypeAll<Collider>()
            : UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);

        Scene scene = SceneManager.GetActiveScene();
        var list = new System.Collections.Generic.List<SceneObjectInfo>(colliders.Length);
        var seen = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null) continue;
            GameObject go = c.gameObject;
            if (go == null) continue;

            // Skip assets/prefabs not in the active scene.
            if (go.hideFlags != HideFlags.None) continue;
            if (!go.scene.IsValid()) continue;
            if (go.scene != scene) continue;

            int id = go.GetInstanceID();
            if (seen.Contains(id)) continue;
            seen.Add(id);

            Transform t = go.transform;
            if (!IsAllowedSceneObject(t.name)) continue;
            if (IsExcludedSceneObject(t.name)) continue;

            list.Add(new SceneObjectInfo
            {
                name = t.name,
                position = t.position
            });
        }

        return list;
    }

    private bool IsAllowedSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        if (allowedSceneObjectNames == null || allowedSceneObjectNames.Length == 0)
            return true;

        for (int i = 0; i < allowedSceneObjectNames.Length; i++)
        {
            string allowedName = allowedSceneObjectNames[i];
            if (string.IsNullOrWhiteSpace(allowedName))
                continue;

            if (objectName == allowedName)
                return true;
        }

        return false;
    }

    private bool IsExcludedSceneObject(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || excludedSceneObjectNames == null)
            return false;

        for (int i = 0; i < excludedSceneObjectNames.Length; i++)
        {
            string excludedName = excludedSceneObjectNames[i];
            if (string.IsNullOrWhiteSpace(excludedName))
                continue;

            if (objectName == excludedName)
                return true;
        }

        return false;
    }

    private PomdpObjectByIndex BuildPomdpMap(SceneObjectInfo[] sceneObjects)
    {
        PomdpObjectByIndex map = new PomdpObjectByIndex();
        if (sceneObjects == null || sceneObjects.Length == 0)
            return map;

        SceneObjectInfo[] ordered = new SceneObjectInfo[sceneObjects.Length];
        Array.Copy(sceneObjects, ordered, sceneObjects.Length);
        Array.Sort(ordered, (a, b) => a.position.x.CompareTo(b.position.x));

        map.obj1 = ordered.Length > 0 ? ordered[0].name : null;
        map.obj2 = ordered.Length > 1 ? ordered[1].name : null;
        map.obj3 = ordered.Length > 2 ? ordered[2].name : null;
        latestPomdpObjectByIndex = map;
        return map;
    }

    public static string GetLatestPomdpObjectName(string objKey)
    {
        if (latestPomdpObjectByIndex == null || string.IsNullOrWhiteSpace(objKey))
            return null;

        switch (objKey.Trim().ToLowerInvariant())
        {
            case "obj1":
                return latestPomdpObjectByIndex.obj1;
            case "obj2":
                return latestPomdpObjectByIndex.obj2;
            case "obj3":
                return latestPomdpObjectByIndex.obj3;
            default:
                return null;
        }
    }

    private void WriteRuntimeSceneObjectMapping(SceneObjectInfo[] sceneObjects, PomdpObjectByIndex mapping)
    {
        if (!writeRuntimeSceneObjectMapping || sceneObjects == null || mapping == null)
            return;

#if UNITY_ANDROID && !UNITY_EDITOR
        return;
#endif

        string path = GetRuntimeSceneObjectMappingPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"sceneObjects\": [");
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                SceneObjectInfo obj = sceneObjects[i];
                if (obj == null) continue;

                sb.AppendLine("    {");
                sb.AppendLine("      \"name\": \"" + EscapeJson(obj.name) + "\",");
                sb.AppendLine("      \"position\": {");
                sb.AppendLine("        \"x\": " + obj.position.x.ToString("R", CultureInfo.InvariantCulture) + ",");
                sb.AppendLine("        \"y\": " + obj.position.y.ToString("R", CultureInfo.InvariantCulture) + ",");
                sb.AppendLine("        \"z\": " + obj.position.z.ToString("R", CultureInfo.InvariantCulture));
                sb.AppendLine("      }");
                sb.Append("    }");
                if (i < sceneObjects.Length - 1)
                    sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");
            sb.AppendLine("  \"pomdpObjectByIndex\": {");
            sb.AppendLine("    \"obj1\": \"" + EscapeJson(mapping.obj1) + "\",");
            sb.AppendLine("    \"obj2\": \"" + EscapeJson(mapping.obj2) + "\",");
            sb.AppendLine("    \"obj3\": \"" + EscapeJson(mapping.obj3) + "\"");
            sb.AppendLine("  }");
            sb.AppendLine("}");

            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PythonSender] Failed to write runtime scene object mapping: " + e.Message);
        }
    }

    private string GetRuntimeSceneObjectMappingPath()
    {
        if (string.IsNullOrWhiteSpace(runtimeSceneObjectMappingPath))
            return string.Empty;

        if (Path.IsPathRooted(runtimeSceneObjectMappingPath))
            return runtimeSceneObjectMappingPath;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, runtimeSceneObjectMappingPath);
    }

    private string EscapeJson(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private string GetWebSocketUrl()
    {
        string url = Application.platform == RuntimePlatform.Android
            ? androidWebSocketUrl
            : editorWebSocketUrl;

        if (string.IsNullOrWhiteSpace(url))
        {
            return "ws://127.0.0.1:8000/ws";
        }

        return url.Trim();
    }
}
