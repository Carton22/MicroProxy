using UnityEngine;

[DefaultExecutionOrder(1500)]
public class ProxySetMiniCameraAligner : MonoBehaviour
{
    [SerializeField] private Transform m_proxySetsRoot;
    [SerializeField] private RectTransform m_miniCameraRect;
    [SerializeField] private float m_proxySetsX = 0.42f;
    [SerializeField] private float m_verticalOffset;
    [SerializeField] private bool m_keepAlignedContinuously = true;
    [SerializeField] private int m_startupRefreshFrames = 6;
    [SerializeField] private float m_hiddenAlphaThreshold = 0.05f;

    private readonly Vector3[] m_worldCorners = new Vector3[4];
    private int m_refreshFramesRemaining;

    private void Reset()
    {
        m_proxySetsRoot = transform;
    }

    private void OnValidate()
    {
        m_startupRefreshFrames = Mathf.Max(0, m_startupRefreshFrames);
        m_hiddenAlphaThreshold = Mathf.Clamp01(m_hiddenAlphaThreshold);
    }

    private void OnEnable()
    {
        EnsureReferences();
        m_refreshFramesRemaining = m_startupRefreshFrames;
    }

    private void LateUpdate()
    {
        EnsureReferences();
        if (m_proxySetsRoot == null)
            return;

        if (!m_keepAlignedContinuously && m_refreshFramesRemaining <= 0)
            return;

        Canvas.ForceUpdateCanvases();
        AlignAllProxySets();

        if (m_refreshFramesRemaining > 0)
            m_refreshFramesRemaining--;
    }

    private void EnsureReferences()
    {
        if (m_proxySetsRoot == null)
            m_proxySetsRoot = transform;
    }

    private void AlignAllProxySets()
    {
        Bounds miniCameraBounds = default;
        bool hasMiniCameraBounds = m_miniCameraRect != null && TryGetBoundsInRootSpace(m_miniCameraRect, out miniCameraBounds);
        float targetCenterY = hasMiniCameraBounds ? miniCameraBounds.center.y + m_verticalOffset : 0f;

        for (int i = 0; i < m_proxySetsRoot.childCount; i++)
        {
            var proxySetRect = m_proxySetsRoot.GetChild(i) as RectTransform;
            if (proxySetRect == null)
                continue;

            Vector2 nextAnchoredPosition = proxySetRect.anchoredPosition;
            nextAnchoredPosition.x = m_proxySetsX;

            if (hasMiniCameraBounds && TryGetProxySetBoundsInRootSpace(proxySetRect, out Bounds proxySetBounds))
                nextAnchoredPosition.y += targetCenterY - proxySetBounds.center.y;

            if ((nextAnchoredPosition - proxySetRect.anchoredPosition).sqrMagnitude <= 0.0000001f)
                continue;

            proxySetRect.anchoredPosition = nextAnchoredPosition;
        }
    }

    private bool TryGetProxySetBoundsInRootSpace(RectTransform proxySetRect, out Bounds bounds)
    {
        if (TryCollectChildBounds(proxySetRect, ignoreHiddenChildren: true, out bounds))
            return true;

        if (TryCollectChildBounds(proxySetRect, ignoreHiddenChildren: false, out bounds))
            return true;

        return TryGetBoundsInRootSpace(proxySetRect, out bounds);
    }

    private bool TryCollectChildBounds(RectTransform proxySetRect, bool ignoreHiddenChildren, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < proxySetRect.childCount; i++)
        {
            var childRect = proxySetRect.GetChild(i) as RectTransform;
            if (childRect == null || !childRect.gameObject.activeSelf)
                continue;

            if (ignoreHiddenChildren && IsEffectivelyHidden(childRect))
                continue;

            if (!TryGetBoundsInRootSpace(childRect, out Bounds childBounds))
                continue;

            if (!hasBounds)
            {
                bounds = childBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(childBounds.min);
                bounds.Encapsulate(childBounds.max);
            }
        }

        return hasBounds;
    }

    private bool IsEffectivelyHidden(RectTransform rect)
    {
        var canvasGroup = rect.GetComponent<CanvasGroup>();
        return canvasGroup != null && canvasGroup.alpha <= m_hiddenAlphaThreshold;
    }

    private bool TryGetBoundsInRootSpace(RectTransform rect, out Bounds bounds)
    {
        bounds = default;
        if (rect == null || m_proxySetsRoot == null)
            return false;

        rect.GetWorldCorners(m_worldCorners);

        Vector3 min = m_proxySetsRoot.InverseTransformPoint(m_worldCorners[0]);
        Vector3 max = min;

        for (int i = 1; i < m_worldCorners.Length; i++)
        {
            Vector3 point = m_proxySetsRoot.InverseTransformPoint(m_worldCorners[i]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        bounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }
}
