using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Replaces label text with a random dollar amount on click. Uses the first <see cref="TMP_Text"/> under this instance.
/// Prefer wiring through the <see cref="Button"/> (same GameObject); <see cref="IPointerClickHandler"/> is a fallback when there is no Button.
/// </summary>
public class ProxyLabelPriceRandomizeOnClick : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private TMP_Text m_text;

    [SerializeField] private int m_minDollars = 10;
    [SerializeField] private int m_maxDollars = 50;

    [SerializeField] private string m_currencyPrefix = "$";

    [Tooltip("If true, ignore the second click of a double-click (pointer path only).")]
    [SerializeField] private bool m_ignoreSecondClickOfDoubleTap = true;

    private Button m_button;
    private bool m_hasAssignedPrice;
    private int m_assignedDollars;
    private string m_cachedBaseLabel;
    private bool m_priceVisible;

    private void Awake()
    {
        EnsureLabelText();
        m_button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (m_button == null)
            m_button = GetComponent<Button>();

        if (m_button != null)
            m_button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDisable()
    {
        if (m_button != null)
            m_button.onClick.RemoveListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        Debug.Log("OnButtonClicked");
        Debug.Log("Try to apply random price " + m_hasAssignedPrice);
        TryApplyRandomPrice();
    }

    private void EnsureLabelText()
    {
        if (m_text == null)
            m_text = GetComponentInChildren<TMP_Text>(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (m_button != null)
            return;

        if (m_ignoreSecondClickOfDoubleTap && eventData.clickCount > 1)
            return;
        Debug.Log("OnPointerClick");
        Debug.Log("Try to apply random price " + m_hasAssignedPrice);
        TryApplyRandomPrice();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        // Do nothing
    }

    private void TryApplyRandomPrice()
    {
        EnsureLabelText();
        Debug.Log("EnsureLabelText");
        Debug.Log("m_text: " + m_text);
        if (m_text == null)
            return;

        if (!m_hasAssignedPrice)
        {
            int lo = Mathf.Min(m_minDollars, m_maxDollars);
            int hi = Mathf.Max(m_minDollars, m_maxDollars);
            m_assignedDollars = Random.Range(lo, hi + 1);
            m_hasAssignedPrice = true;
        }

        if (string.IsNullOrEmpty(m_cachedBaseLabel))
            m_cachedBaseLabel = ResolveBaseLabel(m_text.text);
        m_priceVisible = !m_priceVisible;
        m_text.text = m_priceVisible
            ? $"{m_cachedBaseLabel}: {m_currencyPrefix}{m_assignedDollars}"
            : m_cachedBaseLabel;
    }

    private static string ResolveBaseLabel(string text)
    {
        string current = text != null ? text.Trim() : string.Empty;
        int separatorIndex = current.IndexOf(':');
        string baseLabel = separatorIndex >= 0 ? current.Substring(0, separatorIndex).Trim() : current;
        return string.IsNullOrEmpty(baseLabel) ? "item" : baseLabel;
    }
}
