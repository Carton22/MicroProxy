using UnityEngine;

/// <summary>
/// Detects a right-hand double-tap pinch and groups the active proxy labels by the specified attribute.
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
    [Tooltip("Optional shared logger used to log the double-tap grouping result.")]
    [SerializeField] private SharedLogger m_logger;

    private bool m_isPinching;
    private float m_lastTapTime = -1f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        if (m_uiNavigator == null)
            m_uiNavigator = GetComponent<UINavigator>();
        if (m_rightHand == null)
            m_rightHand = FindFirstObjectByType<OVRHand>();
    }

    private void Update()
    {
        ResolveReferences();

        if (m_rightHand == null)
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
                    bool grouped = TryGroupActiveProxyLabels();
                    if (m_debugLog)
                    {
                        string msg = $"[RightHandDoubleTapGroupByOwner] doubleTap grouped={grouped} attributeKey={m_attributeKey}";
                        if (m_logger != null)
                            m_logger.Log(msg);
                        else
                            Debug.Log(msg);
                    }
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

    private void ResolveReferences()
    {
        if (m_rightHand == null)
            m_rightHand = GetComponent<OVRHand>();
        if (m_rightHand == null)
            m_rightHand = FindFirstObjectByType<OVRHand>();

        if (m_uiNavigator == null)
            m_uiNavigator = GetComponent<UINavigator>();
    }

    private bool TryGroupActiveProxyLabels()
    {
        if (TryGroupWithNavigator(m_uiNavigator))
            return true;

        var navigators = FindObjectsByType<UINavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < navigators.Length; i++)
        {
            var navigator = navigators[i];
            if (navigator == null || navigator == m_uiNavigator)
                continue;

            if (!navigator.isActiveAndEnabled)
                continue;

            if (TryGroupWithNavigator(navigator))
            {
                m_uiNavigator = navigator;
                return true;
            }
        }

        return false;
    }

    private bool TryGroupWithNavigator(UINavigator navigator)
    {
        if (navigator == null)
            return false;

        return navigator.GroupActiveProxyLabelsByAttributeKey(m_attributeKey);
    }
}
