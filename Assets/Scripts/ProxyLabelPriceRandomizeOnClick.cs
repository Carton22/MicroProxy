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

    [Tooltip("When Require Placeholder Match is on, the label text must equal this (trimmed, case-insensitive) before applying.")]
    [SerializeField] private string m_placeholderText = "Price";

    [Tooltip("If on, only clicks apply when the current text matches Placeholder Text (e.g. \"Price\"). Turn off to always set a random price on click (e.g. Book4 → $32).")]
    [SerializeField] private bool m_requirePlaceholderMatch = true;

    [SerializeField] private int m_minDollars = 10;
    [SerializeField] private int m_maxDollars = 50;

    [SerializeField] private string m_currencyPrefix = "$";

    [Tooltip("If true, ignore the second click of a double-click (pointer path only).")]
    [SerializeField] private bool m_ignoreSecondClickOfDoubleTap = true;

    private Button m_button;

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

        TryApplyRandomPrice();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        TryApplyRandomPrice();
    }

    private void TryApplyRandomPrice()
    {
        EnsureLabelText();
        if (m_text == null)
            return;

        if (m_requirePlaceholderMatch)
        {
            string current = m_text.text != null ? m_text.text.Trim() : string.Empty;
            string want = m_placeholderText != null ? m_placeholderText.Trim() : string.Empty;
            if (string.IsNullOrEmpty(want) || !string.Equals(current, want, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        int lo = Mathf.Min(m_minDollars, m_maxDollars);
        int hi = Mathf.Max(m_minDollars, m_maxDollars);
        int dollars = Random.Range(lo, hi + 1);
        m_text.text = $"{m_currencyPrefix}{dollars}";
    }
}
