using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attach to a UI proxy Button (or any GameObject with Selectable) to toggle/enable/disable other GameObjects
/// when the proxy button is pressed.
///
/// Uses both pointer-click and submit events (same pattern as ProxyLabelStatus).
/// </summary>
public class ToggleTargetsOnButtonPress : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [Tooltip("Targets whose active state will be toggled/set when this button is pressed.")]
    [SerializeField] private List<GameObject> m_targets = new();

    [Tooltip("If true, each target's activeSelf is toggled. If false, targets are set to m_setActiveValue.")]
    [SerializeField] private bool m_toggle = true;

    [Tooltip("Used when m_toggle is false. If true, targets will be activated; if false, targets will be deactivated.")]
    [SerializeField] private bool m_setActiveValue = true;

    [Header("Debug")]
    [Tooltip("If true, logs when the press is handled.")]
    [SerializeField] private bool m_debugLog;

    [Tooltip("Optional shared logger used to log the press. If not assigned, falls back to Debug.Log.")]
    [SerializeField] private SharedLogger m_logger;

    public void OnPointerClick(PointerEventData eventData) => HandlePress();

    public void OnSubmit(BaseEventData eventData) => HandlePress();

    private void HandlePress()
    {
        if (m_targets == null || m_targets.Count == 0)
            return;

        if (m_debugLog)
        {
            string msg = $"[ToggleTargetsOnButtonPress] Pressed. Targets={m_targets.Count}, toggle={m_toggle}";
            if (m_logger != null)
                m_logger.Log(msg);
            else
                Debug.Log(msg);
        }

        for (int i = 0; i < m_targets.Count; i++)
        {
            var go = m_targets[i];
            if (go == null)
                continue;

            if (m_toggle)
                go.SetActive(!go.activeSelf);
            else
                go.SetActive(m_setActiveValue);
        }
    }
}

