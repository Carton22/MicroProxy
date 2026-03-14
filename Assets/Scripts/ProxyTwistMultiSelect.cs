using UnityEngine;

/// <summary>
/// Uses pinch-and-twist to extend proxy label selection: twist right adds the next label(s), twist left adds the previous.
/// Every 10% of twist (0.1 normalized) adds one more label to the range. Current focus is the anchor; twist extends the range from there.
/// Wire to PinchAndTwistEventSource (twist event source) in the inspector.
/// </summary>
public class ProxyTwistMultiSelect : MonoBehaviour
{
    [SerializeField] private ProxyCreator m_proxyCreator;
    [SerializeField] private PinchAndTwistEventSource m_twistEventSource;

    [Tooltip("Twist amount (0–1) per one label added. 0.1 = 10% adds one label.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float twistPerLabel = 0.1f;

    [Tooltip("If true, log when twist start/twist/end fire (for debugging).")]
    [SerializeField] private bool m_debugLog;

    private int m_anchorIndex;
    private bool m_inGesture;

    private void OnEnable()
    {
        if (m_twistEventSource == null)
            m_twistEventSource = GetComponent<PinchAndTwistEventSource>();
        if (m_twistEventSource == null)
            m_twistEventSource = FindFirstObjectByType<PinchAndTwistEventSource>();
        if (m_twistEventSource != null)
        {
            m_twistEventSource.OnStartPinchAndTwist.AddListener(OnStartPinchAndTwist);
            m_twistEventSource.OnPinchAndTwist.AddListener(OnPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.AddListener(OnEndPinchAndTwist);
            if (m_debugLog) Debug.Log("[ProxyTwistMultiSelect] Twist event source connected.");
        }
        else if (m_debugLog)
            Debug.LogWarning("[ProxyTwistMultiSelect] No PinchAndTwistEventSource found. Assign one in the inspector or add it to a hand in the scene.");
    }

    private void OnDisable()
    {
        if (m_twistEventSource != null)
        {
            m_twistEventSource.OnStartPinchAndTwist.RemoveListener(OnStartPinchAndTwist);
            m_twistEventSource.OnPinchAndTwist.RemoveListener(OnPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.RemoveListener(OnEndPinchAndTwist);
        }
        if (m_inGesture && m_proxyCreator != null)
            m_proxyCreator.ClearSelectionRangeOverride();
    }

    private void OnStartPinchAndTwist()
    {
        if (m_proxyCreator == null) return;
        m_anchorIndex = m_proxyCreator.GetSelectedLabelIndex();
        if (m_anchorIndex < 0)
            m_anchorIndex = 0;
        int count = m_proxyCreator.GetLabelCount();
        if (count == 0) return;
        m_anchorIndex = Mathf.Clamp(m_anchorIndex, 0, count - 1);
        m_inGesture = true;
        if (m_debugLog) Debug.Log($"[ProxyTwistMultiSelect] Twist started, anchor={m_anchorIndex}, labels={count}");
    }

    private void OnPinchAndTwist(float signedNormalized)
    {
        if (m_proxyCreator == null || !m_inGesture) return;

        int count = m_proxyCreator.GetLabelCount();
        if (count == 0) return;

        int rightSteps = 0;
        int leftSteps = 0;
        if (signedNormalized > 0f)
        {
            rightSteps = Mathf.Max(1, Mathf.FloorToInt(signedNormalized / twistPerLabel));
        }
        else if (signedNormalized < 0f)
        {
            leftSteps = Mathf.Max(1, Mathf.FloorToInt(-signedNormalized / twistPerLabel));
        }

        int minIndex = Mathf.Clamp(m_anchorIndex - leftSteps, 0, count - 1);
        int maxIndex = Mathf.Clamp(m_anchorIndex + rightSteps, 0, count - 1);
        m_proxyCreator.SetSelectionRange(minIndex, maxIndex);
        if (m_debugLog) Debug.Log($"[ProxyTwistMultiSelect] Twist={signedNormalized:F2} range=[{minIndex},{maxIndex}]");
    }

    private void OnEndPinchAndTwist()
    {
        if (m_debugLog) Debug.Log("[ProxyTwistMultiSelect] Twist ended.");
        m_inGesture = false;
    }
}
