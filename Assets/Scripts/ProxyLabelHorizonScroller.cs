using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Creates a "horizon" / viewport-like scroll effect for a long label list:
/// - A fixed number of rows are visible (viewport height is computed from GridLayoutGroup cell/spacing/padding).
/// - Whenever the selected label index changes, the content is shifted so the selected label is centered in the viewport.
///
/// Notes:
/// - This script relies on a Mask/RectMask2D on <see cref="m_viewport"/> to visually clip labels outside the horizon.
/// - It does not deactivate labels; it only moves the content.
/// </summary>
public class ProxyLabelHorizonScroller : MonoBehaviour
{
    [Header("Label source")]
    [SerializeField] private ProxyLabelManager m_labelManager;

    [Header("Layout refs")]
    [Tooltip("The visible clipped area (should have a Mask/RectMask2D).")]
    [SerializeField] private RectTransform m_viewport;

    [Tooltip("The RectTransform containing the GridLayoutGroup content (usually the active labels parent).")]
    [SerializeField] private RectTransform m_content;

    [Tooltip("Optional. If null, tries to find GridLayoutGroup on the active labels parent.")]
    [SerializeField] private GridLayoutGroup m_gridLayout;

    [Header("Horizon settings")]
    [Tooltip("How many grid rows should be visible in the viewport at once.")]
    [SerializeField] private int m_visibleRowCount = 10;

    [Tooltip("If true, keeps the selected label centered vertically.")]
    [SerializeField] private bool m_centerSelectedLabel = true;

    [Tooltip("If true, clamps content so the viewport isn't left empty at the top/bottom.")]
    [SerializeField] private bool m_clampToContentBounds = true;

    private int m_lastSelectedIndex = int.MinValue;
    private Transform m_lastActiveParent;

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    private void Start()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        RefreshGridAndViewportHeight();
        CenterIfNeeded(force: true);
    }

    private void LateUpdate()
    {
        CenterIfNeeded(force: false);
    }

    private void CenterIfNeeded(bool force)
    {
        if (m_labelManager == null || m_viewport == null || m_content == null)
            return;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        if (activeParent != null && activeParent != m_lastActiveParent)
        {
            m_lastActiveParent = activeParent;
            RefreshGridAndViewportHeight();
            force = true;
        }

        int selectedIndex = m_labelManager.GetSelectedLabelIndex();
        if (!force && selectedIndex == m_lastSelectedIndex)
            return;

        m_lastSelectedIndex = selectedIndex;

        if (!m_centerSelectedLabel)
            return;

        if (selectedIndex < 0)
            return;

        var selectedLabel = m_labelManager.GetLabelRectTransform(selectedIndex);
        if (selectedLabel == null)
            return;

        CenterOnLabel(selectedLabel);
    }

    private void RefreshGridAndViewportHeight()
    {
        if (m_viewport == null)
            return;

        if (m_gridLayout == null)
        {
            var activeParent = m_labelManager != null ? m_labelManager.GetActiveLabelsParent() : null;
            if (activeParent != null)
                m_gridLayout = activeParent.GetComponent<GridLayoutGroup>();
        }

        if (m_gridLayout == null)
            return;

        int rows = Mathf.Max(1, m_visibleRowCount);
        float cellH = m_gridLayout.cellSize.y;
        float spacingY = m_gridLayout.spacing.y;
        float paddingTop = m_gridLayout.padding.top;
        float paddingBottom = m_gridLayout.padding.bottom;

        // Height = top padding + N*cellH + (N-1)*spacingY + bottom padding.
        float viewportH = paddingTop + (rows * cellH) + Mathf.Max(0, rows - 1) * spacingY + paddingBottom;

        if (viewportH > 0f)
        {
            var size = m_viewport.sizeDelta;
            size.y = viewportH;
            m_viewport.sizeDelta = size;
        }
    }

    private void CenterOnLabel(RectTransform selectedLabel)
    {
        if (selectedLabel == null || m_viewport == null || m_content == null)
            return;

        // Compute selected label center in world space.
        var selectedWorldCenter = selectedLabel.TransformPoint(selectedLabel.rect.center);

        // Desired viewport center in world space.
        var viewportCenterLocal = new Vector3(m_viewport.rect.center.x, m_viewport.rect.center.y, 0f);
        var desiredWorldCenter = m_viewport.TransformPoint(viewportCenterLocal);

        // Move content so that its selectedLabel center matches the viewport center.
        var contentParent = m_content.parent;
        if (contentParent == null)
            return;

        var selectedOnParent = contentParent.InverseTransformPoint(selectedWorldCenter);
        var desiredOnParent = contentParent.InverseTransformPoint(desiredWorldCenter);
        Vector3 delta = desiredOnParent - selectedOnParent;

        var targetAnchored = m_content.anchoredPosition + new Vector2(delta.x, delta.y);
        m_content.anchoredPosition = targetAnchored;

        if (m_clampToContentBounds)
            ClampContentToViewport();
    }

    private void ClampContentToViewport()
    {
        if (m_viewport == null || m_content == null)
            return;

        // Relative bounds of content in viewport local space.
        var relBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(m_viewport, m_content);
        float vMin = m_viewport.rect.yMin;
        float vMax = m_viewport.rect.yMax;

        // If content is shorter than viewport, don't clamp.
        if (relBounds.size.y <= m_viewport.rect.height + 0.0001f)
            return;

        float correctionY = 0f;
        if (relBounds.max.y < vMax)
            correctionY = vMax - relBounds.max.y;       // move up
        else if (relBounds.min.y > vMin)
            correctionY = vMin - relBounds.min.y;       // move down (negative)

        if (Mathf.Abs(correctionY) < 0.0001f)
            return;

        // Apply correction in the viewport's local "up" direction, converted into content parent space.
        var contentParent = m_content.parent;
        if (contentParent == null)
            return;

        Vector3 correctionWorld = m_viewport.TransformVector(new Vector3(0f, correctionY, 0f));
        Vector3 correctionParent = contentParent.InverseTransformVector(correctionWorld);

        m_content.anchoredPosition += new Vector2(correctionParent.x, correctionParent.y);
    }
}

