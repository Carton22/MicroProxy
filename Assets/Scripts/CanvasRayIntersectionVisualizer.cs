
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.Samples;
using Meta.XR;
using UnityEngine.UI;

// check which marker(s) are actiavted and draw lines to the corresponding label(s)
public class CanvasRayIntersectionVisualizer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MyCameraToWorldManager m_cameraToWorldManager;
    [SerializeField] private PassthroughCameraAccess m_cameraAccess;
    [Tooltip("UI prefab (RectTransform + Image) to place at intersection points on the passthrough canvas.")]
    [SerializeField] private RectTransform m_circlePrefab;

    [Header("Circle materials")]
    [Tooltip("Material for the circle that corresponds to the currently selected label.")]
    [SerializeField] private Material m_selectedCircleMaterial;
    [Tooltip("Material for circles that are raycasting/hit but not selected.")]
    [SerializeField] private Material m_unselectedCircleMaterial;
    [Tooltip("Size (width/height) for the selected circle.")]
    [SerializeField] private float m_selectedCircleSize = 32f;
    [Tooltip("Size (width/height) for unselected circles.")]
    [SerializeField] private float m_unselectedCircleSize = 18f;

    [Tooltip("Optional LineRenderer prefab used to draw lines from circles to their corresponding labels.")]
    [SerializeField] private LineRenderer m_linePrefab;

    [Tooltip("Label managers used to resolve which proxy-set label is selected and which label is bound to each marker index.")]
    [SerializeField] private List<ProxyLabelManager> m_labelManagers = new();

    [Header("Runtime")]
    private RectTransform m_circleInstance;

    [Header("Runtime targets")]
    [Tooltip("Left-hand pinch spawner; intersection markers are driven by its runtime targets.")]
    [SerializeField] private PinchTargetSpawner m_runtimeTargetSource;

    [Tooltip("If false, red circles and lines on the big canvas are hidden (minicamera circles unchanged).")]
    [SerializeField] private bool m_showCirclesAndLinesOnBigCanvas = true;

    [Tooltip("Horizontal interpolation factor (0–1) for the bend point between circle and label in canvas space. 0.3 = 30% towards the label on X, Y matches the label.")]
    [Range(0f, 1f)]
    [SerializeField] private float m_bendXFactor = 0.3f;

    [Tooltip("Padding (in canvas local units) from circle and label so the line does not start/end exactly at their centers.")]
    [SerializeField] private float m_lineEndPadding = 10f;

    private RectTransform[] m_markerInstances;
    private LineRenderer[] m_lineInstances;
    private readonly List<Transform> m_markerTargetsBuffer = new();

    private void Awake()
    {
        if (m_labelManagers == null)
            m_labelManagers = new List<ProxyLabelManager>();

        if (m_labelManagers.Count == 0)
        {
            // Fallback: include all label managers in the scene (including inactive).
            var found = FindObjectsByType<ProxyLabelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                var lm = found[i];
                if (lm == null)
                    continue;

                bool exists = false;
                for (int j = 0; j < m_labelManagers.Count; j++)
                {
                    if (m_labelManagers[j] == lm)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    m_labelManagers.Add(lm);
            }
        }
    }

    private void Update()
    {
        if (!m_showCirclesAndLinesOnBigCanvas)
        {
            HideAllBigCanvasCirclesAndLines();
            return;
        }

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
                var local = canvasTransform.InverseTransformPoint(hitWorld);

                m_markerInstances[i].SetParent(canvasTransform, false);
                m_markerInstances[i].anchoredPosition = new Vector2(local.x, local.y);
                // Keep circles on the canvas plane.
                var lp = m_markerInstances[i].localPosition;
                m_markerInstances[i].localPosition = new Vector3(lp.x, lp.y, 0f);

                // Swap circle material based on selection state.
                bool isSelected = IsMarkerSelected(i);
                ApplyCircleMaterial(m_markerInstances[i], isSelected);
                ApplyCircleSize(m_markerInstances[i], isSelected);
                // Always show circles for markers that are raycasting/hit; selection only affects style and lines.
                m_markerInstances[i].gameObject.SetActive(true);
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

    private void HideAllBigCanvasCirclesAndLines()
    {
        if (m_markerInstances != null)
        {
            for (int i = 0; i < m_markerInstances.Length; i++)
            {
                if (m_markerInstances[i] != null)
                    m_markerInstances[i].gameObject.SetActive(false);
            }
        }
        if (m_lineInstances != null)
        {
            for (int i = 0; i < m_lineInstances.Length; i++)
            {
                if (m_lineInstances[i] != null)
                    m_lineInstances[i].gameObject.SetActive(false);
            }
        }
    }

    private int GetActiveTargetCount(out IReadOnlyList<Transform> targetList)
    {
        targetList = null;
        if (m_runtimeTargetSource == null)
            return 0;

        // Preferred source: marker children currently under MarkerParent (works for restored markers too).
        var markerParent = m_runtimeTargetSource.GetMarkerParentTransform();
        if (markerParent != null && markerParent.childCount > 0)
        {
            m_markerTargetsBuffer.Clear();
            for (int i = 0; i < markerParent.childCount; i++)
            {
                var child = markerParent.GetChild(i);
                if (child != null)
                    m_markerTargetsBuffer.Add(child);
            }

            targetList = m_markerTargetsBuffer;
            return m_markerTargetsBuffer.Count;
        }

        // Fallback source: runtime pinch list.
        if (m_runtimeTargetSource.GetRuntimeTargetCount() == 0)
            return 0;

        targetList = m_runtimeTargetSource.GetRuntimeTargets();
        return targetList != null ? targetList.Count : 0;
    }

    private void UpdateLineForMarker(int index, RectTransform canvasTransform)
    {
        if (m_linePrefab == null || m_markerInstances == null || m_labelManagers == null || m_labelManagers.Count == 0)
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

        // Resolve label associated with this marker index: it must be bound to the currently selected label
        // in ANY proxy set.
        if (!TryGetSelectedLabelRectForMarkerIndex(index, out var labelRect) || labelRect == null)
        {
            if (m_lineInstances != null && index < m_lineInstances.Length && m_lineInstances[index] != null)
                m_lineInstances[index].gameObject.SetActive(false);
            return;
        }

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

        // Draw a bent polyline: circle -> bend point -> label.
        // Compute bend point in canvas local space, then convert to world.
        var canvasRect = canvasTransform;
        var rect = canvasRect.rect;

        // Local positions on canvas
        Vector3 circleLocal = canvasRect.InverseTransformPoint(circle.position);
        Vector3 labelLocal = canvasRect.InverseTransformPoint(labelRect.position);

        // Bend X is interpolated between circle and label X; Y matches the label.
        float bendX = Mathf.Lerp(circleLocal.x, labelLocal.x, m_bendXFactor);
        float bendY = labelLocal.y;
        Vector3 bendLocal = new Vector3(bendX, bendY, circleLocal.z);

        // Apply padding so the line does not overlap circle/label centers.
        Vector3 dirStart = (bendLocal - circleLocal).normalized;
        Vector3 dirEnd = (bendLocal - labelLocal).normalized;

        Vector3 circlePadded = circleLocal + dirStart * m_lineEndPadding;
        Vector3 labelPadded = labelLocal + dirEnd * m_lineEndPadding;

        // Keep the polyline on the canvas plane (local z = 0).
        circlePadded.z = 0f;
        bendLocal.z = 0f;
        labelPadded.z = 0f;

        // Draw in canvas/local space so the line lies on the canvas surface.
        // (The LineRenderer GameObject is parented under the canvas.)
        line.useWorldSpace = false;
        line.positionCount = 3;
        line.SetPosition(0, circlePadded);
        line.SetPosition(1, bendLocal);
        line.SetPosition(2, labelPadded);
    }

    private bool IsMarkerSelected(int markerIndex) =>
        TryGetSelectedLabelRectForMarkerIndex(markerIndex, out _);

    private bool TryGetSelectedLabelRectForMarkerIndex(int markerIndex, out RectTransform selectedLabelRect)
    {
        selectedLabelRect = null;

        if (markerIndex < 0)
            return false;

        if (m_labelManagers == null || m_labelManagers.Count == 0)
            return false;

        // Marker is "selected" if it is bound to the currently selected label in ANY ProxyLabelManager.
        for (int lmIndex = 0; lmIndex < m_labelManagers.Count; lmIndex++)
        {
            var lm = m_labelManagers[lmIndex];
            if (lm == null)
                continue;

            int selectedIndex = lm.GetSelectedLabelIndex();
            if (selectedIndex < 0)
                continue;

            var selectedLabel = lm.GetLabelRectTransform(selectedIndex);
            if (selectedLabel == null)
                continue;

            var markerLabel = lm.GetLabelRectTransformForMarkerIndex(markerIndex);
            if (markerLabel != null && markerLabel == selectedLabel)
            {
                selectedLabelRect = selectedLabel;
                return true;
            }
        }

        return false;
    }

    private void ApplyCircleMaterial(RectTransform circle, bool selected)
    {
        if (circle == null)
            return;

        var img = circle.GetComponent<Image>();
        if (img == null)
            img = circle.GetComponentInChildren<Image>(true);
        if (img == null)
            return;

        var targetMat = selected ? m_selectedCircleMaterial : m_unselectedCircleMaterial;
        if (targetMat == null)
            return;

        if (img.material != targetMat)
            img.material = targetMat;
    }

    private void ApplyCircleSize(RectTransform circle, bool selected)
    {
        if (circle == null)
            return;

        float size = selected ? m_selectedCircleSize : m_unselectedCircleSize;
        if (size <= 0f)
            return;

        var target = new Vector2(size, size);
        if (circle.sizeDelta != target)
            circle.sizeDelta = target;
    }
}