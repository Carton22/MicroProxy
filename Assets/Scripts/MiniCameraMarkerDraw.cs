using System.Collections.Generic;
using UnityEngine;
using Meta.XR.Samples;
using Meta.XR;
using UnityEngine.UI;
using PassthroughCameraSamples;

/// <summary>
/// Uses the same raycasting as the big camera-to-world canvas but draws circles
/// proportionally on a minimized camera canvas. Ray–plane intersection is always
/// done against the big canvas; hit positions are then mapped to the mini canvas
/// by normalized (0–1) coordinates so layout is proportional.
/// </summary>
public class MiniCameraMarkerDraw : MonoBehaviour
{
    [Header("Raycasting (big canvas)")]
    [Tooltip("Used to get camera pose and the big canvas (BoundingBoxOverlayRect) for ray–plane intersection.")]
    [SerializeField] private MyCameraToWorldManager m_cameraToWorldManager;
    [SerializeField] private PassthroughCameraAccess m_cameraAccess;

    [Header("Minimized canvas")]
    [Tooltip("RectTransform of the minimized camera canvas where circles are drawn proportionally.")]
    [SerializeField] private RectTransform m_miniCanvas;

    [Tooltip("Optional cropped camera-view provider. If unset, the first PassthroughCameraPictureTaker in the scene is used.")]
    [SerializeField] private PassthroughCameraPictureTaker m_pictureTaker;

    [Tooltip("Circle prefab (RectTransform + Image) to place on the minimized canvas.")]
    [SerializeField] private RectTransform m_circlePrefab;

    [Header("Runtime targets")]
    [Tooltip("Pinch spawner; only active markers are considered.")]
    [SerializeField] private PinchTargetSpawner m_runtimeTargetSource;

    [Header("Circle size")]
    [Tooltip("Size of the selected circle on the mini canvas (width and height).")]
    [SerializeField] private float m_selectedCircleSize = 18f;
    [Tooltip("Size of unselected circles on the mini canvas (width and height).")]
    [SerializeField] private float m_unselectedCircleSize = 12f;

    [Header("Circle materials")]
    [Tooltip("Material for the circle that corresponds to the currently selected label.")]
    [SerializeField] private Material m_selectedCircleMaterial;
    [Tooltip("Material for circles that are raycasting/hit but not selected.")]
    [SerializeField] private Material m_unselectedCircleMaterial;

    [Tooltip("Label managers used to resolve which proxy-set label is selected and which label is bound to each marker index.")]
    [SerializeField] private List<ProxyLabelManager> m_labelManagers = new();

    private RectTransform[] m_circleInstances;
    private readonly List<Transform> m_markerTargetsBuffer = new();

    private void Awake()
    {
        if (m_pictureTaker == null)
            m_pictureTaker = FindFirstObjectByType<PassthroughCameraPictureTaker>();

        if (m_labelManagers == null)
            m_labelManagers = new List<ProxyLabelManager>();

        if (m_labelManagers.Count == 0)
        {
            var found = FindObjectsByType<ProxyLabelManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                var labelManager = found[i];
                if (labelManager == null)
                    continue;

                bool exists = false;
                for (int j = 0; j < m_labelManagers.Count; j++)
                {
                    if (m_labelManagers[j] == labelManager)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    m_labelManagers.Add(labelManager);
            }
        }
    }

    private void Update()
    {
        int targetCount = GetActiveTargetCount(out var targetList);
        if (targetCount == 0)
        {
            SetCircleCount(0);
            return;
        }

        if (m_cameraToWorldManager == null || m_cameraAccess == null || m_miniCanvas == null || m_runtimeTargetSource == null)
        {
            SetCircleCount(0);
            return;
        }

        var bigCanvas = m_cameraToWorldManager.BoundingBoxOverlayRect;
        if (bigCanvas == null)
        {
            SetCircleCount(0);
            return;
        }

        // Lazily allocate or grow per-target circle instances on the mini canvas
        if (m_circleInstances == null || m_circleInstances.Length < targetCount)
        {
            var next = new RectTransform[targetCount];
            if (m_circleInstances != null)
            {
                for (int j = 0; j < m_circleInstances.Length; j++)
                    next[j] = m_circleInstances[j];
            }
            m_circleInstances = next;
        }

        Rect bigRect = bigCanvas.rect;
        Rect miniRect = m_miniCanvas.rect;

        for (int i = 0; i < targetCount; i++)
        {
            var t = targetList[i];
            if (t == null || !t.gameObject.activeInHierarchy)
            {
                if (i < m_circleInstances.Length && m_circleInstances[i] != null)
                    m_circleInstances[i].gameObject.SetActive(false);
                continue;
            }

            if (!TryGetIntersectionPointBigCanvas(t.position, bigCanvas, out Vector3 hitWorld))
            {
                if (i < m_circleInstances.Length && m_circleInstances[i] != null)
                    m_circleInstances[i].gameObject.SetActive(false);
                continue;
            }

            // Hit in big canvas local space
            Vector3 localBig = bigCanvas.InverseTransformPoint(hitWorld);
            // Normalized position on big canvas (0–1)
            float nx = bigRect.width > 0.001f ? Mathf.InverseLerp(bigRect.xMin, bigRect.xMax, localBig.x) : 0.5f;
            float ny = bigRect.height > 0.001f ? Mathf.InverseLerp(bigRect.yMin, bigRect.yMax, localBig.y) : 0.5f;

            if (!TryMapToVisibleMiniView(nx, ny, out float croppedNx, out float croppedNy))
            {
                if (i < m_circleInstances.Length && m_circleInstances[i] != null)
                    m_circleInstances[i].gameObject.SetActive(false);
                continue;
            }

            // Same proportional position on mini canvas (in mini canvas local space)
            float localX = Mathf.Lerp(miniRect.xMin, miniRect.xMax, croppedNx);
            float localY = Mathf.Lerp(miniRect.yMin, miniRect.yMax, croppedNy);

            if (m_circleInstances[i] == null && m_circlePrefab != null)
                m_circleInstances[i] = Instantiate(m_circlePrefab, m_miniCanvas);

            if (m_circleInstances[i] != null)
            {
                bool isSelected = IsMarkerSelected(i);
                m_circleInstances[i].gameObject.SetActive(true);
                m_circleInstances[i].SetParent(m_miniCanvas, false);
                m_circleInstances[i].anchoredPosition = new Vector2(localX, localY);
                float circleSize = isSelected ? m_selectedCircleSize : m_unselectedCircleSize;
                m_circleInstances[i].sizeDelta = new Vector2(circleSize, circleSize);
                ApplyCircleMaterial(m_circleInstances[i], isSelected);
            }
        }

        for (int i = targetCount; i < m_circleInstances.Length; i++)
        {
            if (m_circleInstances[i] != null)
                m_circleInstances[i].gameObject.SetActive(false);
        }
    }

    private void SetCircleCount(int count)
    {
        if (m_circleInstances == null) return;
        for (int i = count; i < m_circleInstances.Length; i++)
        {
            if (m_circleInstances[i] != null)
                m_circleInstances[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Ray from passthrough camera to world target; intersection with the big canvas plane.
    /// </summary>
    private bool TryGetIntersectionPointBigCanvas(Vector3 worldTarget, RectTransform bigCanvas, out Vector3 hitWorld)
    {
        hitWorld = default;

        if (!m_cameraAccess.IsPlaying)
            return false;

        var camPose = m_cameraAccess.GetCameraPose();
        Vector3 camPos = camPose.position;
        Vector3 dir = (worldTarget - camPos).normalized;
        var ray = new Ray(camPos, dir);
        float distToTarget = Vector3.Distance(camPos, worldTarget);

        var plane = new Plane(bigCanvas.forward, bigCanvas.position);
        if (!plane.Raycast(ray, out float t))
            return false;
        if (t < 0f || t > distToTarget + 0.01f)
            return false;

        hitWorld = ray.GetPoint(t);
        return true;
    }

    private int GetActiveTargetCount(out IReadOnlyList<Transform> targetList)
    {
        targetList = null;
        if (m_runtimeTargetSource == null)
            return 0;

        // Prefer all marker children under MarkerParent so restored markers are included too.
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

        if (m_runtimeTargetSource.GetRuntimeTargetCount() == 0)
            return 0;

        targetList = m_runtimeTargetSource.GetRuntimeTargets();
        return targetList != null ? targetList.Count : 0;
    }

    private bool IsMarkerSelected(int markerIndex) =>
        TryGetSelectedLabelRectForMarkerIndex(markerIndex, out _);

    private bool TryMapToVisibleMiniView(float normalizedX, float normalizedY, out float mappedX, out float mappedY)
    {
        mappedX = 0.5f;
        mappedY = 0.5f;

        Rect visibleUvRect = m_pictureTaker != null
            ? m_pictureTaker.GetVisibleNormalizedUvRect()
            : new Rect(0f, 0f, 1f, 1f);

        if (visibleUvRect.width <= 0.0001f || visibleUvRect.height <= 0.0001f)
            return false;

        if (normalizedX < visibleUvRect.xMin || normalizedX > visibleUvRect.xMax
            || normalizedY < visibleUvRect.yMin || normalizedY > visibleUvRect.yMax)
        {
            return false;
        }

        mappedX = Mathf.InverseLerp(visibleUvRect.xMin, visibleUvRect.xMax, normalizedX);
        mappedY = Mathf.InverseLerp(visibleUvRect.yMin, visibleUvRect.yMax, normalizedY);
        return true;
    }

    private bool TryGetSelectedLabelRectForMarkerIndex(int markerIndex, out RectTransform selectedLabelRect)
    {
        selectedLabelRect = null;

        if (markerIndex < 0)
            return false;

        if (m_labelManagers == null || m_labelManagers.Count == 0)
            return false;

        for (int i = 0; i < m_labelManagers.Count; i++)
        {
            var labelManager = m_labelManagers[i];
            if (labelManager == null)
                continue;

            int selectedIndex = labelManager.GetSelectedLabelIndex();
            if (selectedIndex < 0)
                continue;

            var selectedLabel = labelManager.GetLabelRectTransform(selectedIndex);
            if (selectedLabel == null)
                continue;

            var markerLabel = labelManager.GetLabelRectTransformForMarkerIndex(markerIndex);
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

        var image = circle.GetComponent<Image>();
        if (image == null)
            image = circle.GetComponentInChildren<Image>(true);
        if (image == null)
            return;

        var targetMaterial = selected ? m_selectedCircleMaterial : m_unselectedCircleMaterial;
        if (targetMaterial == null)
            return;

        if (image.material != targetMaterial)
            image.material = targetMaterial;
    }
}
