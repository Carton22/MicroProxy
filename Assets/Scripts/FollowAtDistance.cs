using UnityEngine;

[DefaultExecutionOrder(1000)]
public class FollowAtDistance : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Usually the player's camera (HMD).")]
    public Transform target;

    [Tooltip("Optional: when set, this transform's rotation is used so the follower stays parallel to that plane (e.g. camera-to-world canvas).")]
    public Transform rotationSource;

    [Header("Placement")]
    [Min(0.01f)]
    [Tooltip("How far in front of the target the object should sit.")]
    public float distance = 1.2f;

    [Tooltip("If true, the object inherits the target rotation. If false, it will face the same direction using LookRotation with world up.")]
    public bool inheritRotation = true;

    [Tooltip("If true, only yaw is inherited so the UI stays upright even if the head tilts.")]
    public bool yawOnly = true;

    [Header("Smoothing")]
    [Tooltip("Meters per second the object moves toward the desired position. Set very high to snap.")]
    public float positionFollowSpeed = 100f;

    [Tooltip("Degrees per second the object rotates toward the desired rotation. Set very high to snap.")]
    public float rotationFollowSpeed = 720f;

    [Header("Optional")]
    [Tooltip("If set, the UI will always face this up direction instead of the target's roll.")]
    public Vector3 worldUp = Vector3.up;

    private void OnEnable()
    {
        SnapNow();
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Desired position: directly in front of target at the chosen distance
        Vector3 desiredPos = target.position + target.forward * distance;

        // Desired rotation
        Quaternion desiredRot;
        if (rotationSource != null)
        {
            // Follow the exact rotation of the provided source (e.g. camera-to-world canvas)
            // so this canvas stays parallel to that plane.
            desiredRot = rotationSource.rotation;
        }
        else
        {
            if (inheritRotation)
            {
                if (yawOnly)
                {
                    // Strip pitch and roll so UI stays upright
                    Vector3 fwd = Vector3.ProjectOnPlane(target.forward, worldUp).normalized;
                    if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(target.up, worldUp).normalized;
                    desiredRot = Quaternion.LookRotation(fwd, worldUp);
                }
                else
                {
                    desiredRot = target.rotation;
                }
            }
            else
            {
                // Face the same direction as the target, using world up to keep upright
                desiredRot = Quaternion.LookRotation(target.forward, worldUp);
            }
        }

        // Smooth position
        float posStep = positionFollowSpeed * Time.unscaledDeltaTime;
        if (posStep <= 0f) posStep = Mathf.Infinity; // safety
        transform.position = Vector3.MoveTowards(transform.position, desiredPos, posStep);

        // Smooth rotation
        float rotStep = rotationFollowSpeed * Time.unscaledDeltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRot, rotStep);
    }

    /// <summary>
    /// Call this to instantly snap to the correct spot and rotation.
    /// </summary>
    public void SnapNow()
    {
        if (target == null) return;
        transform.position = target.position + target.forward * distance;

        if (rotationSource != null)
        {
            transform.rotation = rotationSource.rotation;
        }
        else if (inheritRotation)
        {
            if (yawOnly)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(target.forward, worldUp).normalized;
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(target.up, worldUp).normalized;
                transform.rotation = Quaternion.LookRotation(fwd, worldUp);
            }
            else
            {
                transform.rotation = target.rotation;
            }
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(target.forward, worldUp);
        }
    }
}
