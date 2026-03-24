using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Replaces label text with a random dollar amount on click and tracks it in the running total until toggled off.
/// Temporary page/filter hides do not clear the tracked price.
/// Prefer wiring through the <see cref="Button"/> (same GameObject); <see cref="IPointerClickHandler"/> is a fallback when there is no Button.
/// </summary>
public class ProxyLabelPriceRandomizeOnClick : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    // Tracks labels whose price is currently toggled on, even if their page is temporarily hidden.
    private static readonly HashSet<ProxyLabelPriceRandomizeOnClick> s_visiblePriceLabels = new();
    public static event Action<int> OnVisiblePricesTotalChanged;

    private TMP_Text m_text;

    [SerializeField] private int m_minDollars = 10;
    [SerializeField] private int m_maxDollars = 50;
    [SerializeField] private bool m_useManualFixedValue;
    [SerializeField] private int m_manualFixedDollars = 20;

    [SerializeField] private string m_currencyPrefix = "$";

    [Tooltip("If true, ignore the second click of a double-click (pointer path only).")]
    [SerializeField] private bool m_ignoreSecondClickOfDoubleTap = true;

    private Button m_button;
    private bool m_hasAssignedPrice;
    private int m_assignedDollars;
    private string m_cachedBaseLabel;
    private bool m_priceVisible;

    public static int GetVisiblePricesTotal()
    {
        int total = 0;
        foreach (var label in s_visiblePriceLabels)
        {
            if (label == null)
                continue;
            total += Mathf.Max(0, label.m_assignedDollars);
        }
        return total;
    }

    public static int GetVisiblePriceCount()
    {
        int count = 0;
        foreach (var label in s_visiblePriceLabels)
        {
            if (label != null)
                count++;
        }
        return count;
    }

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

        // Keep tracked prices alive across temporary page/filter hides.
    }

    private void OnDestroy()
    {
        if (!m_priceVisible)
            return;

        m_priceVisible = false;
        s_visiblePriceLabels.Remove(this);
        OnVisiblePricesTotalChanged?.Invoke(GetVisiblePricesTotal());
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
        // Do nothing
    }

    private void TryApplyRandomPrice()
    {
        EnsureLabelText();
        if (m_text == null)
            return;

        if (!m_hasAssignedPrice)
        {
            if (m_useManualFixedValue)
            {
                m_assignedDollars = Mathf.Max(0, m_manualFixedDollars);
            }
            else
            {
                int lo = Mathf.Min(m_minDollars, m_maxDollars);
                int hi = Mathf.Max(m_minDollars, m_maxDollars);
                m_assignedDollars = UnityEngine.Random.Range(lo, hi + 1);
            }
            m_hasAssignedPrice = true;
        }

        if (string.IsNullOrEmpty(m_cachedBaseLabel))
            m_cachedBaseLabel = ResolveBaseLabel(m_text.text);
        SetPriceVisible(!m_priceVisible);
        m_text.text = m_priceVisible
            ? $"{m_cachedBaseLabel}: {m_currencyPrefix}{m_assignedDollars}"
            : m_cachedBaseLabel;
    }

    private void SetPriceVisible(bool visible)
    {
        if (m_priceVisible == visible)
            return;

        m_priceVisible = visible;
        if (m_priceVisible)
            s_visiblePriceLabels.Add(this);
        else
            s_visiblePriceLabels.Remove(this);

        OnVisiblePricesTotalChanged?.Invoke(GetVisiblePricesTotal());
    }

    private static string ResolveBaseLabel(string text)
    {
        string current = text != null ? text.Trim() : string.Empty;
        int separatorIndex = current.IndexOf(':');
        string baseLabel = separatorIndex >= 0 ? current.Substring(0, separatorIndex).Trim() : current;
        return string.IsNullOrEmpty(baseLabel) ? "item" : baseLabel;
    }
}
