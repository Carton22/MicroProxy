using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum ProxySetHorizontalTransitionDirection
{
    ToLeft = -1,
    ToRight = 1
}

/// <summary>
/// Plays a lightweight horizontal page transition between two sibling UI roots.
/// This keeps the authored hierarchy intact and restores the original layout state
/// when the transition completes so existing layout scripts continue to work.
/// </summary>
public sealed class ProxySetHorizontalTransitionPlayer : MonoBehaviour
{
    private sealed class TransitionSnapshot
    {
        public RectTransform Rect;
        public CanvasGroup CanvasGroup;
        public Vector2 AnchoredPosition;
        public Vector3 LocalScale;
        public float Alpha;
        public bool Interactable;
        public bool BlocksRaycasts;
        public int SiblingIndex;
    }

    [Header("Horizontal transition")]
    [SerializeField] private float m_transitionDuration = 0.3f;
    [SerializeField] private float m_sideOffsetMultiplier = 0.8f;
    [SerializeField] private float m_centerScale = 1f;
    [SerializeField] private float m_sideScale = 0.9f;
    [SerializeField] private float m_incomingScaleOvershoot = 1.1f;
    [Range(0f, 1f)] [SerializeField] private float m_centerAlpha = 1f;
    [Range(0f, 1f)] [SerializeField] private float m_sideAlpha = 0.1f;
    [SerializeField] private bool m_disableOutgoingRaycasts = true;

    private Coroutine m_transitionCoroutine;

    public bool IsTransitioning => m_transitionCoroutine != null;

    private void OnValidate()
    {
        m_transitionDuration = Mathf.Max(0.01f, m_transitionDuration);
        m_sideOffsetMultiplier = Mathf.Max(0f, m_sideOffsetMultiplier);
        m_centerScale = Mathf.Max(0.01f, m_centerScale);
        m_sideScale = Mathf.Max(0.01f, m_sideScale);
        m_incomingScaleOvershoot = Mathf.Max(0f, m_incomingScaleOvershoot);
        m_centerAlpha = Mathf.Clamp01(m_centerAlpha);
        m_sideAlpha = Mathf.Clamp01(m_sideAlpha);
    }

    public static ProxySetHorizontalTransitionPlayer GetOn(Transform commonParent)
    {
        return commonParent != null ? commonParent.GetComponent<ProxySetHorizontalTransitionPlayer>() : null;
    }

    public static ProxySetHorizontalTransitionPlayer GetOrCreate(Transform commonParent)
    {
        if (commonParent == null)
            return null;

        var player = commonParent.GetComponent<ProxySetHorizontalTransitionPlayer>();
        if (player == null)
            player = commonParent.gameObject.AddComponent<ProxySetHorizontalTransitionPlayer>();

        return player;
    }

    public bool TryPlay(
        RectTransform outgoing,
        RectTransform incoming,
        ProxySetHorizontalTransitionDirection direction,
        Action onComplete = null)
    {
        if (IsTransitioning || outgoing == null || incoming == null)
            return false;

        if (outgoing == incoming)
        {
            incoming.gameObject.SetActive(true);
            onComplete?.Invoke();
            return true;
        }

        if (outgoing.parent == null || outgoing.parent != incoming.parent)
            return false;

        m_transitionCoroutine = StartCoroutine(PlayTransition(outgoing, incoming, direction, onComplete));
        return true;
    }

    private IEnumerator PlayTransition(
        RectTransform outgoing,
        RectTransform incoming,
        ProxySetHorizontalTransitionDirection direction,
        Action onComplete)
    {
        var outgoingSnapshot = Capture(outgoing);
        var incomingSnapshot = Capture(incoming);

        incoming.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(outgoing);
        LayoutRebuilder.ForceRebuildLayoutImmediate(incoming);

        incoming.SetAsLastSibling();

        int directionSign = direction == ProxySetHorizontalTransitionDirection.ToRight ? 1 : -1;
        float travelDistance = ResolveTravelDistance(outgoing, incoming);
        Vector2 sideOffset = Vector2.right * (travelDistance * m_sideOffsetMultiplier * directionSign);

        ApplyInteractiveState(
            outgoingSnapshot,
            interactable: !m_disableOutgoingRaycasts,
            blocksRaycasts: !m_disableOutgoingRaycasts
        );
        ApplyInteractiveState(incomingSnapshot, interactable: true, blocksRaycasts: true);

        outgoing.anchoredPosition = outgoingSnapshot.AnchoredPosition;
        outgoing.localScale = outgoingSnapshot.LocalScale * m_centerScale;
        outgoingSnapshot.CanvasGroup.alpha = outgoingSnapshot.Alpha * m_centerAlpha;

        incoming.anchoredPosition = incomingSnapshot.AnchoredPosition - sideOffset;
        incoming.localScale = incomingSnapshot.LocalScale * m_sideScale;
        incomingSnapshot.CanvasGroup.alpha = incomingSnapshot.Alpha * m_sideAlpha;

        Vector3 outgoingStartScale = outgoingSnapshot.LocalScale * m_centerScale;
        Vector3 outgoingEndScale = outgoingSnapshot.LocalScale * m_sideScale;
        Vector3 incomingStartScale = incomingSnapshot.LocalScale * m_sideScale;
        Vector3 incomingEndScale = incomingSnapshot.LocalScale * m_centerScale;

        float duration = Mathf.Max(0.01f, m_transitionDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float outgoingMoveT = EaseInOutCubic(t);
            float incomingMoveT = EaseOutCubic(t);
            float outgoingFadeT = EaseInCubic(t);
            float incomingFadeT = EaseOutCubic(t);
            float incomingScaleT = EaseOutBack(t, m_incomingScaleOvershoot);

            outgoing.anchoredPosition = Vector2.Lerp(
                outgoingSnapshot.AnchoredPosition,
                outgoingSnapshot.AnchoredPosition + sideOffset,
                outgoingMoveT
            );
            outgoing.localScale = Vector3.Lerp(
                outgoingStartScale,
                outgoingEndScale,
                outgoingMoveT
            );
            outgoingSnapshot.CanvasGroup.alpha = Mathf.Lerp(
                outgoingSnapshot.Alpha * m_centerAlpha,
                outgoingSnapshot.Alpha * m_sideAlpha,
                outgoingFadeT
            );

            incoming.anchoredPosition = Vector2.Lerp(
                incomingSnapshot.AnchoredPosition - sideOffset,
                incomingSnapshot.AnchoredPosition,
                incomingMoveT
            );
            incoming.localScale = Vector3.LerpUnclamped(
                incomingStartScale,
                incomingEndScale,
                incomingScaleT
            );
            incomingSnapshot.CanvasGroup.alpha = Mathf.Lerp(
                incomingSnapshot.Alpha * m_sideAlpha,
                incomingSnapshot.Alpha * m_centerAlpha,
                incomingFadeT
            );

            yield return null;
        }

        RestoreSnapshot(outgoingSnapshot);
        RestoreSnapshot(incomingSnapshot);
        RestoreSiblingOrder(outgoingSnapshot, incomingSnapshot);

        onComplete?.Invoke();
        m_transitionCoroutine = null;
    }

    private static TransitionSnapshot Capture(RectTransform rect)
    {
        var canvasGroup = rect.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();

        return new TransitionSnapshot
        {
            Rect = rect,
            CanvasGroup = canvasGroup,
            AnchoredPosition = rect.anchoredPosition,
            LocalScale = rect.localScale,
            Alpha = canvasGroup.alpha,
            Interactable = canvasGroup.interactable,
            BlocksRaycasts = canvasGroup.blocksRaycasts,
            SiblingIndex = rect.GetSiblingIndex()
        };
    }

    private static void ApplyInteractiveState(
        TransitionSnapshot snapshot,
        bool interactable,
        bool blocksRaycasts)
    {
        if (snapshot == null || snapshot.CanvasGroup == null)
            return;

        snapshot.CanvasGroup.interactable = interactable;
        snapshot.CanvasGroup.blocksRaycasts = blocksRaycasts;
    }

    private static void RestoreSnapshot(TransitionSnapshot snapshot)
    {
        if (snapshot == null || snapshot.Rect == null || snapshot.CanvasGroup == null)
            return;

        snapshot.Rect.anchoredPosition = snapshot.AnchoredPosition;
        snapshot.Rect.localScale = snapshot.LocalScale;
        snapshot.CanvasGroup.alpha = snapshot.Alpha;
        snapshot.CanvasGroup.interactable = snapshot.Interactable;
        snapshot.CanvasGroup.blocksRaycasts = snapshot.BlocksRaycasts;
    }

    private static void RestoreSiblingOrder(TransitionSnapshot a, TransitionSnapshot b)
    {
        if (a?.Rect == null || b?.Rect == null || a.Rect.parent != b.Rect.parent)
            return;

        if (a.SiblingIndex <= b.SiblingIndex)
        {
            a.Rect.SetSiblingIndex(a.SiblingIndex);
            b.Rect.SetSiblingIndex(b.SiblingIndex);
            return;
        }

        b.Rect.SetSiblingIndex(b.SiblingIndex);
        a.Rect.SetSiblingIndex(a.SiblingIndex);
    }

    private static float ResolveTravelDistance(RectTransform outgoing, RectTransform incoming)
    {
        float outgoingWidth = outgoing.rect.width;
        float incomingWidth = incoming.rect.width;
        float parentWidth = 0f;
        if (outgoing.parent is RectTransform parentRect)
            parentWidth = parentRect.rect.width;
        float fallback = 600f;
        return Mathf.Max(outgoingWidth, incomingWidth, parentWidth, fallback);
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(1f - t);
        float inv = 1f - t;
        return 1f - (inv * inv * inv);
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
    }

    private static float EaseOutBack(float t, float overshoot)
    {
        t = Mathf.Clamp01(t);
        float c1 = overshoot;
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
