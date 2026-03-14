using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniCameraBoxOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform m_overlayParent;     // parent over the mini camera image
    [SerializeField] private RectTransform m_boxPrefab;         // UI box prefab (disabled template)

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
    /// <param name="inputSize">Neural net input size, e.g. (640,480).</param>
    public void DrawBoxes(List<(int classId, Vector4 bbox)> detections, Vector2 inputSize)
    {
        if (m_overlayParent == null || m_boxPrefab == null)
            return;

        ClearBoxes();

        for (int i = 0; i < detections.Count; i++)
        {
            var d = detections[i];
            float x1 = d.bbox.x;
            float y1 = d.bbox.y;
            float x2 = d.bbox.z;
            float y2 = d.bbox.w;

            // Normalize to [0,1] in model space.
            float nxMin = x1 / inputSize.x;
            float nxMax = x2 / inputSize.x;
            float nyMin = y1 / inputSize.y;
            float nyMax = y2 / inputSize.y;

            // Model usually has origin at top‑left, UI at bottom‑left -> flip Y.
            float uiYMin = 1f - nyMax;
            float uiYMax = 1f - nyMin;

            var box = GetBox();
            box.SetParent(m_overlayParent, false);

            // Use anchors to place the box in the overlay.
            box.anchorMin = new Vector2(nxMin, uiYMin);
            box.anchorMax = new Vector2(nxMax, uiYMax);
            box.offsetMin = Vector2.zero;
            box.offsetMax = Vector2.zero;
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