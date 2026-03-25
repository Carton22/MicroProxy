using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to the AttributeUI root. When the EventSystem selection is on a label (or any selectable)
/// under this object and the user triggers the configured dismiss gesture, AttributeUI is hidden and
/// selection moves back to the left column (for example ProxyUI). The caller decides which gesture
/// should invoke dismissal for the current UI mode.
/// </summary>
[DisallowMultipleComponent]
public class AttributeUiDismissOnSwipe : MonoBehaviour
{
    [Tooltip("Left column labels root (e.g. ProxyUI). Used to pick the selectable to focus after close.")]
    [SerializeField] private Transform m_leftColumnLabelsParent;

    [Tooltip("Optional. If set with ProxyLabelManager, restores the active labels parent to the left column when closing.")]
    [SerializeField] private Transform m_leftLabelsParentForProxyManager;

    [SerializeField] private ProxyLabelManager m_proxyLabelManager;

    [SerializeField] private bool m_selectFirstInLeftColumn = true;

    private GameObject m_rememberedLeftColumnSelection;
    private int m_rememberedLeftColumnIndex = -1;

    private void Reset()
    {
        m_proxyLabelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    public bool TryHandleMoveLeft()
    {
        return TryHandleDismissGesture();
    }

    public bool TryHandleMoveUp()
    {
        return TryHandleDismissGesture();
    }

    public bool TryHandleDismissGesture()
    {
        return TryDismissAttributeUi();
    }

    public void RememberLeftColumnSelection(GameObject selected, Transform leftColumnParent)
    {
        m_rememberedLeftColumnSelection = null;
        m_rememberedLeftColumnIndex = -1;

        if (selected == null || leftColumnParent == null)
            return;

        if (selected != leftColumnParent.gameObject && !selected.transform.IsChildOf(leftColumnParent))
            return;

        m_rememberedLeftColumnSelection = selected;
        m_rememberedLeftColumnIndex = GetVisibleChildIndexUnder(leftColumnParent, selected.transform);
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
        var leftColumnTarget = ResolveRememberedLeftColumnTarget(leftColumnFocusRoot);
        if (leftColumnTarget == null)
            leftColumnTarget = FindFirstSelectableUnder(leftColumnFocusRoot);
        if (leftColumnTarget == null && m_leftColumnLabelsParent != null && leftColumnFocusRoot != m_leftColumnLabelsParent)
            leftColumnTarget = FindFirstSelectableUnder(m_leftColumnLabelsParent);
        if (leftColumnFocusRoot != null && leftColumnTarget == null)
            return true;

        gameObject.SetActive(false);

        if (m_proxyLabelManager != null && leftLabelsParentToRestore != null)
            m_proxyLabelManager.SetActiveLabelsParent(leftLabelsParentToRestore);

        Canvas.ForceUpdateCanvases();

        bool restoredRememberedSelection = false;
        if (m_selectFirstInLeftColumn)
            restoredRememberedSelection = TryRestoreRememberedLeftColumnSelection(leftColumnFocusRoot);

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

        if (m_selectFirstInLeftColumn && !restoredRememberedSelection && leftColumnTarget != null)
            Select(leftColumnTarget);

        ClearRememberedLeftColumnSelection();
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

    private GameObject ResolveRememberedLeftColumnTarget(Transform root)
    {
        if (root == null || m_rememberedLeftColumnSelection == null)
            return null;

        return m_rememberedLeftColumnSelection == root.gameObject || m_rememberedLeftColumnSelection.transform.IsChildOf(root)
            ? m_rememberedLeftColumnSelection
            : null;
    }

    private bool TryRestoreRememberedLeftColumnSelection(Transform root)
    {
        if (root == null)
            return false;

        var rememberedTarget = ResolveRememberedLeftColumnTarget(root);
        if (rememberedTarget != null && rememberedTarget.activeInHierarchy)
        {
            Select(rememberedTarget);
            return true;
        }

        if (m_proxyLabelManager == null || m_rememberedLeftColumnIndex < 0)
            return false;

        var activeParent = m_proxyLabelManager.GetActiveLabelsParent();
        if (activeParent != root)
            return false;

        m_proxyLabelManager.SetSelectedLabelByIndex(m_rememberedLeftColumnIndex);
        return EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null;
    }

    private void ClearRememberedLeftColumnSelection()
    {
        m_rememberedLeftColumnSelection = null;
        m_rememberedLeftColumnIndex = -1;
    }

    private static int GetVisibleChildIndexUnder(Transform root, Transform selected)
    {
        if (root == null || selected == null)
            return -1;

        int visibleIndex = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            if (selected == child || selected.IsChildOf(child))
                return visibleIndex;

            visibleIndex++;
        }

        return -1;
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
