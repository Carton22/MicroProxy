using System.Collections.Generic;
using System.Linq;
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

    private int m_lastSelectedIndex = int.MinValue;

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        m_pinchTargetSpawner = FindFirstObjectByType<PinchTargetSpawner>();
    }

    private void Update()
    {
        if (m_labelManager == null || m_pinchTargetSpawner == null)
            return;

        int selectedIndex = m_labelManager.GetSelectedLabelIndex();
        if (selectedIndex == m_lastSelectedIndex)
            return;

        m_lastSelectedIndex = selectedIndex;
        ApplyVisibility(selectedIndex);
    }

    private void ApplyVisibility(int selectedLabelIndex)
    {
        IReadOnlyList<Transform> targets = m_pinchTargetSpawner.GetRuntimeTargets();
        if (targets == null)
            return;
    }

    private static void SetActiveIfNeeded(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }
}

