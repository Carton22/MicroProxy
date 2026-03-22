using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Uses pinch-and-twist to switch single selection between proxy labels inside the currently active proxy set.
/// Assign a root that contains the proxy set roots you want to navigate.
/// </summary>
public class ProxyTwistSingleSelect : MonoBehaviour
{
    [Tooltip("Root containing multiple proxy set roots. Twist selection works on whichever active proxy set under this root currently contains the EventSystem selection.")]
    [SerializeField] private Transform m_proxySetsRoot;
    [SerializeField] private PinchAndTwistEventSource m_twistEventSource;

    [Tooltip("Twist amount (0–1) per one label step. 0.1 = 10% twist moves to next/previous label.")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float twistPerStep = 0.1f;

    [Tooltip("If true, log twist start/step/end (for debugging).")]
    [SerializeField] private bool m_debugLog;

    private int m_startIndex;
    private bool m_inGesture;
    private Transform m_gestureScopeRoot;
    private readonly List<Transform> m_scopeItemsBuffer = new();

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
        if (ProxySetDrillDownController.ShouldReserveTwistForDrillDown(m_twistEventSource))
        {
            if (m_debugLog)
                Debug.Log("[ProxyTwistSingleSelect] Twist reserved for drill-down navigation.");
            return;
        }

        if (UINavigator.ShouldReserveTwistForAttributeFilter(m_twistEventSource))
        {
            if (m_debugLog)
                Debug.Log("[ProxyTwistSingleSelect] Twist reserved for AttributeUI filtering.");
            return;
        }

        if (!TryResolveSelectionScope(out m_gestureScopeRoot))
            return;

        int count = BuildSelectableItems(m_gestureScopeRoot, m_scopeItemsBuffer);
        if (count == 0)
            return;

        m_startIndex = FindSelectedItemIndex(m_scopeItemsBuffer);
        if (m_startIndex < 0)
            m_startIndex = 0;
        m_startIndex = Mathf.Clamp(m_startIndex, 0, count - 1);

        m_inGesture = true;
        if (m_debugLog)
            Debug.Log($"[ProxyTwistSingleSelect] Twist started, startIndex={m_startIndex}, scope={m_gestureScopeRoot.name}");
    }

    private void OnPinchAndTwist(float signedNormalized)
    {
        if (!m_inGesture || m_gestureScopeRoot == null) return;

        int count = BuildSelectableItems(m_gestureScopeRoot, m_scopeItemsBuffer);
        if (count == 0) return;

        int rightSteps = 0;
        int leftSteps = 0;
        if (signedNormalized > 0f)
            rightSteps = Mathf.Max(1, Mathf.FloorToInt(signedNormalized / twistPerStep));
        else if (signedNormalized < 0f)
            leftSteps = Mathf.Max(1, Mathf.FloorToInt(-signedNormalized / twistPerStep));

        int targetIndex = Mathf.Clamp(m_startIndex + rightSteps - leftSteps, 0, count - 1);
        SelectItemAtIndex(targetIndex);
        if (m_debugLog)
            Debug.Log($"[ProxyTwistSingleSelect] Twist={signedNormalized:F2} -> index {targetIndex}");
    }

    private void OnEndPinchAndTwist()
    {
        m_inGesture = false;
        m_gestureScopeRoot = null;
        m_scopeItemsBuffer.Clear();
        if (m_debugLog) Debug.Log("[ProxyTwistSingleSelect] Twist ended.");
    }

    private bool TryResolveSelectionScope(out Transform scopeRoot)
    {
        scopeRoot = null;
        if (m_proxySetsRoot == null)
            return false;

        scopeRoot = FindScopeRootUnderAssignedRoot();
        if (scopeRoot == null)
            return false;

        return true;
    }

    private Transform FindScopeRootUnderAssignedRoot()
    {
        if (m_proxySetsRoot == null)
            return null;

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null)
        {
            var grids = selected.GetComponentsInParent<GridLayoutGroup>(true);
            for (int i = 0; i < grids.Length; i++)
            {
                var grid = grids[i];
                if (grid == null)
                    continue;

                var candidate = grid.transform;
                if (candidate != m_proxySetsRoot && !candidate.IsChildOf(m_proxySetsRoot))
                    continue;

                if (BuildSelectableItems(candidate, m_scopeItemsBuffer) > 0)
                    return candidate;
            }
        }

        var availableGrids = m_proxySetsRoot.GetComponentsInChildren<GridLayoutGroup>(true);
        for (int i = 0; i < availableGrids.Length; i++)
        {
            var grid = availableGrids[i];
            if (grid == null || !grid.gameObject.activeInHierarchy)
                continue;

            if (BuildSelectableItems(grid.transform, m_scopeItemsBuffer) > 0)
                return grid.transform;
        }

        return null;
    }

    private int BuildSelectableItems(Transform scopeRoot, List<Transform> buffer)
    {
        buffer.Clear();
        if (scopeRoot == null)
            return 0;

        for (int i = 0; i < scopeRoot.childCount; i++)
        {
            var child = scopeRoot.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            if (FindSelectableIn(child) == null)
                continue;

            buffer.Add(child);
        }

        return buffer.Count;
    }

    private int FindSelectedItemIndex(List<Transform> items)
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == null)
            return -1;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null)
                continue;

            if (selected == item.gameObject || selected.transform.IsChildOf(item))
                return i;
        }

        return -1;
    }

    private void SelectItemAtIndex(int index)
    {
        if (index < 0 || index >= m_scopeItemsBuffer.Count)
            return;

        var itemRoot = m_scopeItemsBuffer[index];
        var target = FindSelectableIn(itemRoot);
        if (target == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(target.gameObject);
        target.Select();
    }

    private static Selectable FindSelectableIn(Transform root)
    {
        if (root == null)
            return null;

        var selectable = root.GetComponent<Selectable>();
        if (selectable != null && selectable.IsInteractable() && selectable.gameObject.activeInHierarchy)
            return selectable;

        selectable = root.GetComponentInChildren<Selectable>(false);
        if (selectable != null && selectable.IsInteractable() && selectable.gameObject.activeInHierarchy)
            return selectable;

        return null;
    }
}
