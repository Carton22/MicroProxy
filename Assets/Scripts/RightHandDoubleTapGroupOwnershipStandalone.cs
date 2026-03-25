using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Quick scene-specific fix for Study6 ownership grouping.
/// Uses hardcoded Attribute-Ownership marker bindings from the current scene and
/// reorders the active ProxyLabelManager labels on right-hand middle-finger double tap.
/// </summary>
public class RightHandDoubleTapGroupOwnershipStandalone : MonoBehaviour
{
    private struct ChildOrderRecord
    {
        public Transform Child;
        public int SiblingIndex;
    }

    [Header("Scene references")]
    [SerializeField] private OVRHand m_rightHand;
    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private SocketManager m_socketManager;
    [SerializeField] private Transform m_proxyLabelsRoot;

    [Header("Pinch settings")]
    [Range(0f, 1f)]
    [SerializeField] private float m_pinchStrengthThreshold = 0.7f;
    [SerializeField] private float m_doubleTapMaxIntervalSeconds = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;
    [SerializeField] private SharedLogger m_logger;

    // Attribute-Ownership mapping from Study6_Icon_Micro_Partial_Makerspace.unity:
    // OwnerA option markers: 2, 3, 9, 5
    // OwnerB option markers: 4, 5, 10, 22
    // OwnerC option markers: 1, 6, 7, 8, 11
    private static readonly int[][] s_ownerMarkerGroups =
    {
        new[] { 0, 9, 10, 16 },
        new[] { 1, 5, 6, 11, 15 },
        new[] { 2, 8, 14, 17 },
        new[] { 3, 12 },
        new[] { 4, 18 },
        new[] { 7, 13, 19 }
    };

    private static readonly string[] s_ownerNames = { "CS", "EE", "HCI", "Bio", "Chem", "Rob" };

    private bool m_isPinching;
    private float m_lastTapTime = -1f;
    private Transform m_lastGroupedParent;
    private bool m_isGrouped;
    private readonly List<ChildOrderRecord> m_initialOrderBuffer = new();

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (m_socketManager != null)
            m_socketManager.OnGestureSignalReceived += OnGestureSignal;
    }

    private void OnDisable()
    {
        if (m_socketManager != null)
            m_socketManager.OnGestureSignalReceived -= OnGestureSignal;
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();

        if (m_rightHand == null || m_labelManager == null)
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
                bool grouped = GroupByHardcodedOwnership();
                Log(grouped
                    ? "[RightHandDoubleTapGroupOwnershipStandalone] grouped by hardcoded ownership mapping."
                    : "[RightHandDoubleTapGroupOwnershipStandalone] grouping failed.");
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

    private void ResolveReferences()
    {
        if (m_rightHand == null)
            m_rightHand = GetComponent<OVRHand>();
        if (m_rightHand == null)
            m_rightHand = FindFirstObjectByType<OVRHand>();

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_socketManager == null)
            m_socketManager = FindFirstObjectByType<SocketManager>();
        if (m_proxyLabelsRoot == null)
            m_proxyLabelsRoot = FindProxyLabelsRoot();
    }

    private void OnGestureSignal(string gestureType)
    {
        if (string.IsNullOrWhiteSpace(gestureType))
            return;

        string normalized = gestureType.ToLowerInvariant();
        if (normalized != "double_tap" && normalized != "doubletap" && normalized != "taptap")
            return;

        RemoteDoubleTap();
    }

    public void RemoteDoubleTap()
    {
        bool grouped = GroupByHardcodedOwnership();
        Log(grouped
            ? "[RightHandDoubleTapGroupOwnershipStandalone] remote double tap handled."
            : "[RightHandDoubleTapGroupOwnershipStandalone] remote double tap failed.");
    }

    private bool GroupByHardcodedOwnership()
    {
        if (m_labelManager == null)
            return false;

        ShowAllProxyLabels();

        var activeParent = m_labelManager.GetActiveLabelsParent();
        if (activeParent == null || activeParent.childCount == 0)
            return false;

        if (m_isGrouped && activeParent == m_lastGroupedParent)
            return RestoreInitialOrder(activeParent);

        CacheInitialOrder(activeParent);

        var grouped = new List<Transform>(activeParent.childCount);
        var used = new HashSet<Transform>();

        for (int groupIndex = 0; groupIndex < s_ownerMarkerGroups.Length; groupIndex++)
        {
            var markers = s_ownerMarkerGroups[groupIndex];

            for (int i = 0; i < activeParent.childCount; i++)
            {
                var child = activeParent.GetChild(i);
                if (child == null || used.Contains(child))
                    continue;

                if (!HasAnyMarker(child, markers))
                    continue;

                grouped.Add(child);
                used.Add(child);
            }
        }

        for (int i = 0; i < activeParent.childCount; i++)
        {
            var child = activeParent.GetChild(i);
            if (child == null || used.Contains(child))
                continue;

            grouped.Add(child);
        }

        if (grouped.Count == 0)
            return false;

        for (int i = 0; i < grouped.Count; i++)
            grouped[i].SetSiblingIndex(i);

        PreserveSelectionOrSelectFirst(activeParent);
        RefreshScroller(activeParent);
        m_lastGroupedParent = activeParent;
        m_isGrouped = true;
        return true;
    }

    private void ShowAllProxyLabels()
    {
        if (m_labelManager == null)
            return;

        m_labelManager.ClearVisibleLabelsFilter();

        if (m_proxyLabelsRoot != null)
            m_labelManager.SetActiveLabelsParent(m_proxyLabelsRoot);
    }

    private void CacheInitialOrder(Transform activeParent)
    {
        m_initialOrderBuffer.Clear();

        if (activeParent == null)
            return;

        for (int i = 0; i < activeParent.childCount; i++)
        {
            var child = activeParent.GetChild(i);
            if (child == null)
                continue;

            m_initialOrderBuffer.Add(new ChildOrderRecord
            {
                Child = child,
                SiblingIndex = i
            });
        }
    }

    private bool RestoreInitialOrder(Transform activeParent)
    {
        if (activeParent == null || m_initialOrderBuffer.Count == 0)
            return false;

        for (int i = 0; i < m_initialOrderBuffer.Count; i++)
        {
            var record = m_initialOrderBuffer[i];
            if (record.Child == null || record.Child.parent != activeParent)
                continue;

            record.Child.SetSiblingIndex(record.SiblingIndex);
        }

        PreserveSelectionOrSelectFirst(activeParent);
        RefreshScroller(activeParent);
        m_isGrouped = false;
        return true;
    }

    private static bool HasAnyMarker(Transform root, IReadOnlyList<int> markerGroup)
    {
        if (root == null || markerGroup == null || markerGroup.Count == 0)
            return false;

        var bindings = root.GetComponentsInChildren<LabelMarkerBinding>(true);
        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            var indices = binding != null ? binding.MarkerIndices : null;
            if (indices == null)
                continue;

            for (int j = 0; j < indices.Count; j++)
            {
                int marker = indices[j];
                for (int k = 0; k < markerGroup.Count; k++)
                {
                    if (marker == markerGroup[k])
                        return true;
                }
            }
        }

        return false;
    }

    private static void PreserveSelectionOrSelectFirst(Transform activeParent)
    {
        if (EventSystem.current == null || activeParent == null)
            return;

        var selected = EventSystem.current.currentSelectedGameObject;
        bool selectionInsideRoot = selected != null &&
            (selected == activeParent.gameObject || selected.transform.IsChildOf(activeParent));

        if (selectionInsideRoot)
            return;

        for (int i = 0; i < activeParent.childCount; i++)
        {
            var child = activeParent.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            EventSystem.current.SetSelectedGameObject(child.gameObject);
            return;
        }
    }

    private static void RefreshScroller(Transform activeParent)
    {
        var scroller = activeParent.GetComponent<ProxyLabelHorizonScroller>();
        if (scroller == null)
            scroller = activeParent.GetComponentInParent<ProxyLabelHorizonScroller>(true);

        if (scroller != null)
            scroller.ForceRefreshNow();
    }

    private Transform FindProxyLabelsRoot()
    {
        if (m_labelManager != null)
        {
            var activeParent = m_labelManager.GetActiveLabelsParent();
            if (activeParent != null && activeParent.name.IndexOf("proxyui", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return activeParent;
        }

        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var candidate = allTransforms[i];
            if (candidate == null || candidate.name.IndexOf("proxyui", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (m_labelManager != null && !m_labelManager.ContainsLabelsParent(candidate))
                continue;

            return candidate;
        }

        return null;
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
