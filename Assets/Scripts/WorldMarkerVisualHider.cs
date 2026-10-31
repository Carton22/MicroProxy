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

    [Tooltip("Hide both the cube and its number when the passthrough canvas lies between the viewer and the marker.")]
    [SerializeField] private bool m_hideBehindPassthroughCanvas;
    [SerializeField] private RectTransform m_occludingCanvas;
    [Tooltip("Center-eye transform. Falls back to Camera.main if unassigned.")]
    [SerializeField] private Transform m_viewer;

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

        var viewer = m_viewer != null ? m_viewer : Camera.main != null ? Camera.main.transform : null;
        var markerParent = m_pinchTargetSpawner.GetMarkerParentTransform();
        if (markerParent != null && markerParent.childCount > 0)
        {
            for (int i = 0; i < markerParent.childCount; i++)
            {
                var child = markerParent.GetChild(i);
                if (child == null)
                    continue;

                SetMarkerVisualsVisible(child.gameObject, ShouldShowMarker(child, viewer));
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

            SetMarkerVisualsVisible(t.gameObject, ShouldShowMarker(t, viewer));
        }
    }

    private bool ShouldShowMarker(Transform marker, Transform viewer)
    {
        if (m_hideWorldMarkers)
            return false;
        if (!m_hideBehindPassthroughCanvas || m_occludingCanvas == null || viewer == null
            || !m_occludingCanvas.gameObject.activeInHierarchy)
            return true;

        Vector3 offset = marker.position - viewer.position;
        float markerDistance = offset.magnitude;
        if (markerDistance <= 0.001f)
            return true;

        var ray = new Ray(viewer.position, offset / markerDistance);
        var plane = new Plane(m_occludingCanvas.forward, m_occludingCanvas.position);
        if (!plane.Raycast(ray, out float canvasDistance) || canvasDistance >= markerDistance - 0.001f)
            return true;

        Vector3 hit = m_occludingCanvas.InverseTransformPoint(ray.GetPoint(canvasDistance));
        // The finite image rectangle occludes; the infinite extension of its plane does not.
        return !m_occludingCanvas.rect.Contains(new Vector2(hit.x, hit.y));
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
