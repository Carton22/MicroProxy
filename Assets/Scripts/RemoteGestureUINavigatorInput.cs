using System;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Bridges remote phone gestures (from the Socket relay) into the same UI navigation logic used by Quest microgestures.
/// Expected JSON example (shape from logs/screenshots):
/// {
///   "type":"gesture",
///   "startNormalized":{"u":0.85,"v":0.27},
///   "endNormalized":{"u":0.87,"v":0.50},
///   "gesture":{"type":"swipe_up"},
///   ...
/// }
/// </summary>
[DisallowMultipleComponent]
public class RemoteGestureUINavigatorInput : MonoBehaviour
{
    [SerializeField] private SocketManager m_socketManager;
    [SerializeField] private UINavigator m_uiNavigator;

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;

    private void Reset()
    {
        m_uiNavigator = GetComponent<UINavigator>();
    }

    private void OnEnable()
    {
        if (m_socketManager == null)
            m_socketManager = FindFirstObjectByType<SocketManager>();
        if (m_uiNavigator == null)
            m_uiNavigator = GetComponent<UINavigator>();

        if (m_socketManager != null)
            m_socketManager.OnMessageReceived += OnSocketMessage;
    }

    private void OnDisable()
    {
        if (m_socketManager != null)
            m_socketManager.OnMessageReceived -= OnSocketMessage;
    }

    private void OnSocketMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        if (!TryGetGestureType(json, out var gestureType))
            return;

        if (m_debugLog)
            Debug.Log($"[RemoteGestureUINavigatorInput] gestureType={gestureType}");

        if (m_uiNavigator == null)
            return;

        // Map phone gesture names into existing UINavigator movement.
        switch (gestureType.ToLowerInvariant())
        {
            case "swipe_right":
            case "swiperight":
                m_uiNavigator.MoveRight();
                break;

            case "swipe_left":
            case "swipeleft":
                m_uiNavigator.MoveLeft();
                break;

            case "swipe_up":
            case "swipeforward":
            case "swipeforwardup":
            case "swipeup":
                m_uiNavigator.MoveUp();
                break;

            case "swipe_down":
            case "swipe_backward":
            case "swipebackward":
            case "swipeback":
            case "swipedown":
                m_uiNavigator.MoveDown();
                break;

            case "thumb_tap":
            case "thumbtap":
            case "tap":
            case "taptap":
                m_uiNavigator.ClickSelected();
                break;

            case "pinch_twist":
            case "pinchandtwist":
            case "pinchandtwistgesture":
                if (TryGetSignedNormalized(json, out var signedNormalized))
                    m_uiNavigator.RemotePinchAndTwist(signedNormalized);
                else if (m_debugLog)
                    Debug.LogWarning($"[RemoteGestureUINavigatorInput] pinch_twist missing signedNormalized. json={json}");
                break;

            case "pinch_twist_in":
            case "pinchtwistin":
                m_uiNavigator.RemotePinchAndTwist(-1f);
                break;

            case "pinch_twist_out":
            case "pinchtwistout":
                m_uiNavigator.RemotePinchAndTwist(1f);
                break;

            default:
                if (m_debugLog)
                    Debug.LogWarning($"[RemoteGestureUINavigatorInput] Unhandled gestureType: {gestureType}");
                break;
        }
    }

    private static bool TryGetGestureType(string json, out string gestureType)
    {
        gestureType = null;
        if (string.IsNullOrEmpty(json))
            return false;

        // Quick reject.
        if (!Regex.IsMatch(json, "\"type\"\\s*:\\s*\"gesture\"", RegexOptions.IgnoreCase))
            return false;

        // Preferred: gesture is an object: "gesture":{"type":"swipe_up", ...}
        var m = Regex.Match(
            json,
            "\"gesture\"\\s*:\\s*\\{[\\s\\S]*?\"type\"\\s*:\\s*\"(?<t>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            gestureType = m.Groups["t"].Value;
            return true;
        }

        // Fallback: gesture is a string: "gesture":"swipe_up"
        m = Regex.Match(
            json,
            "\"gesture\"\\s*:\\s*\"(?<t>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            gestureType = m.Groups["t"].Value;
            return true;
        }

        // Fallback: gestureType field somewhere.
        m = Regex.Match(
            json,
            "\"gestureType\"\\s*:\\s*\"(?<t>[^\"]+)\"",
            RegexOptions.IgnoreCase);
        if (m.Success)
        {
            gestureType = m.Groups["t"].Value;
            return true;
        }

        return false;
    }

    private static bool TryGetSignedNormalized(string json, out float signedNormalized)
    {
        signedNormalized = 0f;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        var m = Regex.Match(
            json,
            "\"signedNormalized\"\\s*:\\s*(?<n>-?\\d+(?:\\.\\d+)?)",
            RegexOptions.IgnoreCase);

        if (!m.Success)
            return false;

        string raw = m.Groups["n"].Value;
        return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out signedNormalized);
    }
}

