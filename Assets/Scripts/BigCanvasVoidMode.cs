using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hides the camera stream (and optionally all graphics) on the big camera-to-world canvas
/// so it acts as a void canvas for ray-casting only and does not block the minicamera view.
/// Does not modify existing camera-to-world scripts; only disables/hides graphics on the canvas.
/// </summary>
public class BigCanvasVoidMode : MonoBehaviour
{
    [Header("Big canvas (for ray-casting)")]
    [Tooltip("The big camera-to-world canvas GameObject (e.g. CameraToWorldCameraCanvas). Its RectTransform stays active for ray-casting; visuals are hidden.")]
    [SerializeField] private RectTransform m_bigCanvas;

    [Tooltip("If set, only this RawImage is hidden. Otherwise the first RawImage under the big canvas is used.")]
    [SerializeField] private RawImage m_streamRawImage;

    [Tooltip("If true, hide all Graphic components (RawImage, Image) under the big canvas so the whole canvas is invisible.")]
    [SerializeField] private bool m_hideAllGraphics;

    private void Start()
    {
        // Run after existing setup (e.g. MyCameraToWorldManager.Start, CameraToWorldCameraCanvas) so we hide after stream is assigned
        if (m_bigCanvas == null)
            return;

        if (m_hideAllGraphics)
        {
            foreach (var g in m_bigCanvas.GetComponentsInChildren<Graphic>(true))
            {
                g.enabled = false;
            }
            return;
        }

        RawImage toHide = m_streamRawImage;
        if (toHide == null)
            toHide = m_bigCanvas.GetComponentInChildren<RawImage>(true);

        if (toHide != null)
            toHide.enabled = false;
    }
}
