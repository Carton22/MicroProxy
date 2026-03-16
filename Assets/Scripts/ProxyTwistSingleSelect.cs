using UnityEngine;

/// <summary>
/// Uses pinch-and-twist to switch single selection between proxy labels: twist right = next label, twist left = previous.
/// Wire to PinchAndTwistEventSource (e.g. on the hand used for twist). Can be used instead of or alongside swipe/microgesture navigation.
/// </summary>
public class ProxyTwistSingleSelect : MonoBehaviour
{
    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private PinchAndTwistEventSource m_twistEventSource;

    [Tooltip("Twist amount (0–1) per one label step. 0.1 = 10% twist moves to next/previous label.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float twistPerStep = 0.1f;

    [Tooltip("When starting a twist, clear any multi-selection so only one label is selected.")]
    [SerializeField] private bool m_clearMultiSelectOnStart = true;

    [Tooltip("If true, log twist start/step/end (for debugging).")]
    [SerializeField] private bool m_debugLog;

    private int m_startIndex;
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
        }
    }

    private void OnDisable()
    {
        if (m_twistEventSource != null)
        {
            m_twistEventSource.OnStartPinchAndTwist.RemoveListener(OnStartPinchAndTwist);
            m_twistEventSource.OnPinchAndTwist.RemoveListener(OnPinchAndTwist);
            m_twistEventSource.OnEndPinchAndTwist.RemoveListener(OnEndPinchAndTwist);
        }
    }

    private void OnStartPinchAndTwist()
    {
        if (m_labelManager == null) return;
        int count = m_labelManager.GetLabelCount();
        if (count == 0) return;
        m_startIndex = m_labelManager.GetSelectedLabelIndex();
        if (m_startIndex < 0)
            m_startIndex = 0;
        m_startIndex = Mathf.Clamp(m_startIndex, 0, count - 1);
        if (m_clearMultiSelectOnStart)
            m_labelManager.ClearSelectionRangeOverride();
        m_inGesture = true;
        if (m_debugLog) Debug.Log($"[ProxyTwistSingleSelect] Twist started, startIndex={m_startIndex}");
    }

    private void OnPinchAndTwist(float signedNormalized)
    {
        if (m_labelManager == null || !m_inGesture) return;
        int count = m_labelManager.GetLabelCount();
        if (count == 0) return;

        int rightSteps = 0;
        int leftSteps = 0;
        if (signedNormalized > 0f)
            rightSteps = Mathf.Max(1, Mathf.FloorToInt(signedNormalized / twistPerStep));
        else if (signedNormalized < 0f)
            leftSteps = Mathf.Max(1, Mathf.FloorToInt(-signedNormalized / twistPerStep));

        int targetIndex = Mathf.Clamp(m_startIndex + rightSteps - leftSteps, 0, count - 1);
        m_labelManager.SetSelectedLabelByIndex(targetIndex);
        if (m_debugLog) Debug.Log($"[ProxyTwistSingleSelect] Twist={signedNormalized:F2} -> index {targetIndex}");
    }

    private void OnEndPinchAndTwist()
    {
        m_inGesture = false;
        if (m_debugLog) Debug.Log("[ProxyTwistSingleSelect] Twist ended.");
    }
}
