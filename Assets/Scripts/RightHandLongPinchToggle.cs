using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detects a long pinch on the right hand (thumb + middle finger) and toggles
/// the active state of assigned GameObjects on each completed long pinch.
/// </summary>
public class RightHandLongPinchToggle : MonoBehaviour
{
    [Header("Right hand")]
    [Tooltip("Right hand OVRHand (from hand tracking rig). Used to read pinch strength.")]
    [SerializeField] private OVRHand m_rightHand;

    [Header("Pinch settings")]
    [Tooltip("Pinch strength threshold (0–1) to consider the thumb–middle-finger pinch as 'down'.")]
    [Range(0f, 1f)]
    [SerializeField] private float m_pinchStrengthThreshold = 0.7f;

    [Tooltip("How long (seconds) the pinch must be held continuously to count as a long pinch.")]
    [SerializeField] private float m_requiredHoldSeconds = 0.7f;

    [Header("Targets")]
    [Tooltip("GameObjects whose active state will be toggled on each long pinch.")]
    [SerializeField] private List<GameObject> m_toggleTargets = new();

    [Header("Debug")]
    [SerializeField] private bool m_debugLog;

    private bool m_isPinching;
    private float m_pinchStartTime;

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
                m_isPinching = true;
                m_pinchStartTime = Time.time;
            }
            else
            {
                float heldFor = Time.time - m_pinchStartTime;
                if (heldFor >= m_requiredHoldSeconds)
                {
                    m_isPinching = false; // prevent multiple toggles in one hold
                    ToggleTargets();
                }
            }
        }
        else
        {
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

        if (m_debugLog)
        {
            Debug.Log("[RightHandLongPinchToggle] Long pinch detected; toggled assigned GameObjects.");
        }
    }
}

