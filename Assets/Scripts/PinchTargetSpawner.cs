using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the left hand index pinch. Each time the user starts an index pinch with the left hand,
/// spawns an instance of the assigned prefab at the pinch position in world space and adds it as a
/// runtime target. These targets can be used by CanvasRayIntersectionVisualizer to draw hit points
/// on the passthrough canvas.
/// Assign the left hand's OVRHand (e.g. from the hand tracking rig) and a prefab to spawn.
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

    [Header("Pinch thresholds")]
    [SerializeField] [Range(0f, 1f)] private float m_pinchStartStrength = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float m_pinchReleaseStrength = 0.35f;

    private readonly List<Transform> m_runtimeTargets = new();
    private bool m_wasPinching;
    private Transform m_thumbTip;
    private Transform m_middleTip;

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

            var parent = m_spawnParent != null ? m_spawnParent : transform;
            var instance = Instantiate(m_targetPrefab, worldPos, Quaternion.identity, parent);
            TryStampMarkerLabel(instance, m_runtimeTargets.Count);
            m_runtimeTargets.Add(instance.transform);
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
        foreach (var t in m_runtimeTargets)
        {
            if (t != null && t.gameObject != null)
                Destroy(t.gameObject);
        }
        m_runtimeTargets.Clear();
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

    private static void TryStampMarkerLabel(GameObject instance, int labelIndex)
    {
        if (instance == null || labelIndex < 0)
            return;

        var binding = instance.GetComponent<MarkerLabelBinding>();
        if (binding == null)
            binding = instance.AddComponent<MarkerLabelBinding>();

        binding.LabelIndex = labelIndex;
    }
}
