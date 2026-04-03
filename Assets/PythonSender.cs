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
}

public class PythonSender : MonoBehaviour
{
    private WebSocket websocket;
    private int seq = 0;
    private bool isConnected = false;

    async void Start()
    {
        websocket = new WebSocket("ws://127.0.0.1:8000/ws");

        websocket.OnOpen += () =>
        {
            Debug.Log("Connected to Python");
            isConnected = true;
            StartCoroutine(SendLoop());
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("WebSocket error: " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("WebSocket closed");
            isConnected = false;
        };

        websocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);
            Debug.Log("Received from Python: " + message);
        };

        await websocket.Connect();
    }

    IEnumerator SendLoop()
    {
        while (isConnected)
        {
            TestMessage msg = new TestMessage
            {
                type = "test",
                seq = seq++,
                unityTime = Time.time,
                text = "Hello from Unity"
            };

            string json = JsonUtility.ToJson(msg);
            websocket.SendText(json);
            Debug.Log("Sent: " + json);

            yield return new WaitForSeconds(1f);
        }
    }

    async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
#endif
    }
}