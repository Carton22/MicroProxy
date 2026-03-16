
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

    [Tooltip("Optional LineRenderer prefab used to draw lines from circles to their corresponding labels.")]
    [SerializeField] private LineRenderer m_linePrefab;

    [Tooltip("Label manager used to resolve label RectTransforms for line endpoints.")]
    [SerializeField] private ProxyLabelManager m_labelManager;

    [Header("Runtime")]
    private RectTransform m_circleInstance;

    [Header("Runtime targets")]
    [Tooltip("Left-hand pinch spawner; intersection markers are driven by its runtime targets.")]
    [SerializeField] private PinchTargetSpawner m_runtimeTargetSource;

    private RectTransform[] m_markerInstances;
    private LineRenderer[] m_lineInstances;

    private void Update()
    {
        int targetCount = GetActiveTargetCount(out var targetList);
        if (targetCount == 0)
            return;

        // Lazily allocate or grow per-target marker instances
        if (m_markerInstances == null || m_markerInstances.Length < targetCount)
        {
            var newSize = targetCount;
            var next = new RectTransform[newSize];
            if (m_markerInstances != null)
            {
                for (int j = 0; j < m_markerInstances.Length; j++)
                    next[j] = m_markerInstances[j];
            }
            m_markerInstances = next;
        }

        // Lazily allocate or grow per-target line instances
        if (m_lineInstances == null || m_lineInstances.Length < targetCount)
        {
            var newSize = targetCount;
            var nextLines = new LineRenderer[newSize];
            if (m_lineInstances != null)
            {
                for (int j = 0; j < m_lineInstances.Length; j++)
                    nextLines[j] = m_lineInstances[j];
            }
            m_lineInstances = nextLines;
        }

        for (int i = 0; i < targetCount; i++)
        {
            var t = targetList[i];
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                if (m_markerInstances != null && i < m_markerInstances.Length && m_markerInstances[i] != null)
                    m_markerInstances[i].gameObject.SetActive(false);
                if (m_lineInstances != null && i < m_lineInstances.Length && m_lineInstances[i] != null)
                    m_lineInstances[i].gameObject.SetActive(false);
                continue;
            }

            if (!TryGetIntersectionPoint(t.position, out var hitWorld, out var canvasTransform))
            {
                if (m_markerInstances[i] != null)
                    m_markerInstances[i].gameObject.SetActive(false);
                if (m_lineInstances != null && i < m_lineInstances.Length && m_lineInstances[i] != null)
                    m_lineInstances[i].gameObject.SetActive(false);
                continue;
            }

            if (m_markerInstances[i] == null && m_circlePrefab != null)
            {
                m_markerInstances[i] = Instantiate(m_circlePrefab, canvasTransform);
            }

            if (m_markerInstances[i] != null)
            {
                m_markerInstances[i].gameObject.SetActive(true);

                var local = canvasTransform.InverseTransformPoint(hitWorld);

                m_markerInstances[i].SetParent(canvasTransform, false);
                m_markerInstances[i].anchoredPosition = new Vector2(local.x, local.y);
                m_markerInstances[i].sizeDelta = new Vector2(25f, 25f);
            }

            // Draw line from circle to its corresponding label
            UpdateLineForMarker(i, canvasTransform);
        }

        for (int i = targetCount; i < m_markerInstances.Length; i++)
        {
            if (m_markerInstances[i] != null)
                m_markerInstances[i].gameObject.SetActive(false);
        }

        if (m_lineInstances != null)
        {
            for (int i = targetCount; i < m_lineInstances.Length; i++)
            {
                if (m_lineInstances[i] != null)
                    m_lineInstances[i].gameObject.SetActive(false);
            }
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
        m_circleInstance.sizeDelta = new Vector2(25f, 25f);
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

    private int GetActiveTargetCount(out IReadOnlyList<Transform> targetList)
    {
        targetList = null;
        if (m_runtimeTargetSource == null || m_runtimeTargetSource.GetRuntimeTargetCount() == 0)
            return 0;
        targetList = m_runtimeTargetSource.GetRuntimeTargets();
        return targetList.Count;
    }

    private void UpdateLineForMarker(int index, RectTransform canvasTransform)
    {
        if (m_linePrefab == null || m_markerInstances == null || m_runtimeTargetSource == null || m_labelManager == null)
            return;

        if (index < 0 || index >= m_markerInstances.Length)
            return;

        var circle = m_markerInstances[index];
        if (circle == null || !circle.gameObject.activeInHierarchy)
        {
            if (m_lineInstances != null && index < m_lineInstances.Length && m_lineInstances[index] != null)
                m_lineInstances[index].gameObject.SetActive(false);
            return;
        }

        // Resolve label index from the corresponding runtime target's MarkerLabelBinding
        var targets = m_runtimeTargetSource.GetRuntimeTargets();
        if (targets == null || index >= targets.Count)
            return;

        var targetTransform = targets[index];
        if (targetTransform == null)
            return;

        var binding = targetTransform.GetComponent<MarkerLabelBinding>();
        if (binding == null || binding.LabelIndex < 0)
            return;

        var labelRect = m_labelManager.GetLabelRectTransform(binding.LabelIndex);
        if (labelRect == null || !labelRect.gameObject.activeInHierarchy)
            return;

        // Ensure line instance exists
        if (m_lineInstances[index] == null && m_linePrefab != null)
        {
            var lineGO = Instantiate(m_linePrefab.gameObject, canvasTransform);
            m_lineInstances[index] = lineGO.GetComponent<LineRenderer>();
        }

        var line = m_lineInstances[index];
        if (line == null)
            return;

        line.gameObject.SetActive(true);

        // Use world positions of circle and label for the line endpoints
        Vector3 start = circle.position;
        Vector3 end = labelRect.position;

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }
}