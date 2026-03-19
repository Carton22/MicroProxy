using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attach this script to each clickable proxy "node" in the current level.
/// When the node is pressed, it:
/// - hides the current-level root (m_levelRootToHideOnDrillDown)
/// - disables all children under m_nextLevelRootToManage (optional but recommended)
/// - enables ONLY the assigned next-level proxy objects in m_childrenToEnable
/// </summary>
public class ProxySetDrillDownController : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    [Header("Level visibility")]
    [Tooltip("Root object for the current proxy set level. This GameObject will be set inactive when drilling down.")]
    [SerializeField] private GameObject m_levelRootToHideOnDrillDown;

    [Tooltip("Root object for the next-level proxy objects. This GameObject will be set active when drilling down.")]
    [SerializeField] private GameObject m_childrenRootToShow;

    [Header("Next level mapping")]
    [Tooltip("Next-level proxy GameObjects to enable after drilling down from this node.")]
    [SerializeField] private List<GameObject> m_childrenToEnable = new();

    [Header("Behavior")]
    [Tooltip("If true, selects the first Selectable under the enabled children.")]
    [SerializeField] private bool m_selectFirstOnShow = true;

    [Header("Debug")]
    [Tooltip("If true, logs when a drill-down switch happens.")]
    [SerializeField] private bool m_debugLog;

    public void OnPointerClick(PointerEventData eventData) => HandlePress();

    public void OnSubmit(BaseEventData eventData) => HandlePress();

    private void HandlePress()
    {
        if (m_childrenToEnable == null || m_childrenToEnable.Count == 0)
            return;

        if (m_debugLog)
            Debug.Log($"[ProxySetDrillDownController] DrillDown from {gameObject.name}. children={m_childrenToEnable.Count}");

        if (m_levelRootToHideOnDrillDown != null)
            m_levelRootToHideOnDrillDown.SetActive(false);

        if (m_childrenRootToShow != null)
            m_childrenRootToShow.SetActive(true);

        // Inside the children root:
        // - disable all direct child containers
        // - enable only the containers that contain our selected targets
        if (m_childrenRootToShow != null)
        {
            var rootTf = m_childrenRootToShow.transform;

            for (int i = 0; i < rootTf.childCount; i++)
            {
                var childTf = rootTf.GetChild(i);
                if (childTf != null)
                    childTf.gameObject.SetActive(false);
            }

            for (int i = 0; i < m_childrenToEnable.Count; i++)
            {
                var enabled = m_childrenToEnable[i];
                if (enabled == null)
                    continue;

                enabled.SetActive(true);

                // Enable the direct child container under m_childrenRootToShow.
                Transform cur = enabled.transform;
                while (cur != null && cur.parent != rootTf)
                    cur = cur.parent;

                if (cur != null && cur.parent == rootTf)
                    cur.gameObject.SetActive(true);
            }
        }
        else
        {
            // Fallback: just enable the objects we were given (may require their parents to already be active).
            for (int i = 0; i < m_childrenToEnable.Count; i++)
            {
                var go = m_childrenToEnable[i];
                if (go != null)
                    go.SetActive(true);
            }
        }

        // Enable only the children assigned to this node (selection focus).
        GameObject firstEnabledSelectableRoot = null;
        for (int i = 0; i < m_childrenToEnable.Count; i++)
        {
            var go = m_childrenToEnable[i];
            if (go == null)
                continue;

            go.SetActive(true);
            if (firstEnabledSelectableRoot == null)
                firstEnabledSelectableRoot = go;
        }

        if (m_selectFirstOnShow)
            SelectFirstSelectableUnder(firstEnabledSelectableRoot);
    }

    private static void SelectFirstSelectableUnder(GameObject root)
    {
        if (root == null || EventSystem.current == null)
            return;

        var selectable = root.GetComponentInChildren<Selectable>(false);
        if (selectable == null)
            return;

        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        selectable.Select();
    }
}

