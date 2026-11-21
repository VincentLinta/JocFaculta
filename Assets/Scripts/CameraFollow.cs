using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Isometric camera that follows the target (the player), and switches to the killer
/// while he is hunting (chase phase), so you can watch him search.
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

    [Header("Killer focus")]
    [Tooltip("While the killer is hunting, the camera centers on him instead of the player.")]
    public bool followKillerWhenHunting = true;
    [Tooltip("Smooth time of the glide when the camera switches between player and killer.")]
    public float switchSmoothTime = 0.7f;

    [Header("Rotation (right mouse button + drag)")]
    [Tooltip("Degrees per pixel of mouse movement.")]
    public float rotationSpeed = 0.2f;
    [Tooltip("Max degrees left/right from the start angle.")]
    public float maxYawOffset = 50f;
    [Tooltip("Max degrees the camera can go DOWN (flatter view) from the start angle.")]
    public float maxPitchDown = 20f;
    [Tooltip("Max degrees the camera can go UP (more top-down) from the start angle.")]
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
    Transform currentFocus;
    float switchUntil;

    public static CameraFollow Instance { get; private set; }
    Transform overrideFocus;
    float overrideUntil;

    void Awake() { Instance = this; }

    /// <summary>Moves the camera to look at something (e.g. the exit that just opened) for a few seconds.</summary>
    public void FocusOn(Transform t, float seconds)
    {
        overrideFocus = t;
        overrideUntil = Time.unscaledTime + seconds;
    }

    /// <summary>The player, or the killer while he is hunting.</summary>
    Transform GetFocus()
    {
        if (overrideFocus != null && Time.unscaledTime < overrideUntil) return overrideFocus;
        // While the player runs for the exit, watch the player, not the clown.
        if (GameManager.Instance != null && GameManager.Instance.EscapeActive) return target;
        if (followKillerWhenHunting)
        {
            KillerAI killer = KillerAI.Instance;
            if (killer != null && killer.IsHunting) return killer.transform;
        }
        return target;
    }

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
                yaw = Mathf.Clamp(yaw, baseYaw - maxYawOffset, baseYaw + maxYawOffset);
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
        Transform focus = GetFocus();
        if (focus != currentFocus)
        {
            currentFocus = focus;
            switchUntil = Time.unscaledTime + switchSmoothTime * 1.5f; // slower glide while switching
        }
        bool overriding = overrideFocus != null && Time.unscaledTime < overrideUntil;
        float smooth = overriding ? 0.3f : (Time.unscaledTime < switchUntil ? switchSmoothTime : smoothTime);
        followPoint = Vector3.SmoothDamp(followPoint, focus.position, ref followVelocity, smooth);
        ApplyTransform();
    }

    /// <summary>Jump straight to the follow position (used at start and after a restart).</summary>
    public void SnapToTarget()
    {
        if (!target) return;
        currentFocus = GetFocus();
        followPoint = currentFocus.position;
        followVelocity = Vector3.zero;
        switchUntil = 0f;
        ApplyTransform();
    }

    void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rot;
        transform.position = followPoint - rot * Vector3.forward * distance;
    }
}
