using UnityEngine;

/// <summary>
/// Bridges normalized remote gesture signals into the same UI navigation logic used by Quest microgestures.
/// This script consumes only gesture names from SocketManager (e.g. swipe_left, zoom_out), not raw JSON.
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
            m_socketManager.OnGestureSignalReceived += OnGestureSignal;
    }

    private void OnDisable()
    {
        if (m_socketManager != null)
            m_socketManager.OnGestureSignalReceived -= OnGestureSignal;
    }

    private void OnGestureSignal(string gestureType)
    {
        if (string.IsNullOrWhiteSpace(gestureType))
            return;

        string normalized = gestureType.ToLowerInvariant();

        if (m_debugLog)
            Debug.Log($"[RemoteGestureUINavigatorInput] gestureType={gestureType}");

        if (m_uiNavigator == null)
            return;

        // Map phone gesture names into existing UINavigator movement.
        switch (normalized)
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
                m_uiNavigator.ClickSelected();
                break;

            case "taptap":
            case "double_tap":
            case "doubletap":
                m_uiNavigator.DoubleTapSelected();
                break;

            case "pinch_twist":
            case "pinchandtwist":
            case "pinchandtwistgesture":
                // Generic pinch_twist has no direction in signal-only mode.
                break;

            case "pinch_twist_in":
            case "pinchtwistin":
                m_uiNavigator.RemotePinchAndTwist(-1f);
                break;

            case "pinch_twist_out":
            case "pinchtwistout":
                m_uiNavigator.RemotePinchAndTwist(1f);
                break;

            case "zoom_in":
            case "zoomin":
                if (!ProxySetDrillDownController.TryHandleRemoteSignedTwist(-1f))
                    m_uiNavigator.RemoteZoomSwitchLayer(zoomOut: false);
                break;

            case "zoom_out":
            case "zoomout":
                if (!ProxySetDrillDownController.TryHandleRemoteSignedTwist(1f))
                    m_uiNavigator.RemoteZoomSwitchLayer(zoomOut: true);
                break;

            default:
                if (m_debugLog)
                    Debug.LogWarning($"[RemoteGestureUINavigatorInput] Unhandled gestureType: {gestureType}");
                break;
        }
    }
}

