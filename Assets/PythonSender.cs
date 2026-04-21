using System;
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

    private WebSocket websocket;
    private int seq = 0;
    private bool isConnected = false;
    [Header("WebSocket")]
    [Tooltip("Used in Unity Editor and desktop builds.")]
    public string editorWebSocketUrl = "ws://127.0.0.1:8000/ws";
    [Tooltip("Used on Android/device builds. Set this to your computer's LAN IP by running 'ipconfig getifaddr en0', e.g. ws://192.168.1.42:8000/ws.")]
    public string androidWebSocketUrl = "ws://xx.xx.xx.xx:8000/ws";
    [Header("Gaze Source")]
    public CombinedGaze combinedGaze;
    [Header("Scene Objects")]
    public bool sendSceneObjects = true;
    public GazeTargetDetector gazeTargetDetector;
    public bool includeInactiveColliders = true;
    public bool requireNonEmptySceneObjects = true;
    // Desired send rate (messages per second).
    public float targetHz = 30f;
    // Time between sends (seconds).
    private float sendInterval = 0f;
    // Accumulator for unscaled time to pace sends.
    private float sendAccumulator = 0f;
    private SceneObjectInfo[] lastNonEmptySceneObjects;

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

        if (combinedGaze != null)
        {
            msg.usingEyeTracking = combinedGaze.UsingEyeTracking;
            msg.gazeOrigin = combinedGaze.CombinedRay.origin;
            msg.gazeDirection = combinedGaze.CombinedRay.direction;
        }

        if (combinedGaze == null)
        {
            Transform fallback = Camera.main != null ? Camera.main.transform : transform;
            msg.usingEyeTracking = false;
            msg.gazeOrigin = fallback.position;
            msg.gazeDirection = fallback.forward;
        }

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
        }

        // Serialize and send to Python.
        string json = JsonUtility.ToJson(msg);
        websocket.SendText(json);
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
            list.Add(new SceneObjectInfo
            {
                name = t.name,
                position = t.position
            });
        }

        return list;
    }

    private PomdpObjectByIndex BuildPomdpMap(SceneObjectInfo[] sceneObjects)
    {
        PomdpObjectByIndex map = new PomdpObjectByIndex();
        if (sceneObjects == null || sceneObjects.Length == 0)
            return map;

        map.obj1 = sceneObjects.Length > 0 ? sceneObjects[0].name : null;
        map.obj2 = sceneObjects.Length > 1 ? sceneObjects[1].name : null;
        map.obj3 = sceneObjects.Length > 2 ? sceneObjects[2].name : null;
        return map;
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
