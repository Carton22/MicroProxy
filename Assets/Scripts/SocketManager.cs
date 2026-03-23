using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WebSocketSharp;

public class SocketManager : MonoBehaviour
{
    [Serializable]
    private class GestureTypeEnvelope
    {
        public string type;
        public GestureObject gesture;
        public string gestureType;
    }

    [Serializable]
    private class GestureObject
    {
        public string type;
    }

    public WebSocket ws;

    public string ipAddress = "10.136.123.61";
    [SerializeField] private int port = 3000;
    [SerializeField] private bool logRawMessages;

    /// <summary>
    /// Fired for every raw websocket text message from the server (e.g. touch_frame / gesture JSON).
    /// </summary>
    public event Action<string> OnMessageReceived;
    /// <summary>
    /// Fired only with normalized gesture signal (e.g. swipe_left, zoom_out).
    /// </summary>
    public event Action<string> OnGestureSignalReceived;
    private readonly Queue<Action> m_mainThreadActions = new();
    private readonly object m_mainThreadLock = new();

    void Awake()
    {
        ws = new WebSocket($"ws://{ipAddress}:{port}");

        ws.OnOpen += (sender, e) => EnqueueOnMainThread(() => Debug.Log("Connected to server"));

        ws.OnMessage += (sender, e) =>
        {
            string data = e.Data;
            EnqueueOnMainThread(() =>
            {
                if (logRawMessages)
                    Debug.Log("Raw message: " + data);
                OnMessageReceived?.Invoke(data);
                if (TryExtractGestureSignal(data, out string gestureSignal))
                    OnGestureSignalReceived?.Invoke(gestureSignal);
            });
        };

        ws.OnClose += (sender, e) => EnqueueOnMainThread(() => Debug.Log("Disconnected"));

        ws.Connect();
    }

    void Update()
    {
        while (true)
        {
            Action action;
            lock (m_mainThreadLock)
            {
                if (m_mainThreadActions.Count == 0)
                    break;
                action = m_mainThreadActions.Dequeue();
            }

            action?.Invoke();
        }
    }

    void OnDestroy()
    {
        ws?.Close();
    }

    private void EnqueueOnMainThread(Action action)
    {
        if (action == null)
            return;

        lock (m_mainThreadLock)
            m_mainThreadActions.Enqueue(action);
    }

    private static bool TryExtractGestureSignal(string json, out string gestureSignal)
    {
        gestureSignal = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        GestureTypeEnvelope envelope;
        try
        {
            envelope = JsonUtility.FromJson<GestureTypeEnvelope>(json);
        }
        catch
        {
            return false;
        }

        if (envelope == null || !string.Equals(envelope.type, "gesture", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(envelope.gestureType))
        {
            gestureSignal = envelope.gestureType.Trim();
            return true;
        }

        if (envelope.gesture != null && !string.IsNullOrWhiteSpace(envelope.gesture.type))
        {
            gestureSignal = envelope.gesture.type.Trim();
            return true;
        }

        // Fallback for payloads using "gesture":"swipe_up" string form.
        var match = Regex.Match(json, "\"gesture\"\\s*:\\s*\"(?<t>[^\"]+)\"", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            gestureSignal = match.Groups["t"].Value.Trim();
            return true;
        }

        return false;
    }
}
