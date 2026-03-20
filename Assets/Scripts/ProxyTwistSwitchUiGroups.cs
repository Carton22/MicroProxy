using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Uses left-hand pinch-and-twist to switch between a fixed list of UI groups
/// (for example: ProxyUI, VerticalGroup, HorizontalGroup).
/// Twist right -> next group, twist left -> previous group.
/// After switching, selects the first Selectable under the active group.
/// </summary>
public class ProxyTwistSwitchUiGroups : MonoBehaviour
{
    [Header("UI groups (order matters)")]
    [Tooltip("List of root transforms for UI groups (e.g. ProxyUI, VerticalGroup, HorizontalGroup).")]
    [SerializeField] private List<Transform> m_groups = new();

    [Header("Gesture source (left hand)")]
    [SerializeField] private PinchAndTwistEventSource m_twistEventSource;

    [Header("Twist settings")]
    [Tooltip("Twist amount (0–1) per one group step. 0.2 = 20% twist switches one group.")]
    [Range(0.05f, 0.75f)]
    [SerializeField] private float m_twistPerGroup = 0.2f;

    [Tooltip("If true, log twist start/step/end (for debugging).")]
    [SerializeField] private bool m_debugLog;

    private int m_startGroupIndex;
    private int m_lastStepOffset;
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
        else if (m_debugLog)
        {
            Debug.LogWarning("[ProxyTwistSwitchUiGroups] No PinchAndTwistEventSource found.");
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
        if (ProxySetDrillDownController.ShouldReserveTwistForDrillDown())
        {
            if (m_debugLog)
                Debug.Log("[ProxyTwistSwitchUiGroups] Twist reserved for drill-down navigation.");
            return;
        }

        int count = m_groups.Count;
        if (count == 0)
            return;

        // Only allow twisting if the currently active proxy set is one of our configured groups.
        // (If the selection isn't under any of our roots, and none of our roots is active, ignore the gesture.)
        m_startGroupIndex = GetCurrentGroupIndex();
        if (m_startGroupIndex < 0)
            m_startGroupIndex = GetFirstActiveGroupIndex();
        if (m_startGroupIndex < 0)
            return;

        m_startGroupIndex = Mathf.Clamp(m_startGroupIndex, 0, count - 1);
        m_lastStepOffset = 0;
        m_inGesture = true;

        if (m_debugLog)
            Debug.Log($"[ProxyTwistSwitchUiGroups] Twist started, startGroupIndex={m_startGroupIndex}");
    }

    private void OnPinchAndTwist(float signedNormalized)
    {
        if (!m_inGesture)
            return;

        int count = m_groups.Count;
        if (count == 0)
            return;

        int stepOffset = ComputeStepOffset(signedNormalized);
        if (stepOffset == m_lastStepOffset)
            return;

        m_lastStepOffset = stepOffset;

        int targetIndex = Mathf.Clamp(m_startGroupIndex + stepOffset, 0, count - 1);
        ActivateGroup(targetIndex);

        if (m_debugLog)
            Debug.Log($"[ProxyTwistSwitchUiGroups] Twist={signedNormalized:F2} -> group {targetIndex}");
    }

    private void OnEndPinchAndTwist()
    {
        m_inGesture = false;
        if (m_debugLog)
            Debug.Log("[ProxyTwistSwitchUiGroups] Twist ended.");
    }

    private int ComputeStepOffset(float signedNormalized)
    {
        if (Mathf.Abs(signedNormalized) < 0.0001f)
            return 0;

        int steps = Mathf.FloorToInt(Mathf.Abs(signedNormalized) / Mathf.Max(0.0001f, m_twistPerGroup));
        if (steps <= 0)
            return 0;

        return signedNormalized > 0f ? steps : -steps;
    }

    private int GetFirstActiveGroupIndex()
    {
        for (int i = 0; i < m_groups.Count; i++)
        {
            var g = m_groups[i];
            if (g != null && g.gameObject.activeInHierarchy)
                return i;
        }
        return -1;
    }

    private int GetCurrentGroupIndex()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return -1;

        var selT = selected.transform;
        for (int i = 0; i < m_groups.Count; i++)
        {
            var g = m_groups[i];
            if (g != null && selT.IsChildOf(g))
                return i;
        }
        return -1;
    }

    private void ActivateGroup(int targetIndex)
    {
        targetIndex = Mathf.Clamp(targetIndex, 0, m_groups.Count - 1);

        for (int i = 0; i < m_groups.Count; i++)
        {
            var g = m_groups[i];
            if (g != null)
                g.gameObject.SetActive(i == targetIndex);
        }

        var targetRoot = m_groups[targetIndex];
        var first = FindFirstSelectableIn(targetRoot);
        if (first != null)
            Select(first);
    }

    private static GameObject FindFirstSelectableIn(Transform root)
    {
        if (root == null)
            return null;
        var sel = root.GetComponentInChildren<Selectable>(false);
        return sel != null ? sel.gameObject : null;
    }

    private static void Select(GameObject go)
    {
        if (go == null || EventSystem.current == null)
            return;
        EventSystem.current.SetSelectedGameObject(go);
        var sel = go.GetComponent<Selectable>();
        if (sel != null)
            sel.Select();
    }
}
