using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects a double-tap pinch on the right hand (thumb + middle finger) and toggles
/// the active state of assigned GameObjects on each completed double tap.
/// </summary>
public class RightHandDoubleTapToggle : MonoBehaviour
{
    [Header("Right hand")]
    [Tooltip("Right hand OVRHand (from hand tracking rig). Used to read pinch strength.")]
    [SerializeField] private OVRHand m_rightHand;

    [Header("Pinch settings")]
    [Tooltip("Pinch strength threshold (0–1) to consider the thumb–middle-finger pinch as 'down'.")]
    [Range(0f, 1f)]
    [SerializeField] private float m_pinchStrengthThreshold = 0.7f;

    [Tooltip("Maximum time (seconds) allowed between two pinch taps to count as a double tap.")]
    [SerializeField] private float m_doubleTapMaxIntervalSeconds = 0.4f;

    [Header("Targets")]
    [Tooltip("GameObjects whose active state will be toggled on each double tap.")]
    [SerializeField] private List<GameObject> m_toggleTargets = new();

    [Tooltip("Optional. When set, the first double tap will also hide world markers (via WorldMarkerVisualHider).")]
    [SerializeField] private WorldMarkerVisualHider m_worldMarkerVisualHider;

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;

    private bool m_isPinching;
    private float m_lastTapTime = -1f;
    private bool m_markersHiddenOnce;

    private void Reset()
    {
        if (m_rightHand == null)
            m_rightHand = FindFirstObjectByType<OVRHand>();
    }

    private void Update()
    {
        if (m_rightHand == null)
            return;

        if (!m_rightHand.IsDataValid)
        {
            m_isPinching = false;
            return;
        }

        // Use middle finger pinch strength on the right hand.
        float pinch = m_rightHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        bool pinchDown = pinch >= m_pinchStrengthThreshold;

        if (pinchDown)
        {
            if (!m_isPinching)
            {
                // Rising edge: pinch just started
                m_isPinching = true;

                float now = Time.time;
                if (m_lastTapTime >= 0f && (now - m_lastTapTime) <= m_doubleTapMaxIntervalSeconds)
                {
                    // Second tap within allowed interval → double tap detected
                    m_lastTapTime = -1f;
                    ToggleTargets();
                }
                else
                {
                    // First tap: record time
                    m_lastTapTime = now;
                }
            }
        }
        else
        {
            // Pinch released
            m_isPinching = false;
        }
    }

    private void ToggleTargets()
    {
        for (int i = 0; i < m_toggleTargets.Count; i++)
        {
            var go = m_toggleTargets[i];
            if (go == null)
                continue;

            bool newState = !go.activeSelf;
            go.SetActive(newState);
        }

        if (m_worldMarkerVisualHider != null && !m_markersHiddenOnce)
        {
            m_worldMarkerVisualHider.SetHideWorldMarkers(true);
            m_markersHiddenOnce = true;
        }

        if (m_debugLog)
        {
            Debug.Log("[RightHandLongPinchToggle] Long pinch detected; toggled assigned GameObjects.");
        }
    }
}

