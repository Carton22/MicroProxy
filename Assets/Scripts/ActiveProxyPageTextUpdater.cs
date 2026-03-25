using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Updates a TMP text based on which ProxyLabelManager page is active:
/// SpatialHierarchy -> "Top", MaterialArea -> "Middle", ProxyUI -> "Down".
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

    [Header("Matching (by name)")]
    [Tooltip("Substring match (case-insensitive) against the active labels parent's name.")]
    [SerializeField] private string m_spatialHierarchyName = "SpatialHierarchy";

    [Tooltip("Substring match (case-insensitive) against the active labels parent's name.")]
    [SerializeField] private string m_materialAreaName = "MaterialArea";

    [Tooltip("Substring match (case-insensitive) against the active labels parent's name.")]
    [SerializeField] private string m_proxyUiName = "ProxyUI";

    [Header("Update behavior")]
    [Tooltip("How often to check for active page changes (seconds). 0 = every frame.")]
    [SerializeField] private float m_updateIntervalSeconds = 0.15f;

    private Transform m_lastActiveParent;
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

        var activeParent = m_proxyLabelManager.GetActiveLabelsParent();
        if (!force && activeParent == m_lastActiveParent)
            return;

        m_lastActiveParent = activeParent;

        string next = ResolvePageText(activeParent);
        if (m_targetText != null && m_targetText.text != next)
            m_targetText.text = next;
    }

    private string ResolvePageText(Transform activeParent)
    {
        if (activeParent == null)
            return string.Empty;

        string name = activeParent.name ?? string.Empty;
        string normalized = name.ToLowerInvariant();

        if (!string.IsNullOrEmpty(m_spatialHierarchyName) &&
            normalized.Contains(m_spatialHierarchyName.ToLowerInvariant()))
            return "Top";

        if (!string.IsNullOrEmpty(m_materialAreaName) &&
            normalized.Contains(m_materialAreaName.ToLowerInvariant()))
            return "Middle";

        if (!string.IsNullOrEmpty(m_proxyUiName) &&
            normalized.Contains(m_proxyUiName.ToLowerInvariant()))
            return "Down";

        // If the active page name doesn't match any known option.
        return string.Empty;
    }
}

