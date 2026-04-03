using System;
using System.Collections;
using System.Text;
using UnityEngine;

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
}

public class PythonSender : MonoBehaviour
{
    private WebSocket websocket;
    private int seq = 0;
    private bool isConnected = false;
    [Header("Gaze Source")]
    public CombinedGaze combinedGaze;
    // Desired send rate (messages per second).
    public float targetHz = 30f;
    // Time between sends (seconds).
    private float sendInterval = 0f;
    // Accumulator for unscaled time to pace sends.
    private float sendAccumulator = 0f;

    async void Start()
    {
        // Initialize interval from inspector value before connecting.
        sendInterval = 1f / targetHz;
        // Create the WebSocket client.
        websocket = new WebSocket("ws://127.0.0.1:8000/ws");

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
            // Log incoming messages from Python (ack or responses).
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Received from Python: " + message);
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

        // Serialize and send to Python.
        string json = JsonUtility.ToJson(msg);
        websocket.SendText(json);
    }
}
