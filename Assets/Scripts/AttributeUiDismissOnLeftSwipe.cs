using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to the AttributeUI root. When the EventSystem selection is on a label (or any selectable)
/// under this object and the user triggers the configured dismiss move (left in older vertical scenes,
/// up in horizontal Study5-style scenes), AttributeUI is hidden and selection moves back to the left column
/// (e.g. ProxyUI).
/// </summary>
[DisallowMultipleComponent]
public class AttributeUiDismissOnLeftSwipe : MonoBehaviour
{
    [Tooltip("Left column labels root (e.g. ProxyUI). Used to pick the selectable to focus after close.")]
    [SerializeField] private Transform m_leftColumnLabelsParent;

    [Tooltip("Optional. If set with ProxyLabelManager, restores the active labels parent to the left column when closing.")]
    [SerializeField] private Transform m_leftLabelsParentForProxyManager;

    [SerializeField] private ProxyLabelManager m_proxyLabelManager;

    [SerializeField] private bool m_selectFirstInLeftColumn = true;

    private void Reset()
    {
        m_proxyLabelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    /// <summary>
    /// Invoked from <see cref="UINavigator.MoveLeft"/> (and vector-based left moves). Returns true if the gesture was consumed.
    /// </summary>
    public bool TryHandleMoveLeft()
    {
        return TryDismissAttributeUi();
    }

    /// <summary>
    /// Invoked from <see cref="UINavigator.MoveUp"/> for horizontal proxy rows that open AttributeUI on MoveDown.
    /// Returns true if the gesture was consumed.
    /// </summary>
    public bool TryHandleMoveUp()
    {
        return TryDismissAttributeUi();
    }

    private bool TryDismissAttributeUi()
    {
        if (!gameObject.activeSelf)
            return false;

        if (m_proxyLabelManager == null)
            m_proxyLabelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (m_proxyLabelManager != null && m_proxyLabelManager.IsTransitioning)
            return false;

        if (EventSystem.current == null)
            return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        if (selected != gameObject && !selected.transform.IsChildOf(transform))
            return false;

        var leftLabelsParentToRestore = ResolveLeftLabelsParentToRestore();
        var leftColumnFocusRoot = leftLabelsParentToRestore != null
            ? leftLabelsParentToRestore
            : (m_leftColumnLabelsParent != null ? m_leftColumnLabelsParent : m_leftLabelsParentForProxyManager);
        var leftColumnTarget = FindFirstSelectableUnder(leftColumnFocusRoot);
        if (leftColumnTarget == null && m_leftColumnLabelsParent != null && leftColumnFocusRoot != m_leftColumnLabelsParent)
            leftColumnTarget = FindFirstSelectableUnder(m_leftColumnLabelsParent);
        if (leftColumnFocusRoot != null && leftColumnTarget == null)
            return true;

        gameObject.SetActive(false);

        if (m_proxyLabelManager != null && leftLabelsParentToRestore != null)
            m_proxyLabelManager.SetActiveLabelsParent(leftLabelsParentToRestore);

        Canvas.ForceUpdateCanvases();

        var activeParent = m_proxyLabelManager != null ? m_proxyLabelManager.GetActiveLabelsParent() : null;
        var refreshRoot = activeParent != null ? activeParent : m_leftColumnLabelsParent;
        if (refreshRoot != null)
        {
            var scroller = refreshRoot.GetComponent<ProxyLabelHorizonScroller>();
            if (scroller == null)
                scroller = refreshRoot.GetComponentInParent<ProxyLabelHorizonScroller>(true);
            if (scroller == null)
                scroller = refreshRoot.GetComponentInChildren<ProxyLabelHorizonScroller>(true);
            if (scroller != null)
                scroller.ForceRefreshNow();
        }

        if (m_selectFirstInLeftColumn && leftColumnTarget != null)
        {
            Select(leftColumnTarget);
        }

        return true;
    }

    private Transform ResolveLeftLabelsParentToRestore()
    {
        var activeParent = m_proxyLabelManager != null ? m_proxyLabelManager.GetActiveLabelsParent() : null;
        if (activeParent != null && activeParent != transform && !activeParent.IsChildOf(transform))
            return activeParent;

        if (m_leftLabelsParentForProxyManager != null)
            return m_leftLabelsParentForProxyManager;

        return m_leftColumnLabelsParent;
    }

    private static GameObject FindFirstSelectableUnder(Transform root)
    {
        if (root == null)
            return null;

        var selectables = root.GetComponentsInChildren<Selectable>(false);
        for (int i = 0; i < selectables.Length; i++)
        {
            var s = selectables[i];
            if (s != null && s.IsInteractable() && s.gameObject.activeInHierarchy)
                return s.gameObject;
        }

        return null;
    }

    private static void Select(GameObject go)
    {
        if (go == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(go);
        var sel = go.GetComponent<Selectable>();
        if (sel != null)
            sel.Select();
    }
}
