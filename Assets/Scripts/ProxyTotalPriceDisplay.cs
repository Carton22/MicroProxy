using TMPro;
using UnityEngine;

/// <summary>
/// Displays the running total of all proxy labels whose price is currently shown.
/// </summary>
[DisallowMultipleComponent]
public class ProxyTotalPriceDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text m_totalText;
    [SerializeField] private GameObject m_infoUiRoot;
    [SerializeField] private string m_prefix = "Total";
    [SerializeField] private string m_currencyPrefix = "$";

    private void Reset()
    {
        if (m_totalText == null)
            m_totalText = GetComponentInChildren<TMP_Text>(true);
        if (m_infoUiRoot == null)
            m_infoUiRoot = m_totalText != null ? m_totalText.gameObject : gameObject;
    }

    private void OnEnable()
    {
        ProxyLabelPriceRandomizeOnClick.OnVisiblePricesTotalChanged += HandleTotalChanged;
        HandleTotalChanged(ProxyLabelPriceRandomizeOnClick.GetVisiblePricesTotal());
    }

    private void OnDisable()
    {
        ProxyLabelPriceRandomizeOnClick.OnVisiblePricesTotalChanged -= HandleTotalChanged;
    }

    private void HandleTotalChanged(int total)
    {
        int visibleCount = ProxyLabelPriceRandomizeOnClick.GetVisiblePriceCount();
        bool shouldShowInfo = visibleCount > 0;
        SetInfoVisible(shouldShowInfo);

        if (!shouldShowInfo)
            return;

        if (m_totalText == null)
            m_totalText = GetComponentInChildren<TMP_Text>(true);
        if (m_totalText == null)
            return;

        m_totalText.text = $"{m_prefix}: {m_currencyPrefix}{Mathf.Max(0, total)}";
    }

    private void SetInfoVisible(bool visible)
    {
        if (m_infoUiRoot == null)
            return;

        // Prefer true GameObject hide/show for the whole UI when safe.
        if (m_infoUiRoot != gameObject)
        {
            if (m_infoUiRoot.activeSelf != visible)
                m_infoUiRoot.SetActive(visible);
        }
        else
        {
            // If info root is this same GameObject, avoid disabling this script's host.
            var group = m_infoUiRoot.GetComponent<CanvasGroup>();
            if (group == null)
                group = m_infoUiRoot.AddComponent<CanvasGroup>();

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        // Fallback for TMP variants that may ignore parent visual state.
        if (m_totalText != null)
            m_totalText.enabled = visible;
    }
}

