using System;
using UnityEngine;
using WebSocketSharp;

public class SocketManager : MonoBehaviour
{
    public WebSocket ws;

    public string ipAddress = "10.136.123.61";

    /// <summary>
    /// Fired for every raw websocket text message from the server (e.g. touch_frame / gesture JSON).
    /// </summary>
    public event Action<string> OnMessageReceived;

    void Awake()
    {
        ws = new WebSocket("ws://" + ipAddress + ":3000");

        ws.OnOpen += (sender, e) => Debug.Log("Connected to server");

        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Raw message: " + e.Data);
            OnMessageReceived?.Invoke(e.Data);
        };

        ws.OnClose += (sender, e) => Debug.Log("Disconnected");

        ws.Connect();
    }

    void OnDestroy()
    {
        ws?.Close();
    }
}
