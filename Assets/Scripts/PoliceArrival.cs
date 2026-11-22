using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rescue scene that plays when the search time runs out: the main entrance (the east exit door) slides open,
/// a police car with flashing lights waits outside and three officers walk in. The camera follows them, the clown
/// runs away, and then the player wins. Everything is built at runtime from primitives: no scene setup needed.
/// Created by the GameManager (see GameManager.StartPoliceArrival).
/// </summary>
public class PoliceArrival : MonoBehaviour
{
    [Tooltip("Walking speed of the officers (units per second).")]
    public float walkSpeed = 3.6f;
    [Tooltip("How far outside the door the officers start.")]
    public float startOutside = 9f;
    [Tooltip("How far inside the room the leading officer stops.")]
    public float walkInside = 7f;
    [Tooltip("Seconds the camera stays on the officers after they stopped.")]
    public float holdSeconds = 2f;

    public bool CanPlay { get; private set; }

    ExitDoor door;       // may be null
    Transform doorT;     // may be null
    Vector3 doorPos;
    Vector3 inward;      // from the door into the building (flat, unit)
    Vector3 side;        // along the wall
    readonly List<Officer> officers = new List<Officer>();
    readonly List<Light> sirenLights = new List<Light>();
    readonly List<Renderer> barRed = new List<Renderer>();
    readonly List<Renderer> barBlue = new List<Renderer>();
    AudioSource sirenSource;
    Transform root;

    class Officer
    {
        public Transform root, legL, legR, armL, armR;
        public float phase;
    }

    // ------------------------------------------------------------------ setup

    public static PoliceArrival Create()
    {
        var go = new GameObject("PoliceArrival");
        var pa = go.AddComponent<PoliceArrival>();
        pa.Setup();
        return pa;
    }

    void Setup()
    {
        // The main entrance: the original east exit door of the house (named "ExitDoor").
        // Looked up by name, also when it is switched off; if it is missing the plan position is used.
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "ExitDoor") { doorT = t; break; }
        }
        door = doorT != null ? doorT.GetComponent<ExitDoor>() : null;
        doorPos = doorT != null ? doorT.position : new Vector3(30f, 0f, -13f);
        doorPos.y = 0f;
        Debug.Log(doorT != null ? $"[Police] Entrance = ExitDoor at {doorPos}" : "[Police] No ExitDoor found, using the east wall (30, -13).");

        // The door sits in an outer wall: "inward" points from the wall to the middle of the level.
        if (Mathf.Abs(doorPos.x) > Mathf.Abs(doorPos.z)) inward = new Vector3(-Mathf.Sign(doorPos.x), 0f, 0f);
        else inward = new Vector3(0f, 0f, -Mathf.Sign(doorPos.z == 0f ? 1f : doorPos.z));
        side = Vector3.Cross(Vector3.up, inward).normalized;

        root = transform;
        BuildPoliceCar();
        BuildOfficers();
        BuildSiren();
        CanPlay = true;
    }

    // ------------------------------------------------------------------ materials / primitives

    static Material Mat(Color c, bool unlit = false)
    {
        Shader s = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find(unlit ? "Unlit/Color" : "Standard");
        var m = new Material(s);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (!unlit && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        return m;
    }

    static Transform Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(g.GetComponent<Collider>()); // decoration only
        return g.transform;
    }

    static Light AddLight(Transform parent, Vector3 localPos, Color c, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // ------------------------------------------------------------------ police car

    void BuildPoliceCar()
    {
        Transform car = new GameObject("PoliceCar").transform;
        car.SetParent(root, false);
        car.position = doorPos - inward * (startOutside + 5.5f) + side * 3.2f;
        car.rotation = Quaternion.LookRotation(side); // parked along the wall

        Material white = Mat(new Color(0.88f, 0.9f, 0.95f));
        Material blueM = Mat(new Color(0.08f, 0.14f, 0.38f));
        Material glass = Mat(new Color(0.1f, 0.13f, 0.18f));
        Material black = Mat(new Color(0.04f, 0.04f, 0.05f));

        Prim(PrimitiveType.Cube, "Body", car, new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.8f, 4.2f), white);
        Prim(PrimitiveType.Cube, "Stripe", car, new Vector3(0f, 0.6f, 0f), new Vector3(1.82f, 0.28f, 4.22f), blueM);
        Prim(PrimitiveType.Cube, "Cabin", car, new Vector3(0f, 1.25f, -0.2f), new Vector3(1.6f, 0.6f, 2.1f), white);
        Prim(PrimitiveType.Cube, "Windows", car, new Vector3(0f, 1.27f, -0.2f), new Vector3(1.62f, 0.4f, 2.12f), glass);
        foreach (Vector2 w in new[] { new Vector2(-0.95f, 1.3f), new Vector2(0.95f, 1.3f), new Vector2(-0.95f, -1.3f), new Vector2(0.95f, -1.3f) })
        {
            Transform wheel = Prim(PrimitiveType.Cylinder, "Wheel", car, new Vector3(w.x, 0.32f, w.y), new Vector3(0.64f, 0.12f, 0.64f), black);
            wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // Light bar on the roof: one red half, one blue half, flashing alternately.
        Material redEm = Mat(new Color(1f, 0.1f, 0.1f), true);
        Material blueEm = Mat(new Color(0.15f, 0.3f, 1f), true);
        barRed.Add(Prim(PrimitiveType.Cube, "BarRed", car, new Vector3(-0.4f, 1.65f, -0.2f), new Vector3(0.7f, 0.16f, 0.35f), redEm).GetComponent<Renderer>());
        barBlue.Add(Prim(PrimitiveType.Cube, "BarBlue", car, new Vector3(0.4f, 1.65f, -0.2f), new Vector3(0.7f, 0.16f, 0.35f), blueEm).GetComponent<Renderer>());
        sirenLights.Add(AddLight(car, new Vector3(-0.4f, 2.0f, -0.2f), new Color(1f, 0.12f, 0.1f), 0f, 16f));
        sirenLights.Add(AddLight(car, new Vector3(0.4f, 2.0f, -0.2f), new Color(0.15f, 0.3f, 1f), 0f, 16f));
    }

    // ------------------------------------------------------------------ officers

    void BuildOfficers()
    {
        // lead officer in front, two behind him on both sides
        Vector3[] offsets =
        {
            Vector3.zero,
            side * 1.3f - inward * 1.1f,
            -side * 1.3f - inward * 1.1f,
        };
        Vector3 start = doorPos - inward * startOutside;
        for (int i = 0; i < offsets.Length; i++)
            officers.Add(BuildOfficer(start + offsets[i], i == 0));
    }

    Officer BuildOfficer(Vector3 pos, bool lead)
    {
        var o = new Officer();
        o.root = new GameObject(lead ? "Officer_Lead" : "Officer").transform;
        o.root.SetParent(root, false);
        o.root.position = pos;
        o.root.rotation = Quaternion.LookRotation(inward);

        Material uniform = Mat(new Color(0.1f, 0.17f, 0.4f));
        Material pants = Mat(new Color(0.07f, 0.1f, 0.24f));
        Material skin = Mat(new Color(0.85f, 0.68f, 0.55f));
        Material hat = Mat(new Color(0.05f, 0.08f, 0.2f));
        Material gold = Mat(new Color(0.95f, 0.8f, 0.25f), true);
        Material shoe = Mat(new Color(0.04f, 0.04f, 0.05f));
        Material vest = Mat(new Color(0.9f, 0.9f, 0.3f), true);

        // legs (pivot at the hip)
        o.legL = MakeLimb(o.root, "LegL", new Vector3(-0.17f, 0.9f, 0f), new Vector3(0.24f, 0.9f, 0.26f), 0.45f, pants, shoe);
        o.legR = MakeLimb(o.root, "LegR", new Vector3(0.17f, 0.9f, 0f), new Vector3(0.24f, 0.9f, 0.26f), 0.45f, pants, shoe);
        // torso + reflective stripe + badge
        Prim(PrimitiveType.Cube, "Torso", o.root, new Vector3(0f, 1.3f, 0f), new Vector3(0.74f, 0.82f, 0.38f), uniform);
        Prim(PrimitiveType.Cube, "Stripe", o.root, new Vector3(0f, 1.1f, 0f), new Vector3(0.76f, 0.07f, 0.4f), vest);
        Prim(PrimitiveType.Cube, "Badge", o.root, new Vector3(-0.2f, 1.5f, 0.2f), new Vector3(0.12f, 0.14f, 0.02f), gold);
        // arms (pivot at the shoulder)
        o.armL = MakeLimb(o.root, "ArmL", new Vector3(-0.47f, 1.65f, 0f), new Vector3(0.17f, 0.72f, 0.2f), 0.36f, uniform, skin);
        o.armR = MakeLimb(o.root, "ArmR", new Vector3(0.47f, 1.65f, 0f), new Vector3(0.17f, 0.72f, 0.2f), 0.36f, uniform, skin);
        // head + police cap
        Prim(PrimitiveType.Sphere, "Head", o.root, new Vector3(0f, 1.93f, 0f), Vector3.one * 0.4f, skin);
        Prim(PrimitiveType.Cylinder, "CapBrim", o.root, new Vector3(0f, 2.08f, 0.02f), new Vector3(0.46f, 0.025f, 0.46f), hat);
        Prim(PrimitiveType.Cylinder, "CapTop", o.root, new Vector3(0f, 2.16f, 0f), new Vector3(0.36f, 0.07f, 0.36f), hat);
        Prim(PrimitiveType.Cube, "CapVisor", o.root, new Vector3(0f, 2.08f, 0.24f), new Vector3(0.3f, 0.02f, 0.16f), hat);
        Prim(PrimitiveType.Cube, "CapBadge", o.root, new Vector3(0f, 2.15f, 0.19f), new Vector3(0.08f, 0.07f, 0.02f), gold);

        // a small flashlight glow so they stand out in the dark yard
        AddLight(o.root, new Vector3(0f, 1.8f, 0.5f), new Color(1f, 0.97f, 0.85f), 1.6f, 6f);
        return o;
    }

    static Transform MakeLimb(Transform parent, string name, Vector3 pivotPos, Vector3 size, float centerDrop, Material main, Material tip)
    {
        var pivot = new GameObject(name + "Pivot").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = pivotPos;
        Prim(PrimitiveType.Cube, name, pivot, new Vector3(0f, -centerDrop, 0f), size, main);
        // hand / shoe at the bottom end
        Prim(PrimitiveType.Cube, name + "Tip", pivot, new Vector3(0f, -size.y + 0.04f, name.StartsWith("Leg") ? 0.05f : 0f),
            new Vector3(size.x * 1.05f, 0.1f, name.StartsWith("Leg") ? size.z * 1.4f : size.z), tip);
        return pivot;
    }

    // ------------------------------------------------------------------ siren (lights + sound)

    void BuildSiren()
    {
        // two-tone wail, generated in code (no audio file needed); 1.6 s, loops seamlessly
        const int rate = 22050;
        int samples = (int)(rate * 1.6f);
        float[] data = new float[samples];
        double ph = 0;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / samples;
            float tri = t < 0.5f ? t * 2f : 2f - t * 2f;          // 0..1..0
            float freq = Mathf.Lerp(650f, 1150f, tri);
            ph += 2.0 * System.Math.PI * freq / rate;
            data[i] = (float)System.Math.Sin(ph) * 0.35f;
        }
        var clip = AudioClip.Create("PoliceSiren", samples, 1, rate, false);
        clip.SetData(data, 0);

        sirenSource = gameObject.AddComponent<AudioSource>();
        sirenSource.clip = clip;
        sirenSource.loop = true;
        sirenSource.spatialBlend = 0f;
        sirenSource.volume = 0.35f;
    }

    void Update()
    {
        // flashing red / blue (works while the scene plays; stops mattering when the game ends)
        float t = Time.unscaledTime * 6f;
        bool redOn = Mathf.Sin(t) > 0f;
        bool on = sirenSource != null && sirenSource.isPlaying;
        for (int i = 0; i < sirenLights.Count; i++)
        {
            bool isRed = i % 2 == 0;
            sirenLights[i].intensity = on && (isRed == redOn) ? 18f : 0f;
        }
        foreach (Renderer r in barRed) if (r) r.enabled = !on || redOn;
        foreach (Renderer r in barBlue) if (r) r.enabled = !on || !redOn;
    }

    // ------------------------------------------------------------------ the scene

    /// <summary>Plays the whole scene. The GameManager waits for it, then wins the game.</summary>
    public IEnumerator Run()
    {
        GameManager gm = GameManager.Instance;
        CameraFollow cam = CameraFollow.Instance;
        Officer lead = officers[0];

        // 1) the clown runs away, siren starts, camera glides to the entrance
        if (KillerAI.Instance != null) KillerAI.Instance.FleeFromPolice();
        if (sirenSource != null) sirenSource.Play();
        SearchableObject.ShowToast("POLICE! Someone called them - you are saved!", new Color(0.45f, 0.7f, 1f), 5f);
        float total = 1.6f + (startOutside + walkInside) / walkSpeed + holdSeconds + 1f;
        if (cam != null) cam.FocusOn(lead.root, total);
        yield return Wait(1.4f);

        // 2) the main entrance opens (the door slides up, if there is one)
        if (doorT != null && doorT.gameObject.activeInHierarchy)
        {
            Vector3 from = doorT.position;
            Vector3 to = from + Vector3.up * 2.6f;
            if (door != null && door.doorLight != null) door.doorLight.color = door.openColor;
            for (float e = 0f; e < 0.6f; e += Time.deltaTime)
            {
                doorT.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / 0.6f));
                yield return null;
            }
            doorT.position = to;
        }
        else yield return Wait(0.6f);

        // 3) the officers walk in
        Vector3 leadTarget = doorPos + inward * walkInside;
        float walked = 0f;
        float need = Vector3.Distance(lead.root.position, leadTarget);
        while (walked < need)
        {
            float step = walkSpeed * Time.deltaTime;
            walked += step;
            foreach (Officer o in officers)
            {
                o.root.position += inward * step;
                Swing(o, step, true);
            }
            if (cam != null && walked > 0.1f) cam.FocusOn(lead.root, 1.5f + holdSeconds); // keep the shot on them
            yield return null;
        }
        foreach (Officer o in officers) Swing(o, 0f, false);

        // 4) they look around for you, then the game ends with the rescue
        float look = 0f;
        while (look < holdSeconds)
        {
            look += Time.deltaTime;
            float turn = Mathf.Sin(look * 2.4f) * 35f;
            foreach (Officer o in officers) o.root.rotation = Quaternion.LookRotation(inward) * Quaternion.Euler(0f, turn, 0f);
            yield return null;
        }

        if (sirenSource != null) sirenSource.Stop();
    }

    void Swing(Officer o, float dist, bool moving)
    {
        if (moving) o.phase += dist * 2.3f;
        float s = moving ? Mathf.Sin(o.phase) : 0f;
        o.legL.localRotation = Quaternion.Euler(s * 35f, 0f, 0f);
        o.legR.localRotation = Quaternion.Euler(-s * 35f, 0f, 0f);
        o.armL.localRotation = Quaternion.Euler(-s * 28f, 0f, 0f);
        o.armR.localRotation = Quaternion.Euler(s * 28f, 0f, 0f);
    }

    static IEnumerator Wait(float seconds)
    {
        for (float e = 0f; e < seconds; e += Time.deltaTime) yield return null;
    }
}
