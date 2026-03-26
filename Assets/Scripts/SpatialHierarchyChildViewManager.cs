using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SpatialHierarchyChildViewManager : MonoBehaviour
{
    [SerializeField] private ProxyLabelManager m_labelManager;
    [SerializeField] private List<Transform> m_levelRoots = new();
    [SerializeField] private Transform m_parentLabelsRoot;
    [SerializeField] private Transform m_childLabelsRoot;
    [SerializeField] private bool m_selectFirstVisibleChild = true;
    [SerializeField] private bool m_debugLog;

    private readonly List<GameObject> m_rememberedSelections = new();
    private readonly List<int> m_rememberedVisibleIndices = new();

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

    public static bool TryHandleSwipeDown()
    {
        var managers = FindObjectsByType<SpatialHierarchyChildViewManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            var manager = managers[i];
            if (manager != null && manager.isActiveAndEnabled && manager.TryDrillDownToCurrentSelectionChildren())
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
        RebuildLegacyLevelsIfNeeded();
    }

    public bool TryHandleTapSelected()
    {
        if (EventSystem.current == null)
            return false;

        return TryHandleTappedObject(EventSystem.current.currentSelectedGameObject);
    }

    public bool TryDrillDownToCurrentSelectionChildren()
    {
        if (EventSystem.current == null)
            return false;

        return TryHandleTappedObject(EventSystem.current.currentSelectedGameObject);
    }

    public bool TryHandleTappedObject(GameObject tappedObject)
    {
        ResolveReferences();
        if (m_labelManager == null)
            return false;

        var levels = GetConfiguredLevels();
        if (levels.Count < 2)
            return false;

        int currentLevelIndex = GetCurrentLevelIndex(levels, tappedObject);
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Count - 1)
            return false;

        var currentRoot = levels[currentLevelIndex];
        var nextRoot = levels[currentLevelIndex + 1];
        if (currentRoot == null || nextRoot == null)
            return false;

        Transform selectedNode = tappedObject != null ? GetDirectChildUnder(tappedObject.transform, currentRoot) : null;
        if (selectedNode == null)
            return false;

        RememberSelectionAtLevel(levels, currentLevelIndex, selectedNode.gameObject);

        var markers = new List<int>();
        CollectMarkers(selectedNode, markers);
        PrepareLevelSwitch(levels, currentLevelIndex + 1);
        ShowLevelForMarkers(nextRoot, markers, $"Showing children for {selectedNode.name} at level {currentLevelIndex + 1}.");
        return true;
    }

    public bool TryRestoreParentView()
    {
        ResolveReferences();
        if (m_labelManager == null)
            return false;

        var levels = GetConfiguredLevels();
        if (levels.Count < 2)
            return false;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int currentLevelIndex = GetCurrentLevelIndex(levels, selected);
        if (currentLevelIndex < 0)
            currentLevelIndex = FindLevelIndexForRoot(levels, activeParent);

        if (currentLevelIndex <= 0)
            return false;

        var currentRoot = levels[currentLevelIndex];
        var parentRoot = levels[currentLevelIndex - 1];
        if (currentRoot == null || parentRoot == null)
            return false;

        Transform selectedNode = selected != null ? GetDirectChildUnder(selected.transform, currentRoot) : null;
        var selectedMarkers = new List<int>();
        CollectMarkers(selectedNode, selectedMarkers);

        PrepareLevelSwitch(levels, currentLevelIndex - 1);

        Canvas.ForceUpdateCanvases();

        var matchedParent = FindBestMatchingChild(parentRoot, selectedMarkers);
        if (matchedParent != null)
        {
            Select(matchedParent.gameObject);
        }
        else if (TryRestoreRememberedSelection(levels, currentLevelIndex - 1))
        {
        }
        else
        {
            var first = FindFirstSelectableUnder(parentRoot);
            if (first != null)
                Select(first);
        }

        RefreshScroller(parentRoot);
        Log($"Restored parent level {currentLevelIndex - 1}.");
        return true;
    }

    private void ShowLevelForMarkers(Transform targetRoot, List<int> markers, string logMessage)
    {
        if (targetRoot == null)
            return;

        m_labelManager.SetActiveLabelsParent(targetRoot);
        if (markers != null && markers.Count > 0)
            m_labelManager.SetVisibleLabelsForMarkerIndices(markers);
        else
            m_labelManager.ClearVisibleLabelsFilter();

        Canvas.ForceUpdateCanvases();

        if (m_selectFirstVisibleChild)
        {
            var first = FindFirstSelectableUnder(targetRoot);
            if (first != null)
                Select(first);
        }

        RefreshScroller(targetRoot);
        Log(logMessage);
    }

    private void PrepareLevelSwitch(List<Transform> levels, int targetLevelIndex)
    {
        if (levels == null || targetLevelIndex < 0 || targetLevelIndex >= levels.Count)
            return;

        m_labelManager.ClearVisibleLabelsFilter();
        RestoreAllLevels(levels);
        ShowOnlyLevel(levels, targetLevelIndex);
        m_labelManager.SetActiveLabelsParent(levels[targetLevelIndex]);
    }

    private void RememberSelectionAtLevel(List<Transform> levels, int levelIndex, GameObject selectedObject)
    {
        EnsureRememberedSelectionCapacity(levels.Count);
        m_rememberedSelections[levelIndex] = selectedObject;
        m_rememberedVisibleIndices[levelIndex] = GetVisibleChildIndexUnder(levels[levelIndex], selectedObject != null ? selectedObject.transform : null);
    }

    private void ResolveReferences()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        RebuildLegacyLevelsIfNeeded();
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

    private void RebuildLegacyLevelsIfNeeded()
    {
        if (m_levelRoots != null && m_levelRoots.Count > 0)
            return;

        if (m_levelRoots == null)
            m_levelRoots = new List<Transform>();

        m_levelRoots.Clear();
        if (m_parentLabelsRoot != null)
            m_levelRoots.Add(m_parentLabelsRoot);
        if (m_childLabelsRoot != null && m_childLabelsRoot != m_parentLabelsRoot)
            m_levelRoots.Add(m_childLabelsRoot);
    }

    private List<Transform> GetConfiguredLevels()
    {
        RebuildLegacyLevelsIfNeeded();
        return m_levelRoots;
    }

    private void EnsureRememberedSelectionCapacity(int count)
    {
        while (m_rememberedSelections.Count < count)
            m_rememberedSelections.Add(null);

        while (m_rememberedVisibleIndices.Count < count)
            m_rememberedVisibleIndices.Add(-1);
    }

    private static int FindLevelIndexForRoot(List<Transform> levels, Transform root)
    {
        if (root == null || levels == null)
            return -1;

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == root)
                return i;
        }

        return -1;
    }

    private static int GetCurrentLevelIndex(List<Transform> levels, GameObject selected)
    {
        if (levels == null || levels.Count == 0)
            return -1;

        if (selected != null)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level == null)
                    continue;

                if (selected == level.gameObject || selected.transform.IsChildOf(level))
                    return i;
            }
        }

        for (int i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            if (level != null && level.gameObject.activeInHierarchy)
                return i;
        }

        return -1;
    }

    private void RestoreAllLevels(List<Transform> levels)
    {
        if (levels == null)
            return;

        for (int i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            if (level == null)
                continue;

            level.gameObject.SetActive(true);
            m_labelManager.RestoreAuthoredLabelsParentState(level);
        }
    }

    private static void ShowOnlyLevel(List<Transform> levels, int targetLevelIndex)
    {
        if (levels == null)
            return;

        for (int i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            if (level == null)
                continue;

            level.gameObject.SetActive(i == targetLevelIndex);
        }
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

    private static Transform FindBestMatchingChild(Transform root, List<int> markers)
    {
        if (root == null || markers == null || markers.Count == 0)
            return null;

        Transform bestMatch = null;
        int bestScore = 0;

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            var binding = child.GetComponent<LabelMarkerBinding>();
            var indices = binding != null ? binding.MarkerIndices : null;
            if (indices == null || indices.Count == 0)
                continue;

            int score = 0;
            for (int j = 0; j < indices.Count; j++)
            {
                if (markers.Contains(indices[j]))
                    score++;
            }

            if (score <= 0 || score < bestScore)
                continue;

            bestScore = score;
            bestMatch = child;
        }

        return bestMatch;
    }

    private bool TryRestoreRememberedSelection(List<Transform> levels, int levelIndex)
    {
        if (levels == null || levelIndex < 0 || levelIndex >= levels.Count)
            return false;

        EnsureRememberedSelectionCapacity(levels.Count);

        var remembered = m_rememberedSelections[levelIndex];
        if (remembered != null && remembered.activeInHierarchy)
        {
            Select(remembered);
            return true;
        }

        int visibleIndex = m_rememberedVisibleIndices[levelIndex];
        if (visibleIndex < 0)
            return false;

        m_labelManager.SetSelectedLabelByIndex(visibleIndex);
        return true;
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
