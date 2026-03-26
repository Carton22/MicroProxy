using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Updates a TMP text based on which ProxyLabelManager label page is active.
/// Level number maps to the active label parent's index in ProxyLabelManager's `Label Parents` list.
/// </summary>
[DisallowMultipleComponent]
public class ActiveProxyPageTextUpdater : MonoBehaviour
{
    [Header("Sources")]
    [Tooltip("ProxyLabelManager that owns the page transforms and can tell us which one is active.")]
    [SerializeField] private ProxyLabelManager m_proxyLabelManager;

    [Header("Target")]
    [Tooltip("TMP text to update.")]
    [SerializeField] private TMP_Text m_targetText;

    [Header("Level text")]
    [Tooltip("Prefix used when formatting the level text (e.g. prefix='Level ' -> 'Level 0').")]
    [SerializeField] private string m_levelPrefix = "Level ";

    [Header("Update behavior")]
    [Tooltip("How often to check for active page changes (seconds). 0 = every frame.")]
    [SerializeField] private float m_updateIntervalSeconds = 0.15f;

    private int m_lastActiveIndex = int.MinValue;
    private float m_nextUpdateTime;

    private void Reset()
    {
        m_proxyLabelManager = FindFirstObjectByType<ProxyLabelManager>();
        m_targetText = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        m_nextUpdateTime = Time.unscaledTime;
        TryUpdateText(force: true);
    }

    private void Update()
    {
        float now = Time.unscaledTime;

        if (m_updateIntervalSeconds <= 0f)
        {
            TryUpdateText(force: false);
            return;
        }

        if (now < m_nextUpdateTime)
            return;

        m_nextUpdateTime = now + Mathf.Max(0.01f, m_updateIntervalSeconds);
        TryUpdateText(force: false);
    }

    private void TryUpdateText(bool force)
    {
        if (m_proxyLabelManager == null)
            return;

        int activeIndex = m_proxyLabelManager.GetActiveLabelsParentIndex();
        if (!force && activeIndex == m_lastActiveIndex)
            return;

        m_lastActiveIndex = activeIndex;
        string next = ResolveLevelText(activeIndex);
        if (m_targetText != null && m_targetText.text != next)
            m_targetText.text = next;
    }

    private string ResolveLevelText(int activeIndex)
    {
        if (activeIndex < 0)
            return string.Empty;

        return $"{m_levelPrefix}{activeIndex}";
    }
}

