using UnityEngine;

/// <summary>
/// Procedural walk animation for the clown killer: legs and arms swing from pivots,
/// the body bounces on every step and leans forward a bit while chasing.
/// Put it on the "Visual" child. Pivots are created at runtime, so existing scenes work without a rebuild.
/// The swing is synced with KillerAI.stepStride so it matches the footstep sounds.
/// </summary>
public class KillerWalkAnimator : MonoBehaviour
{
    public float legSwing = 38f;
    public float armSwing = 28f;
    [Tooltip("Extra forward lean (degrees) while chasing.")]
    public float chaseLean = 10f;
    public float stepBounce = 0.07f;

    KillerAI ai;
    KillerBob bob;
    Transform rig; // the "Visual" object that holds the clown parts
    Transform legL, legR, armL;
    Vector3 lastPos;
    float phase, blend, lean;

    void Start()
    {
        ai = GetComponentInParent<KillerAI>();
        // Works even if the script was put on the Killer root instead of "Visual".
        rig = transform;
        if (name != "Visual")
        {
            Transform v = transform.Find("Visual");
            if (v) rig = v;
        }
        bob = rig.GetComponent<KillerBob>();
        lastPos = transform.position;

        legL = MakePivot("LegLPivot", new Vector3(-0.2f, 1.02f, 0f), "LegL", "ShoeL");
        legR = MakePivot("LegRPivot", new Vector3(0.2f, 1.02f, 0f), "LegR", "ShoeR");
        armL = MakePivot("ArmLPivot", new Vector3(-0.6f, 2.0f, 0.02f), "ArmL", "GloveL");
    }

    Transform MakePivot(string name, Vector3 localPos, params string[] childNames)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(rig, false);
        pivot.localPosition = localPos;
        foreach (string n in childNames)
        {
            Transform c = rig.Find(n);
            if (c) c.SetParent(pivot, true);
        }
        return pivot;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 d = transform.position - lastPos;
        d.y = 0f;
        lastPos = transform.position;
        float dist = d.magnitude;
        float speed = dist / dt;
        // Ignore teleports (spawn) and standing still.
        bool moving = speed > 0.5f && speed < 40f;

        blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, dt * 7f);
        float stride = ai != null ? Mathf.Max(0.5f, ai.stepStride) : 2.6f;
        if (moving) phase += dist * Mathf.PI / stride; // one footstep per half cycle

        bool chasing = ai != null && ai.CurrentState == KillerAI.State.Chase;
        float s = Mathf.Sin(phase) * blend;
        float mul = chasing ? 1.2f : 1f;

        if (legL) legL.localRotation = Quaternion.Euler(s * legSwing * mul, 0f, 0f);
        if (legR) legR.localRotation = Quaternion.Euler(-s * legSwing * mul, 0f, 0f);
        if (armL) armL.localRotation = Quaternion.Euler(-s * armSwing * mul, 0f, 0f);
        if (ai != null) ai.WalkArmSwing = s * armSwing * mul; // right arm is driven by KillerAI

        lean = Mathf.MoveTowards(lean, chasing ? chaseLean * blend : 0f, dt * 60f);
        if (bob != null)
        {
            bob.walkBounce = Mathf.Abs(Mathf.Sin(phase)) * stepBounce * blend;
            bob.walkLean = lean;
        }
    }
}
