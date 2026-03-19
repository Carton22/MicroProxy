using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Tracks the left hand index pinch. Each time the user starts an index pinch with the left hand,
/// assigns an intersection marker (reused from pre-created children first, otherwise instantiated)
/// at the pinch position in world space and adds it as a runtime target.
/// These targets can be used by CanvasRayIntersectionVisualizer to draw hit points on the passthrough canvas.
/// </summary>
public class PinchTargetSpawner : MonoBehaviour
{
    [Header("Left hand")]
    [Tooltip("Left hand OVRHand (from hand tracking rig). Used for pinch state and pinch position.")]
    [SerializeField] private OVRHand m_leftHand;
    [Tooltip("Optional OVRSkeleton for the same hand. Used to resolve the thumb and index tip bones.")]
    [SerializeField] private OVRSkeleton m_leftHandSkeleton;

    [Header("Spawn")]
    [Tooltip("Prefab to instantiate at each pinch position (world space). Assign any prefab with a Transform.")]
    [SerializeField] private GameObject m_targetPrefab;
    [Tooltip("Optional parent for spawned objects. If null, spawned under this transform.")]
    [SerializeField] private Transform m_spawnParent;
    [Tooltip("If enabled, uses the midpoint between thumb tip and index tip. If disabled, uses index tip only.")]
    [SerializeField] private bool m_useThumbIndexMidpoint = true;

    [Header("Debug logging")]
    [Tooltip("Optional shared logger used to log the index of each created marker.")]
    [SerializeField] private SharedLogger m_logger;
    [Tooltip("If false, newly created markers will not be logged even if a logger is assigned.")]
    [SerializeField] private bool m_enableLogging = true;

    [Header("Pinch thresholds")]
    [SerializeField] [Range(0f, 1f)] private float m_pinchStartStrength = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float m_pinchReleaseStrength = 0.35f;

    // Pool of marker transforms under the spawn parent (child objects containing a TextMeshPro).
    private readonly List<Transform> m_markerPool = new();

    // Tracks only the marker transforms currently used for this session (by pinch order).
    private readonly List<Transform> m_runtimeTargets = new();

    // Markers instantiated at runtime (so ClearRuntimeTargets can destroy them, while leaving pre-created pool children intact).
    private readonly List<GameObject> m_instantiatedRuntimeMarkers = new();

    private bool m_wasPinching;
    private Transform m_thumbTip;
    private Transform m_middleTip;

    private Transform GetMarkerParent()
    {
        return m_spawnParent != null ? m_spawnParent : transform;
    }

    private void Awake()
    {
        CacheMarkerPoolFromChildren();
    }

    private void CacheMarkerPoolFromChildren()
    {
        m_markerPool.Clear();

        var parent = GetMarkerParent();
        if (parent == null)
            return;

        // Reuse only direct children that look like markers (i.e., they contain a TextMeshPro somewhere).
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == null)
                continue;

            var tmp = child.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
                m_markerPool.Add(child);
        }
    }

    private void Update()
    {
        if (m_leftHand == null || m_targetPrefab == null)
            return;

        if (m_middleTip == null || (m_useThumbIndexMidpoint && m_thumbTip == null))
            TryResolveTipsFromSkeleton();

        if (!m_leftHand.IsDataValid)
        {
            m_wasPinching = false;
            return;
        }

        float pinch = m_leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
        bool isPinching = pinch >= m_pinchStartStrength;

        if (isPinching && !m_wasPinching)
        {
            Vector3 worldPos = GetPinchWorldPosition();
            if (float.IsNaN(worldPos.x))
            {
                // Skeleton tips not ready yet; don't spawn an incorrect marker.
                m_wasPinching = true;
                return;
            }

            int markerIndex = m_runtimeTargets.Count;

            // Reuse pre-created markers first. If we don't have enough, instantiate new ones.
            Transform instance = null;
            var markerParent = GetMarkerParent();
            if (markerIndex < m_markerPool.Count)
            {
                instance = m_markerPool[markerIndex];
            }

            if (instance == null)
            {
                var go = Instantiate(m_targetPrefab, markerParent);
                instance = go.transform;
                m_instantiatedRuntimeMarkers.Add(go);

                // Add to pool so future pinches can reuse it.
                while (m_markerPool.Count <= markerIndex)
                    m_markerPool.Add(null);
                m_markerPool[markerIndex] = instance;

                if (m_enableLogging && m_logger != null)
                {
                    int siblingIndex = instance.GetSiblingIndex();
                    m_logger.Log($"[PinchTargetSpawner] Created marker for markerIndex={markerIndex}, sibling index under parent: {siblingIndex}");
                }
            }

            // Ensure marker stays active and moves to the pinch position.
            instance.gameObject.SetActive(true);
            instance.SetPositionAndRotation(worldPos, Quaternion.identity);

            // Set TMP label to its 0-based marker index.
            var tmp = instance.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
                tmp.text = markerIndex.ToString();

            m_runtimeTargets.Add(instance);
        }

        if (!isPinching && pinch < m_pinchReleaseStrength)
            m_wasPinching = false;
        else if (isPinching)
            m_wasPinching = true;
    }

    /// <summary>
    /// Returns the list of runtime-created targets (one per left-hand index pinch). Used by CanvasRayIntersectionVisualizer.
    /// </summary>
    public IReadOnlyList<Transform> GetRuntimeTargets() => m_runtimeTargets;

    /// <summary>
    /// Number of runtime targets (pinch spawns). Used to size the visualizer's instances.
    /// </summary>
    public int GetRuntimeTargetCount() => m_runtimeTargets.Count;

    /// <summary>
    /// Remove all runtime targets and destroy their GameObjects. Call to clear pinch markers.
    /// </summary>
    public void ClearRuntimeTargets()
    {
        // Disable/release used pre-created pool markers.
        foreach (var t in m_runtimeTargets)
        {
            if (t == null || t.gameObject == null)
                continue;

            bool isRuntimeInstantiated = m_instantiatedRuntimeMarkers.Contains(t.gameObject);
            if (isRuntimeInstantiated)
            {
                Destroy(t.gameObject);
            }
            else
            {
                t.gameObject.SetActive(false);
            }
        }

        m_runtimeTargets.Clear();

        // Destroyed/cleared runtime-instantiated markers; reset tracking list.
        m_instantiatedRuntimeMarkers.Clear();
    }

    private Vector3 GetPinchWorldPosition()
    {
        // Prefer skeleton tip positions. Pointer-ray fallback is intentionally removed to avoid incorrect marker placement.
        if (m_middleTip != null && (!m_useThumbIndexMidpoint || m_thumbTip == null))
            return m_middleTip.position;

        if (m_thumbTip != null && m_middleTip != null)
            return (m_thumbTip.position + m_middleTip.position) * 0.5f;

        // If skeleton tips aren't available yet, skip spawning by returning NaN (checked by caller).
        return new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private void TryResolveTipsFromSkeleton()
    {
        if (m_leftHandSkeleton == null)
            return;

        var bones = m_leftHandSkeleton.Bones;
        if (bones == null || bones.Count == 0)
            return;

        foreach (var bone in bones)
        {
            if (bone == null || bone.Transform == null)
                continue;

            if (m_thumbTip == null && bone.Id == OVRSkeleton.BoneId.Hand_ThumbTip)
                m_thumbTip = bone.Transform;

            if (m_middleTip == null && bone.Id == OVRSkeleton.BoneId.Hand_MiddleTip)
                m_middleTip = bone.Transform;

            if (m_thumbTip != null && m_middleTip != null)
                break;
        }
    }

}
