using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SpatialHierarchyChildViewManager : MonoBehaviour
{
    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private Transform m_parentLabelsRoot;
    [SerializeField] private Transform m_childLabelsRoot;
    [SerializeField] private bool m_selectFirstVisibleChild = true;
    [SerializeField] private bool m_debugLog;

    private GameObject m_rememberedParentSelection;
    private int m_rememberedParentVisibleIndex = -1;

    public static bool TryHandleTapOnCurrentSelection()
    {
        var managers = FindObjectsByType<SpatialHierarchyChildViewManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.isActiveAndEnabled && manager.TryHandleTapSelected())
                return true;
        }

        return false;
    }

    public static bool TryHandleSwipeUp()
    {
        var managers = FindObjectsByType<SpatialHierarchyChildViewManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.isActiveAndEnabled && manager.TryRestoreParentView())
                return true;
        }

        return false;
    }

    public static bool TryHandlePointerTap(GameObject tappedObject)
    {
        var managers = FindObjectsByType<SpatialHierarchyChildViewManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.isActiveAndEnabled && manager.TryHandleTappedObject(tappedObject))
                return true;
        }

        return false;
    }

    private void Reset()
    {
        m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    public bool TryHandleTapSelected()
    {
        if (EventSystem.current == null)
            return false;

        return TryHandleTappedObject(EventSystem.current.currentSelectedGameObject);
    }

    public bool TryHandleTappedObject(GameObject tappedObject)
    {
        ResolveReferences();
        if (m_labelManager == null || m_childLabelsRoot == null)
            return false;

        if (tappedObject == null)
        {
            ShowAllChildren();
            return true;
        }

        var selectedParentRoot = GetDirectChildUnder(tappedObject.transform, m_parentLabelsRoot);
        if (selectedParentRoot == null)
            return false;

        RememberParentSelection(selectedParentRoot.gameObject);

        var markers = new List<int>();
        CollectMarkers(selectedParentRoot, markers);
        if (markers.Count == 0)
        {
            ShowAllChildren();
            return true;
        }

        ShowChildrenForMarkers(markers);
        return true;
    }

    public bool TryRestoreParentView()
    {
        ResolveReferences();
        if (m_labelManager == null || m_parentLabelsRoot == null || m_childLabelsRoot == null)
            return false;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool inChildView = activeParent == m_childLabelsRoot ||
            (selected != null && (selected == m_childLabelsRoot.gameObject || selected.transform.IsChildOf(m_childLabelsRoot)));

        if (!inChildView)
            return false;

        m_labelManager.ClearVisibleLabelsFilter();
        m_labelManager.SetActiveLabelsParent(m_parentLabelsRoot);

        Canvas.ForceUpdateCanvases();

        if (m_rememberedParentSelection != null && m_rememberedParentSelection.activeInHierarchy)
        {
            Select(m_rememberedParentSelection);
        }
        else if (m_rememberedParentVisibleIndex >= 0)
        {
            m_labelManager.SetSelectedLabelByIndex(m_rememberedParentVisibleIndex);
        }
        else
        {
            var first = FindFirstSelectableUnder(m_parentLabelsRoot);
            if (first != null)
                Select(first);
        }

        RefreshScroller(m_parentLabelsRoot);
        Log("Restored parent view.");
        return true;
    }

    private void ShowAllChildren()
    {
        m_labelManager.ClearVisibleLabelsFilter();
        m_labelManager.SetActiveLabelsParent(m_childLabelsRoot);
        Canvas.ForceUpdateCanvases();

        if (m_selectFirstVisibleChild)
        {
            var first = FindFirstSelectableUnder(m_childLabelsRoot);
            if (first != null)
                Select(first);
        }

        RefreshScroller(m_childLabelsRoot);
        Log("Showing all child labels.");
    }

    private void ShowChildrenForMarkers(List<int> markers)
    {
        m_labelManager.SetActiveLabelsParent(m_childLabelsRoot);
        m_labelManager.SetVisibleLabelsForMarkerIndices(markers);
        Canvas.ForceUpdateCanvases();

        if (m_selectFirstVisibleChild)
        {
            var first = FindFirstSelectableUnder(m_childLabelsRoot);
            if (first != null)
                Select(first);
        }

        RefreshScroller(m_childLabelsRoot);
        Log($"Showing child labels for {markers.Count} marker(s).");
    }

    private void RememberParentSelection(GameObject selectedParent)
    {
        m_rememberedParentSelection = selectedParent;
        m_rememberedParentVisibleIndex = GetVisibleChildIndexUnder(m_parentLabelsRoot, selectedParent != null ? selectedParent.transform : null);
    }

    private void ResolveReferences()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
    }

    private static Transform GetDirectChildUnder(Transform candidate, Transform parent)
    {
        if (candidate == null || parent == null || (candidate != parent && !candidate.IsChildOf(parent)))
            return null;

        var walk = candidate;
        while (walk != null && walk.parent != parent)
            walk = walk.parent;

        return walk;
    }

    private static void CollectMarkers(Transform root, List<int> destination)
    {
        destination.Clear();
        if (root == null)
            return;

        var seen = new HashSet<int>();
        var bindings = root.GetComponentsInChildren<LabelMarkerBinding>(true);
        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            var indices = binding != null ? binding.MarkerIndices : null;
            if (indices == null)
                continue;

            for (int j = 0; j < indices.Count; j++)
            {
                int marker = indices[j];
                if (marker < 0 || seen.Contains(marker))
                    continue;

                seen.Add(marker);
                destination.Add(marker);
            }
        }
    }

    private static int GetVisibleChildIndexUnder(Transform root, Transform selected)
    {
        if (root == null || selected == null)
            return -1;

        int visibleIndex = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            if (selected == child || selected.IsChildOf(child))
                return visibleIndex;

            visibleIndex++;
        }

        return -1;
    }

    private static GameObject FindFirstSelectableUnder(Transform root)
    {
        if (root == null)
            return null;

        var selectables = root.GetComponentsInChildren<Selectable>(false);
        for (int i = 0; i < selectables.Length; i++)
        {
            var selectable = selectables[i];
            if (selectable != null && selectable.IsInteractable() && selectable.gameObject.activeInHierarchy)
                return selectable.gameObject;
        }

        return null;
    }

    private static void RefreshScroller(Transform root)
    {
        if (root == null)
            return;

        var scroller = root.GetComponent<ProxyLabelHorizonScroller>();
        if (scroller == null)
            scroller = root.GetComponentInParent<ProxyLabelHorizonScroller>(true);
        if (scroller == null)
            scroller = root.GetComponentInChildren<ProxyLabelHorizonScroller>(true);

        if (scroller != null)
            scroller.ForceRefreshNow();
    }

    private static void Select(GameObject go)
    {
        if (go == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(go);
        var selectable = go.GetComponent<Selectable>();
        if (selectable != null)
            selectable.Select();
    }

    private void Log(string message)
    {
        if (m_debugLog)
            Debug.Log($"[SpatialHierarchyChildViewManager] {message}");
    }
}
