using System.Collections.Generic;
using TMPro;
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

    [Header("ScreenUI ↔ AttributeUI")]
    [Tooltip("Left column labels root (e.g. ProxyUI grid under ScreenUI). When focus is here and the user moves right, AttributeUI is shown.")]
    [SerializeField] private Transform m_leftColumnLabelsParent;

    [Tooltip("Right column root to enable (e.g. AttributeUI).")]
    [SerializeField] private GameObject m_attributeUiRoot;

    [Tooltip("If set, ProxyLabelManager.SetActiveLabelsParent is called after AttributeUI is shown (must be an entry in the manager's label parents list).")]
    [SerializeField] private Transform m_attributeLabelsParentForManager;

    [SerializeField] private bool m_selectFirstSelectableInAttributeUi = true;

    [Tooltip("When true, AttributeUI is turned off when the scene loads (play mode), even if left active in the editor.")]
    [SerializeField] private bool m_attributeUiInactiveByDefault = true;

    [Header("Attribute twist filter")]
    [Tooltip("Right-hand twist source used to cycle attribute values while a top-level attribute button is selected. Defaults to a PinchAndTwistEventSource on this GameObject.")]
    [SerializeField] private PinchAndTwistEventSource m_attributeTwistEventSource;

    [Tooltip("Optional root that contains the top-level attribute buttons (for example the AttributeName group). Auto-resolved under AttributeUI when left empty.")]
    [SerializeField] private Transform m_attributeNamesParent;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float m_attributeTwistPerStep = 0.12f;

    [SerializeField] private string m_attributeButtonValueSeparator = ": ";

    private readonly Dictionary<Transform, string> m_cachedAttributeBaseLabels = new();
    private readonly List<Transform> m_attributeOptionRootsBuffer = new();
    private Transform m_activeAttributeFilterButtonRoot;
    private int m_activeAttributeFilterOptionIndex = -1;
    private bool m_inAttributeTwistGesture;
    private Transform m_attributeGestureButtonRoot;
    private Transform m_attributeGestureOptionsRoot;
    private int m_attributeGestureStartOptionIndex = -1;
    private int m_attributeGestureLastAppliedOptionIndex = int.MinValue;

    void Awake()
    {
        ResolveAttributeTwistEventSource();
        if (m_attributeUiInactiveByDefault && m_attributeUiRoot != null)
            m_attributeUiRoot.SetActive(false);
    }

    void OnEnable()
    {
        ResolveAttributeTwistEventSource();
        SubscribeToAttributeTwistEvents();
    }

    void OnDisable()
    {
        UnsubscribeFromAttributeTwistEvents();
        m_inAttributeTwistGesture = false;
    }

    void Start()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        // Ensure something is selected at start if you want keyboard/gamepad style focus
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            Select(defaultSelectable ? defaultSelectable.gameObject : FindFirstSelectable());
    }

    public static bool ShouldReserveTwistForAttributeFilter(PinchAndTwistEventSource twistEventSource = null)
    {
        var navigators = FindObjectsByType<UINavigator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < navigators.Length; i++)
        {
            var navigator = navigators[i];
            if (navigator == null || !navigator.isActiveAndEnabled)
                continue;

            if (navigator.ShouldHandleAttributeTwistForCurrentSelection(twistEventSource))
                return true;
        }

        return false;
    }

    private void ResolveAttributeTwistEventSource()
    {
        if (m_attributeTwistEventSource == null)
            m_attributeTwistEventSource = GetComponent<PinchAndTwistEventSource>();
    }

    private void SubscribeToAttributeTwistEvents()
    {
        if (m_attributeTwistEventSource == null)
            return;

        m_attributeTwistEventSource.OnStartPinchAndTwist.RemoveListener(OnAttributeTwistStart);
        m_attributeTwistEventSource.OnPinchAndTwist.RemoveListener(OnAttributeTwistProgress);
        m_attributeTwistEventSource.OnEndPinchAndTwist.RemoveListener(OnAttributeTwistEnd);

        m_attributeTwistEventSource.OnStartPinchAndTwist.AddListener(OnAttributeTwistStart);
        m_attributeTwistEventSource.OnPinchAndTwist.AddListener(OnAttributeTwistProgress);
        m_attributeTwistEventSource.OnEndPinchAndTwist.AddListener(OnAttributeTwistEnd);
    }

    private void UnsubscribeFromAttributeTwistEvents()
    {
        if (m_attributeTwistEventSource == null)
            return;

        m_attributeTwistEventSource.OnStartPinchAndTwist.RemoveListener(OnAttributeTwistStart);
        m_attributeTwistEventSource.OnPinchAndTwist.RemoveListener(OnAttributeTwistProgress);
        m_attributeTwistEventSource.OnEndPinchAndTwist.RemoveListener(OnAttributeTwistEnd);
    }

    private void OnAttributeTwistStart()
    {
        if (!TryResolveAttributeTwistContext(out var attributeButtonRoot, out var optionsRoot, out _, out _))
            return;

        m_inAttributeTwistGesture = true;
        m_attributeGestureButtonRoot = attributeButtonRoot;
        m_attributeGestureOptionsRoot = optionsRoot;
        m_attributeGestureStartOptionIndex = attributeButtonRoot == m_activeAttributeFilterButtonRoot
            ? m_activeAttributeFilterOptionIndex
            : -1;
        m_attributeGestureLastAppliedOptionIndex = m_attributeGestureStartOptionIndex;
    }

    private void OnAttributeTwistProgress(float signedNormalized)
    {
        if (!m_inAttributeTwistGesture || m_attributeGestureButtonRoot == null || m_attributeGestureOptionsRoot == null)
            return;

        int optionCount = BuildAttributeOptionRoots(m_attributeGestureOptionsRoot, m_attributeOptionRootsBuffer);
        if (optionCount <= 0)
            return;

        int targetIndex = Mathf.Clamp(
            m_attributeGestureStartOptionIndex + ComputeTwistStepOffset(signedNormalized),
            -1,
            optionCount - 1);

        if (targetIndex == m_attributeGestureLastAppliedOptionIndex)
            return;

        m_attributeGestureLastAppliedOptionIndex = targetIndex;
        ApplyAttributeFilterSelection(m_attributeGestureButtonRoot, targetIndex);
    }

    private void OnAttributeTwistEnd()
    {
        m_inAttributeTwistGesture = false;
        m_attributeGestureButtonRoot = null;
        m_attributeGestureOptionsRoot = null;
        m_attributeGestureStartOptionIndex = -1;
        m_attributeGestureLastAppliedOptionIndex = int.MinValue;
        m_attributeOptionRootsBuffer.Clear();
    }

    private bool ShouldHandleAttributeTwistForCurrentSelection(PinchAndTwistEventSource twistEventSource)
    {
        if (!isActiveAndEnabled || m_attributeUiRoot == null || !m_attributeUiRoot.activeInHierarchy)
            return false;

        ResolveAttributeTwistEventSource();
        if (m_attributeTwistEventSource == null)
            return false;

        if (twistEventSource != null && twistEventSource != m_attributeTwistEventSource)
            return false;

        return TryResolveAttributeTwistContext(out _, out _, out _, out _);
    }

    private bool TryResolveAttributeTwistContext(
        out Transform attributeButtonRoot,
        out Transform optionsRoot,
        out string attributeBaseLabel,
        out int optionCount)
    {
        attributeButtonRoot = null;
        optionsRoot = null;
        attributeBaseLabel = null;
        optionCount = 0;
        m_attributeOptionRootsBuffer.Clear();

        if (m_attributeUiRoot == null || !m_attributeUiRoot.activeInHierarchy || EventSystem.current == null)
            return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
            return false;

        var attributeNamesRoot = ResolveAttributeNamesParent();
        if (attributeNamesRoot == null)
            return false;

        attributeButtonRoot = GetDirectChildUnder(selected.transform, attributeNamesRoot);
        if (attributeButtonRoot == null)
            return false;

        attributeBaseLabel = GetOrCacheAttributeBaseLabel(attributeButtonRoot);
        if (string.IsNullOrEmpty(attributeBaseLabel))
            return false;

        optionsRoot = FindOptionsRootForAttributeKey(attributeBaseLabel);
        if (optionsRoot == null)
            return false;

        optionCount = BuildAttributeOptionRoots(optionsRoot, m_attributeOptionRootsBuffer);
        return optionCount > 0;
    }

    private void ApplyAttributeFilterSelection(Transform attributeButtonRoot, int optionIndex)
    {
        if (attributeButtonRoot == null)
            return;

        string baseLabel = GetOrCacheAttributeBaseLabel(attributeButtonRoot);
        RestoreOtherAttributeButtonTexts(attributeButtonRoot);

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (optionIndex < 0)
        {
            SetAttributeButtonText(attributeButtonRoot, baseLabel);

            m_activeAttributeFilterButtonRoot = null;
            m_activeAttributeFilterOptionIndex = -1;

            if (m_labelManager != null)
                m_labelManager.ClearVisibleLabelsFilter();
            return;
        }

        if (optionIndex >= m_attributeOptionRootsBuffer.Count)
            return;

        var optionRoot = m_attributeOptionRootsBuffer[optionIndex];
        string optionLabel = GetTextFromRoot(optionRoot);
        SetAttributeButtonText(attributeButtonRoot, FormatAttributeButtonLabel(baseLabel, optionLabel));

        var binding = optionRoot != null ? optionRoot.GetComponent<LabelMarkerBinding>() : null;
        var markerIndices = binding != null ? binding.MarkerIndices : null;
        if (m_labelManager != null)
        {
            if (markerIndices != null && markerIndices.Count > 0)
                m_labelManager.SetVisibleLabelsForMarkerIndices(markerIndices);
            else
                m_labelManager.ClearVisibleLabelsFilter();
        }

        m_activeAttributeFilterButtonRoot = attributeButtonRoot;
        m_activeAttributeFilterOptionIndex = optionIndex;
    }

    private Transform ResolveAttributeNamesParent()
    {
        if (m_attributeNamesParent != null)
            return m_attributeNamesParent;

        if (m_attributeUiRoot == null)
            return null;

        var attributeRoot = m_attributeUiRoot.transform;
        for (int i = 0; i < attributeRoot.childCount; i++)
        {
            var child = attributeRoot.GetChild(i);
            if (child == null)
                continue;

            if (NormalizeAttributeKey(child.name).Contains("attributename"))
            {
                m_attributeNamesParent = child;
                return child;
            }
        }

        return null;
    }

    private Transform FindOptionsRootForAttributeKey(string attributeLabel)
    {
        if (m_attributeUiRoot == null)
            return null;

        string normalizedKey = NormalizeAttributeKey(attributeLabel);
        if (string.IsNullOrEmpty(normalizedKey))
            return null;

        var attributeNamesRoot = ResolveAttributeNamesParent();
        var attributeRoot = m_attributeUiRoot.transform;
        for (int i = 0; i < attributeRoot.childCount; i++)
        {
            var child = attributeRoot.GetChild(i);
            if (child == null || child == attributeNamesRoot)
                continue;

            if (MatchesAttributeRoot(child.name, normalizedKey))
                return child;
        }

        return null;
    }

    private int BuildAttributeOptionRoots(Transform optionsRoot, List<Transform> buffer)
    {
        buffer.Clear();
        if (optionsRoot == null)
            return 0;

        for (int i = 0; i < optionsRoot.childCount; i++)
        {
            var child = optionsRoot.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
                continue;

            if (FindSelectableInInactiveAware(child) == null)
                continue;

            buffer.Add(child);
        }

        return buffer.Count;
    }

    private void RestoreOtherAttributeButtonTexts(Transform exceptButtonRoot)
    {
        var attributeNamesRoot = ResolveAttributeNamesParent();
        if (attributeNamesRoot == null)
            return;

        for (int i = 0; i < attributeNamesRoot.childCount; i++)
        {
            var child = attributeNamesRoot.GetChild(i);
            if (child == null || child == exceptButtonRoot)
                continue;

            SetAttributeButtonText(child, GetOrCacheAttributeBaseLabel(child));
        }
    }

    private string GetOrCacheAttributeBaseLabel(Transform attributeButtonRoot)
    {
        if (attributeButtonRoot == null)
            return string.Empty;

        if (m_cachedAttributeBaseLabels.TryGetValue(attributeButtonRoot, out var cached) && !string.IsNullOrEmpty(cached))
            return cached;

        string text = GetTextFromRoot(attributeButtonRoot);
        if (string.IsNullOrEmpty(text))
            text = attributeButtonRoot.name;

        if (!string.IsNullOrEmpty(m_attributeButtonValueSeparator))
        {
            int separatorIndex = text.IndexOf(m_attributeButtonValueSeparator);
            if (separatorIndex >= 0)
                text = text.Substring(0, separatorIndex);
        }

        int colonIndex = text.IndexOf(':');
        if (colonIndex >= 0)
            text = text.Substring(0, colonIndex);

        text = text.Trim();
        m_cachedAttributeBaseLabels[attributeButtonRoot] = text;
        return text;
    }

    private void SetAttributeButtonText(Transform attributeButtonRoot, string value)
    {
        if (attributeButtonRoot == null)
            return;

        var text = attributeButtonRoot.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = value;
    }

    private string GetTextFromRoot(Transform root)
    {
        if (root == null)
            return string.Empty;

        var text = root.GetComponentInChildren<TMP_Text>(true);
        return text != null ? text.text : root.name;
    }

    private string FormatAttributeButtonLabel(string baseLabel, string optionLabel)
    {
        if (string.IsNullOrEmpty(optionLabel))
            return baseLabel;

        string separator = string.IsNullOrEmpty(m_attributeButtonValueSeparator)
            ? ": "
            : m_attributeButtonValueSeparator;

        return $"{baseLabel}{separator}{optionLabel}";
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

    private static Selectable FindSelectableInInactiveAware(Transform root)
    {
        if (root == null)
            return null;

        var selectable = root.GetComponent<Selectable>();
        if (selectable != null)
            return selectable;

        return root.GetComponentInChildren<Selectable>(true);
    }

    private int ComputeTwistStepOffset(float signedNormalized)
    {
        if (Mathf.Abs(signedNormalized) < 0.0001f)
            return 0;

        int steps = Mathf.FloorToInt(Mathf.Abs(signedNormalized) / Mathf.Max(0.0001f, m_attributeTwistPerStep));
        if (steps <= 0)
            return 0;

        return signedNormalized > 0f ? steps : -steps;
    }

    private static string NormalizeAttributeKey(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var chars = new char[text.Length];
        int length = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!char.IsLetterOrDigit(c))
                continue;

            chars[length++] = char.ToLowerInvariant(c);
        }

        return new string(chars, 0, length);
    }

    private static bool MatchesAttributeRoot(string rootName, string normalizedKey)
    {
        if (string.IsNullOrEmpty(normalizedKey))
            return false;

        string normalizedRoot = NormalizeAttributeKey(rootName);
        if (normalizedRoot.Contains(normalizedKey))
            return true;

        switch (normalizedKey)
        {
            case "availability":
                return normalizedRoot.Contains("available");
            case "owner":
                return normalizedRoot.Contains("ownership");
            case "color":
                return normalizedRoot.Contains("colour");
            default:
                return false;
        }
    }

    // Call these from your custom events or input
    public void MoveUp()
    {
        if (IsNavigationLocked())
            return;

        SendMove(MoveDirection.Up, Vector2.up);
    }

    public void MoveDown()
    {
        if (IsNavigationLocked())
            return;

        SendMove(MoveDirection.Down, Vector2.down);
    }

    public void MoveLeft()
    {
        if (IsNavigationLocked())
            return;

        if (TryDismissAttributeUiFromLeftSwipe())
            return;

        if (TrySwitchToPreviousProxySet())
            return;
        SendMove(MoveDirection.Left, Vector2.left);
    }

    public void MoveRight()
    {
        if (IsNavigationLocked())
            return;

        if (TryShowAttributeUiFromLeftColumn())
            return;

        if (TrySwitchToNextProxySet())
            return;
        SendMove(MoveDirection.Right, Vector2.right);
    }

    public void ClickSelected()
    {
        if (IsNavigationLocked())
            return;

        SendSubmit();
    }

    // Optionally expose a vector based move if you prefer
    public void Move(Vector2 dir)
    {
        if (IsNavigationLocked())
            return;

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

    // ---------- ScreenUI left column → show AttributeUI ----------

    /// <summary>
    /// When selection is under the left column (e.g. ProxyUI) and AttributeUI is off, moving right enables AttributeUI
    /// and optionally moves focus there. Runs before proxy-set switching so single-column grids still work.
    /// </summary>
    bool TryShowAttributeUiFromLeftColumn()
    {
        if (m_leftColumnLabelsParent == null || m_attributeUiRoot == null)
            return false;
        if (ProxySetDrillDownController.IsAnyDrillDownChildViewActive)
            return false;

        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
            return false;

        if (selected != m_leftColumnLabelsParent.gameObject && !selected.transform.IsChildOf(m_leftColumnLabelsParent))
            return false;

        if (m_attributeUiRoot.activeSelf)
            return false;

        m_attributeUiRoot.SetActive(true);

        if (m_labelManager != null && m_attributeLabelsParentForManager != null)
            m_labelManager.SetActiveLabelsParent(m_attributeLabelsParentForManager);

        Canvas.ForceUpdateCanvases();

        if (m_selectFirstSelectableInAttributeUi)
        {
            var first = FindFirstSelectableIn(m_attributeUiRoot.transform);
            if (first != null)
                Select(first);
        }

        return true;
    }

    /// <summary>
    /// Delegates to <see cref="AttributeUiDismissOnLeftSwipe"/> on <see cref="m_attributeUiRoot"/> when assigned.
    /// </summary>
    bool TryDismissAttributeUiFromLeftSwipe()
    {
        if (m_attributeUiRoot == null)
            return false;

        var dismiss = m_attributeUiRoot.GetComponent<AttributeUiDismissOnLeftSwipe>();
        return dismiss != null && dismiss.TryHandleMoveLeft();
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
        if (ProxySetDrillDownController.IsAnyDrillDownChildViewActive) return false;
        if (!TryGetSelectedColumnInfo(out int col, out _) || col != 0) return false;

        if (!m_labelManager.TrySwitchToPreviousLabelsParent(ProxySetHorizontalTransitionDirection.ToLeft))
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
        if (ProxySetDrillDownController.IsAnyDrillDownChildViewActive) return false;
        if (!TryGetSelectedColumnInfo(out int col, out int columnCount) || col != columnCount - 1) return false;

        if (!m_labelManager.TrySwitchToNextLabelsParent(ProxySetHorizontalTransitionDirection.ToRight))
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

    bool IsNavigationLocked()
    {
        return m_labelManager != null && m_labelManager.IsTransitioning;
    }
}
