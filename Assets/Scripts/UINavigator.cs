using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UINavigator : MonoBehaviour
{
    [Header("Optional default selection")]
    [SerializeField] private Selectable defaultSelectable;
    [SerializeField] private GameObject selectionRoot;

    [Header("Label management")]
    [Tooltip("ProxyLabelManager used to determine the currently active labels parent for selection.")]
    [SerializeField] private ProxyLabelManager m_labelManager;

    void Start()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        // Ensure something is selected at start if you want keyboard/gamepad style focus
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            Select(defaultSelectable ? defaultSelectable.gameObject : FindFirstSelectable());
    }

    // Call these from your custom events or input
    public void MoveUp() => SendMove(MoveDirection.Up, Vector2.up);
    public void MoveDown() => SendMove(MoveDirection.Down, Vector2.down);

    public void MoveLeft()
    {
        if (TrySwitchToPreviousProxySet())
            return;
        SendMove(MoveDirection.Left, Vector2.left);
    }

    public void MoveRight()
    {
        if (TrySwitchToNextProxySet())
            return;
        SendMove(MoveDirection.Right, Vector2.right);
    }

    public void ClickSelected() => SendSubmit();

    // Optionally expose a vector based move if you prefer
    public void Move(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        dir.Normalize();
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            if (dir.x > 0) MoveRight();
            else MoveLeft();
        }
        else
        {
            var md = dir.y > 0 ? MoveDirection.Up : MoveDirection.Down;
            var moveVector = dir.y > 0 ? Vector2.up : Vector2.down;
            SendMove(md, moveVector);
        }
    }

    // ---------- Internals ----------

    void SendMove(MoveDirection md, Vector2 moveVector)
    {
        EnsureSelection();
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return;

        // Let uGUI handle navigation according to the Selectable's Navigation settings
        var axis = new AxisEventData(EventSystem.current)
        {
            moveDir = md,
            moveVector = moveVector
        };
        ExecuteEvents.Execute(selected, axis, ExecuteEvents.moveHandler);

        // If nothing changed, try a manual fallback using Selectable neighbors
        if (selected == EventSystem.current.currentSelectedGameObject)
            ManualNeighborFallback(md, selected);
    }

    void ManualNeighborFallback(MoveDirection md, GameObject fromGO)
    {
        var fromSel = fromGO.GetComponent<Selectable>();
        if (fromSel == null) return;

        Selectable target = null;
        var nav = fromSel.navigation;

        // Prefer explicit neighbors if set, else geometry based
        switch (md)
        {
            case MoveDirection.Up:
                target = nav.selectOnUp ? nav.selectOnUp : fromSel.FindSelectableOnUp();
                break;
            case MoveDirection.Down:
                target = nav.selectOnDown ? nav.selectOnDown : fromSel.FindSelectableOnDown();
                break;
            case MoveDirection.Left:
                target = nav.selectOnLeft ? nav.selectOnLeft : fromSel.FindSelectableOnLeft();
                break;
            case MoveDirection.Right:
                target = nav.selectOnRight ? nav.selectOnRight : fromSel.FindSelectableOnRight();
                break;
        }

        if (target && target.IsInteractable() && target.gameObject.activeInHierarchy)
            Select(target.gameObject);
    }

    void SendSubmit()
    {
        EnsureSelection();
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return;

        var data = new BaseEventData(EventSystem.current);

        // Works for Button, Toggle, etc.
        if(!ExecuteEvents.Execute(selected, data, ExecuteEvents.submitHandler))
        {
            // Some controls only react to click
            ExecuteEvents.Execute(selected, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
    }

    void EnsureSelection()
    {
        if (EventSystem.current == null) return;
        if (EventSystem.current.currentSelectedGameObject != null) return;

        var first = defaultSelectable ? defaultSelectable.gameObject : FindFirstSelectable();
        if (first != null) Select(first);
    }

    GameObject FindFirstSelectable()
    {
        Selectable any = null;

        // Prefer explicit selectionRoot if assigned,
        // otherwise fall back to the active labels parent from ProxyLabelManager.
        GameObject root = selectionRoot;
        if (root == null && m_labelManager != null)
        {
            var activeLabelsParent = m_labelManager.GetActiveLabelsParent();
            if (activeLabelsParent != null)
                root = activeLabelsParent.gameObject;
        }

        if (root != null)
        {
            any = root.GetComponentInChildren<Selectable>(false);
        }
        else
        {
            any = FindFirstObjectByType<Selectable>();
        }

        return any ? any.gameObject : null;
    }

    void Select(GameObject go)
    {
        if (go == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(go);
        var sel = go.GetComponent<Selectable>();
        if (sel) sel.Select();
    }

    // ---------- Proxy set switching (grid with any fixed column count) ----------

    /// <summary>
    /// Gets the current selection's column index (0 = leftmost) and the grid's column count.
    /// Returns true only when the selection is inside a GridLayoutGroup with Constraint = Fixed Column Count.
    /// </summary>
    bool TryGetSelectedColumnInfo(out int columnIndex, out int columnCount)
    {
        columnIndex = -1;
        columnCount = -1;

        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null) return false;

        Transform t = selected.transform;
        var grid = t.GetComponentInParent<GridLayoutGroup>();
        if (grid == null) return false;
        if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) return false;

        Transform content = grid.transform;
        if (!t.IsChildOf(content)) return false;

        int cellIndex = GetCellIndexUnder(t, content);
        if (cellIndex < 0) return false;

        columnCount = grid.constraintCount;
        if (columnCount <= 0) return false;

        columnIndex = cellIndex % columnCount;
        return true;
    }

    /// <summary>
    /// Index of the grid cell that contains 'child' (direct child of 'content' that is self or ancestor of child).
    /// </summary>
    static int GetCellIndexUnder(Transform child, Transform content)
    {
        if (child == null || content == null || !child.IsChildOf(content)) return -1;
        Transform walk = child;
        while (walk != null && walk.parent != content)
            walk = walk.parent;
        return walk != null ? walk.GetSiblingIndex() : -1;
    }

    /// <summary>
    /// If selection is on the leftmost column, switch to previous active label parent in ProxyLabelManager.
    /// Returns true if a switch occurred.
    /// </summary>
    bool TrySwitchToPreviousProxySet()
    {
        if (m_labelManager == null) return false;
        if (!IsSelectionInsideActiveManagedProxySet()) return false;
        if (!TryGetSelectedColumnInfo(out int col, out _) || col != 0) return false;

        if (!m_labelManager.TrySwitchToPreviousLabelsParent())
            return false;

        var newRoot = m_labelManager.GetActiveLabelsParent();
        var first = FindFirstSelectableIn(newRoot);
        if (first != null) Select(first);
        return true;
    }

    /// <summary>
    /// If selection is on the rightmost column, switch to next active label parent in ProxyLabelManager.
    /// Returns true if a switch occurred.
    /// </summary>
    bool TrySwitchToNextProxySet()
    {
        if (m_labelManager == null) return false;
        if (!IsSelectionInsideActiveManagedProxySet()) return false;
        if (!TryGetSelectedColumnInfo(out int col, out int columnCount) || col != columnCount - 1) return false;

        if (!m_labelManager.TrySwitchToNextLabelsParent())
            return false;

        var newRoot = m_labelManager.GetActiveLabelsParent();
        var first = FindFirstSelectableIn(newRoot);
        if (first != null) Select(first);
        return true;
    }

    GameObject FindFirstSelectableIn(Transform root)
    {
        if (root == null) return null;
        var sel = root.GetComponentInChildren<Selectable>(false);
        return sel != null ? sel.gameObject : null;
    }

    /// <summary>
    /// Returns true only when the current EventSystem selection is inside
    /// the active labels parent managed by ProxyLabelManager.
    /// </summary>
    bool IsSelectionInsideActiveManagedProxySet()
    {
        if (m_labelManager == null || EventSystem.current == null)
            return false;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        if (activeParent == null)
            return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        return selected == activeParent.gameObject || selected.transform.IsChildOf(activeParent);
    }
}
