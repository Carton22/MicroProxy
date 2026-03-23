using System;
using UnityEngine;
using WebSocketSharp;

public class WebSocketManager : MonoBehaviour
{
    public WebSocket ws;

    public string ipAddress = "10.136.123.61";

    void Awake()
    {
        ws = new WebSocket("ws://" + ipAddress + ":3000");

        ws.OnOpen += (sender, e) => Debug.Log("Connected to server");

        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Raw message: " + e.Data);
        };

        ws.OnClose += (sender, e) => Debug.Log("Disconnected");

        ws.Connect();
    }

    void OnDestroy()
    {
        ws?.Close();
    }
}
