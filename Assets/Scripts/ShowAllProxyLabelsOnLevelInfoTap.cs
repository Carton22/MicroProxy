using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach to the LevelInfo button.
/// On tap/submit, switches to ProxyUI and shows all proxy labels.
/// </summary>
public class ShowAllProxyLabelsOnLevelInfoTap : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private GameObject m_proxyUiRoot;
    [SerializeField] private Transform m_proxyLabelsRoot;
    [SerializeField] private bool m_selectFirstProxyLabel = true;

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ShowAllProxyLabels();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        ShowAllProxyLabels();
    }

    public void ShowAllProxyLabels()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (m_proxyUiRoot != null)
            m_proxyUiRoot.SetActive(true);

        if (m_labelManager != null)
        {
            m_labelManager.ClearVisibleLabelsFilter();

            if (m_proxyLabelsRoot != null)
                m_labelManager.SetActiveLabelsParent(m_proxyLabelsRoot);
        }

        Canvas.ForceUpdateCanvases();

        if (m_proxyLabelsRoot != null)
        {
            var scroller = m_proxyLabelsRoot.GetComponent<ProxyLabelHorizonScroller>();
            if (scroller == null)
                scroller = m_proxyLabelsRoot.GetComponentInParent<ProxyLabelHorizonScroller>(true);
            if (scroller != null)
                scroller.ForceRefreshNow();
        }

        if (m_selectFirstProxyLabel && m_proxyLabelsRoot != null && EventSystem.current != null)
        {
            var firstSelectable = m_proxyLabelsRoot.GetComponentInChildren<Selectable>(false);
            if (firstSelectable != null)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
                firstSelectable.Select();
            }
        }
    }
}
