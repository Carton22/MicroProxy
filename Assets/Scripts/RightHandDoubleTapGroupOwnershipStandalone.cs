using UnityEngine;

/// <summary>
/// Local double-tap handler for toggling the logical middle hierarchy level.
/// Remote server double-tap listening is optional so scenes that already use
/// RemoteGestureUINavigatorInput do not double-handle the same gesture.
/// </summary>
[DisallowMultipleComponent]
public class RightHandDoubleTapGroupOwnershipStandalone : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private OVRHand m_rightHand;
    [SerializeField] private SocketManager m_socketManager;
    [SerializeField] private bool m_listenForRemoteGestureSignals;

    [Header("Pinch settings")]
    [Range(0f, 1f)]
    [SerializeField] private float m_pinchStrengthThreshold = 0.7f;
    [SerializeField] private float m_doubleTapMaxIntervalSeconds = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;
    [SerializeField] private SharedLogger m_logger;

    private bool m_isPinching;
    private float m_lastTapTime = -1f;

    private void Awake()
    {
        ResolveReferences(includeSocketManager: m_listenForRemoteGestureSignals);
    }

    private void OnEnable()
    {
        ResolveReferences(includeSocketManager: m_listenForRemoteGestureSignals);

        if (m_listenForRemoteGestureSignals && m_socketManager != null)
            m_socketManager.OnGestureSignalReceived += OnGestureSignal;
    }

    private void OnDisable()
    {
        if (m_socketManager != null)
            m_socketManager.OnGestureSignalReceived -= OnGestureSignal;
    }

    private void Reset()
    {
        ResolveReferences(includeSocketManager: m_listenForRemoteGestureSignals);
    }

    private void Update()
    {
        ResolveReferences(includeSocketManager: false);
        if (m_rightHand == null)
            return;

        if (!m_rightHand.IsDataValid)
        {
            m_isPinching = false;
            return;
        }

        float pinch = m_rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        bool pinchDown = pinch >= m_pinchStrengthThreshold;

        if (pinchDown)
        {
            if (m_isPinching)
                return;

            m_isPinching = true;
            float now = Time.time;
            if (m_lastTapTime >= 0f && (now - m_lastTapTime) <= m_doubleTapMaxIntervalSeconds)
            {
                m_lastTapTime = -1f;
                HandleDoubleTap();
            }
            else
            {
                m_lastTapTime = now;
            }
        }
        else
        {
            m_isPinching = false;
        }
    }

    public void RemoteDoubleTap()
    {
        HandleDoubleTap();
    }

    private void ResolveReferences(bool includeSocketManager)
    {
        if (m_rightHand == null)
            m_rightHand = GetComponent<OVRHand>();
        if (m_rightHand == null)
            m_rightHand = FindFirstObjectByType<OVRHand>();

        if (includeSocketManager && m_socketManager == null)
            m_socketManager = FindFirstObjectByType<SocketManager>();
    }

    private void OnGestureSignal(string gestureType)
    {
        if (string.IsNullOrWhiteSpace(gestureType))
            return;

        string normalized = gestureType.ToLowerInvariant();
        if (normalized != "double_tap" && normalized != "doubletap" && normalized != "taptap")
            return;

        HandleDoubleTap();
    }

    private void HandleDoubleTap()
    {
        bool toggled = SpatialHierarchyChildViewManager.TryHandleDoubleTapToggleLevelVariant();
        Log(toggled
            ? "[RightHandDoubleTapGroupOwnershipStandalone] toggled the middle hierarchy layer."
            : "[RightHandDoubleTapGroupOwnershipStandalone] no hierarchy toggle handled.");
    }

    private void Log(string message)
    {
        if (!m_debugLog || string.IsNullOrEmpty(message))
            return;

        if (m_logger != null)
            m_logger.Log(message);
        else
            Debug.Log(message);
    }
}
