using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class MetaProxyPageVisualStyleBootstrap : MonoBehaviour
{
    private sealed class StyledRootState
    {
        public RectTransform Root;
        public GridLayoutGroup GridLayout;
        public ProxyLabelHorizonScroller Scroller;
        public RectTransform UnderlayRoot;
        public RawImage Backplate;
        public RectTransform OverlayRoot;
        public RawImage LeftFade;
        public RawImage RightFade;
        public int LastVisibleCardCount = -1;
        public int LastConfiguredWindowCount = -1;
    }

    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private List<RectTransform> m_pageRoots = new();
    [SerializeField] private int m_visibleCardCount = 3;
    [SerializeField] private Vector2 m_backplatePadding = new(36f, 18f);
    [SerializeField] private float m_edgeFadeWidth = 82f;
    [SerializeField] private float m_cornerRadius = 28f;
    [SerializeField] private Color m_backplateColor = new(0.05f, 0.09f, 0.13f, 0.24f);
    [SerializeField] private Color m_edgeFadeColor = new(0.05f, 0.09f, 0.13f, 0.42f);
    [SerializeField] private bool m_debugLog;

    private readonly List<StyledRootState> m_states = new();
    private Texture2D m_backplateTexture;
    private Texture2D m_leftFadeTexture;
    private Texture2D m_rightFadeTexture;

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_pageRoots.Count > 0)
            return;

        var roots = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TryAddRootByName(roots, "RootNode");
        TryAddRootByName(roots, "Cars");
        TryAddRootByName(roots, "ProxyUI");
        TryAddRootByName(roots, "Attribute-Owner");
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyStyle();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        ApplyStyle();
        SyncChrome();
    }

    private void OnDisable()
    {
        for (int i = 0; i < m_states.Count; i++)
        {
            DestroyObject(m_states[i].UnderlayRoot);
            DestroyObject(m_states[i].OverlayRoot);
        }

        m_states.Clear();
        DestroyObject(m_backplateTexture);
        DestroyObject(m_leftFadeTexture);
        DestroyObject(m_rightFadeTexture);
        m_backplateTexture = null;
        m_leftFadeTexture = null;
        m_rightFadeTexture = null;
    }

    public void ApplyStyle()
    {
        EnsureTextures();

        for (int i = 0; i < m_pageRoots.Count; i++)
        {
            var root = m_pageRoots[i];
            if (root == null)
                continue;

            var state = GetOrCreateState(root);
            ConfigureScroller(state);
            EnsureChromeObjects(state);
        }
    }

    private void ResolveReferences()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        EnsurePageRoots();
    }

    private void EnsurePageRoots()
    {
        bool needsPopulation = m_pageRoots.Count == 0;
        if (!needsPopulation)
        {
            for (int i = 0; i < m_pageRoots.Count; i++)
            {
                if (m_pageRoots[i] == null)
                {
                    needsPopulation = true;
                    break;
                }
            }
        }

        if (!needsPopulation)
            return;

        var roots = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TryPopulateRootSlot(roots, 0, "RootNode");
        TryPopulateRootSlot(roots, 1, "Cars");
        TryPopulateRootSlot(roots, 2, "ProxyUI");
        TryPopulateRootSlot(roots, 3, "Attribute-Owner");
    }

    private void TryPopulateRootSlot(RectTransform[] roots, int index, string name)
    {
        while (m_pageRoots.Count <= index)
            m_pageRoots.Add(null);

        if (m_pageRoots[index] != null)
            return;

        for (int i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root != null && root.name == name)
            {
                m_pageRoots[index] = root;
                return;
            }
        }
    }

    private StyledRootState GetOrCreateState(RectTransform root)
    {
        for (int i = 0; i < m_states.Count; i++)
        {
            if (m_states[i].Root == root)
                return m_states[i];
        }

        var state = new StyledRootState
        {
            Root = root,
            GridLayout = root.GetComponent<GridLayoutGroup>()
        };

        m_states.Add(state);
        return state;
    }

    private void ConfigureScroller(StyledRootState state)
    {
        if (state.Root == null)
            return;

        if (state.GridLayout == null)
            state.GridLayout = state.Root.GetComponent<GridLayoutGroup>();
        if (state.GridLayout == null)
            return;

        bool createdScroller = false;
        if (state.Scroller == null)
            state.Scroller = state.Root.GetComponent<ProxyLabelHorizonScroller>();
        if (state.Scroller == null)
        {
            state.Scroller = state.Root.gameObject.AddComponent<ProxyLabelHorizonScroller>();
            createdScroller = true;
        }

        state.Scroller.Configure(m_labelManager, state.Root, state.Root, state.GridLayout);
        state.Scroller.ApplyMetaRayBanHorizontalPreset(m_visibleCardCount);

        if (state.Root.gameObject.activeInHierarchy &&
            (createdScroller || state.LastConfiguredWindowCount != m_visibleCardCount))
            state.Scroller.ForceRefreshNow();

        state.LastConfiguredWindowCount = m_visibleCardCount;
    }

    private void EnsureChromeObjects(StyledRootState state)
    {
        if (state.Root == null)
            return;

        var parent = state.Root.parent as RectTransform;
        if (parent == null)
            return;

        if (state.UnderlayRoot == null)
        {
            var underlayObject = new GameObject($"{state.Root.name}__ViewportUnderlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            state.UnderlayRoot = underlayObject.GetComponent<RectTransform>();
            state.UnderlayRoot.SetParent(parent, false);
            state.Backplate = underlayObject.GetComponent<RawImage>();
            state.Backplate.raycastTarget = false;
            state.Backplate.texture = m_backplateTexture;
            state.Backplate.color = m_backplateColor;
        }

        if (state.OverlayRoot == null)
        {
            var overlayObject = new GameObject($"{state.Root.name}__ViewportOverlay", typeof(RectTransform));
            state.OverlayRoot = overlayObject.GetComponent<RectTransform>();
            state.OverlayRoot.SetParent(parent, false);

            state.LeftFade = CreateFadeGraphic("LeftFade", state.OverlayRoot, m_leftFadeTexture, m_edgeFadeColor);
            state.RightFade = CreateFadeGraphic("RightFade", state.OverlayRoot, m_rightFadeTexture, m_edgeFadeColor);
        }
    }

    private void SyncChrome()
    {
        for (int i = 0; i < m_states.Count; i++)
        {
            var state = m_states[i];
            if (state.Root == null)
                continue;

            EnsureChromeObjects(state);
            if (state.UnderlayRoot == null || state.OverlayRoot == null || state.GridLayout == null)
                continue;

            bool isActive = state.Root.gameObject.activeInHierarchy;
            state.UnderlayRoot.gameObject.SetActive(isActive);
            state.OverlayRoot.gameObject.SetActive(isActive);
            if (!isActive)
                continue;

            MatchRootTransform(state.Root, state.UnderlayRoot);
            MatchRootTransform(state.Root, state.OverlayRoot);

            int rootSiblingIndex = state.Root.GetSiblingIndex();
            state.UnderlayRoot.SetSiblingIndex(Mathf.Max(0, rootSiblingIndex));
            state.OverlayRoot.SetSiblingIndex(Mathf.Min(state.OverlayRoot.parent.childCount - 1, state.Root.GetSiblingIndex() + 1));

            int visibleCards = Mathf.Max(1, Mathf.Min(m_visibleCardCount, CountVisibleDirectChildren(state.Root)));
            if (visibleCards != state.LastVisibleCardCount)
            {
                state.LastVisibleCardCount = visibleCards;
                if (state.Scroller != null)
                    state.Scroller.ForceRefreshNow();
            }

            float width = (visibleCards * state.GridLayout.cellSize.x)
                + (Mathf.Max(0, visibleCards - 1) * state.GridLayout.spacing.x)
                + (m_backplatePadding.x * 2f);
            float height = state.GridLayout.cellSize.y + (m_backplatePadding.y * 2f);

            state.Backplate.color = m_backplateColor;
            state.Backplate.texture = m_backplateTexture;
            state.UnderlayRoot.sizeDelta = state.Root.sizeDelta;
            state.Backplate.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            state.Backplate.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            state.Backplate.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            state.Backplate.rectTransform.anchoredPosition = Vector2.zero;
            state.Backplate.rectTransform.sizeDelta = new Vector2(width, height);

            state.LeftFade.color = m_edgeFadeColor;
            state.RightFade.color = m_edgeFadeColor;
            ConfigureFadeRect(state.LeftFade.rectTransform, width, height, isLeft: true);
            ConfigureFadeRect(state.RightFade.rectTransform, width, height, isLeft: false);
        }
    }

    private RawImage CreateFadeGraphic(string name, RectTransform parent, Texture texture, Color color)
    {
        var fadeObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        var fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.SetParent(parent, false);

        var rawImage = fadeObject.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = color;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private void ConfigureFadeRect(RectTransform fadeRect, float width, float height, bool isLeft)
    {
        if (fadeRect == null)
            return;

        fadeRect.anchorMin = new Vector2(0.5f, 0.5f);
        fadeRect.anchorMax = new Vector2(0.5f, 0.5f);
        fadeRect.pivot = new Vector2(0.5f, 0.5f);
        fadeRect.sizeDelta = new Vector2(m_edgeFadeWidth, height);

        float x = (width * 0.5f) - (m_edgeFadeWidth * 0.5f);
        fadeRect.anchoredPosition = new Vector2(isLeft ? -x : x, 0f);
    }

    private void MatchRootTransform(RectTransform source, RectTransform target)
    {
        if (source == null || target == null)
            return;

        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private int CountVisibleDirectChildren(RectTransform root)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy)
                count++;
        }

        return count;
    }

    private void EnsureTextures()
    {
        if (m_backplateTexture == null)
            m_backplateTexture = CreateRoundedRectTexture(512, 256, m_cornerRadius, 4f, topAlphaMultiplier: 0.94f, bottomAlphaMultiplier: 1f);
        if (m_leftFadeTexture == null)
            m_leftFadeTexture = CreateHorizontalFadeTexture(128, 32, leftOpaque: true);
        if (m_rightFadeTexture == null)
            m_rightFadeTexture = CreateHorizontalFadeTexture(128, 32, leftOpaque: false);
    }

    private Texture2D CreateRoundedRectTexture(int width, int height, float cornerRadius, float edgeSoftness, float topAlphaMultiplier, float bottomAlphaMultiplier)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float radius = Mathf.Max(1f, cornerRadius);
        var pixels = new Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            float normalizedY = height > 1 ? y / (float)(height - 1) : 0f;
            float verticalMultiplier = Mathf.Lerp(bottomAlphaMultiplier, topAlphaMultiplier, normalizedY);

            for (int x = 0; x < width; x++)
            {
                float px = (x + 0.5f) - halfWidth;
                float py = (y + 0.5f) - halfHeight;
                float dx = Mathf.Abs(px) - (halfWidth - radius);
                float dy = Mathf.Abs(py) - (halfHeight - radius);
                float outsideX = Mathf.Max(dx, 0f);
                float outsideY = Mathf.Max(dy, 0f);
                float outsideDistance = Mathf.Sqrt((outsideX * outsideX) + (outsideY * outsideY));
                float insideDistance = Mathf.Min(Mathf.Max(dx, dy), 0f);
                float signedDistance = outsideDistance + insideDistance - radius;
                float alpha = 1f - Mathf.SmoothStep(-edgeSoftness, edgeSoftness, signedDistance);
                byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha * verticalMultiplier) * 255f);
                pixels[(y * width) + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private Texture2D CreateHorizontalFadeTexture(int width, int height, bool leftOpaque)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float t = width > 1 ? x / (float)(width - 1) : 0f;
                float alpha = leftOpaque ? Mathf.Pow(1f - t, 1.85f) : Mathf.Pow(t, 1.85f);
                byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
                pixels[(y * width) + x] = new Color32(255, 255, 255, alphaByte);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void TryAddRootByName(RectTransform[] roots, string name)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            var candidate = roots[i];
            if (candidate == null || candidate.name != name)
                continue;

            if (!m_pageRoots.Contains(candidate))
                m_pageRoots.Add(candidate);
            return;
        }
    }

    private void DestroyObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}
