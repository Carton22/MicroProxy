using System.Collections.Generic;
using UnityEngine;

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

    private readonly List<GameObject> m_activeLabels = new();
    private readonly List<GameObject> m_labelPool = new();

    /// <summary>
    /// When true, SyncLabelsWithDetections does nothing so the current labels stay fixed.
    /// Toggled by B button on Meta Quest 3 controller.
    /// </summary>
    private bool m_labelsFrozen;

    private void Awake()
    {
        if (m_labelPrefab != null)
        {
            m_labelPrefab.SetActive(false);
        }
    }

    private void Update()
    {
        // Meta Quest 3: B button toggles whether labels are updated from detections
        if (OVRInput.GetDown(OVRInput.Button.Two))
        {
            m_labelsFrozen = !m_labelsFrozen;
            Debug.Log($"[ProxyCreator] Labels {(m_labelsFrozen ? "frozen" : "updating")} (B pressed).");
        }
    }

    /// <summary>
    /// Called (e.g. from SentisInferenceUiManager.OnObjectsDetected)
    /// to ensure there are exactly detectionCount label objects
    /// under the configured parent.
    /// </summary>
    /// <param name="detectionCount">Current number of detected objects.</param>
    public void SyncLabelsWithDetections(int detectionCount)
    {
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
}

