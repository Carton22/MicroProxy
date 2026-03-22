using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to the AttributeUI root. When the EventSystem selection is on a label (or any selectable)
/// under this object and the user moves left (e.g. UINavigator swipe left), AttributeUI is hidden and
/// selection moves back to the left column (e.g. ProxyUI).
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
        if (!gameObject.activeSelf)
            return false;

        if (ProxySetDrillDownController.IsAnyDrillDownChildViewActive)
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

        gameObject.SetActive(false);

        if (m_proxyLabelManager != null && m_leftLabelsParentForProxyManager != null)
            m_proxyLabelManager.SetActiveLabelsParent(m_leftLabelsParentForProxyManager);

        Canvas.ForceUpdateCanvases();

        if (m_selectFirstInLeftColumn && m_leftColumnLabelsParent != null)
        {
            var target = FindFirstSelectableUnder(m_leftColumnLabelsParent);
            if (target != null)
                Select(target);
        }

        return true;
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
