using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach this script to each clickable proxy "node" in the current level.
/// When the node is pressed, it:
/// - hides the current-level root (m_levelRootToHideOnDrillDown)
/// - disables all children under m_nextLevelRootToManage (optional but recommended)
/// - enables ONLY the assigned next-level proxy objects in m_childrenToEnable
/// </summary>
public class ProxySetDrillDownController : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [Header("Level visibility")]
    [Tooltip("Root object for the current proxy set level. This GameObject will be set inactive when drilling down.")]
    [SerializeField] private GameObject m_levelRootToHideOnDrillDown;

    [Tooltip("Root object for the next-level proxy objects. This GameObject will be set active when drilling down.")]
    [SerializeField] private GameObject m_childrenRootToShow;

    [Header("Next level mapping")]
    [Tooltip("Next-level proxy GameObjects to enable after drilling down from this node.")]
    [SerializeField] private List<GameObject> m_childrenToEnable = new();

    [Header("Behavior")]
    [Tooltip("If true, selects the first Selectable under the enabled children.")]
    [SerializeField] private bool m_selectFirstOnShow = true;

    [Header("Debug")]
    [Tooltip("If true, logs when a drill-down switch happens.")]
    [SerializeField] static private bool m_debugLog = false;
    [Tooltip("Optional shared logger used for debug output. Falls back to Debug.Log if not assigned.")]
    [SerializeField] private SharedLogger m_logger;

    [Header("Right-hand double tap (back)")]
    [Tooltip("Optional right hand. If assigned, a thumb+middle double tap returns from child view to parent view.")]
    [SerializeField] private OVRHand m_rightHand;
    [Range(0f, 1f)]
    [SerializeField] private float m_pinchStrengthThreshold = 0.7f;
    [SerializeField] private float m_doubleTapMaxIntervalSeconds = 0.4f;

    public void OnPointerClick(PointerEventData eventData) => HandlePress();

    public void OnSubmit(BaseEventData eventData) => HandlePress();

    private void HandlePress()
    {
        if (m_childrenToEnable == null || m_childrenToEnable.Count == 0)
            return;

        if (m_debugLog)
            LogDebug($"[ProxySetDrillDownController] DrillDown from {gameObject.name}. children={m_childrenToEnable.Count}");

        if (m_levelRootToHideOnDrillDown != null)
            m_levelRootToHideOnDrillDown.SetActive(false);

        if (m_childrenRootToShow != null)
            m_childrenRootToShow.SetActive(true);

        // Important: this component may get disabled with the parent root.
        // Install/update a relay on children root so double-tap back keeps working in child view.
        ConfigureBackRelay();

        // Inside the children root:
        // - disable all direct child containers
        // - enable only the containers that contain our selected targets
        if (m_childrenRootToShow != null)
        {
            var rootTf = m_childrenRootToShow.transform;

            for (int i = 0; i < rootTf.childCount; i++)
            {
                var childTf = rootTf.GetChild(i);
                if (childTf != null)
                    childTf.gameObject.SetActive(false);
            }

            for (int i = 0; i < m_childrenToEnable.Count; i++)
            {
                var enabled = m_childrenToEnable[i];
                if (enabled == null)
                    continue;

                enabled.SetActive(true);

                // Enable the direct child container under m_childrenRootToShow.
                Transform cur = enabled.transform;
                while (cur != null && cur.parent != rootTf)
                    cur = cur.parent;

                if (cur != null && cur.parent == rootTf)
                    cur.gameObject.SetActive(true);
            }
        }
        else
        {
            // Fallback: just enable the objects we were given (may require their parents to already be active).
            for (int i = 0; i < m_childrenToEnable.Count; i++)
            {
                var go = m_childrenToEnable[i];
                if (go != null)
                    go.SetActive(true);
            }
        }

        // Enable only the children assigned to this node (selection focus).
        GameObject firstEnabledSelectableRoot = null;
        for (int i = 0; i < m_childrenToEnable.Count; i++)
        {
            var go = m_childrenToEnable[i];
            if (go == null)
                continue;

            go.SetActive(true);
            if (firstEnabledSelectableRoot == null)
                firstEnabledSelectableRoot = go;
        }

        if (m_selectFirstOnShow)
            SelectFirstSelectableUnder(firstEnabledSelectableRoot);
    }

    private bool IsInChildView()
    {
        bool parentHidden = m_levelRootToHideOnDrillDown != null && !m_levelRootToHideOnDrillDown.activeSelf;
        bool childShown = m_childrenRootToShow != null && m_childrenRootToShow.activeSelf;
        LogDebug($"[ProxySetDrillDownController] IsInChildView: parentHidden={parentHidden}, childShown={childShown}");
        return parentHidden && childShown;
    }

    private void ReturnToParentView()
    {
        if (m_levelRootToHideOnDrillDown != null)
            m_levelRootToHideOnDrillDown.SetActive(true);

        if (m_childrenRootToShow != null)
        {
            // Hide all direct children under the child root, then hide the root.
            var rootTf = m_childrenRootToShow.transform;
            for (int i = 0; i < rootTf.childCount; i++)
            {
                var childTf = rootTf.GetChild(i);
                if (childTf != null)
                    childTf.gameObject.SetActive(false);
            }
            m_childrenRootToShow.SetActive(false);
        }

        if (m_debugLog)
            LogDebug($"[ProxySetDrillDownController] Double tap back to parent from {gameObject.name}");

        // Restore focus to the label node this controller is attached to.
        if (EventSystem.current != null && gameObject.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
            var selectable = GetComponent<Selectable>();
            if (selectable == null)
                selectable = GetComponentInChildren<Selectable>(false);
            if (selectable != null)
                selectable.Select();
        }
    }

    private void LogDebug(string message)
    {
        if (!m_debugLog)
            return;

        if (m_logger != null)
            m_logger.Log(message);
        else
            Debug.Log(message);
    }

    private void ConfigureBackRelay()
    {
        if (m_childrenRootToShow == null)
            return;

        var relay = m_childrenRootToShow.GetComponent<BackGestureRelay>();
        if (relay == null)
            relay = m_childrenRootToShow.AddComponent<BackGestureRelay>();

        relay.Configure(
            this,
            m_rightHand,
            m_pinchStrengthThreshold,
            m_doubleTapMaxIntervalSeconds,
            m_debugLog,
            m_logger
        );
    }

    private sealed class BackGestureRelay : MonoBehaviour
    {
        private ProxySetDrillDownController m_owner;
        private OVRHand m_rightHand;
        private float m_pinchStrengthThreshold;
        private float m_doubleTapMaxIntervalSeconds;
        private bool m_debugLog;
        private SharedLogger m_logger;

        private bool m_isPinching;
        private float m_lastTapTime = -1f;

        public void Configure(
            ProxySetDrillDownController owner,
            OVRHand rightHand,
            float pinchStrengthThreshold,
            float doubleTapMaxIntervalSeconds,
            bool debugLog,
            SharedLogger logger)
        {
            m_owner = owner;
            m_rightHand = rightHand;
            m_pinchStrengthThreshold = pinchStrengthThreshold;
            m_doubleTapMaxIntervalSeconds = doubleTapMaxIntervalSeconds;
            m_debugLog = debugLog;
            m_logger = logger;
        }

        private void Update()
        {
            if (m_owner == null || !m_owner.IsInChildView())
                return;

            if (m_rightHand == null)
            {
                LogDebug("[ProxySetDrillDownController] Back relay: m_rightHand is null.");
                return;
            }

            if (!m_rightHand.IsDataValid)
            {
                m_isPinching = false;
                LogDebug("[ProxySetDrillDownController] Back relay: right hand data invalid.");
                return;
            }

            float pinch = m_rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
            bool pinchDown = pinch >= m_pinchStrengthThreshold;

            if (pinchDown)
            {
                if (!m_isPinching)
                {
                    m_isPinching = true;
                    float now = Time.time;
                    if (m_lastTapTime >= 0f && (now - m_lastTapTime) <= m_doubleTapMaxIntervalSeconds)
                    {
                        m_lastTapTime = -1f;
                        LogDebug("[ProxySetDrillDownController] Back relay: double tap detected.");
                        m_owner.ReturnToParentView();
                    }
                    else
                    {
                        m_lastTapTime = now;
                        LogDebug("[ProxySetDrillDownController] Back relay: first tap recorded.");
                    }
                }
            }
            else
            {
                m_isPinching = false;
            }
        }

        private void LogDebug(string message)
        {
            if (!m_debugLog)
                return;

            if (m_logger != null)
                m_logger.Log(message);
            else
                Debug.Log(message);
        }
    }

    private static void SelectFirstSelectableUnder(GameObject root)
    {
        if (root == null || EventSystem.current == null)
            return;

        var selectable = root.GetComponentInChildren<Selectable>(false);
        if (selectable == null)
            return;

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }
}

