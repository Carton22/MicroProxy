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
    [SerializeField] private SpatialHierarchyChildViewManager m_hierarchyManager;

    [Header("Target")]
    [Tooltip("TMP text to update.")]
    [SerializeField] private TMP_Text m_targetText;

    [Header("Level text")]
    [Tooltip("Prefix used when formatting the level text (e.g. prefix='Level ' -> 'Level 0').")]
    [SerializeField] private string m_levelPrefix = "Level ";

    [Header("Organization tag")]
    [Tooltip("When enabled, appends an organization hint such as 'by Space' or 'by Owner'.")]
    [SerializeField] private bool m_includeOrganizationTag = true;
    [SerializeField] private string m_organizationSeparator = "  ";
    [SerializeField] private string m_spaceGroupRootName = "Cars";
    [SerializeField] private string m_spaceTagText = "by Space";
    [SerializeField] private string m_ownerGroupRootName = "Attribute-Owner";
    [SerializeField] private string m_ownerTagText = "by Owner";

    [Header("Update behavior")]
    [Tooltip("How often to check for active page changes (seconds). 0 = every frame.")]
    [SerializeField] private float m_updateIntervalSeconds = 0.15f;

    private int m_lastActiveIndex = int.MinValue;
    private string m_lastResolvedText = string.Empty;
    private float m_nextUpdateTime;

    private void Reset()
    {
        m_proxyLabelManager = FindFirstObjectByType<ProxyLabelManager>();
        m_hierarchyManager = FindFirstObjectByType<SpatialHierarchyChildViewManager>();
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
            m_proxyLabelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_hierarchyManager == null)
            m_hierarchyManager = FindFirstObjectByType<SpatialHierarchyChildViewManager>();

        if (m_proxyLabelManager == null && m_hierarchyManager == null)
            return;

        int activeIndex = m_hierarchyManager != null
            ? m_hierarchyManager.GetCurrentLogicalLevelIndex()
            : m_proxyLabelManager.GetActiveLabelsParentIndex();
        string next = ResolveLevelText(activeIndex);
        if (!force &&
            activeIndex == m_lastActiveIndex &&
            string.Equals(next, m_lastResolvedText, StringComparison.Ordinal))
        {
            return;
        }

        m_lastActiveIndex = activeIndex;
        m_lastResolvedText = next;
        if (m_targetText != null && m_targetText.text != next)
            m_targetText.text = next;
    }

    private string ResolveLevelText(int activeIndex)
    {
        if (activeIndex < 0)
            return string.Empty;

        string levelText = $"{m_levelPrefix}{activeIndex}";
        string organizationTag = ResolveOrganizationTag();
        if (string.IsNullOrWhiteSpace(organizationTag))
            return levelText;

        return string.IsNullOrEmpty(m_organizationSeparator)
            ? $"{levelText} {organizationTag}".Trim()
            : $"{levelText}{m_organizationSeparator}{organizationTag}";
    }

    private string ResolveOrganizationTag()
    {
        if (!m_includeOrganizationTag || m_hierarchyManager == null)
            return string.Empty;

        Transform activeToggleRoot = m_hierarchyManager.GetCurrentToggleLevelRoot();
        if (activeToggleRoot == null || string.IsNullOrWhiteSpace(activeToggleRoot.name))
            return string.Empty;

        string rootName = activeToggleRoot.name;
        if (MatchesRootName(rootName, m_spaceGroupRootName))
            return m_spaceTagText;
        if (MatchesRootName(rootName, m_ownerGroupRootName))
            return m_ownerTagText;

        return string.Empty;
    }

    private static bool MatchesRootName(string rootName, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(rootName) || string.IsNullOrWhiteSpace(expectedName))
            return false;

        return rootName.IndexOf(expectedName, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
