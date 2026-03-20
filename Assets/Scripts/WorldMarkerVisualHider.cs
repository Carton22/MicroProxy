using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hides the 3D pinch markers in the real world visually (disables renderers and canvas)
/// without changing their active state, so ray-casting and label selection still work.
/// Toggle "Hide World Markers" to show or hide the marker visuals.
/// </summary>
public class WorldMarkerVisualHider : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private PinchTargetSpawner m_pinchTargetSpawner;

    [Header("Visibility")]
    [Tooltip("When true, world markers are hidden (renderers and canvas disabled). They still exist for ray-casting.")]
    [SerializeField] private bool m_hideWorldMarkers = false;

    /// <summary>
    /// Set whether world markers are hidden. Call from RightHandDoubleTapToggle (or elsewhere) to hide markers after double tap.
    /// </summary>
    public void SetHideWorldMarkers(bool hide)
    {
        m_hideWorldMarkers = hide;
    }

    private void OnEnable()
    {
        ApplyVisibility();
    }

    private void LateUpdate()
    {
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        if (m_pinchTargetSpawner == null)
            return;

        bool visible = !m_hideWorldMarkers;
        var markerParent = m_pinchTargetSpawner.GetMarkerParentTransform();
        if (markerParent != null && markerParent.childCount > 0)
        {
            for (int i = 0; i < markerParent.childCount; i++)
            {
                var child = markerParent.GetChild(i);
                if (child == null)
                    continue;

                SetMarkerVisualsVisible(child.gameObject, visible);
            }
            return;
        }

        IReadOnlyList<Transform> targets = m_pinchTargetSpawner.GetRuntimeTargets();
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null)
                continue;

            SetMarkerVisualsVisible(t.gameObject, visible);
        }
    }

    private static void SetMarkerVisualsVisible(GameObject go, bool visible)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        foreach (var c in go.GetComponentsInChildren<Canvas>(true))
            c.enabled = visible;

        foreach (var g in go.GetComponentsInChildren<Graphic>(true))
            g.enabled = visible;
    }
}
