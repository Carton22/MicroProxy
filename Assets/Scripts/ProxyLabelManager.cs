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
    [Tooltip("Parent whose direct children are the proxy label GameObjects (authored ahead of time).")]
    [SerializeField] private Transform m_labelsParent;

    [Header("Debug logging")]
    [Tooltip("Optional shared logger used to log which label index is currently selected.")]
    [SerializeField] private SharedLogger m_logger;
    [Tooltip("If false, selection changes will not be logged even if a logger is assigned.")]
    [SerializeField] private bool m_enableLogging = true;

    private int m_selectionMin;
    private int m_selectionMax;
    private bool m_selectionRangeOverride;

    public int GetLabelCount() => m_labelsParent != null ? m_labelsParent.childCount : 0;

    /// <summary>
    /// Returns the RectTransform for the label at the given index, or null if out of range.
    /// </summary>
    public RectTransform GetLabelRectTransform(int index)
    {
        if (m_labelsParent == null)
            return null;

        if (index < 0 || index >= m_labelsParent.childCount)
            return null;

        return m_labelsParent.GetChild(index) as RectTransform;
    }

    /// <summary>
    /// Index of the currently selected label under m_labelsParent (0-based), or -1 if none.
    /// </summary>
    public int GetSelectedLabelIndex()
    {
        if (m_labelsParent == null || EventSystem.current == null)
            return -1;

        var current = EventSystem.current.currentSelectedGameObject;
        if (current == null)
            return -1;

        // Accept selection on the child itself or any nested descendant.
        for (int i = 0; i < m_labelsParent.childCount; i++)
        {
            var child = m_labelsParent.GetChild(i);
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
        if (m_labelsParent == null || EventSystem.current == null)
            return;

        int count = m_labelsParent.childCount;
        if (count <= 0)
            return;

        index = Mathf.Clamp(index, 0, count - 1);
        var go = m_labelsParent.GetChild(index).gameObject;
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

