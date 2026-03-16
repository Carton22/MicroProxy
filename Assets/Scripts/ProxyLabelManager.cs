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

    public Transform GetActiveLabelsParent()
    {
        for (int i = 0; i < m_labelParents.Count; i++)
        {
            var parent = m_labelParents[i];
            if (parent != null && parent.gameObject.activeInHierarchy)
                return parent;
        }
        return null;
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
}

