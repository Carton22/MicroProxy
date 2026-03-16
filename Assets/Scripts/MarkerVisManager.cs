using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls visibility of spawned pinch markers based on the currently selected label(s) in ProxyLabelManager.
/// Markers are expected to have MarkerLabelBinding with a 0-based LabelIndex.
/// </summary>
public class MarkerVisManager : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private PinchTargetSpawner m_pinchTargetSpawner;

    [Header("Behavior")]
    [Tooltip("If true, uses label manager selection range (min..max). If false, uses single selected index.")]
    [SerializeField] private bool m_useSelectionRange = true;

    private int m_lastMin = int.MinValue;
    private int m_lastMax = int.MinValue;

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        m_pinchTargetSpawner = FindFirstObjectByType<PinchTargetSpawner>();
    }

    private void Update()
    {
        if (m_labelManager == null || m_pinchTargetSpawner == null)
            return;

        GetCurrentSelection(out int min, out int max);
        if (min == m_lastMin && max == m_lastMax)
            return;

        m_lastMin = min;
        m_lastMax = max;
        ApplyVisibility(min, max);
    }

    private void GetCurrentSelection(out int min, out int max)
    {
        min = -1;
        max = -1;

        if (m_useSelectionRange)
        {
            m_labelManager.GetSelectionRange(out min, out max);
        }
        else
        {
            int idx = m_labelManager.GetSelectedLabelIndex();
            min = max = idx;
        }
    }

    private void ApplyVisibility(int min, int max)
    {
        IReadOnlyList<Transform> targets = m_pinchTargetSpawner.GetRuntimeTargets();
        if (targets == null)
            return;

        bool hasSelection = min >= 0 && max >= 0;

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null)
                continue;

            var binding = t.GetComponent<MarkerLabelBinding>();
            bool visible = false;
            if (hasSelection && binding != null)
            {
                int li = binding.LabelIndex;
                visible = li >= min && li <= max;
            }

            SetActiveIfNeeded(t.gameObject, visible);
        }
    }

    private static void SetActiveIfNeeded(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}

