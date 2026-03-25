using UnityEngine;

/// <summary>
/// Detects a right-hand double-tap pinch and groups the active proxy labels by the Owner attribute.
/// Uses the same middle-finger pinch timing model as the other right-hand double-tap utilities.
/// </summary>
public class RightHandDoubleTapGroupByOwner : MonoBehaviour
{
    [Header("Right hand")]
    [SerializeField] private OVRHand m_rightHand;

    [Header("Grouping target")]
    [SerializeField] private UINavigator m_uiNavigator;
    [SerializeField] private string m_attributeKey = "owner";

    [Header("Pinch settings")]
    [Range(0f, 1f)]
    [SerializeField] private float m_pinchStrengthThreshold = 0.7f;
    [SerializeField] private float m_doubleTapMaxIntervalSeconds = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;

    private bool m_isPinching;
    private float m_lastTapTime = -1f;

    private void Reset()
    {
        if (m_uiNavigator == null)
            m_uiNavigator = GetComponent<UINavigator>();
        if (m_rightHand == null)
            m_rightHand = FindFirstObjectByType<OVRHand>();
    }

    private void Update()
    {
        if (m_rightHand == null || m_uiNavigator == null)
            return;

        if (!m_rightHand.IsDataValid)
        {
            m_isPinching = false;
            return;
        }

        float pinch = m_rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        bool pinchDown = pinch >= m_pinchStrengthThreshold;

        if (pinchDown)
        {
            if (!m_isPinching)
            {
                m_isPinching = true;

                float now = Time.time;
                if (m_lastTapTime >= 0f && (now - m_lastTapTime) <= m_doubleTapMaxIntervalSeconds)
                {
                    m_lastTapTime = -1f;
                    bool grouped = m_uiNavigator.GroupActiveProxyLabelsByAttributeKey(m_attributeKey);
                    if (m_debugLog)
                        Debug.Log($"[RightHandDoubleTapGroupByOwner] doubleTap grouped={grouped} attributeKey={m_attributeKey}");
                }
                else
                {
                    m_lastTapTime = now;
                }
            }
        }
        else
        {
            m_isPinching = false;
        }
    }
}
