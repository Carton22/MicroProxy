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
    /// <summary>
    /// Global state: true when any drill-down controller is currently in child view.
    /// </summary>
    public static bool IsAnyDrillDownChildViewActive => s_activeChildViewCount > 0;

    private static int s_activeChildViewCount;

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

    [Header("Left-hand pinch twist")]
    [Tooltip("Optional left-hand twist source. When assigned, twisting one way drills down and the opposite way returns to parent.")]
    [SerializeField] private PinchAndTwistEventSource m_twistEventSource;
    [Tooltip("If true, positive twist drills down and negative twist returns to parent. If false, the mapping is reversed.")]
    [SerializeField] private bool m_positiveTwistDrillsDown = true;
    [Range(0.05f, 0.75f)]
    [Tooltip("Minimum absolute normalized twist required before changing between parent and child.")]
    [SerializeField] private static float m_twistThresholdNormalized = 0.08f;

    [Header("Debug")]
    [Tooltip("If true, logs when a drill-down switch happens.")]
    [SerializeField] static private bool m_debugLog = false;
    [Tooltip("Optional shared logger used for debug output. Falls back to Debug.Log if not assigned.")]
    [SerializeField] private SharedLogger m_logger;
    private bool m_registeredAsChildView;
    private bool m_subscribedToTwistEvents;
    private bool m_inTwistGesture;
    private bool m_twistConsumed;
    private ProxyLabelManager m_cachedLabelManager;

    public static bool ShouldReserveTwistForDrillDown()
    {
        if (IsAnyDrillDownChildViewActive)
            return true;

        return TryGetSelectedDrillDownController(out _);
    }

    public static bool ShouldReserveTwistForDrillDown(PinchAndTwistEventSource twistEventSource)
    {
        if (twistEventSource == null)
            return ShouldReserveTwistForDrillDown();

        if (TryGetSelectedDrillDownController(out var selectedController) &&
            selectedController.IsUsingTwistEventSource(twistEventSource))
        {
            return true;
        }

        if (!IsAnyDrillDownChildViewActive)
            return false;

        var controllers = FindObjectsByType<ProxySetDrillDownController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            var controller = controllers[i];
            if (controller == null)
                continue;

            if (controller.IsInChildView() && controller.IsUsingTwistEventSource(twistEventSource))
                return true;
        }

        return false;
    }

    public void OnPointerClick(PointerEventData eventData) => HandlePress();

    public void OnSubmit(BaseEventData eventData) => HandlePress();

    private void Reset()
    {
        AssignDefaultTwistEventSourceIfNeeded();
    }

    private void OnValidate()
    {
        AssignDefaultTwistEventSourceIfNeeded();
    }

    private void OnEnable()
    {
        ResolveTwistEventSource();
        SubscribeToTwistEvents();
        UpdateGlobalDrillDownState(IsInChildView());
    }

    private void OnDisable()
    {
        bool keepRegisteredChildView = m_registeredAsChildView && IsInChildView();
        UnsubscribeFromTwistEvents();

        if (!keepRegisteredChildView)
            UpdateGlobalDrillDownState(false);
    }

    private void OnDestroy()
    {
        UpdateGlobalDrillDownState(false);
    }

    private void HandlePress()
    {
        if (IsInChildView() || IsTransitioningBetweenViews())
            return;

        if (m_childrenToEnable == null || m_childrenToEnable.Count == 0)
            return;

        if (m_debugLog)
            LogDebug($"[ProxySetDrillDownController] DrillDown from {gameObject.name}. children={m_childrenToEnable.Count}");

        if (m_childrenRootToShow != null)
            m_childrenRootToShow.SetActive(true);

        // Important: this component may get disabled with the parent root.
        // Install/update a relay on children root so gesture-based return keeps working in child view.
        ConfigureChildViewRelay();

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

        var labelManager = ResolveOwningLabelManager();
        if (labelManager != null && m_childrenRootToShow != null)
            labelManager.SetActiveLabelsParent(m_childrenRootToShow.transform);

        if (!TryPlayTransition(
                m_levelRootToHideOnDrillDown,
                m_childrenRootToShow,
                ProxySetHorizontalTransitionDirection.ToRight,
                HideParentRoot))
        {
            HideParentRoot();
        }

        UpdateGlobalDrillDownState(true);
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
        if (IsTransitioningBetweenViews())
            return;

        if (m_levelRootToHideOnDrillDown != null)
            m_levelRootToHideOnDrillDown.SetActive(true);

        if (m_debugLog)
            LogDebug($"[ProxySetDrillDownController] Return to parent from {gameObject.name}");

        var labelManager = ResolveOwningLabelManager();
        if (labelManager != null && m_levelRootToHideOnDrillDown != null)
            labelManager.SetActiveLabelsParent(m_levelRootToHideOnDrillDown.transform);

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

        if (!TryPlayTransition(
                m_childrenRootToShow,
                m_levelRootToHideOnDrillDown,
                ProxySetHorizontalTransitionDirection.ToLeft,
                HideChildRoot))
        {
            HideChildRoot();
        }

        UpdateGlobalDrillDownState(false);
    }

    private static bool TryGetSelectedDrillDownController(out ProxySetDrillDownController controller)
    {
        controller = null;

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return false;

        controller = selected.GetComponentInParent<ProxySetDrillDownController>();
        if (controller == null)
            return false;

        if (!controller.CanDrillDownFromSelection(selected))
        {
            controller = null;
            return false;
        }

        return true;
    }

    private bool CanDrillDownFromCurrentSelection()
    {
        if (IsTransitioningBetweenViews())
            return false;

        if (IsInChildView())
            return false;

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return CanDrillDownFromSelection(selected);
    }

    private bool CanDrillDownFromSelection(GameObject selected)
    {
        if (!isActiveAndEnabled)
            return false;

        if (m_childrenToEnable == null || m_childrenToEnable.Count == 0)
            return false;

        if (selected == null)
            return false;

        return selected == gameObject || selected.transform.IsChildOf(transform);
    }

    private bool IsUsingTwistEventSource(PinchAndTwistEventSource twistEventSource)
    {
        return m_twistEventSource == twistEventSource;
    }

    private void ResolveTwistEventSource()
    {
        AssignDefaultTwistEventSourceIfNeeded();
    }

    private void AssignDefaultTwistEventSourceIfNeeded()
    {
        if (m_twistEventSource != null)
            return;

        m_twistEventSource = GetComponent<PinchAndTwistEventSource>();
        if (m_twistEventSource != null)
            return;

        m_twistEventSource = FindPreferredLeftTwistEventSource();
    }

    private static PinchAndTwistEventSource FindPreferredLeftTwistEventSource()
    {
        var sources = FindObjectsByType<PinchAndTwistEventSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        PinchAndTwistEventSource fallback = null;

        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            if (source == null)
                continue;

            if (fallback == null)
                fallback = source;

            string sourceName = source.gameObject.name;
            if (!string.IsNullOrEmpty(sourceName) &&
                sourceName.IndexOf("left", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return source;
            }
        }

        return fallback;
    }

    private void SubscribeToTwistEvents()
    {
        if (m_twistEventSource == null || m_subscribedToTwistEvents)
            return;

        m_twistEventSource.OnStartPinchAndTwist.AddListener(OnStartPinchAndTwist);
        m_twistEventSource.OnPinchAndTwist.AddListener(OnPinchAndTwist);
        m_twistEventSource.OnEndPinchAndTwist.AddListener(OnEndPinchAndTwist);
        m_subscribedToTwistEvents = true;
    }

    private void UnsubscribeFromTwistEvents()
    {
        if (m_twistEventSource == null || !m_subscribedToTwistEvents)
            return;

        m_twistEventSource.OnStartPinchAndTwist.RemoveListener(OnStartPinchAndTwist);
        m_twistEventSource.OnPinchAndTwist.RemoveListener(OnPinchAndTwist);
        m_twistEventSource.OnEndPinchAndTwist.RemoveListener(OnEndPinchAndTwist);
        m_subscribedToTwistEvents = false;
        ResetTwistGesture();
    }

    private void OnStartPinchAndTwist()
    {
        if (!CanDrillDownFromCurrentSelection())
            return;

        m_inTwistGesture = true;
        m_twistConsumed = false;
        LogDebug($"[ProxySetDrillDownController] Twist started for drill-down on {gameObject.name}.");
    }

    private void OnPinchAndTwist(float signedNormalized)
    {
        if (!m_inTwistGesture || m_twistConsumed)
            return;

        if (!IsTwistTowardChild(signedNormalized))
            return;

        m_twistConsumed = true;
        LogDebug($"[ProxySetDrillDownController] Twist -> child view on {gameObject.name} ({signedNormalized:F2}).");
        HandlePress();
    }

    private void OnEndPinchAndTwist()
    {
        ResetTwistGesture();
    }

    private void ResetTwistGesture()
    {
        m_inTwistGesture = false;
        m_twistConsumed = false;
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

    private bool IsTwistTowardChild(float signedNormalized)
    {
        if (Mathf.Abs(signedNormalized) < m_twistThresholdNormalized)
            return false;

        return m_positiveTwistDrillsDown ? signedNormalized > 0f : signedNormalized < 0f;
    }

    private bool IsTwistTowardParent(float signedNormalized)
    {
        if (Mathf.Abs(signedNormalized) < m_twistThresholdNormalized)
            return false;

        return m_positiveTwistDrillsDown ? signedNormalized < 0f : signedNormalized > 0f;
    }

    private void ConfigureChildViewRelay()
    {
        if (m_childrenRootToShow == null)
            return;

        var relay = m_childrenRootToShow.GetComponent<ChildViewGestureRelay>();
        if (relay == null)
            relay = m_childrenRootToShow.AddComponent<ChildViewGestureRelay>();

        relay.Configure(
            this,
            m_twistEventSource,
            m_debugLog,
            m_logger
        );
    }

    private sealed class ChildViewGestureRelay : MonoBehaviour
    {
        private ProxySetDrillDownController m_owner;
        private PinchAndTwistEventSource m_twistEventSource;
        private bool m_debugLog;
        private SharedLogger m_logger;
        private bool m_subscribedToTwistEvents;
        private bool m_inTwistGesture;
        private bool m_twistConsumed;

        public void Configure(
            ProxySetDrillDownController owner,
            PinchAndTwistEventSource twistEventSource,
            bool debugLog,
            SharedLogger logger)
        {
            m_owner = owner;
            m_twistEventSource = twistEventSource;
            m_debugLog = debugLog;
            m_logger = logger;
            RefreshTwistSubscriptions();
        }

        private void OnEnable()
        {
            RefreshTwistSubscriptions();
        }

        private void OnDisable()
        {
            RemoveTwistSubscriptions();
            ResetTwistGesture();
        }

        private void RefreshTwistSubscriptions()
        {
            RemoveTwistSubscriptions();

            if (!isActiveAndEnabled || m_twistEventSource == null)
                return;

            m_twistEventSource.OnStartPinchAndTwist.AddListener(OnStartPinchAndTwist);
            m_twistEventSource.OnPinchAndTwist.AddListener(OnPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.AddListener(OnEndPinchAndTwist);
            m_subscribedToTwistEvents = true;
        }

        private void RemoveTwistSubscriptions()
        {
            if (m_twistEventSource == null || !m_subscribedToTwistEvents)
                return;

            m_twistEventSource.OnStartPinchAndTwist.RemoveListener(OnStartPinchAndTwist);
            m_twistEventSource.OnPinchAndTwist.RemoveListener(OnPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.RemoveListener(OnEndPinchAndTwist);
            m_subscribedToTwistEvents = false;
        }

        private void OnStartPinchAndTwist()
        {
            if (m_owner == null || !m_owner.IsInChildView())
                return;

            m_inTwistGesture = true;
            m_twistConsumed = false;
            LogDebug($"[ProxySetDrillDownController] Twist started for parent return on {m_owner.gameObject.name}.");
        }

        private void OnPinchAndTwist(float signedNormalized)
        {
            if (m_owner == null || !m_owner.IsInChildView())
                return;

            if (!m_inTwistGesture || m_twistConsumed)
                return;

            if (!m_owner.IsTwistTowardParent(signedNormalized))
                return;

            m_twistConsumed = true;
            LogDebug($"[ProxySetDrillDownController] Twist -> parent view from {m_owner.gameObject.name} ({signedNormalized:F2}).");
            m_owner.ReturnToParentView();
        }

        private void OnEndPinchAndTwist()
        {
            ResetTwistGesture();
        }

        private void ResetTwistGesture()
        {
            m_inTwistGesture = false;
            m_twistConsumed = false;
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

    private void UpdateGlobalDrillDownState(bool inChildView)
    {
        if (inChildView)
        {
            if (m_registeredAsChildView)
                return;

            s_activeChildViewCount++;
            m_registeredAsChildView = true;
            return;
        }

        if (!m_registeredAsChildView)
            return;

        s_activeChildViewCount = Mathf.Max(0, s_activeChildViewCount - 1);
        m_registeredAsChildView = false;
    }

    private ProxyLabelManager ResolveOwningLabelManager()
    {
        if (m_cachedLabelManager != null &&
            (m_cachedLabelManager.ContainsLabelsParent(m_levelRootToHideOnDrillDown != null ? m_levelRootToHideOnDrillDown.transform : null) ||
             m_cachedLabelManager.ContainsLabelsParent(m_childrenRootToShow != null ? m_childrenRootToShow.transform : null)))
        {
            return m_cachedLabelManager;
        }

        var managers = FindObjectsByType<ProxyLabelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager == null)
                continue;

            bool managesParent = m_levelRootToHideOnDrillDown != null &&
                manager.ContainsLabelsParent(m_levelRootToHideOnDrillDown.transform);
            bool managesChild = m_childrenRootToShow != null &&
                manager.ContainsLabelsParent(m_childrenRootToShow.transform);
            if (!managesParent && !managesChild)
                continue;

            m_cachedLabelManager = manager;
            return manager;
        }

        return null;
    }

    private bool IsTransitioningBetweenViews()
    {
        var player = GetTransitionPlayer(createIfMissing: false);
        return player != null && player.IsTransitioning;
    }

    private bool TryPlayTransition(
        GameObject outgoingRoot,
        GameObject incomingRoot,
        ProxySetHorizontalTransitionDirection direction,
        System.Action onComplete)
    {
        if (outgoingRoot == null || incomingRoot == null)
            return false;

        var outgoingRect = outgoingRoot.transform as RectTransform;
        var incomingRect = incomingRoot.transform as RectTransform;
        if (outgoingRect == null || incomingRect == null)
            return false;

        if (outgoingRect.parent == null || outgoingRect.parent != incomingRect.parent)
            return false;

        var player = GetTransitionPlayer(createIfMissing: true);
        if (player == null)
            return false;

        incomingRoot.SetActive(true);
        return player.TryPlay(outgoingRect, incomingRect, direction, onComplete);
    }

    private ProxySetHorizontalTransitionPlayer GetTransitionPlayer(bool createIfMissing)
    {
        Transform parentTransform = null;

        if (m_levelRootToHideOnDrillDown != null)
            parentTransform = m_levelRootToHideOnDrillDown.transform.parent;

        if (parentTransform == null && m_childrenRootToShow != null)
            parentTransform = m_childrenRootToShow.transform.parent;

        if (parentTransform == null)
            return null;

        return createIfMissing
            ? ProxySetHorizontalTransitionPlayer.GetOrCreate(parentTransform)
            : ProxySetHorizontalTransitionPlayer.GetOn(parentTransform);
    }

    private void HideParentRoot()
    {
        if (m_levelRootToHideOnDrillDown != null)
            m_levelRootToHideOnDrillDown.SetActive(false);
    }

    private void HideChildRoot()
    {
        if (m_childrenRootToShow == null)
            return;

        var rootTf = m_childrenRootToShow.transform;
        for (int i = 0; i < rootTf.childCount; i++)
        {
            var childTf = rootTf.GetChild(i);
            if (childTf != null)
                childTf.gameObject.SetActive(false);
        }

        m_childrenRootToShow.SetActive(false);
    }
}
