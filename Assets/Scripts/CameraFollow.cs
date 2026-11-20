using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Isometric camera that follows the target.
/// Right mouse button + drag: orbit around the player (limited).
/// Mouse wheel: zoom in / out (limited).
/// Uses only the new Input System (the legacy UnityEngine.Input class is disabled in this project).
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Tooltip("Downward tilt in degrees (start value).")]
    public float pitch = 52f;
    [Tooltip("Horizontal angle in degrees (start value, 45 = isometric).")]
    public float yaw = 45f;
    public float distance = 24f;
    public float smoothTime = 0.25f;

    [Header("Rotation (right mouse button + drag)")]
    [Tooltip("Degrees per pixel of mouse movement.")]
    public float rotationSpeed = 0.2f;
    [Tooltip("Max degrees left/right from the start angle.")]
    public float maxYaw = 50f;
    [Tooltip("Max degrees the camera can go DOWN (lower, more horizontal view) from the start angle.")]
    public float maxPitchDown = 20f;
    [Tooltip("Max degrees the camera can go UP (higher, more top-down view) from the start angle.")]
    public float maxPitchUp = 15f;

    [Header("Zoom (mouse wheel)")]
    [Tooltip("Distance change per wheel notch.")]
    public float zoomStep = 2.5f;
    public float minDistance = 12f;
    public float maxDistance = 34f;
    [Tooltip("How fast the zoom eases to its target (higher = snappier).")]
    public float zoomSmoothing = 12f;

    float baseYaw;
    float basePitch;
    float targetDistance;
    Vector3 followPoint;     // smoothed position of the target
    Vector3 followVelocity;

    void Start()
    {
        baseYaw = yaw;
        basePitch = pitch;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        targetDistance = distance;
        SnapToTarget();
    }

    void LateUpdate()
    {
        if (!target) return;

        // Time.timeScale is 0 while paused and on the YOU DIED / YOU ESCAPED screens:
        // the camera must not move then (the wheel would also move the volume slider).
        bool inputAllowed = Time.timeScale > 0f;
        Mouse mouse = Mouse.current;

        if (inputAllowed && mouse != null)
        {
            // Orbit with the right mouse button, limited around the start angle.
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * rotationSpeed;
                pitch -= delta.y * rotationSpeed;
                yaw = Mathf.Clamp(yaw, baseYaw - maxYaw, baseYaw + maxYaw);
                pitch = Mathf.Clamp(pitch, basePitch - maxPitchDown, basePitch + maxPitchUp);
            }

            // Zoom with the wheel; only the sign is used, so every mouse/OS behaves the same.
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        // The zoom eases even if the game gets paused mid-move (unscaled time).
        distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime));

        // Only the follow point is smoothed; the orbit offset is applied directly,
        // so rotating and zooming feel instant and the player stays centered.
        followPoint = Vector3.SmoothDamp(followPoint, target.position, ref followVelocity, smoothTime);
        ApplyTransform();
    }

    /// <summary>Jump straight to the follow position (used at start and after a restart).</summary>
    public void SnapToTarget()
    {
        if (!target) return;
        followPoint = target.position;
        followVelocity = Vector3.zero;
        ApplyTransform();
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = followPoint - rot * Vector3.forward * distance;
    }
}
