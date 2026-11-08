using UnityEngine;

/// <summary>Fixed isometric camera that smoothly follows a target from high above.</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("Downward tilt in degrees.")]
    public float pitch = 52f;
    [Tooltip("Horizontal angle in degrees (45 = isometric).")]
    public float yaw = 45f;
    public float distance = 24f;
    public float smoothTime = 0.25f;

    Vector3 velocity;

    void Start() { SnapToTarget(); }

    void LateUpdate()
    {
        if (!target) return;
        transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>Jump straight to the follow position (used at start).</summary>
    public void SnapToTarget()
    {
        if (!target) return;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = DesiredPosition();
        velocity = Vector3.zero;
    }

    Vector3 DesiredPosition()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        return target.position - rot * Vector3.forward * distance;
    }
}
