using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Creates and manages a list of UI label instances under a target Canvas
/// based on the current number of detected objects.
/// Hook this up to SentisInferenceUiManager.OnObjectsDetected in the inspector.
/// Press B (Meta Quest 3) to freeze the label list so it is no longer updated.
/// </summary>
public class ProxyCreator : MonoBehaviour
{
    [Header("Label UI")]
    [SerializeField] private RectTransform m_labelsParent;
    [SerializeField] private GameObject m_labelPrefab;
    [Header("Server Detection")]
    [SerializeField] private PassthroughCameraSamples.MultiObjectDetection.ServerObjDetector m_serverDetector;
    [Header("2D Boxes (screen space)")]
    [SerializeField] private ScreenSpaceBoundingBoxDrawer m_screenSpaceBoxDrawer;

    private readonly List<GameObject> m_activeLabels = new();
    private readonly List<GameObject> m_labelPool = new();

    private int m_selectionMin;
    private int m_selectionMax;
    private bool m_selectionRangeOverride;

    /// <summary>
    /// When true, SyncLabelsWithDetections does nothing so the current labels stay fixed.
    /// Toggled by B button on Meta Quest 3 controller.
    /// </summary>
    private bool m_labelsFrozen;

    private void Update()
    {
        // Keep selection range in sync: when not overridden by twist, use current focus (single selection)
        if (!m_selectionRangeOverride)
        {
            int focus = GetSelectedLabelIndex();
            int count = m_labelsParent != null ? m_labelsParent.childCount : 0;
            if (count > 0 && focus >= 0 && focus < count)
            {
                m_selectionMin = m_selectionMax = focus;
            }
        }

        // Apply selected/normal color to each label by selection range
        ApplySelectionRangeVisual();

        // Meta Quest 3: B button toggles whether labels are updated from detections
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            m_labelsFrozen = !m_labelsFrozen;
            Debug.Log($"[ProxyCreator] Labels {(m_labelsFrozen ? "frozen" : "updating")} (B pressed).");

            if (m_serverDetector != null)
            {
                if (m_labelsFrozen)
                {
                    m_serverDetector.FreezeDetection();
                }
                else
                {
                    m_serverDetector.UnfreezeDetection();
                }
                // When unfreezing, next frame can be a "generate" request for crops/masks.
                m_serverDetector.RequestGenerateForNextFrame();
            }
        }
    }

    /// <summary>
    /// Index of the currently selected proxy label under m_labelsParent (0-based), or -1 if none.
    /// One-to-one with detection/box index.
    /// </summary>
    public int GetSelectedLabelIndex()
    {
        if (m_labelsParent == null || EventSystem.current == null)
            return -1;
        var current = EventSystem.current.currentSelectedGameObject;
        if (current == null)
            return -1;
        for (int i = 0; i < m_labelsParent.childCount; i++)
        {
            if (m_labelsParent.GetChild(i).gameObject == current)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Selected color from the proxy label button (so bounding box can match when that label is selected).
    /// </summary>
    public Color GetSelectedColor()
    {
        if (m_labelsParent == null || m_labelsParent.childCount == 0)
            return Color.white;
        var first = m_labelsParent.GetChild(0).GetComponent<Button>();
        if (first != null)
            return first.colors.selectedColor;
        return Color.white;
    }

    /// <summary>
    /// Normal (unselected) color from the first proxy label button.
    /// </summary>
    public Color GetNormalColor()
    {
        if (m_labelsParent == null || m_labelsParent.childCount == 0)
            return Color.white;
        var first = m_labelsParent.GetChild(0).GetComponent<Button>();
        if (first != null)
            return first.colors.normalColor;
        return Color.white;
    }

    /// <summary>
    /// Set a multi-selection range (e.g. from twist). Labels and boxes in [min, max] will show selected style.
    /// Clamped to valid indices. Cleared when twist ends so single selection follows focus again.
    /// </summary>
    public void SetSelectionRange(int minIndex, int maxIndex)
    {
        m_selectionRangeOverride = true;
        int count = m_labelsParent != null ? m_labelsParent.childCount : 0;
        if (count == 0) return;
        m_selectionMin = Mathf.Clamp(Mathf.Min(minIndex, maxIndex), 0, count - 1);
        m_selectionMax = Mathf.Clamp(Mathf.Max(minIndex, maxIndex), 0, count - 1);
    }

    /// <summary>
    /// Clear the twist-driven range so selection follows the current focus (single) again.
    /// </summary>
    public void ClearSelectionRangeOverride()
    {
        m_selectionRangeOverride = false;
    }

    /// <summary>
    /// Number of active proxy labels (children under m_labelsParent).
    /// </summary>
    public int GetLabelCount()
    {
        return m_labelsParent != null ? m_labelsParent.childCount : 0;
    }

    /// <summary>
    /// Set the current single selection to the proxy label at the given index (0-based). Used e.g. by pinch-twist single-select.
    /// </summary>
    public void SetSelectedLabelByIndex(int index)
    {
        if (m_labelsParent == null || EventSystem.current == null) return;
        int count = m_labelsParent.childCount;
        if (count == 0) return;
        index = Mathf.Clamp(index, 0, count - 1);
        var go = m_labelsParent.GetChild(index).gameObject;
        EventSystem.current.SetSelectedGameObject(go);
        var sel = go.GetComponent<Selectable>();
        if (sel != null) sel.Select();
    }

    /// <summary>
    /// Current selection range (0-based indices). Used for multi-highlight on labels and world-space boxes.
    /// </summary>
    public void GetSelectionRange(out int minIndex, out int maxIndex)
    {
        if (!m_selectionRangeOverride)
        {
            int focus = GetSelectedLabelIndex();
            m_selectionMin = m_selectionMax = focus >= 0 ? focus : 0;
        }
        minIndex = m_selectionMin;
        maxIndex = m_selectionMax;
    }

    /// <summary>
    /// Called (e.g. from SentisInferenceUiManager.OnObjectsDetected)
    /// to ensure there are exactly detectionCount label objects
    /// under the configured parent.
    /// </summary>
    /// <param name="detectionCount">Current number of detected objects.</param>
    public void SyncLabelsWithDetections(int detectionCount)
    {
        // Remove any inactive children under the labels parent so they
        // don't get counted or renamed by other systems (e.g. ProxyInject).
        if (m_labelsParent != null)
        {
            for (int i = m_labelsParent.childCount - 1; i >= 0; i--)
            {
                var child = m_labelsParent.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        if (m_labelsFrozen)
        {
            return;
        }

        if (m_labelsParent == null || m_labelPrefab == null)
        {
            Debug.LogWarning("[ProxyCreator] Labels parent or label prefab is not assigned.");
            return;
        }

        if (detectionCount < 0)
        {
            detectionCount = 0;
        }

        // Grow
        while (m_activeLabels.Count < detectionCount)
        {
            var label = GetOrCreateLabel();
            label.transform.SetParent(m_labelsParent, false);
            label.SetActive(true);
            m_activeLabels.Add(label);
        }

        // Ensure each active label shows an ID like "ID: 1", "ID: 2", ...
        for (int i = 0; i < m_activeLabels.Count; i++)
        {
            var go = m_activeLabels[i];
            if (go == null) continue;
            var text = go.GetComponentInChildren<TextMeshPro>();
            if (text != null)
            {
                text.text = $"ID: {i + 1}";
            }
        }

        // Shrink
        while (m_activeLabels.Count > detectionCount)
        {
            var lastIndex = m_activeLabels.Count - 1;
            var label = m_activeLabels[lastIndex];
            m_activeLabels.RemoveAt(lastIndex);
            ReturnToPool(label);
        }
    }

    /// <summary>
    /// Removes all active labels and returns them to the pool.
    /// </summary>
    public void ClearAllLabels()
    {
        for (int i = m_activeLabels.Count - 1; i >= 0; i--)
        {
            ReturnToPool(m_activeLabels[i]);
        }

        m_activeLabels.Clear();
    }

    private GameObject GetOrCreateLabel()
    {
        if (m_labelPool.Count > 0)
        {
            var pooled = m_labelPool[m_labelPool.Count - 1];
            m_labelPool.RemoveAt(m_labelPool.Count - 1);
            return pooled;
        }

        var instance = Instantiate(m_labelPrefab);
        return instance;
    }

    private void ReturnToPool(GameObject label)
    {
        if (label == null)
        {
            return;
        }

        label.SetActive(false);
        label.transform.SetParent(transform, false);
        m_labelPool.Add(label);
    }

    private void ApplySelectionRangeVisual()
    {
        if (m_labelsParent == null) return;
        int count = m_labelsParent.childCount;
        if (count == 0) return;
        Color selectedColor = GetSelectedColor();
        Color normalColor = GetNormalColor();
        for (int i = 0; i < count; i++)
        {
            var child = m_labelsParent.GetChild(i).gameObject;
            var btn = child.GetComponent<Button>();
            var graphic = btn != null && btn.targetGraphic != null ? btn.targetGraphic : child.GetComponentInChildren<Image>();
            if (graphic != null)
            {
                graphic.color = (i >= m_selectionMin && i <= m_selectionMax) ? selectedColor : normalColor;
            }
        }

        // Keep 2D screen-space boxes in sync with the same selection range
        if (m_screenSpaceBoxDrawer != null)
        {
            m_screenSpaceBoxDrawer.UpdateSelectionRangeHighlight(
                m_selectionMin,
                m_selectionMax,
                selectedColor,
                normalColor);
        }
    }
}

