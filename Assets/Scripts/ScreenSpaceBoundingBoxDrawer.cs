using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws 2D bounding boxes in screen space (e.g. over the passthrough camera view).
/// For alignment with world-space boxes: Fit Aspect Ratio = false, Flip Y = true (or try false if vertically off),
/// and use the same resolution as the camera image (Input Size Override 0,0 if server returns that resolution).
/// </summary>
public class ScreenSpaceBoundingBoxDrawer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform m_overlayParent;
    [SerializeField] private RectTransform m_boxPrefab;

    [Header("Coordinates")]
    [Tooltip("If set (x>0, y>0), use this resolution for normalizing bbox. Must match the resolution your server returns bboxes in (e.g. 640x480).")]
    [SerializeField] private Vector2Int m_inputSizeOverride;

    [Tooltip("When enabled, fits the detection aspect ratio inside the overlay (letterboxing). Disable for alignment with world-space boxes so 2D maps to full viewport.")]
    [SerializeField] private bool m_fitAspectRatio = false;

    [Tooltip("When enabled, flip Y (image top-down -> UI bottom-up). Disable if 2D boxes are vertically flipped vs world-space.")]
    [SerializeField] private bool m_flipY = true;

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

    /// <param name="detections">Each (classId, bbox) where bbox = (x1,y1,x2,y2) in input pixels.</param>
    /// <param name="inputSize">Resolution the detections are in (ignored if Input Size Override is set).</param>
    public void DrawBoxes(List<(int classId, Vector4 bbox)> detections, Vector2 inputSize)
    {
        if (m_overlayParent == null || m_boxPrefab == null)
            return;

        ClearBoxes();
        if (detections == null || detections.Count == 0)
            return;

        Vector2 size = (m_inputSizeOverride.x > 0 && m_inputSizeOverride.y > 0)
            ? new Vector2(m_inputSizeOverride.x, m_inputSizeOverride.y)
            : inputSize;

        float contentMinX, contentMinY, contentWidth, contentHeight;
        if (m_fitAspectRatio)
            GetAspectFittedContent(size, out contentMinX, out contentMinY, out contentWidth, out contentHeight);
        else
        {
            contentMinX = 0f;
            contentMinY = 0f;
            contentWidth = 1f;
            contentHeight = 1f;
        }

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

            float uiYMin = m_flipY ? (1f - nyMax) : nyMin;
            float uiYMax = m_flipY ? (1f - nyMin) : nyMax;

            float axMin = contentMinX + nxMin * contentWidth;
            float axMax = contentMinX + nxMax * contentWidth;
            float ayMin = contentMinY + uiYMin * contentHeight;
            float ayMax = contentMinY + uiYMax * contentHeight;

            var box = GetBox();
            box.SetParent(m_overlayParent, false);
            box.anchorMin = new Vector2(axMin, ayMin);
            box.anchorMax = new Vector2(axMax, ayMax);
            box.offsetMin = Vector2.zero;
            box.offsetMax = Vector2.zero;
            m_activeBoxes.Add(box);
        }
    }

    private void GetAspectFittedContent(Vector2 inputSize, out float minX, out float minY, out float width, out float height)
    {
        minX = 0f;
        minY = 0f;
        width = 1f;
        height = 1f;
        if (m_overlayParent == null || inputSize.x <= 0 || inputSize.y <= 0) return;

        float inputAspect = inputSize.x / inputSize.y;
        float overlayW = m_overlayParent.rect.width;
        float overlayH = m_overlayParent.rect.height;
        if (overlayW <= 0 || overlayH <= 0) return;
        float overlayAspect = overlayW / overlayH;

        if (inputAspect >= overlayAspect)
        {
            width = 1f;
            height = overlayAspect / inputAspect;
            minY = (1f - height) * 0.5f;
        }
        else
        {
            height = 1f;
            width = inputAspect / overlayAspect;
            minX = (1f - width) * 0.5f;
        }
    }

    private RectTransform GetBox()
    {
        if (m_boxPool.Count > 0)
        {
            var rt = m_boxPool[m_boxPool.Count - 1];
            m_boxPool.RemoveAt(m_boxPool.Count - 1);
            rt.gameObject.SetActive(true);
            return rt;
        }
        var instance = Instantiate(m_boxPrefab, m_overlayParent);
        instance.gameObject.SetActive(true);
        return instance;
    }
}
