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

    [Tooltip("ProxyUI page scope: ancestor of the main proxy list and any in-ProxyUI drill-down label parents. Swipe-right opens AttributeUI only when selection and active label parent lie under this transform. Leave empty to use Left Column Labels Parent.")]
    [SerializeField] private Transform m_proxyUiPageRoot;

    [Tooltip("Right column root to enable (e.g. AttributeUI).")]
    [SerializeField] private GameObject m_attributeUiRoot;

    [Tooltip("If set, ProxyLabelManager.SetActiveLabelsParent is called after AttributeUI is shown (must be an entry in the manager's label parents list).")]
    [SerializeField] private Transform m_attributeLabelsParentForManager;

    [SerializeField] private bool m_selectFirstSelectableInAttributeUi = true;

    [Tooltip("Optional explicit scroller to refresh on ProxyUI <-> AttributeUI transitions. If null, auto-find is used.")]
    [SerializeField] private ProxyLabelHorizonScroller m_proxyLabelHorizonScroller;

    [Tooltip("When true, AttributeUI is turned off when the scene loads (play mode), even if left active in the editor.")]
    [SerializeField] private bool m_attributeUiInactiveByDefault = true;

    [Header("Attribute twist filter")]
    [Tooltip("Right-hand twist source used to cycle attribute values while a top-level attribute button is selected. Defaults to a PinchAndTwistEventSource on this GameObject.")]
    [SerializeField] private PinchAndTwistEventSource m_attributeTwistEventSource;

    [Tooltip("Optional root that contains the top-level attribute buttons (for example the AttributeName group). Auto-resolved under AttributeUI when left empty.")]
    [SerializeField] private Transform m_attributeNamesParent;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float m_attributeTwistPerStep = 0.12f;
    [SerializeField] private bool m_debugRemotePinchTwist;

    [SerializeField] private string m_attributeButtonValueSeparator = ": ";

    private readonly Dictionary<Transform, string> m_cachedAttributeBaseLabels = new();
    private readonly Dictionary<Transform, int> m_attributeFilterSelections = new();
    private readonly List<Transform> m_attributeOptionRootsBuffer = new();
    private readonly List<int> m_conjunctiveMarkerWork = new();
    private bool m_inAttributeTwistGesture;
    private Transform m_attributeGestureButtonRoot;
    private Transform m_attributeGestureOptionsRoot;
    private int m_attributeGestureStartOptionIndex = -1;
    private int m_attributeGestureLastAppliedOptionIndex = int.MinValue;
    private bool m_inRemoteProxyMultiSelect;
    private int m_remoteProxyAnchorIndex = -1;
    private GameObject m_remoteProxyAnchorObject;

    void Reset()
    {
        if (m_proxyUiPageRoot == null)
            m_proxyUiPageRoot = m_leftColumnLabelsParent;
    }

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
        m_attributeGestureStartOptionIndex = m_attributeFilterSelections.TryGetValue(attributeButtonRoot, out var stored)
            ? stored
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

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (optionIndex < 0)
        {
            SetAttributeButtonText(attributeButtonRoot, baseLabel);
            m_attributeFilterSelections.Remove(attributeButtonRoot);
            ReapplyConjunctiveAttributeFilter();
            return;
        }

        if (optionIndex >= m_attributeOptionRootsBuffer.Count)
            return;

        var optionRoot = m_attributeOptionRootsBuffer[optionIndex];
        string optionLabel = GetTextFromRoot(optionRoot);
        SetAttributeButtonText(attributeButtonRoot, FormatAttributeButtonLabel(baseLabel, optionLabel));

        m_attributeFilterSelections[attributeButtonRoot] = optionIndex;
        ReapplyConjunctiveAttributeFilter();
    }

    /// <summary>
    /// Rebuilds the left-column visibility filter as the intersection of marker sets for every attribute that has a twist-selected value (AND).
    /// </summary>
    private void ReapplyConjunctiveAttributeFilter()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (m_labelManager == null)
            return;

        if (m_attributeFilterSelections.Count == 0)
        {
            m_labelManager.ClearVisibleLabelsFilter();
            return;
        }

        HashSet<int> intersection = null;
        bool anyConstraint = false;

        foreach (var kvp in m_attributeFilterSelections)
        {
            if (kvp.Value < 0)
                continue;

            var attrRoot = kvp.Key;
            if (attrRoot == null)
                continue;

            string keyLabel = GetOrCacheAttributeBaseLabel(attrRoot);
            var optionsRoot = FindOptionsRootForAttributeKey(keyLabel);
            if (optionsRoot == null)
                continue;

            int optionCount = BuildAttributeOptionRoots(optionsRoot, m_attributeOptionRootsBuffer);
            if (kvp.Value >= optionCount)
                continue;

            anyConstraint = true;
            var optionTransform = m_attributeOptionRootsBuffer[kvp.Value];
            var binding = optionTransform != null ? optionTransform.GetComponent<LabelMarkerBinding>() : null;
            var indices = binding != null ? binding.MarkerIndices : null;

            if (indices == null || indices.Count == 0)
            {
                m_labelManager.SetVisibleLabelsForMarkerIndices(System.Array.Empty<int>(), emptyMeansHideAll: true);
                return;
            }

            if (intersection == null)
            {
                intersection = new HashSet<int>();
                for (int i = 0; i < indices.Count; i++)
                {
                    int m = indices[i];
                    if (m >= 0)
                        intersection.Add(m);
                }
            }
            else
            {
                var narrowed = new HashSet<int>();
                foreach (var marker in intersection)
                {
                    for (int i = 0; i < indices.Count; i++)
                    {
                        if (indices[i] != marker)
                            continue;
                        narrowed.Add(marker);
                        break;
                    }
                }

                intersection = narrowed;
            }
        }

        if (!anyConstraint)
        {
            m_labelManager.ClearVisibleLabelsFilter();
            return;
        }

        m_conjunctiveMarkerWork.Clear();
        if (intersection != null)
        {
            foreach (var marker in intersection)
                m_conjunctiveMarkerWork.Add(marker);
        }

        bool emptyIntersection = m_conjunctiveMarkerWork.Count == 0;
        m_labelManager.SetVisibleLabelsForMarkerIndices(
            m_conjunctiveMarkerWork,
            emptyMeansHideAll: emptyIntersection);
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

        // Gesture name signals (pinch_twist_in/out) arrive as exactly +/-1 with no true magnitude.
        // Treat them as a single incremental step to avoid jumping straight to boundary ranges.
        float abs = Mathf.Abs(signedNormalized);
        if (Mathf.Approximately(abs, 1f))
            return signedNormalized > 0f ? 1 : -1;

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

        if (TryAdvanceAttributeValueFromTap())
            return;

        SendSubmit();
    }

    /// <summary>
    /// Double-tap action. If focused AttributeUI button is currently on "none", regroup ProxyUI labels by this attribute's values.
    /// Otherwise falls back to the same behavior as single tap.
    /// </summary>
    public void DoubleTapSelected()
    {
        if (IsNavigationLocked())
            return;

        if (TryGroupActiveProxyLabelsByFocusedAttributeFromNone())
            return;

        ClickSelected();
    }

    /// <summary>
    /// Entry point for socket-driven zoom gestures to switch ScreenUI parent layers directly.
    /// zoomOut=true goes to previous layer (e.g. ProxyUI -> SpatialHierarchy when ordered that way in ProxyLabelManager).
    /// zoomOut=false goes to next layer.
    /// </summary>
    public void RemoteZoomSwitchLayer(bool zoomOut)
    {
        if (IsNavigationLocked())
            return;

        TrySwitchScreenLayerDirect(zoomOut ? -1 : 1);
    }

    /// <summary>
    /// Entry point for socket-driven pinch_twist gestures.
    /// Continuous mode: signed magnitude controls how far the selection extends.
    /// </summary>
    public void RemotePinchAndTwist(float signedNormalized)
    {
        if (m_debugRemotePinchTwist)
            Debug.Log($"[UINavigator] RemotePinchAndTwist input={signedNormalized:0.###}");

        if (IsNavigationLocked())
        {
            if (m_debugRemotePinchTwist)
                Debug.Log("[UINavigator] RemotePinchAndTwist blocked: navigation locked.");
            return;
        }

        if (Mathf.Approximately(signedNormalized, 0f))
        {
            if (m_debugRemotePinchTwist)
                Debug.Log("[UINavigator] RemotePinchAndTwist ignored: zero input.");
            return;
        }

        ApplyRemoteAttributeTwistContinuous(signedNormalized);
    }

    /// <summary>
    /// Uses pinch_twist in/out as ProxyUI multi-selection only when current focus is on ProxyUI labels.
    /// Returns true when the gesture is consumed by ProxyUI multi-select.
    /// </summary>
    public bool TryHandleRemoteProxyMultiSelect(float signedNormalized)
    {
        if (Mathf.Approximately(signedNormalized, 0f) || IsNavigationLocked())
        {
            if (m_debugRemotePinchTwist)
                Debug.Log($"[UINavigator] MultiSelect ignored: zeroInput={Mathf.Approximately(signedNormalized, 0f)} navLocked={IsNavigationLocked()}");
            return false;
        }

        if (!CanUseRemoteProxyMultiSelectNow())
        {
            if (m_debugRemotePinchTwist)
            {
                string selectedName = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                    ? EventSystem.current.currentSelectedGameObject.name
                    : "<null>";
                Debug.Log($"[UINavigator] MultiSelect blocked by context. selected={selectedName}");
            }
            ResetRemoteProxyMultiSelectionState(clearRangeOverride: true);
            return false;
        }

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_labelManager == null)
        {
            if (m_debugRemotePinchTwist)
                Debug.LogWarning("[UINavigator] MultiSelect blocked: ProxyLabelManager not found.");
            return false;
        }

        int count = m_labelManager.GetLabelCount();
        if (count <= 0)
        {
            if (m_debugRemotePinchTwist)
                Debug.Log("[UINavigator] MultiSelect blocked: no labels in active manager.");
            return false;
        }

        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (!m_inRemoteProxyMultiSelect || selected != m_remoteProxyAnchorObject)
        {
            m_remoteProxyAnchorIndex = m_labelManager.GetSelectedLabelIndex();
            if (m_remoteProxyAnchorIndex < 0)
                m_remoteProxyAnchorIndex = 0;
            m_remoteProxyAnchorIndex = Mathf.Clamp(m_remoteProxyAnchorIndex, 0, count - 1);
            m_remoteProxyAnchorObject = selected;
            m_inRemoteProxyMultiSelect = true;
        }

        int stepCount = ComputeTwistStepOffset(signedNormalized);
        if (stepCount == 0)
        {
            if (m_debugRemotePinchTwist)
                Debug.Log($"[UINavigator] MultiSelect ignored: stepCount=0 input={signedNormalized:0.###} perStep={m_attributeTwistPerStep:0.###}");
            return false;
        }

        int leftSteps = stepCount < 0 ? -stepCount : 0;
        int rightSteps = stepCount > 0 ? stepCount : 0;
        int minIndex = Mathf.Clamp(m_remoteProxyAnchorIndex - leftSteps, 0, count - 1);
        int maxIndex = Mathf.Clamp(m_remoteProxyAnchorIndex + rightSteps, 0, count - 1);
        m_labelManager.SetSelectionRange(minIndex, maxIndex);

        if (m_debugRemotePinchTwist)
        {
            string selectedName = selected != null ? selected.name : "<null>";
            Debug.Log($"[UINavigator] MultiSelect applied input={signedNormalized:0.###} stepOffset={stepCount} anchor={m_remoteProxyAnchorIndex} range=[{minIndex},{maxIndex}] count={count} selected={selectedName}");
        }

        return true;
    }

    private void ApplyRemoteAttributeTwistContinuous(float signedNormalized)
    {
        int stepOffset = ComputeTwistStepOffset(signedNormalized);
        if (stepOffset == 0)
        {
            if (m_debugRemotePinchTwist)
                Debug.Log($"[UINavigator] AttributeTwist ignored: stepOffset=0 input={signedNormalized:0.###} perStep={m_attributeTwistPerStep:0.###}");
            return;
        }

        if (!TryResolveAttributeTwistContext(out var attributeButtonRoot, out var optionsRoot, out _, out var optionCount))
        {
            if (m_debugRemotePinchTwist)
                Debug.Log("[UINavigator] AttributeTwist blocked: no valid attribute context.");
            return;
        }

        if (optionCount <= 0)
        {
            if (m_debugRemotePinchTwist)
                Debug.Log("[UINavigator] AttributeTwist blocked: optionCount <= 0.");
            return;
        }

        int currentIndex = m_attributeFilterSelections.TryGetValue(attributeButtonRoot, out var storedIndex)
            ? storedIndex
            : -1;

        int targetIndex = Mathf.Clamp(currentIndex + stepOffset, -1, optionCount - 1);
        if (targetIndex == currentIndex)
        {
            if (m_debugRemotePinchTwist)
                Debug.Log($"[UINavigator] AttributeTwist clamped: current={currentIndex} stepOffset={stepOffset} options={optionCount}");
            return;
        }

        BuildAttributeOptionRoots(optionsRoot, m_attributeOptionRootsBuffer);
        ApplyAttributeFilterSelection(attributeButtonRoot, targetIndex);

        if (m_debugRemotePinchTwist)
            Debug.Log($"[UINavigator] AttributeTwist applied input={signedNormalized:0.###} stepOffset={stepOffset} current={currentIndex} target={targetIndex} options={optionCount}");
    }

    /// <summary>
    /// When an AttributeUI top-level button is focused, tap cycles through:
    /// none (-1) -> option 0 -> ... -> option N-1 -> none (-1) ...
    /// </summary>
    private bool TryAdvanceAttributeValueFromTap()
    {
        if (!TryResolveAttributeTwistContext(out var attributeButtonRoot, out var optionsRoot, out _, out var optionCount))
            return false;

        if (optionCount <= 0)
            return false;

        int currentIndex = m_attributeFilterSelections.TryGetValue(attributeButtonRoot, out var storedIndex)
            ? storedIndex
            : -1;

        int nextIndex = currentIndex + 1;
        if (nextIndex >= optionCount)
            nextIndex = -1;

        if (nextIndex < 0)
        {
            ApplyAttributeFilterSelection(attributeButtonRoot, -1);
            return true;
        }

        BuildAttributeOptionRoots(optionsRoot, m_attributeOptionRootsBuffer);
        ApplyAttributeFilterSelection(attributeButtonRoot, nextIndex);
        return true;
    }

    private bool TryGroupActiveProxyLabelsByFocusedAttributeFromNone()
    {
        if (!TryResolveAttributeTwistContext(out var attributeButtonRoot, out var optionsRoot, out _, out var optionCount))
            return false;

        int currentIndex = m_attributeFilterSelections.TryGetValue(attributeButtonRoot, out var storedIndex)
            ? storedIndex
            : -1;
        if (currentIndex >= 0)
            return false; // only when this attribute is currently "none"

        if (optionCount <= 0)
            return false;

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_labelManager == null)
            return false;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        if (activeParent == null)
            return false;

        BuildAttributeOptionRoots(optionsRoot, m_attributeOptionRootsBuffer);
        if (m_attributeOptionRootsBuffer.Count == 0)
            return false;

        var grouped = new List<Transform>(activeParent.childCount);
        var used = new HashSet<Transform>();

        // Group labels by attribute option order.
        for (int optionIndex = 0; optionIndex < m_attributeOptionRootsBuffer.Count; optionIndex++)
        {
            var optionRoot = m_attributeOptionRootsBuffer[optionIndex];
            if (optionRoot == null)
                continue;

            var optionBinding = optionRoot.GetComponent<LabelMarkerBinding>();
            var optionMarkers = optionBinding != null ? optionBinding.MarkerIndices : null;
            if (optionMarkers == null || optionMarkers.Count == 0)
                continue;

            for (int i = 0; i < activeParent.childCount; i++)
            {
                var child = activeParent.GetChild(i);
                if (child == null || used.Contains(child))
                    continue;

                var childBinding = child.GetComponent<LabelMarkerBinding>();
                var childMarkers = childBinding != null ? childBinding.MarkerIndices : null;
                if (!HasAnyCommonMarker(optionMarkers, childMarkers))
                    continue;

                grouped.Add(child);
                used.Add(child);
            }
        }

        // Keep unmatched labels at the end in authored order.
        for (int i = 0; i < activeParent.childCount; i++)
        {
            var child = activeParent.GetChild(i);
            if (child == null || used.Contains(child))
                continue;
            grouped.Add(child);
        }

        if (grouped.Count == 0)
            return false;

        for (int i = 0; i < grouped.Count; i++)
            grouped[i].SetSiblingIndex(i);

        // Preserve current selection if still valid; otherwise pick first visible selectable.
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool selectionInsideGroupedRoot = selected != null &&
            (selected == activeParent.gameObject || selected.transform.IsChildOf(activeParent));
        if (!selectionInsideGroupedRoot)
        {
            var first = FindFirstSelectableIn(activeParent);
            if (first != null)
                Select(first);
        }

        var scroller = activeParent.GetComponent<ProxyLabelHorizonScroller>();
        if (scroller == null)
            scroller = activeParent.GetComponentInParent<ProxyLabelHorizonScroller>(true);
        if (scroller != null)
            scroller.ForceRefreshNow();

        return true;
    }

    private static bool HasAnyCommonMarker(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a == null || b == null || a.Count == 0 || b.Count == 0)
            return false;

        for (int i = 0; i < a.Count; i++)
        {
            int marker = a[i];
            for (int j = 0; j < b.Count; j++)
            {
                if (b[j] == marker)
                    return true;
            }
        }

        return false;
    }

    private bool TrySwitchScreenLayerDirect(int stepDirection)
    {
        if (stepDirection == 0)
            return false;

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (m_labelManager == null)
            return false;

        if (!IsSelectionInsideActiveManagedProxySet())
            return false;

        if (ProxySetDrillDownController.IsAnyDrillDownChildViewActive)
            return false;

        bool switched = stepDirection < 0
            ? m_labelManager.TrySwitchToPreviousLabelsParent(ProxySetHorizontalTransitionDirection.ToLeft)
            : m_labelManager.TrySwitchToNextLabelsParent(ProxySetHorizontalTransitionDirection.ToRight);

        if (!switched)
            return false;

        var newRoot = m_labelManager.GetActiveLabelsParent();
        var first = FindFirstSelectableIn(newRoot);
        if (first != null)
            Select(first);
        return true;
    }

    private bool CanUseRemoteProxyMultiSelectNow()
    {
        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_labelManager == null)
            return false;

        if (m_leftColumnLabelsParent == null)
            return false;

        if (!IsActiveLabelsParentWithinProxyUi())
            return false;

        var activeParent = m_labelManager.GetActiveLabelsParent();
        if (activeParent == null || activeParent.childCount <= 0)
            return false;

        if (EventSystem.current == null)
            return false;

        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
        {
            // Focus can be lost transiently on device; keep allowing ProxyUI pinch/twist
            // when we are clearly on the ProxyUI page (not AttributeUI).
            bool attributeUiVisible = m_attributeUiRoot != null && m_attributeUiRoot.activeInHierarchy;
            return !attributeUiVisible;
        }

        if (selected != m_leftColumnLabelsParent.gameObject && !selected.transform.IsChildOf(m_leftColumnLabelsParent))
            return false;

        return true;
    }

    private void ResetRemoteProxyMultiSelectionState(bool clearRangeOverride)
    {
        m_inRemoteProxyMultiSelect = false;
        m_remoteProxyAnchorIndex = -1;
        m_remoteProxyAnchorObject = null;

        if (!clearRangeOverride)
            return;

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();
        if (m_labelManager != null)
            m_labelManager.ClearSelectionRangeOverride();
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

        var submitTarget = ResolveSubmitTarget(selected);
        var data = new BaseEventData(EventSystem.current);

        // Works for Button, Toggle, etc.
        if(!ExecuteEvents.Execute(submitTarget, data, ExecuteEvents.submitHandler))
        {
            // Some controls only react to click
            ExecuteEvents.Execute(submitTarget, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
    }

    GameObject ResolveSubmitTarget(GameObject selected)
    {
        if (selected == null)
            return null;

        // If selection is on a text/image child, submit the nearest selectable parent (Button/Toggle/etc.).
        var selectable = selected.GetComponentInParent<Selectable>();
        if (selectable != null)
            return selectable.gameObject;

        var submit = selected.GetComponentInParent<ISubmitHandler>();
        if (submit is Component submitComponent)
            return submitComponent.gameObject;

        return selected;
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
    /// Active proxy label parent must be the configured ProxyUI left-column root or one of its runtime child views.
    /// This keeps sibling ScreenUI sections like SpatialHierarchy / MaterialArea from opening AttributeUI.
    /// </summary>
    bool IsActiveLabelsParentWithinProxyUi()
    {
        if (m_leftColumnLabelsParent == null)
            return false;

        if (m_labelManager == null)
            m_labelManager = FindFirstObjectByType<ProxyLabelManager>();

        if (m_labelManager == null)
            return false;

        var active = m_labelManager.GetActiveLabelsParent();
        if (active == null)
            return false;

        return active == m_leftColumnLabelsParent ||
               active.IsChildOf(m_leftColumnLabelsParent) ||
               m_leftColumnLabelsParent.IsChildOf(active);
    }

    bool IsActiveLabelsParentUnderProxyUiPage(Transform pageRoot)
    {
        if (pageRoot == null || !IsActiveLabelsParentWithinProxyUi())
            return false;

        var active = m_labelManager != null ? m_labelManager.GetActiveLabelsParent() : null;
        if (active == null)
            return false;

        return active == pageRoot ||
               active.IsChildOf(pageRoot) ||
               pageRoot.IsChildOf(active);
    }

    /// <summary>
    /// When selection is under the left column (e.g. ProxyUI) and AttributeUI is off, moving right enables AttributeUI
    /// and optionally moves focus there. Runs before proxy-set switching so single-column grids still work.
    /// Only when the user is on the ProxyUI page: top-level proxy list or an in-scope drill-down view (not e.g. SpatialHierarchy).
    /// </summary>
    bool TryShowAttributeUiFromLeftColumn()
    {
        if (m_leftColumnLabelsParent == null || m_attributeUiRoot == null)
            return false;

        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null)
            return false;

        if (selected != m_leftColumnLabelsParent.gameObject && !selected.transform.IsChildOf(m_leftColumnLabelsParent))
            return false;

        if (!IsActiveLabelsParentWithinProxyUi())
            return false;

        if (m_attributeUiRoot.activeSelf)
            return false;

        m_attributeUiRoot.SetActive(true);

        if (m_labelManager != null && m_attributeLabelsParentForManager != null)
            m_labelManager.SetActiveLabelsParent(m_attributeLabelsParentForManager);

        Canvas.ForceUpdateCanvases();
        var attributeScroller = m_proxyLabelHorizonScroller;
        if (attributeScroller == null)
        {
            var attributeRefreshRoot = m_attributeLabelsParentForManager != null ? m_attributeLabelsParentForManager : m_attributeUiRoot.transform;
            attributeScroller = attributeRefreshRoot.GetComponent<ProxyLabelHorizonScroller>();
            if (attributeScroller == null)
                attributeScroller = attributeRefreshRoot.GetComponentInParent<ProxyLabelHorizonScroller>(true);
            if (attributeScroller == null)
                attributeScroller = attributeRefreshRoot.GetComponentInChildren<ProxyLabelHorizonScroller>(true);
        }
        if (attributeScroller != null)
        {
            Debug.Log("Force refresh attribute scroller when showing AttributeUI");
            attributeScroller.ForceRefreshNow();
        }

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
