
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.Samples;
using Meta.XR;

public class CanvasRayIntersectionVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MyCameraToWorldManager m_cameraToWorldManager;
    [SerializeField] private PassthroughCameraAccess m_cameraAccess;
    [Tooltip("UI prefab (RectTransform + Image) to place at intersection points on the passthrough canvas.")]
    [SerializeField] private RectTransform m_circlePrefab;

    [Header("Runtime")]
    private RectTransform m_circleInstance;

    [Header("Runtime targets (optional)")]
    [Tooltip("When set, intersection markers are driven by left-hand pinch spawns from this spawner.")]
    [SerializeField] private PinchTargetSpawner m_runtimeTargetSource;

    [Header("Debug targets (optional)")]
    [Tooltip("World-space points to test intersection with the passthrough canvas (for debugging). Used only when Runtime Target Source is not set or has no targets.")]
    [SerializeField] private Transform[] m_debugTargets;
    [SerializeField] private bool m_updateDebugEveryFrame = true;
    private RectTransform[] m_debugInstances;

    private void Update()
    {
        int targetCount = GetActiveTargetCount(out var targetList);
        if (!m_updateDebugEveryFrame || targetCount == 0)
            return;

        // Lazily allocate or grow per-target instances
        if (m_debugInstances == null || m_debugInstances.Length < targetCount)
        {
            var newSize = targetCount;
            var next = new RectTransform[newSize];
            if (m_debugInstances != null)
            {
                for (int j = 0; j < m_debugInstances.Length; j++)
                    next[j] = m_debugInstances[j];
            }
            m_debugInstances = next;
        }

        for (int i = 0; i < targetCount; i++)
        {
            var t = targetList[i];
            if (t == null)
                continue;

            if (!TryGetIntersectionPoint(t.position, out var hitWorld, out var canvasTransform))
            {
                if (m_debugInstances[i] != null)
                    m_debugInstances[i].gameObject.SetActive(false);
                continue;
            }

            if (m_debugInstances[i] == null && m_circlePrefab != null)
            {
                m_debugInstances[i] = Instantiate(m_circlePrefab, canvasTransform);
            }

            if (m_debugInstances[i] != null)
            {
                m_debugInstances[i].gameObject.SetActive(true);

                // Convert world hit point into local-space position of the overlay parent,
                // exactly like ScreenSpaceBoundingBoxDrawer does for anchors.
                var local = canvasTransform.InverseTransformPoint(hitWorld);

                m_debugInstances[i].SetParent(canvasTransform, false);
                m_debugInstances[i].anchoredPosition = new Vector2(local.x, local.y);
                m_debugInstances[i].sizeDelta = new Vector2(40f, 40f);
            }
        }

        // Hide canvas markers when target list shrinks
        for (int i = targetCount; i < m_debugInstances.Length; i++)
        {
            if (m_debugInstances[i] != null)
                m_debugInstances[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Call this with a world-space target point. It will:
    /// - cast a ray from the passthrough camera to the point,
    /// - check intersection with the camera-to-world canvas plane,
    /// - place a circle on the canvas at the hit point (if any).
    /// </summary>
    public void ShowIntersection(Vector3 worldTarget)
    {
        if (!TryGetIntersectionPoint(worldTarget, out var hitWorld, out var canvasTransform))
            return;

        // 5) Ensure we have an instance of the circle
        if (m_circleInstance == null)
        {
            m_circleInstance = Instantiate(m_circlePrefab, canvasTransform);
            m_circleInstance.gameObject.SetActive(true);
        }

        // 6) Place the circle at the hit point in overlay-parent local space
        var local = canvasTransform.InverseTransformPoint(hitWorld);

        m_circleInstance.SetParent(canvasTransform, false);
        m_circleInstance.anchoredPosition = new Vector2(local.x, local.y);
        m_circleInstance.sizeDelta = new Vector2(40f, 40f);
    }

    /// <summary>
    /// Core math: from a world target, compute the intersection point (if any) of the ray
    /// from the passthrough camera to that target with the same overlay parent
    /// that ScreenSpaceBoundingBoxDrawer uses (BoundingBoxOverlayRect).
    /// </summary>
    private bool TryGetIntersectionPoint(Vector3 worldTarget, out Vector3 hitWorld, out RectTransform canvasTransform)
    {
        hitWorld = default;
        canvasTransform = null;

        if (m_cameraToWorldManager == null || m_cameraAccess == null)
            return false;

        if (!m_cameraAccess.IsPlaying)
            return false;

        // 1) Get camera pose (same as used by MyCameraToWorldManager)
        var camPose = m_cameraAccess.GetCameraPose();
        Vector3 camPos = camPose.position;

        // 2) Ray from camera to target
        Vector3 dir = (worldTarget - camPos).normalized;
        var ray = new Ray(camPos, dir);
        float distToTarget = Vector3.Distance(camPos, worldTarget);

        // 3) Canvas plane (same as overlay parent used for 2D boxes)
        var overlayParent = m_cameraToWorldManager != null
            ? m_cameraToWorldManager.BoundingBoxOverlayRect
            : null;
        if (overlayParent == null)
            return false;

        canvasTransform = overlayParent;
        var plane = new Plane(canvasTransform.forward, canvasTransform.position);

        // 4) Ray–plane intersection
        if (!plane.Raycast(ray, out float t))
            return false;

        // Only accept hits between camera and target (small epsilon)
        if (t < 0f || t > distToTarget + 0.01f)
            return false;

        hitWorld = ray.GetPoint(t);
        return true;
    }

    /// <summary>
    /// Returns the number of targets to visualize and the list to use: runtime pinch targets if available, otherwise debug targets.
    /// </summary>
    private int GetActiveTargetCount(out IReadOnlyList<Transform> targetList)
    {
        if (m_runtimeTargetSource != null && m_runtimeTargetSource.GetRuntimeTargetCount() > 0)
        {
            targetList = m_runtimeTargetSource.GetRuntimeTargets();
            return targetList.Count;
        }
        if (m_debugTargets == null || m_debugTargets.Length == 0)
        {
            targetList = null;
            return 0;
        }
        targetList = m_debugTargets;
        return m_debugTargets.Length;
    }
}