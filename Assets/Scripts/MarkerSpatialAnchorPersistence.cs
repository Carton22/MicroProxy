using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

/// <summary>
/// Persists marker layout relative to a spatial anchor root so marker placement can be restored across sessions.
///
/// Workflow:
/// 1) Make sure MarkerParent is parented under a spatial-anchor root.
/// 2) Save anchor UUID + marker local transforms once.
/// 3) On later sessions, load marker local transforms and re-attach under the same anchor root.
/// </summary>
public class MarkerSpatialAnchorPersistence : MonoBehaviour
{
    [Header("Marker references")]
    [Tooltip("Optional PinchTargetSpawner used to resolve MarkerParent automatically.")]
    [SerializeField] private PinchTargetSpawner m_spawner;
    [Tooltip("Optional explicit MarkerParent. If null, resolves from m_spawner.")]
    [SerializeField] private Transform m_markerParent;

    [Header("Spatial anchor root")]
    [Tooltip("Root transform that should own MarkerParent. Assign your loaded/spawned spatial anchor root here.")]
    [SerializeField] private Transform m_spatialAnchorRoot;
    [SerializeField] private OVRSpatialAnchor m_spatialAnchor;

    [Header("Persistence")]
    [SerializeField] private bool m_autoLoadOnStart = true;
    [Tooltip("How long to wait for anchor tracking/localization before applying marker layout.")]
    [SerializeField] private float m_waitForAnchorReadySeconds = 12f;
    [SerializeField] private string m_outputSubfolder = "MarkerSpatial";
    [SerializeField] private string m_markerLayoutFileName = "marker_layout.json";

    [Header("Debug")]
    [SerializeField] private SharedLogger m_logger;
    [SerializeField] private bool m_enableLogging = true;

    private void Start()
    {
        if (m_autoLoadOnStart)
            StartCoroutine(LoadAnchorAndMarkersCoroutine());
    }

    /// <summary>
    /// Optional hook for external systems/events (e.g., building block "anchor loaded" callback).
    /// Call this after your spatial anchor is ready to force marker restore.
    /// </summary>
    public void OnExternalAnchorReadyLoadMarkers()
    {
        StartCoroutine(LoadAnchorAndMarkersCoroutine());
    }

    /// <summary>
    /// Building-block event hook:
    /// wire this to "On Anchors Load Completed (List`1)".
    /// </summary>
    public void OnExternalAnchorsLoadCompleted(List<OVRSpatialAnchor> anchors)
    {
        if (anchors != null && anchors.Count > 0 && anchors[0] != null)
        {
            m_spatialAnchor = anchors[0];
            m_spatialAnchorRoot = m_spatialAnchor.transform;
            Log($"[MarkerSpatialAnchorPersistence] External load event: using anchor uuid={m_spatialAnchor.Uuid}.");
        }
        else
        {
            Log("[MarkerSpatialAnchorPersistence] External load event: no anchors in callback; will use existing references.");
        }

        StartCoroutine(LoadAnchorAndMarkersCoroutine());
    }

    /// <summary>
    /// Building-block event hook:
    /// wire this to "On Anchor Create Completed (OVRSpatialAnchor, OperationResult)".
    /// </summary>
    public void OnExternalAnchorCreateCompleted(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        if (anchor != null)
        {
            m_spatialAnchor = anchor;
            m_spatialAnchorRoot = anchor.transform;
            Log($"[MarkerSpatialAnchorPersistence] External create event: result={result}, anchor uuid={anchor.Uuid}.");
        }
        else
        {
            Log($"[MarkerSpatialAnchorPersistence] External create event: result={result}, anchor is null.");
        }

        StartCoroutine(LoadAnchorAndMarkersCoroutine());
    }

    public void SaveAnchorAndMarkers()
    {
        StartCoroutine(SaveAnchorAndMarkersCoroutine());
    }

    public void LoadAnchorAndMarkers()
    {
        StartCoroutine(LoadAnchorAndMarkersCoroutine());
    }

    private IEnumerator SaveAnchorAndMarkersCoroutine()
    {
        ResolveReferences();
        yield return WaitForAnchorReadyCoroutine("save");
        if (!EnsureMarkerParentUnderAnchorRoot())
        {
            LogError("[MarkerSpatialAnchorPersistence] Save aborted: missing marker parent or anchor root.");
            yield break;
        }

        Log($"[MarkerSpatialAnchorPersistence] External anchor mode: using anchor uuid={(m_spatialAnchor != null ? m_spatialAnchor.Uuid.ToString() : "null")}.");

        SaveMarkerLayoutToDisk();
    }

    private IEnumerator LoadAnchorAndMarkersCoroutine()
    {
        ResolveReferences();
        yield return WaitForAnchorReadyCoroutine("load");
        if (!EnsureMarkerParentUnderAnchorRoot())
        {
            LogError("[MarkerSpatialAnchorPersistence] Load aborted: missing marker parent or anchor root.");
            yield break;
        }

        Log($"[MarkerSpatialAnchorPersistence] External anchor mode: current anchor uuid={(m_spatialAnchor != null ? m_spatialAnchor.Uuid.ToString() : "null")} tracked={(m_spatialAnchor != null && m_spatialAnchor.IsTracked)}.");

        LoadMarkerLayoutFromDisk();
    }

    private void ResolveReferences()
    {
        if (m_spawner == null)
            m_spawner = FindFirstObjectByType<PinchTargetSpawner>();

        if (m_markerParent == null && m_spawner != null)
            m_markerParent = m_spawner.GetMarkerParentTransform();

        if (m_spatialAnchorRoot == null)
            m_spatialAnchorRoot = transform;

        if (m_spatialAnchor == null && m_spatialAnchorRoot != null)
            m_spatialAnchor = m_spatialAnchorRoot.GetComponent<OVRSpatialAnchor>();

        Log($"[MarkerSpatialAnchorPersistence] ResolveReferences: root={(m_spatialAnchorRoot != null ? m_spatialAnchorRoot.name : "null")}, anchor={(m_spatialAnchor != null ? m_spatialAnchor.Uuid.ToString() : "null")} mode=external.");
    }

    private IEnumerator WaitForAnchorReadyCoroutine(string phase)
    {
        float start = Time.time;
        while (Time.time - start < Mathf.Max(0.1f, m_waitForAnchorReadySeconds))
        {
            ResolveReferences();

            // If no anchor component is present, we still proceed using the root transform (best effort).
            if (m_spatialAnchor == null)
            {
                if (m_spatialAnchorRoot != null)
                {
                    Log($"[MarkerSpatialAnchorPersistence] {phase}: no OVRSpatialAnchor on root, proceeding with root transform only.");
                    yield break;
                }
            }
            else
            {
                // External mode: prefer tracked/localized but don't block forever.
                if (m_spatialAnchor.IsTracked || m_spatialAnchor.Localized)
                {
                    Log($"[MarkerSpatialAnchorPersistence] {phase}: anchor ready. Uuid={m_spatialAnchor.Uuid} Tracked={m_spatialAnchor.IsTracked} Localized={m_spatialAnchor.Localized}");
                    yield break;
                }
            }

            yield return null;
        }

        // Timed out; continue anyway to avoid deadlock.
        LogError($"[MarkerSpatialAnchorPersistence] {phase}: wait-for-anchor timeout, proceeding best-effort.");
    }

    private bool EnsureMarkerParentUnderAnchorRoot()
    {
        if (m_markerParent == null || m_spatialAnchorRoot == null)
            return false;

        if (m_markerParent.parent != m_spatialAnchorRoot)
            m_markerParent.SetParent(m_spatialAnchorRoot, true);

        return true;
    }

    private string GetLayoutFilePath()
    {
        string baseDir = Application.persistentDataPath;
        string folder = string.IsNullOrWhiteSpace(m_outputSubfolder) ? baseDir : Path.Combine(baseDir, m_outputSubfolder);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, m_markerLayoutFileName);
    }

    private void SaveMarkerLayoutToDisk()
    {
        if (m_markerParent == null)
        {
            LogError("[MarkerSpatialAnchorPersistence] Save markers failed: marker parent is null.");
            return;
        }

        var layout = new MarkerLayout
        {
            markerParentLocalPosition = m_markerParent.localPosition,
            markerParentLocalRotation = m_markerParent.localRotation,
            markerParentLocalScale = m_markerParent.localScale
        };
        for (int i = 0; i < m_markerParent.childCount; i++)
        {
            var child = m_markerParent.GetChild(i);
            if (child == null)
                continue;

            layout.markers.Add(new MarkerRecord
            {
                siblingIndex = i,
                name = child.name,
                activeSelf = child.gameObject.activeSelf,
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale,
                text = GetMarkerText(child)
            });
        }

        string path = GetLayoutFilePath();
        string json = JsonUtility.ToJson(layout, true);
        File.WriteAllText(path, json);
        string anchorUuid = m_spatialAnchor != null ? m_spatialAnchor.Uuid.ToString() : "null";
        Log($"[MarkerSpatialAnchorPersistence] Marker layout saved: {path} (count={layout.markers.Count}) anchorUuid={anchorUuid}");
    }

    private void LoadMarkerLayoutFromDisk()
    {
        if (m_markerParent == null)
        {
            LogError("[MarkerSpatialAnchorPersistence] Load markers failed: marker parent is null.");
            return;
        }

        string path = GetLayoutFilePath();
        if (!File.Exists(path))
        {
            Log($"[MarkerSpatialAnchorPersistence] No marker layout file found: {path}");
            return;
        }

        string json = File.ReadAllText(path);
        var layout = JsonUtility.FromJson<MarkerLayout>(json);
        if (layout == null || layout.markers == null)
        {
            LogError("[MarkerSpatialAnchorPersistence] Marker layout JSON is empty/invalid.");
            return;
        }

        // Restore MarkerParent local transform relative to spatial anchor root first.
        m_markerParent.localPosition = layout.markerParentLocalPosition;
        m_markerParent.localRotation = layout.markerParentLocalRotation;
        m_markerParent.localScale = layout.markerParentLocalScale;

        for (int i = 0; i < layout.markers.Count; i++)
        {
            var record = layout.markers[i];
            if (record.siblingIndex < 0 || record.siblingIndex >= m_markerParent.childCount)
                continue;

            var child = m_markerParent.GetChild(record.siblingIndex);
            if (child == null)
                continue;

            child.localPosition = record.localPosition;
            child.localRotation = record.localRotation;
            child.localScale = record.localScale;
            child.gameObject.SetActive(record.activeSelf);
            SetMarkerText(child, record.text);
        }

        // After restoring markers from anchor layout, stop pinch-spawner from moving them.
        if (m_spawner != null)
            m_spawner.SetTargetPlacementLocked(true);

        string anchorUuid = m_spatialAnchor != null ? m_spatialAnchor.Uuid.ToString() : "null";
        Log($"[MarkerSpatialAnchorPersistence] Marker layout loaded: {path} (count={layout.markers.Count}) anchorUuid={anchorUuid}");
    }

    private void Log(string msg)
    {
        if (!m_enableLogging)
            return;
        if (m_logger != null)
            m_logger.Log(msg);
        else
            Debug.Log(msg);
    }

    private void LogError(string msg)
    {
        if (!m_enableLogging)
            return;
        if (m_logger != null)
            m_logger.LogError(msg);
        else
            Debug.LogError(msg);
    }

    [Serializable]
    private class MarkerLayout
    {
        public Vector3 markerParentLocalPosition;
        public Quaternion markerParentLocalRotation;
        public Vector3 markerParentLocalScale;
        public List<MarkerRecord> markers = new();
    }

    [Serializable]
    private class MarkerRecord
    {
        public int siblingIndex;
        public string name;
        public bool activeSelf;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public string text;
    }

    private static string GetMarkerText(Transform marker)
    {
        if (marker == null)
            return null;

        var tmp = marker.GetComponentInChildren<TMP_Text>(true);
        return tmp != null ? tmp.text : null;
    }

    private static void SetMarkerText(Transform marker, string text)
    {
        if (marker == null)
            return;

        var tmp = marker.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null && text != null)
            tmp.text = text;
    }
}

