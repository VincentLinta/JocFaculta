using UnityEngine;

/// <summary>
/// Simple procedural animation for the human player model:
/// walking bob + arm/leg swing, a forward lunge while dashing, and slow idle breathing.
/// Uses scaled time, so it freezes in the pause menu.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    public Transform model;
    public Transform torso;
    public Transform legL;
    public Transform legR;
    public Transform armL;
    public Transform armR;

    public float swingAngle = 32f;
    public float strideSpeed = 9f;
    public float bobHeight = 0.06f;
    [Tooltip("Forward lean in degrees during a dash.")]
    public float dashLean = 16f;

    PlayerController player;
    Vector3 lastPosition;
    Vector3 modelBase;
    Vector3 torsoScale;
    float phase;
    float moveBlend;
    float lean;

    void Start()
    {
        player = GetComponentInParent<PlayerController>();
        lastPosition = transform.position;
        if (model) modelBase = model.localPosition;
        if (torso) torsoScale = torso.localScale;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || model == null) return;

        // Speed from how far the player moved this frame (the controller animates tile steps).
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        bool moving = delta.magnitude / dt > 0.5f && player != null && !player.IsHidden;
        bool dashing = moving && player.IsDashMove;

        moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, dt * 6f);
        if (moving) phase += dt * strideSpeed * (dashing ? 1.4f : 1f);

        float swing = Mathf.Sin(phase) * swingAngle * (dashing ? 1.3f : 1f) * moveBlend;
        if (legL) legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        if (legR) legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
        if (armL) armL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 0f);
        if (armR) armR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, 0f);

        // Dash lunge: lean forward and push the body ahead a little.
        lean = Mathf.MoveTowards(lean, dashing ? dashLean : 0f, dt * 120f);
        float lunge = lean / Mathf.Max(1f, dashLean) * 0.12f;

        // Walking bob, or slow breathing when standing still.
        float bob = Mathf.Abs(Mathf.Sin(phase)) * bobHeight * moveBlend;
        float breathe = Mathf.Sin(Time.time * 1.6f) * (1f - moveBlend);
        model.localPosition = modelBase + new Vector3(0f, bob + breathe * 0.008f, lunge);
        model.localRotation = Quaternion.Euler(lean, 0f, 0f);
        if (torso)
            torso.localScale = new Vector3(torsoScale.x * (1f + breathe * 0.012f), torsoScale.y * (1f + breathe * 0.015f), torsoScale.z * (1f + breathe * 0.025f));
    }
}
