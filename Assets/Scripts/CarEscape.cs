using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Escape by car. Two special items are hidden in the cabinets: the CAR KEY and the GARAGE REMOTE.
/// They are normal cards (they take the card slot): the player carries them to the old car in the garage and presses E to leave them there.
/// When both are in the car, the player escapes automatically: the garage gate rolls up, the car drives out and the game is won.
/// All state is static and cleared when a match starts (SearchableSpawner.Spawn).
/// </summary>
public static class CarEscape
{
    public const float ReachDistance = 2.2f; // how close to the car (world units) the player must stand to use E

    public static bool KeyDelivered { get; private set; }
    public static bool RemoteDelivered { get; private set; }
    public static bool Running { get; private set; }
    public static bool BothDelivered => KeyDelivered && RemoteDelivered;

    static Transform car;
    static Bounds carBounds;
    static bool boundsReady;

    public static void Reset()
    {
        KeyDelivered = RemoteDelivered = Running = false;
        outlineParts.Clear();
        outlineOn = false;
        car = null;
        boundsReady = false;
    }

    /// <summary>The car exists (the garage was built by LevelTweaks). Without it the two items are not spawned.</summary>
    public static bool Exists => LevelTweaks.HasGate && GetCar() != null;

    static Transform GetCar()
    {
        if (car == null)
        {
            GameObject go = GameObject.Find("OldCar");
            car = go != null ? go.transform : null;
            boundsReady = false;
        }
        if (car != null && !boundsReady)
        {
            Renderer[] rs = car.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                carBounds = rs[0].bounds;
                foreach (Renderer r in rs) carBounds.Encapsulate(r.bounds);
                boundsReady = true;
            }
        }
        return car;
    }

    /// <summary>True if the player stands next to the car.</summary>
    public static bool IsNear(PlayerController p)
    {
        if (p == null || Running || GetCar() == null || !boundsReady) return false;
        Vector3 pt = p.transform.position;
        pt.y = carBounds.center.y;
        return carBounds.SqrDistance(pt) <= ReachDistance * ReachDistance;
    }

    // ------------------------------------------------------------------ yellow highlight

    static readonly List<GameObject> outlineParts = new List<GameObject>();
    static Material outlineMat;
    static bool outlineOn;
    static readonly Color OutlineColor = new Color(1f, 0.92f, 0.2f);
    const float OutlineGrow = 0.07f; // world units

    /// <summary>The car glows yellow while the player stands next to it with the car key or the garage remote.</summary>
    public static void UpdateHighlight(PlayerController p)
    {
        bool on = !Running && CanDeliver(p);
        if (on)
        {
            BuildOutline();
            if (outlineMat != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                Color c = OutlineColor * Mathf.Lerp(0.55f, 1f, pulse);
                c.a = 1f;
                outlineMat.color = c;
                if (outlineMat.HasProperty("_BaseColor")) outlineMat.SetColor("_BaseColor", c);
            }
        }
        if (on == outlineOn) return;
        outlineOn = on;
        foreach (GameObject go in outlineParts) if (go) go.SetActive(on);
    }

    static void BuildOutline()
    {
        if (outlineParts.Count > 0 && outlineParts[0] != null) return;
        outlineParts.Clear();
        Transform c = GetCar();
        if (c == null) return;
        if (outlineMat == null)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            outlineMat = new Material(s) { name = "CarOutline (runtime)" };
            if (outlineMat.HasProperty("_Cull")) outlineMat.SetFloat("_Cull", 1f); // back faces only = inside-out shell
        }
        foreach (MeshFilter mf in c.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.GetComponent<MeshRenderer>() == null) continue;
            var go = new GameObject("CarOutline");
            go.transform.SetParent(mf.transform, false);
            Vector3 sc = mf.transform.lossyScale;
            go.transform.localScale = new Vector3(
                1f + OutlineGrow * 2f / Mathf.Max(0.01f, Mathf.Abs(sc.x)),
                1f + OutlineGrow * 2f / Mathf.Max(0.01f, Mathf.Abs(sc.y)),
                1f + OutlineGrow * 2f / Mathf.Max(0.01f, Mathf.Abs(sc.z)));
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            outlineParts.Add(go);
        }
        outlineOn = false;
    }

    /// <summary>E at the car while carrying the key and / or the remote.</summary>
    public static bool CanDeliver(PlayerController p)
    {
        return p != null && !p.IsHidden && (p.HasCarKey || p.HasGarageRemote) && IsNear(p);
    }

    public static void Deliver(PlayerController p)
    {
        string left = "";
        if (p.TakeItem(CardType.CarKey)) { KeyDelivered = true; left = "CAR KEY"; }
        else if (p.TakeItem(CardType.GarageRemote)) { RemoteDelivered = true; left = "GARAGE REMOTE"; }
        if (left.Length == 0) return;
        AudioManager.Play(AudioManager.Sfx.Key);

        if (BothDelivered)
        {
            SearchableObject.ShowToast("Car key and garage remote are in the car - get ready to drive out!", CardInfo.Tint(CardType.CarKey), 3f);
            p.StartCoroutine(Run(p));
        }
        else
        {
            string missing = KeyDelivered ? "the GARAGE REMOTE" : "the CAR KEY";
            SearchableObject.ShowToast($"You left the {left} in the car. Find {missing} too.", CardInfo.Tint(CardType.CarKey), 4f);
        }
    }

    /// <summary>The automatic getaway: gate opens, car drives to it, white flash, win.</summary>
    static IEnumerator Run(PlayerController p)
    {
        Running = true;
        Transform c = GetCar();
        GameManager gm = GameManager.Instance;
        if (c == null || gm == null) { Running = false; yield break; }

        // The player is in the car: invisible, glued to it, cannot be caught while it drives off.
        p.BeginCarEscape(c);
        if (KillerAI.Instance != null) KillerAI.Instance.Stun(60f);

        // --- gate: slats roll up into the header, daylight shows behind ---
        Transform gate = GameObject.Find("GarageGate") != null ? GameObject.Find("GarageGate").transform : null;
        Transform[] slats = new Transform[5];
        float[] y0 = new float[5], h0 = new float[5];
        float headerY = 2.3f;
        if (gate != null)
        {
            for (int i = 0; i < slats.Length; i++)
            {
                slats[i] = gate.Find("Slat" + i);
                if (slats[i] != null) { y0[i] = slats[i].position.y; h0[i] = slats[i].localScale.y; }
            }
            Transform outside = gate.Find("Outside");
            if (outside != null) outside.gameObject.SetActive(true);
            foreach (string n in new[] { "Handle", "Stripe", "DaylightGap" })
            {
                Transform t = gate.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }
        }
        AudioManager.Play(AudioManager.Sfx.Rustle);

        const float openTime = 1.6f;
        for (float t = 0f; t < openTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / openTime);
            for (int i = 0; i < slats.Length; i++)
            {
                if (slats[i] == null) continue;
                Vector3 pos = slats[i].position;
                pos.y = Mathf.Lerp(y0[i], headerY, k);
                slats[i].position = pos;
                Vector3 sc = slats[i].localScale;
                sc.y = Mathf.Lerp(h0[i], 0.02f, k);
                slats[i].localScale = sc;
            }
            yield return null;
        }
        foreach (Transform s in slats) if (s != null) s.gameObject.SetActive(false);

        // --- the car drives to the gate (engine revving up) ---
        float frontZ = boundsReady ? carBounds.max.z : c.position.z + 1.9f;
        float distance = Mathf.Max(0.5f, LevelTweaks.GateCenter.z - 0.35f - frontZ);
        Vector3 start = c.position;
        const float driveTime = 1.6f;
        bool flashed = false;
        AudioManager.PlayAt(AudioManager.Sfx.KillerHonk, c.position);
        for (float t = 0f; t < driveTime; t += Time.deltaTime)
        {
            float k = (t / driveTime);
            k = k * k; // accelerates
            c.position = start + Vector3.forward * (distance * k);
            if (!flashed && t > driveTime * 0.6f)
            {
                flashed = true;
                PlayerController.LastFlashTime = Time.time; // white flash, same as the flashbang
            }
            yield return null;
        }
        c.position = start + Vector3.forward * distance;
        yield return new WaitForSeconds(0.6f);
        gm.Win("You escaped in the car! The clown is left behind.");
        Running = false;
    }
}
