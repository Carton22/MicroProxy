using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Creates a wheel-like focus window for proxy labels.
/// - Keeps a small band of rows around the current selection visually prominent.
/// - Smoothly eases labels in/out as selection moves.
/// - Adds a slight bend so the list feels like a scrolling wheel instead of a flat grid.
/// </summary>
public class ProxyLabelHorizonScroller : MonoBehaviour
{
    private sealed class LabelVisualState
    {
        public RectTransform Rect;
        public CanvasGroup CanvasGroup;
        public Vector2 AuthoredAnchoredPosition;
        public Vector3 AuthoredScale;
        public TMP_Text[] Texts;
        public Color[] AuthoredTextColors;
        public Renderer[] TextRenderers;
    }

    [Header("Label source")]
    [SerializeField] private ProxyLabelManager m_labelManager;

    [Header("Layout refs")]
    [Tooltip("Optional focus frame. If null, the content rect itself is used as the wheel frame.")]
    [SerializeField] private RectTransform m_viewport;

    [Tooltip("The RectTransform containing the GridLayoutGroup content.")]
    [SerializeField] private RectTransform m_content;

    [Tooltip("Optional. If null, tries to find GridLayoutGroup on the content.")]
    [SerializeField] private GridLayoutGroup m_gridLayout;

    [Header("Window")]
    [Tooltip("How many rows stay in the wheel window at once. Five works well for the bookshelf proxy set.")]
    [SerializeField] private int m_visibleRowCount = 5;

    [Tooltip("If true, the wheel follows the current selection.")]
    [SerializeField] private bool m_centerSelectedLabel = true;

    [Tooltip("If true, the wheel window sticks to the start/end instead of leaving empty space near the edges.")]
    [SerializeField] private bool m_clampToContentBounds = true;

    [Header("Animation")]
    [SerializeField] private float m_scrollSmoothTime = 0.12f;
    [SerializeField] private float m_visualLerpSpeed = 12f;
    [SerializeField] private float m_focusScale = 1.08f;
    [SerializeField] private float m_edgeScale = 0.84f;
    [SerializeField] private float m_hiddenScale = 0.62f;
    [SerializeField] private float m_sideBend = 24f;
    [SerializeField] private float m_rowBend = 14f;
    [SerializeField] private float m_focusAnchorY = 0f;
    [Range(0f, 1f)] [SerializeField] private float m_edgeAlpha = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float m_hiddenAlpha = 0f;
    [SerializeField] private bool m_disableRaycastsForHiddenItems = true;
    [SerializeField] private bool m_hideLabelsCompletelyOutsideWindow = true;
    [SerializeField] private float m_hardCullAlphaThreshold = 0.02f;

    private readonly List<LabelVisualState> m_labelStates = new();
    private readonly List<float> m_rowAnchoredPositions = new();

    private int m_lastChildCount = -1;
    private Vector2 m_lastContentSize;
    private float m_smoothedWindowCenterRow;
    private float m_windowCenterVelocity;
    private bool m_hasInitializedWindowCenter;

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        m_content = transform as RectTransform;
        m_viewport = m_content;
        if (m_content != null)
            m_gridLayout = m_content.GetComponent<GridLayoutGroup>();
    }

    private void OnValidate()
    {
        m_visibleRowCount = Mathf.Max(1, m_visibleRowCount);
        if ((m_visibleRowCount & 1) == 0)
            m_visibleRowCount += 1;

        m_scrollSmoothTime = Mathf.Max(0.01f, m_scrollSmoothTime);
        m_visualLerpSpeed = Mathf.Max(0.01f, m_visualLerpSpeed);
        m_focusScale = Mathf.Max(0.01f, m_focusScale);
        m_edgeScale = Mathf.Max(0.01f, m_edgeScale);
        m_hiddenScale = Mathf.Max(0.01f, m_hiddenScale);
        m_hardCullAlphaThreshold = Mathf.Clamp01(m_hardCullAlphaThreshold);
    }

    private void OnEnable()
    {
        EnsureReferences();
        RebuildAuthoredLayout(force: true);
        SnapToCurrentSelection();
    }

    private void OnDisable()
    {
        RestoreAuthoredVisuals();
        m_hasInitializedWindowCenter = false;
        m_windowCenterVelocity = 0f;
    }

    private void LateUpdate()
    {
        EnsureReferences();
        if (m_content == null || !m_content.gameObject.activeInHierarchy)
            return;

        RebuildAuthoredLayout(force: false);
        if (m_labelStates.Count == 0)
            return;

        int selectedIndex = FindSelectedIndex();
        if (selectedIndex < 0)
            selectedIndex = Mathf.Clamp(GetFallbackSelectedIndex(), 0, m_labelStates.Count - 1);

        int columnCount = GetColumnCount();
        int totalRows = Mathf.CeilToInt((float)m_labelStates.Count / columnCount);
        if (totalRows <= 0)
            return;

        int selectedRow = selectedIndex / columnCount;
        float targetWindowCenterRow = m_centerSelectedLabel
            ? GetTargetWindowCenterRow(selectedRow, totalRows)
            : selectedRow;

        if (!m_hasInitializedWindowCenter)
        {
            m_smoothedWindowCenterRow = targetWindowCenterRow;
            m_hasInitializedWindowCenter = true;
        }
        else
        {
            m_smoothedWindowCenterRow = Mathf.SmoothDamp(
                m_smoothedWindowCenterRow,
                targetWindowCenterRow,
                ref m_windowCenterVelocity,
                m_scrollSmoothTime
            );
        }

        ApplyWheelVisuals(selectedIndex, columnCount, totalRows, Time.unscaledDeltaTime);
    }

    private void EnsureReferences()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (m_content == null)
            m_content = transform as RectTransform;

        if (m_viewport == null)
            m_viewport = m_content;

        if (m_gridLayout == null && m_content != null)
            m_gridLayout = m_content.GetComponent<GridLayoutGroup>();
    }

    private void RebuildAuthoredLayout(bool force)
    {
        if (m_content == null)
            return;

        Vector2 currentSize = m_content.rect.size;
        bool needsRefresh = force
            || m_labelStates.Count == 0
            || m_content.childCount != m_lastChildCount
            || currentSize != m_lastContentSize;

        if (!needsRefresh)
            return;

        Canvas.ForceUpdateCanvases();
        if (m_gridLayout != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_content);

        m_labelStates.Clear();
        m_rowAnchoredPositions.Clear();

        int columnCount = GetColumnCount();
        for (int i = 0; i < m_content.childCount; i++)
        {
            var child = m_content.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            var canvasGroup = child.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = child.gameObject.AddComponent<CanvasGroup>();

            var texts = child.GetComponentsInChildren<TMP_Text>(true);
            var textColors = new Color[texts.Length];
            var textRenderers = new Renderer[texts.Length];
            for (int textIndex = 0; textIndex < texts.Length; textIndex++)
            {
                var text = texts[textIndex];
                if (text == null)
                    continue;

                textColors[textIndex] = text.color;
                textRenderers[textIndex] = text.GetComponent<Renderer>();
            }

            m_labelStates.Add(new LabelVisualState
            {
                Rect = child,
                CanvasGroup = canvasGroup,
                AuthoredAnchoredPosition = child.anchoredPosition,
                AuthoredScale = child.localScale,
                Texts = texts,
                AuthoredTextColors = textColors,
                TextRenderers = textRenderers
            });

            int row = i / columnCount;
            if (row >= m_rowAnchoredPositions.Count)
                m_rowAnchoredPositions.Add(child.anchoredPosition.y);
        }

        m_lastChildCount = m_content.childCount;
        m_lastContentSize = currentSize;
    }

    private void SnapToCurrentSelection()
    {
        if (m_labelStates.Count == 0)
            return;

        int selectedIndex = FindSelectedIndex();
        if (selectedIndex < 0)
            selectedIndex = Mathf.Clamp(GetFallbackSelectedIndex(), 0, m_labelStates.Count - 1);

        int columnCount = GetColumnCount();
        int totalRows = Mathf.CeilToInt((float)m_labelStates.Count / columnCount);
        int selectedRow = selectedIndex / columnCount;

        m_smoothedWindowCenterRow = m_centerSelectedLabel
            ? GetTargetWindowCenterRow(selectedRow, totalRows)
            : selectedRow;
        m_windowCenterVelocity = 0f;
        m_hasInitializedWindowCenter = true;

        ApplyWheelVisuals(selectedIndex, columnCount, totalRows, float.MaxValue);
    }

    private void ApplyWheelVisuals(int selectedIndex, int columnCount, int totalRows, float deltaTime)
    {
        if (m_labelStates.Count == 0 || totalRows <= 0)
            return;

        float lerpFactor = deltaTime == float.MaxValue
            ? 1f
            : 1f - Mathf.Exp(-m_visualLerpSpeed * Mathf.Max(0f, deltaTime));

        float focusRowY = GetInterpolatedRowY(m_smoothedWindowCenterRow);
        float scrollOffsetY = m_centerSelectedLabel ? m_focusAnchorY - focusRowY : 0f;
        float halfWindow = (m_visibleRowCount - 1) * 0.5f;
        float softWindow = halfWindow + 0.85f;

        for (int i = 0; i < m_labelStates.Count; i++)
        {
            var state = m_labelStates[i];
            if (state.Rect == null)
                continue;

            int row = i / columnCount;
            int column = i % columnCount;

            float rowDistance = row - m_smoothedWindowCenterRow;
            float absRowDistance = Mathf.Abs(rowDistance);
            float bandT = EaseOutCubic(Mathf.InverseLerp(softWindow, 0f, absRowDistance));
            float focusT = EaseOutCubic(Mathf.InverseLerp(1.15f, 0f, absRowDistance));
            bool isSelected = i == selectedIndex;

            float columnOffset = GetCenteredColumnOffset(column, columnCount);
            float bendAmount = 1f - bandT;
            float xOffset = columnOffset * m_sideBend * bendAmount;
            float yOffset = Mathf.Sign(rowDistance) * m_rowBend * bendAmount;

            Vector2 targetAnchoredPosition = state.AuthoredAnchoredPosition + new Vector2(xOffset, scrollOffsetY + yOffset);
            float targetScaleFactor = Mathf.Lerp(m_hiddenScale, m_edgeScale, bandT);
            targetScaleFactor = Mathf.Lerp(targetScaleFactor, m_focusScale, focusT);
            if (isSelected)
                targetScaleFactor = Mathf.Max(targetScaleFactor, m_focusScale);

            float targetAlpha = Mathf.Lerp(m_hiddenAlpha, m_edgeAlpha, bandT);
            targetAlpha = Mathf.Lerp(targetAlpha, 1f, focusT);
            if (isSelected)
                targetAlpha = 1f;

            state.Rect.anchoredPosition = Vector2.Lerp(state.Rect.anchoredPosition, targetAnchoredPosition, lerpFactor);
            state.Rect.localScale = Vector3.Lerp(state.Rect.localScale, state.AuthoredScale * targetScaleFactor, lerpFactor);
            float appliedAlpha = Mathf.Lerp(state.CanvasGroup.alpha, targetAlpha, lerpFactor);
            state.CanvasGroup.alpha = appliedAlpha;
            ApplyTextAlpha(state, appliedAlpha);

            if (m_disableRaycastsForHiddenItems)
            {
                bool isVisibleEnough = targetAlpha > 0.1f;
                state.CanvasGroup.blocksRaycasts = isVisibleEnough;
            }

            bool shouldRender = !m_hideLabelsCompletelyOutsideWindow || appliedAlpha > m_hardCullAlphaThreshold || isSelected;
            SetTextRenderersEnabled(state, shouldRender);
        }
    }

    private void RestoreAuthoredVisuals()
    {
        for (int i = 0; i < m_labelStates.Count; i++)
        {
            var state = m_labelStates[i];
            if (state.Rect == null)
                continue;

            state.Rect.anchoredPosition = state.AuthoredAnchoredPosition;
            state.Rect.localScale = state.AuthoredScale;

            if (state.CanvasGroup != null)
            {
                state.CanvasGroup.alpha = 1f;
                state.CanvasGroup.blocksRaycasts = true;
            }

            ApplyTextAlpha(state, 1f);
            SetTextRenderersEnabled(state, true);
        }
    }

    private int FindSelectedIndex()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return -1;

        for (int i = 0; i < m_labelStates.Count; i++)
        {
            var state = m_labelStates[i];
            if (state.Rect == null)
                continue;

            if (selected == state.Rect.gameObject || selected.transform.IsChildOf(state.Rect))
                return i;
        }

        return -1;
    }

    private int GetFallbackSelectedIndex()
    {
        if (m_labelManager != null)
        {
            var activeParent = m_labelManager.GetActiveLabelsParent();
            if (activeParent == m_content)
            {
                int managerIndex = m_labelManager.GetSelectedLabelIndex();
                if (managerIndex >= 0)
                    return managerIndex;
            }
        }

        return 0;
    }

    private int GetColumnCount()
    {
        if (m_gridLayout != null && m_gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            return Mathf.Max(1, m_gridLayout.constraintCount);

        return 1;
    }

    private float GetTargetWindowCenterRow(int selectedRow, int totalRows)
    {
        if (totalRows <= 0)
            return 0f;

        if (!m_clampToContentBounds)
            return selectedRow;

        if (totalRows <= m_visibleRowCount)
            return (totalRows - 1) * 0.5f;

        float halfWindow = (m_visibleRowCount - 1) * 0.5f;
        float min = halfWindow;
        float max = totalRows - 1 - halfWindow;
        return Mathf.Clamp(selectedRow, min, max);
    }

    private float GetInterpolatedRowY(float rowIndex)
    {
        if (m_rowAnchoredPositions.Count == 0)
            return 0f;

        if (m_rowAnchoredPositions.Count == 1)
            return m_rowAnchoredPositions[0];

        float clamped = Mathf.Clamp(rowIndex, 0f, m_rowAnchoredPositions.Count - 1);
        int lower = Mathf.FloorToInt(clamped);
        int upper = Mathf.Min(lower + 1, m_rowAnchoredPositions.Count - 1);
        float t = clamped - lower;
        return Mathf.Lerp(m_rowAnchoredPositions[lower], m_rowAnchoredPositions[upper], t);
    }

    private static float GetCenteredColumnOffset(int columnIndex, int columnCount)
    {
        if (columnCount <= 1)
            return 0f;

        return columnIndex - (columnCount - 1) * 0.5f;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static void ApplyTextAlpha(LabelVisualState state, float alphaMultiplier)
    {
        if (state.Texts == null || state.AuthoredTextColors == null)
            return;

        int count = Mathf.Min(state.Texts.Length, state.AuthoredTextColors.Length);
        for (int i = 0; i < count; i++)
        {
            var text = state.Texts[i];
            if (text == null)
                continue;

            var color = state.AuthoredTextColors[i];
            color.a *= alphaMultiplier;
            text.color = color;
        }
    }

    private static void SetTextRenderersEnabled(LabelVisualState state, bool enabled)
    {
        if (state.TextRenderers == null)
            return;

        for (int i = 0; i < state.TextRenderers.Length; i++)
        {
            var renderer = state.TextRenderers[i];
            if (renderer == null)
                continue;

            renderer.enabled = enabled;
        }
    }
}
