using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages a pre-authored set of proxy labels (children under a parent transform) and exposes which label(s)
/// are currently selected.
///
/// Selection is driven by Unity's EventSystem (currentSelectedGameObject). This manager maps the selected
/// GameObject back to a 0-based index under <see cref="m_labelsParent"/>.
///
/// It also supports an optional selection range override (min..max) for multi-highlight workflows.
/// </summary>
public class ProxyLabelManager : MonoBehaviour
{
    private struct ActiveStateRecord
    {
        public GameObject Target;
        public bool ActiveSelf;
    }

    [Header("Authored labels")]
    [Tooltip("Possible parents whose direct children are proxy label GameObjects. Exactly one should be active in the hierarchy at a time.")]
    [SerializeField] private List<Transform> m_labelParents = new();

    [Header("Debug logging")]
    [Tooltip("Optional shared logger used to log which label index is currently selected.")]
    [SerializeField] private SharedLogger m_logger;
    [Tooltip("If false, selection changes will not be logged even if a logger is assigned.")]
    [SerializeField] private bool m_enableLogging = true;

    private int m_selectionMin;
    private int m_selectionMax;
    private bool m_selectionRangeOverride;
    private int m_runtimeActiveLabelsParentIndex = -1;
    private readonly Dictionary<Transform, List<ActiveStateRecord>> m_authoredChildStates = new();

    private void Awake()
    {
        CacheAuthoredChildStates();
        m_runtimeActiveLabelsParentIndex = FindFirstActiveLabelsParentIndex();
    }

    private void CacheAuthoredChildStates()
    {
        m_authoredChildStates.Clear();

        for (int i = 0; i < m_labelParents.Count; i++)
        {
            var parent = m_labelParents[i];
            if (parent == null)
                continue;

            var records = new List<ActiveStateRecord>();
            var descendants = parent.GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < descendants.Length; j++)
            {
                var descendant = descendants[j];
                if (descendant == null || descendant == parent)
                    continue;

                records.Add(new ActiveStateRecord
                {
                    Target = descendant.gameObject,
                    ActiveSelf = descendant.gameObject.activeSelf
                });
            }

            m_authoredChildStates[parent] = records;
        }
    }

    private void RestoreAuthoredChildStates(Transform parent)
    {
        if (parent == null)
            return;

        if (!m_authoredChildStates.TryGetValue(parent, out var records))
            return;

        for (int i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Target == null)
                continue;

            record.Target.SetActive(record.ActiveSelf);
        }
    }

    public Transform GetActiveLabelsParent()
    {
        int activeIndex = GetActiveLabelsParentIndex();
        return GetLabelsParentAtIndex(activeIndex);
    }

    private int GetActiveLabelsParentIndex()
    {
        if (IsValidLabelsParentIndex(m_runtimeActiveLabelsParentIndex))
        {
            var runtimeParent = GetLabelsParentAtIndex(m_runtimeActiveLabelsParentIndex);
            if (runtimeParent != null && runtimeParent.gameObject.activeInHierarchy)
                return m_runtimeActiveLabelsParentIndex;
        }

        int activeIndex = FindFirstActiveLabelsParentIndex();
        if (activeIndex >= 0)
        {
            m_runtimeActiveLabelsParentIndex = activeIndex;
            return activeIndex;
        }

        if (TryGetSelectedLabelsParentIndex(out int selectedIndex))
        {
            m_runtimeActiveLabelsParentIndex = selectedIndex;
            return selectedIndex;
        }

        for (int i = 0; i < m_labelParents.Count; i++)
        {
            var parent = m_labelParents[i];
            if (parent != null)
            {
                m_runtimeActiveLabelsParentIndex = i;
                return i;
            }
        }
        return -1;
    }

    private Transform GetLabelsParentAtIndex(int index)
    {
        if (index < 0 || index >= m_labelParents.Count)
            return null;
        return m_labelParents[index];
    }

    /// <summary>
    /// Activates the previous label parent in m_labelParents (based on currently active one).
    /// Returns true if a switch occurred.
    /// </summary>
    public bool TrySwitchToPreviousLabelsParent()
    {
        return TrySwitchToPreviousLabelsParent(ProxySetHorizontalTransitionDirection.ToLeft);
    }

    /// <summary>
    /// Activates the next label parent in m_labelParents (based on currently active one).
    /// Returns true if a switch occurred.
    /// </summary>
    public bool TrySwitchToNextLabelsParent()
    {
        return TrySwitchToNextLabelsParent(ProxySetHorizontalTransitionDirection.ToRight);
    }

    public bool TrySwitchToPreviousLabelsParent(ProxySetHorizontalTransitionDirection direction)
    {
        int activeIndex = GetActiveLabelsParentIndex();
        if (activeIndex <= 0)
            return false;

        return TrySwitchToLabelsParentIndex(activeIndex - 1, direction);
    }

    public bool TrySwitchToNextLabelsParent(ProxySetHorizontalTransitionDirection direction)
    {
        int activeIndex = GetActiveLabelsParentIndex();
        if (activeIndex < 0 || activeIndex >= m_labelParents.Count - 1)
            return false;

        return TrySwitchToLabelsParentIndex(activeIndex + 1, direction);
    }

    public bool ContainsLabelsParent(Transform parent)
    {
        return FindBestLabelsParentIndex(parent, m_runtimeActiveLabelsParentIndex) >= 0;
    }

    public void SetActiveLabelsParent(Transform parent)
    {
        int index = FindBestLabelsParentIndex(parent, m_runtimeActiveLabelsParentIndex);
        if (index >= 0)
            m_runtimeActiveLabelsParentIndex = index;
    }

    public bool IsTransitioning
    {
        get
        {
            var activeParent = GetLabelsParentAtIndex(GetActiveLabelsParentIndex());
            if (activeParent == null || activeParent.parent == null)
                return false;

            var player = ProxySetHorizontalTransitionPlayer.GetOn(activeParent.parent);
            return player != null && player.IsTransitioning;
        }
    }

    public int GetLabelCount()
    {
        var parent = GetActiveLabelsParent();
        return parent != null ? parent.childCount : 0;
    }

    /// <summary>
    /// Returns the RectTransform for the label at the given index, or null if out of range.
    /// </summary>
    public RectTransform GetLabelRectTransform(int index)
    {
        var parent = GetActiveLabelsParent();
        if (parent == null)
            return null;

        if (index < 0 || index >= parent.childCount)
            return null;

        return parent.GetChild(index) as RectTransform;
    }

    /// <summary>
    /// Finds the first label whose LabelMarkerBinding contains the given marker index.
    /// Returns its RectTransform, or null if none is found.
    /// </summary>
    public RectTransform GetLabelRectTransformForMarkerIndex(int markerIndex)
    {
        if (markerIndex < 0)
            return null;

        var parent = GetActiveLabelsParent();
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            var binding = child.GetComponent<LabelMarkerBinding>();
            var indices = binding != null ? binding.MarkerIndices : null;
            if (indices == null)
                continue;

            for (int j = 0; j < indices.Count; j++)
            {
                if (indices[j] == markerIndex)
                    return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Index of the currently selected label under m_labelsParent (0-based), or -1 if none.
    /// </summary>
    public int GetSelectedLabelIndex()
    {
        var parent = GetActiveLabelsParent();
        if (parent == null || EventSystem.current == null)
            return -1;

        var current = EventSystem.current.currentSelectedGameObject;
        if (current == null)
            return -1;

        // Accept selection on the child itself or any nested descendant.
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == null) continue;
            if (current == child.gameObject || current.transform.IsChildOf(child))
            {
                LogSelectedIndex(i, child.gameObject.name);
                return i;
            }
        }

        return -1;
    }

    private void LogSelectedIndex(int index, string labelName)
    {
        if (!m_enableLogging || m_logger == null)
            return;

        m_logger.Log($"[ProxyLabelManager] Selected label index: {index}");
    }

    /// <summary>
    /// Set the current single selection to the label at the given index (0-based).
    /// </summary>
    public void SetSelectedLabelByIndex(int index)
    {
        var parent = GetActiveLabelsParent();
        if (parent == null || EventSystem.current == null)
            return;

        int count = parent.childCount;
        if (count <= 0)
            return;

        index = Mathf.Clamp(index, 0, count - 1);
        var go = parent.GetChild(index).gameObject;
        EventSystem.current.SetSelectedGameObject(go);

        var sel = go.GetComponent<Selectable>();
        if (sel != null)
            sel.Select();
    }

    /// <summary>
    /// Set a selection range override (e.g. from twist). Indices are clamped to valid range.
    /// </summary>
    public void SetSelectionRange(int minIndex, int maxIndex)
    {
        m_selectionRangeOverride = true;
        int count = GetLabelCount();
        if (count <= 0) return;
        m_selectionMin = Mathf.Clamp(Mathf.Min(minIndex, maxIndex), 0, count - 1);
        m_selectionMax = Mathf.Clamp(Mathf.Max(minIndex, maxIndex), 0, count - 1);
    }

    /// <summary>
    /// Clears the range override so selection range follows the currently selected label again.
    /// </summary>
    public void ClearSelectionRangeOverride()
    {
        m_selectionRangeOverride = false;
    }

    /// <summary>
    /// Current selection range (0-based). If range override is not active, returns [focus, focus].
    /// If nothing is selected, returns [-1, -1].
    /// </summary>
    public void GetSelectionRange(out int minIndex, out int maxIndex)
    {
        if (!m_selectionRangeOverride)
        {
            int focus = GetSelectedLabelIndex();
            if (focus < 0)
            {
                minIndex = maxIndex = -1;
                return;
            }

            m_selectionMin = m_selectionMax = focus;
        }

        minIndex = m_selectionMin;
        maxIndex = m_selectionMax;
    }

    private bool TrySwitchToLabelsParentIndex(int targetIndex, ProxySetHorizontalTransitionDirection direction)
    {
        if (IsTransitioning)
            return false;

        if (!IsValidLabelsParentIndex(targetIndex))
            return false;

        int activeIndex = GetActiveLabelsParentIndex();
        var current = GetLabelsParentAtIndex(activeIndex);
        var target = GetLabelsParentAtIndex(targetIndex);
        if (target == null)
            return false;

        RestoreAuthoredChildStates(target);
        target.gameObject.SetActive(true);
        m_runtimeActiveLabelsParentIndex = targetIndex;

        if (current == null || current == target)
            return true;

        if (TryPlayHorizontalTransition(current, target, direction))
            return true;

        current.gameObject.SetActive(false);
        return true;
    }

    private bool TryPlayHorizontalTransition(
        Transform outgoing,
        Transform incoming,
        ProxySetHorizontalTransitionDirection direction)
    {
        var outgoingRect = outgoing as RectTransform;
        var incomingRect = incoming as RectTransform;
        if (outgoingRect == null || incomingRect == null)
            return false;

        if (outgoingRect.parent == null || outgoingRect.parent != incomingRect.parent)
            return false;

        var player = ProxySetHorizontalTransitionPlayer.GetOrCreate(outgoingRect.parent);
        if (player == null)
            return false;

        return player.TryPlay(
            outgoingRect,
            incomingRect,
            direction,
            () =>
            {
                if (outgoing != null)
                    outgoing.gameObject.SetActive(false);
            }
        );
    }

    private int FindFirstActiveLabelsParentIndex()
    {
        for (int i = 0; i < m_labelParents.Count; i++)
        {
            var parent = m_labelParents[i];
            if (parent != null && parent.gameObject.activeInHierarchy)
                return i;
        }

        return -1;
    }

    private bool TryGetSelectedLabelsParentIndex(out int selectedIndex)
    {
        selectedIndex = -1;

        var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (current == null)
            return false;

        int bestIndex = -1;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < m_labelParents.Count; i++)
        {
            var parent = m_labelParents[i];
            if (parent == null)
                continue;

            if (current != parent.gameObject && !current.transform.IsChildOf(parent))
                continue;

            if (i == m_runtimeActiveLabelsParentIndex)
            {
                selectedIndex = i;
                return true;
            }

            int distance = m_runtimeActiveLabelsParentIndex >= 0
                ? Mathf.Abs(i - m_runtimeActiveLabelsParentIndex)
                : i;

            if (bestIndex >= 0 && distance >= bestDistance)
                continue;

            bestIndex = i;
            bestDistance = distance;
        }

        if (bestIndex < 0)
            return false;

        selectedIndex = bestIndex;
        return true;
    }

    private int FindBestLabelsParentIndex(Transform parent, int preferredIndex)
    {
        if (parent == null)
            return -1;

        int bestIndex = -1;
        int bestDistance = int.MaxValue;

        for (int i = 0; i < m_labelParents.Count; i++)
        {
            if (m_labelParents[i] != parent)
                continue;

            if (i == preferredIndex)
                return i;

            int distance = preferredIndex >= 0 ? Mathf.Abs(i - preferredIndex) : i;
            if (bestIndex >= 0 && distance >= bestDistance)
                continue;

            bestIndex = i;
            bestDistance = distance;
        }

        return bestIndex;
    }

    private bool IsValidLabelsParentIndex(int index)
    {
        return index >= 0 && index < m_labelParents.Count && m_labelParents[index] != null;
    }
}
