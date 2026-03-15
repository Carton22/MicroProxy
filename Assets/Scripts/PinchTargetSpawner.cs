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

    [Header("Spawn")]
    [Tooltip("Prefab to instantiate at each pinch position (world space). Assign any prefab with a Transform.")]
    [SerializeField] private GameObject m_targetPrefab;
    [Tooltip("Optional parent for spawned objects. If null, spawned under this transform.")]
    [SerializeField] private Transform m_spawnParent;

    [Header("Pinch thresholds")]
    [SerializeField] [Range(0f, 1f)] private float m_pinchStartStrength = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float m_pinchReleaseStrength = 0.35f;

    private readonly List<Transform> m_runtimeTargets = new();
    private bool m_wasPinching;

    private void Update()
    {
        if (m_leftHand == null || m_targetPrefab == null)
            return;

        if (!m_leftHand.IsDataValid)
        {
            m_wasPinching = false;
            return;
        }

        float pinch = m_leftHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        bool isPinching = pinch >= m_pinchStartStrength;

        if (isPinching && !m_wasPinching)
        {
            // Pinch just started: spawn a target at current pinch/pointer position in world space
            var rayTransform = m_leftHand.GetPointerRayTransform();
            Vector3 worldPos = rayTransform.position;
            var parent = m_spawnParent != null ? m_spawnParent : transform;
            var instance = Instantiate(m_targetPrefab, worldPos, Quaternion.identity, parent);
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
}
