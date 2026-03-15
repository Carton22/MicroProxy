using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws 2D bounding boxes on the canvas managed by MyCameraToWorldManager (passthrough camera view).
/// </summary>
public class ScreenSpaceBoundingBoxDrawer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private MyCameraToWorldManager m_cameraToWorldManager;
    [SerializeField] private RectTransform m_boxPrefab;

    // Bounding boxes are normalized using the inputSize passed from the detector.
    // The canvas already matches the passthrough camera texture via MyCameraToWorldManager,
    // so no additional resolution override or aspect-fit is applied here.

    private readonly List<RectTransform> m_activeBoxes = new();
    private readonly List<RectTransform> m_boxPool = new();

    private void Awake()
    {
        if (m_boxPrefab != null)
            m_boxPrefab.gameObject.SetActive(false);
    }

    public void ClearBoxes()
    {
        foreach (var rt in m_activeBoxes)
        {
            rt.gameObject.SetActive(false);
            m_boxPool.Add(rt);
        }
        m_activeBoxes.Clear();
    }

    private RectTransform GetOverlayParent() => m_cameraToWorldManager != null ? m_cameraToWorldManager.BoundingBoxOverlayRect : null;

    /// <param name="detections">Each (classId, bbox) where bbox = (x1,y1,x2,y2) in input pixels.</param>
    /// <param name="inputSize">Resolution the detections are in (ignored if Input Size Override is set).</param>
    public void DrawBoxes(List<(int classId, Vector4 bbox)> detections, Vector2 inputSize)
    {
        var overlayParent = GetOverlayParent();
        if (overlayParent == null || m_boxPrefab == null)
            return;

        ClearBoxes();
        if (detections == null || detections.Count == 0)
            return;

        Vector2 size = inputSize;

        float contentMinX = 0f;
        float contentMinY = 0f;
        float contentWidth = 1f;
        float contentHeight = 1f;

        for (int i = 0; i < detections.Count; i++)
        {
            var d = detections[i];
            float x1 = d.bbox.x;
            float y1 = d.bbox.y;
            float x2 = d.bbox.z;
            float y2 = d.bbox.w;

            float nxMin = x1 / size.x;
            float nxMax = x2 / size.x;
            float nyMin = y1 / size.y;
            float nyMax = y2 / size.y;

            float w = nxMax - nxMin;
            float h = nyMax - nyMin;
            if (w < 0.002f || h < 0.002f)
                continue;

            // Image Y is top-down; UI is bottom-up
            float uiYMin = 1f - nyMax;
            float uiYMax = 1f - nyMin;

            float axMin = contentMinX + nxMin * contentWidth;
            float axMax = contentMinX + nxMax * contentWidth;
            float ayMin = contentMinY + uiYMin * contentHeight;
            float ayMax = contentMinY + uiYMax * contentHeight;

            var box = GetBox(overlayParent);
            box.SetParent(overlayParent, false);
            box.anchorMin = new Vector2(axMin, ayMin);
            box.anchorMax = new Vector2(axMax, ayMax);
            box.offsetMin = Vector2.zero;
            box.offsetMax = Vector2.zero;
            m_activeBoxes.Add(box);
        }
    }

    /// <summary>
    /// Updates 2D box colors based on a selection range so screen-space boxes
    /// visually track the same selection as proxy labels and (optionally) world-space boxes.
    /// </summary>
    public void UpdateSelectionRangeHighlight(int minIndex, int maxIndex, Color selectedColor, Color normalColor)
    {
        if (m_activeBoxes.Count == 0)
            return;

        for (int i = 0; i < m_activeBoxes.Count; i++)
        {
            var boxRoot = m_activeBoxes[i];
            if (boxRoot == null) continue;

            bool inRange = (minIndex >= 0 && maxIndex >= minIndex && i >= minIndex && i <= maxIndex);
            var color = inRange ? selectedColor : normalColor;

            var images = boxRoot.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.color = color;
            }

            if (images.Length == 0)
            {
                var graphic = boxRoot.GetComponentInChildren<Graphic>(true);
                if (graphic != null)
                {
                    graphic.color = color;
                }
            }
        }
    }

    /// <summary>
    /// Convenience wrapper for single-index selection (min = max = selectedIndex).
    /// </summary>
    public void UpdateSelectionHighlight(int selectedIndex, Color selectedColor, Color normalColor)
    {
        UpdateSelectionRangeHighlight(selectedIndex, selectedIndex, selectedColor, normalColor);
    }

    private RectTransform GetBox(RectTransform overlayParent)
    {
        if (m_boxPool.Count > 0)
        {
            var rt = m_boxPool[m_boxPool.Count - 1];
            m_boxPool.RemoveAt(m_boxPool.Count - 1);
            rt.gameObject.SetActive(true);
            return rt;
        }
        var instance = Instantiate(m_boxPrefab, overlayParent);
        instance.gameObject.SetActive(true);
        return instance;
    }
}
