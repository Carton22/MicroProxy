using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Detects a controller button press and writes a JSON file
/// capturing transform information for a target GameObject and all its children.
/// </summary>
public class RightHandDoubleTapSaveHierarchyJson : MonoBehaviour
{
    [Header("Controller input")]
    [Tooltip("Controller button used to trigger save. Default = Y button.")]
    [SerializeField] private OVRInput.RawButton m_saveButton = OVRInput.RawButton.Y;

    [Header("Save target")]
    [Tooltip("Root GameObject to snapshot (includes all children).")]
    [SerializeField] private GameObject m_root;

    [Tooltip("If true, include inactive children in the snapshot.")]
    [SerializeField] private bool m_includeInactive = true;

    [Tooltip("Optional subfolder under Application.persistentDataPath.")]
    [SerializeField] private string m_outputSubfolder = "Snapshots";

    [Tooltip("If true, writes pretty-printed JSON.")]
    [SerializeField] private bool m_prettyPrint = true;

    [Header("Optional marker spatial persistence")]
    [Tooltip("Optional. If assigned, this script also saves anchor + marker layout on double tap.")]
    [SerializeField] private MarkerSpatialAnchorPersistence m_markerSpatialPersistence;
    [Tooltip("If true and m_markerSpatialPersistence is assigned, trigger SaveAnchorAndMarkers() after JSON save.")]
    [SerializeField] private bool m_saveSpatialMarkersOnDoubleTap = true;

    [Header("Debug")]
    [Tooltip("Optional shared logger used to log save results.")]
    [SerializeField] private SharedLogger m_logger;

    [Tooltip("If false, snapshot save results will not be logged even if a logger is assigned.")]
    [SerializeField] private bool m_enableLogging = true;

    private void Update()
    {
        if (m_root == null)
            return;

        if (OVRInput.GetDown(m_saveButton))
            SaveSnapshot();
    }

    private void SaveSnapshot()
    {
        try
        {
            var snapshot = HierarchySnapshot.Create(m_root.transform, m_includeInactive);

            string safeRootName = MakeFileNameSafe(m_root.name);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string fileName = $"{safeRootName}-{timestamp}.json";

            string baseDir = Application.persistentDataPath;
            string outDir = string.IsNullOrWhiteSpace(m_outputSubfolder)
                ? baseDir
                : Path.Combine(baseDir, m_outputSubfolder);

            Directory.CreateDirectory(outDir);
            string fullPath = Path.Combine(outDir, fileName);

            string json = JsonUtility.ToJson(snapshot, m_prettyPrint);
            File.WriteAllText(fullPath, json);

            if (m_saveSpatialMarkersOnDoubleTap && m_markerSpatialPersistence != null)
                m_markerSpatialPersistence.SaveAnchorAndMarkers();

            if (m_enableLogging && m_logger != null)
                m_logger.Log($"[RightHandDoubleTapSaveHierarchyJson] Saved snapshot: {fullPath}");
        }
        catch (Exception ex)
        {
            if (m_enableLogging && m_logger != null)
                m_logger.LogError($"[RightHandDoubleTapSaveHierarchyJson] Failed to save snapshot: {ex}");
            Debug.LogError($"[RightHandDoubleTapSaveHierarchyJson] Failed to save snapshot: {ex}");
        }
    }

    private static string MakeFileNameSafe(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Root";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    [Serializable]
    private class HierarchySnapshot
    {
        public string rootName;
        public string rootPath;
        public List<TransformSnapshot> transforms = new();

        public static HierarchySnapshot Create(Transform root, bool includeInactive)
        {
            var s = new HierarchySnapshot
            {
                rootName = root != null ? root.name : null,
                rootPath = root != null ? GetPath(root, root) : null,
            };

            if (root == null)
                return s;

            AddRecursive(s.transforms, root, root, includeInactive);
            return s;
        }

        private static void AddRecursive(List<TransformSnapshot> list, Transform root, Transform t, bool includeInactive)
        {
            if (t == null)
                return;

            if (!includeInactive && !t.gameObject.activeInHierarchy)
                return;

            list.Add(TransformSnapshot.FromTransform(root, t));

            for (int i = 0; i < t.childCount; i++)
                AddRecursive(list, root, t.GetChild(i), includeInactive);
        }

        public static string GetPath(Transform root, Transform t)
        {
            if (t == null)
                return string.Empty;
            if (t == root)
                return t.name;

            var parts = new List<string>();
            Transform cur = t;
            while (cur != null)
            {
                parts.Add(cur.name);
                if (cur == root)
                    break;
                cur = cur.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }

    [Serializable]
    private class TransformSnapshot
    {
        public string name;
        public string path;
        public bool activeSelf;
        public bool activeInHierarchy;

        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public Vector3 worldPosition;
        public Quaternion worldRotation;

        public static TransformSnapshot FromTransform(Transform root, Transform t)
        {
            return new TransformSnapshot
            {
                name = t.name,
                path = HierarchySnapshot.GetPath(root, t),
                activeSelf = t.gameObject.activeSelf,
                activeInHierarchy = t.gameObject.activeInHierarchy,
                localPosition = t.localPosition,
                localRotation = t.localRotation,
                localScale = t.localScale,
                worldPosition = t.position,
                worldRotation = t.rotation
            };
        }
    }
}

