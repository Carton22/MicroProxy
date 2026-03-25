using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone helper for proxy-label twist scrolling.
/// While a pinch-and-twist gesture is active, every label that passes through the center
/// selection position toggles its selected state and is tinted red while selected.
/// </summary>
[DisallowMultipleComponent]
public class ProxyTwistToggleLabelsPassedCenter : MonoBehaviour
{
    private sealed class VisualCache
    {
        public Button Button;
        public Graphic TargetGraphic;
        public ColorBlock OriginalButtonColors;
        public bool HasOriginalButtonColors;
        public Color OriginalGraphicColor;
        public bool HasOriginalGraphicColor;
    }

    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private PinchAndTwistEventSource m_twistEventSource;

    [Header("Visuals")]
    [SerializeField] private Color m_selectedColor = new Color(1f, 0f, 0f, 0.84f);

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;

    private readonly HashSet<Transform> m_selectedLabels = new();
    private readonly Dictionary<Transform, VisualCache> m_visualCache = new();

    private bool m_inGesture;
    private Transform m_gestureParent;
    private int m_lastCenteredVisibleIndex = -1;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (m_twistEventSource != null)
        {
            m_twistEventSource.OnStartPinchAndTwist.AddListener(OnStartPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.AddListener(OnEndPinchAndTwist);
        }
    }

    private void OnDisable()
    {
        if (m_twistEventSource != null)
        {
            m_twistEventSource.OnStartPinchAndTwist.RemoveListener(OnStartPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.RemoveListener(OnEndPinchAndTwist);
        }

        m_inGesture = false;
        m_gestureParent = null;
        m_lastCenteredVisibleIndex = -1;
    }

    private void LateUpdate()
    {
        if (!m_inGesture || m_labelManager == null)
            return;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        if (activeParent == null)
            return;

        if (activeParent != m_gestureParent)
        {
            m_gestureParent = activeParent;
            m_lastCenteredVisibleIndex = m_labelManager.GetSelectedLabelIndex();
            return;
        }

        int currentCenteredVisibleIndex = m_labelManager.GetSelectedLabelIndex();
        if (currentCenteredVisibleIndex < 0 || currentCenteredVisibleIndex == m_lastCenteredVisibleIndex)
            return;

        ToggleLabelsPassedBetween(activeParent, m_lastCenteredVisibleIndex, currentCenteredVisibleIndex);
        m_lastCenteredVisibleIndex = currentCenteredVisibleIndex;
    }

    private void ResolveReferences()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_twistEventSource == null)
            m_twistEventSource = GetComponent<PinchAndTwistEventSource>();
        if (m_twistEventSource == null)
            m_twistEventSource = FindFirstObjectByType<PinchAndTwistEventSource>();
    }

    private void OnStartPinchAndTwist()
    {
        ResolveReferences();
        if (m_labelManager == null)
            return;

        m_gestureParent = m_labelManager.GetActiveLabelsParent();
        m_lastCenteredVisibleIndex = m_labelManager.GetSelectedLabelIndex();
        m_inGesture = m_gestureParent != null && m_lastCenteredVisibleIndex >= 0;

        if (m_debugLog)
        {
            string parentName = m_gestureParent != null ? m_gestureParent.name : "<null>";
            Debug.Log($"[ProxyTwistToggleLabelsPassedCenter] Twist started. parent={parentName} centerIndex={m_lastCenteredVisibleIndex}");
        }
    }

    private void OnEndPinchAndTwist()
    {
        if (m_debugLog)
            Debug.Log("[ProxyTwistToggleLabelsPassedCenter] Twist ended.");

        m_inGesture = false;
        m_gestureParent = null;
        m_lastCenteredVisibleIndex = -1;
    }

    private void ToggleLabelsPassedBetween(Transform activeParent, int fromVisibleIndex, int toVisibleIndex)
    {
        if (activeParent == null || fromVisibleIndex < 0 || toVisibleIndex < 0 || fromVisibleIndex == toVisibleIndex)
            return;

        int step = toVisibleIndex > fromVisibleIndex ? 1 : -1;
        for (int visibleIndex = fromVisibleIndex + step; ; visibleIndex += step)
        {
            var child = GetVisibleChildAtIndex(activeParent, visibleIndex);
            if (child != null)
                ToggleLabel(child);

            if (visibleIndex == toVisibleIndex)
                break;
        }
    }

    private Transform GetVisibleChildAtIndex(Transform parent, int targetVisibleIndex)
    {
        if (parent == null || targetVisibleIndex < 0)
            return null;

        int visibleIndex = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            if (visibleIndex == targetVisibleIndex)
                return child;

            visibleIndex++;
        }

        return null;
    }

    private void ToggleLabel(Transform labelRoot)
    {
        if (labelRoot == null)
            return;

        bool isSelected = m_selectedLabels.Contains(labelRoot);
        if (isSelected)
            m_selectedLabels.Remove(labelRoot);
        else
            m_selectedLabels.Add(labelRoot);

        ApplyVisualState(labelRoot, !isSelected);

        if (m_debugLog)
            Debug.Log($"[ProxyTwistToggleLabelsPassedCenter] Toggled {labelRoot.name} selected={!isSelected}");
    }

    private void ApplyVisualState(Transform labelRoot, bool selected)
    {
        var cache = GetOrCreateVisualCache(labelRoot);
        if (cache == null)
            return;

        if (cache.Button != null && cache.HasOriginalButtonColors)
        {
            if (selected)
            {
                var colors = cache.OriginalButtonColors;
                colors.highlightedColor = m_selectedColor;
                colors.pressedColor = m_selectedColor;
                colors.selectedColor = m_selectedColor;
                cache.Button.colors = colors;
            }
            else
            {
                cache.Button.colors = cache.OriginalButtonColors;
            }
        }

        if (cache.TargetGraphic != null)
            cache.TargetGraphic.color = selected ? m_selectedColor : cache.OriginalGraphicColor;
    }

    private VisualCache GetOrCreateVisualCache(Transform labelRoot)
    {
        if (labelRoot == null)
            return null;

        if (m_visualCache.TryGetValue(labelRoot, out var cache))
            return cache;

        cache = new VisualCache
        {
            Button = labelRoot.GetComponent<Button>(),
            TargetGraphic = null
        };

        if (cache.Button == null)
            cache.Button = labelRoot.GetComponentInChildren<Button>(true);

        if (cache.Button != null)
        {
            cache.TargetGraphic = cache.Button.targetGraphic;
            cache.OriginalButtonColors = cache.Button.colors;
            cache.HasOriginalButtonColors = true;
        }

        if (cache.TargetGraphic == null)
            cache.TargetGraphic = labelRoot.GetComponent<Graphic>();
        if (cache.TargetGraphic == null)
            cache.TargetGraphic = labelRoot.GetComponentInChildren<Graphic>(true);

        if (cache.TargetGraphic != null)
        {
            cache.OriginalGraphicColor = cache.TargetGraphic.color;
            cache.HasOriginalGraphicColor = true;
        }

        m_visualCache[labelRoot] = cache;
        return cache;
    }
}
