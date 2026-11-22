using UnityEngine;

/// <summary>
/// Small changes to the finished level, applied at runtime before the walking grid is scanned
/// (called from GridManager.Awake right after PropCleaner):
///  1) takes out loose clutter that can not be used (crates, barrels, boxes, chairs ...) so rooms are easy to cross,
///  2) turns the car room into a garage: a big garage gate on the north wall, the car parked facing it.
/// Hiding-spot props are never touched. To remove or keep other props edit <see cref="ClutterExact"/> / <see cref="ClutterPrefix"/>.
/// </summary>
public static class LevelTweaks
{
    /// <summary>Props whose name is exactly one of these are removed.</summary>
    static readonly string[] ClutterExact =
    {
        "CardboardBoxes", "Box", "Box_Open", "BrokenChair", "TrashPile", "Tire", "Pallet", "Plank",
        "Bookcase_Fallen", "Wardrobe_Fallen", "FileCabinet_Fallen", "Dresser_Tipped", "Bench_Fallen", "SmallTable", "Mattress",
    };

    /// <summary>Props whose name starts with one of these are removed.</summary>
    static readonly string[] ClutterPrefix = { "Crate", "Barrel", "OilBarrel" };

    /// <summary>Center of the garage gate on the floor (zero until the gate exists).</summary>
    public static Vector3 GateCenter { get; private set; }
    public static bool HasGate { get; private set; }

    /// <summary>True when a point is in front of the garage gate (keeps cabinets and windows off it).</summary>
    public static bool NearGate(Vector3 p, float radius = 3.6f)
    {
        if (!HasGate) return false;
        Vector3 d = p - GateCenter;
        d.y = 0f;
        return d.magnitude < radius;
    }

    public static void Apply()
    {
        HasGate = false;
        RemoveClutter();
        RemoveGarageProps();
        BuildGarage();
        OpenBedroomDoorway();
        RemoveVents();
    }

    // ------------------------------------------------------------------ 4) vents

    /// <summary>All vent hiding spots are taken out (they never register, so nobody can hide there or check them).</summary>
    static void RemoveVents()
    {
        int removed = 0;
        foreach (HidingSpot hs in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (hs.spotType != HidingSpot.SpotType.Vent) continue;
            hs.gameObject.SetActive(false);
            removed++;
        }
        Debug.Log($"[LevelTweaks] Removed {removed} vent hiding spots.");
    }

    // ------------------------------------------------------------------ 3) bedroom doorway

    // Doorway cut in the north wall (z = 20) where the curtain used to be: one tile wide (x 10..12),
    // it joins the Bedroom with Bedroom 2 in the north wing.
    const float DoorwayCenterX = 11f;
    const float DoorwayHalf = 1f;
    const float WallLineZ = 20f;

    static void OpenBedroomDoorway()
    {
        // 1) the curtain hiding spot at (10, 19) goes away
        int curtains = 0;
        foreach (HidingSpot hs in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (hs.spotType != HidingSpot.SpotType.Curtain) continue;
            Vector3 p = hs.transform.position;
            if (Mathf.Abs(p.x - 10f) > 1.5f || Mathf.Abs(p.z - 19f) > 1.5f) continue;
            hs.gameObject.SetActive(false);
            curtains++;
        }

        // 2) the wall piece (and its baseboard) in front of it is cut open
        float x0 = DoorwayCenterX - DoorwayHalf, x1 = DoorwayCenterX + DoorwayHalf;
        int cut = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            bool wall = t.name.StartsWith("Wall_North");
            bool baseboard = t.name == "Baseboard" && t.parent != null && t.parent.name == "Trim";
            if (!wall && !baseboard) continue;
            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;
            Bounds b = r.bounds;
            if (b.size.x < b.size.z) continue;                       // only pieces running along X
            if (Mathf.Abs(b.center.z - WallLineZ) > 0.3f) continue;  // only on the z = 20 line
            if (b.min.x >= x0 - 0.01f || b.max.x <= x1 + 0.01f) continue; // must span the whole doorway
            CutX(t, b, x0, x1);
            cut++;
        }
        Debug.Log($"[LevelTweaks] Bedroom doorway at x={DoorwayCenterX}: removed {curtains} curtain, cut {cut} wall piece(s).");
    }

    /// <summary>Replaces one wall piece by two, leaving the gap x0..x1 open.</summary>
    static void CutX(Transform t, Bounds b, float x0, float x1)
    {
        float total = b.size.x;
        MakePiece(t, b, b.min.x, x0, total);
        MakePiece(t, b, x1, b.max.x, total);
        t.gameObject.SetActive(false);
    }

    static void MakePiece(Transform src, Bounds b, float from, float to, float total)
    {
        if (to - from < 0.05f) return;
        Transform n = Object.Instantiate(src.gameObject, src.parent).transform;
        n.name = src.name;
        n.gameObject.SetActive(true);
        n.position = new Vector3((from + to) / 2f, src.position.y, src.position.z);
        Vector3 sc = src.localScale;
        sc.x *= (to - from) / total;
        n.localScale = sc;
    }

    // ------------------------------------------------------------------ 1) clutter

    static bool IsClutter(string n)
    {
        foreach (string s in ClutterExact) if (n == s) return true;
        foreach (string s in ClutterPrefix) if (n.StartsWith(s)) return true;
        return false;
    }

    static void RemoveClutter()
    {
        int removed = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsClutter(t.name)) continue;
            if (t.GetComponentInParent<HidingSpot>() != null) continue;   // hiding spots stay
            if (t.GetComponentInParent<SearchableObject>() != null) continue;
            t.gameObject.SetActive(false);
            removed++;
        }
        Debug.Log($"[LevelTweaks] Removed {removed} loose props.");
    }

    // ------------------------------------------------------------------ 1b) garage crate

    // The garage room on the floor plan (x -30..-12, z 20..30).
    const float GarageX0 = -30f, GarageX1 = -12f, GarageZ0 = 20f, GarageZ1 = 30f;

    /// <summary>
    /// Removes only the big crate stack in the garage (a "behind the crate" hiding spot): the player could hide
    /// there on the way to the car and it got in the way. The spot is switched off, like the vents.
    /// Everything else in the garage (workbench, car, locker ...) stays as it is.
    /// </summary>
    static void RemoveGarageProps()
    {
        int spots = 0;
        foreach (HidingSpot hs in Object.FindObjectsByType<HidingSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (hs.spotType != HidingSpot.SpotType.BehindCrate) continue;
            Vector3 p = hs.transform.position;
            if (p.x < GarageX0 || p.x > GarageX1 || p.z < GarageZ0 || p.z > GarageZ1) continue;
            hs.gameObject.SetActive(false);
            spots++;
        }
        Debug.Log($"[LevelTweaks] Garage: removed {spots} crate hiding spot(s).");
    }

    // ------------------------------------------------------------------ 2) garage

    static Material Mat(Color c, bool unlit)
    {
        Shader s = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find(unlit ? "Unlit/Color" : "Standard");
        var m = new Material(s);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (!unlit && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
        return m;
    }

    static void Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.SetParent(parent, false);
        b.transform.position = pos;
        b.transform.localScale = scale;
        b.GetComponent<Renderer>().sharedMaterial = mat;
        Object.Destroy(b.GetComponent<Collider>()); // decoration only, never blocks the grid
    }

    static void BuildGarage()
    {
        GameObject carGO = GameObject.Find("OldCar");
        if (carGO == null) { Debug.Log("[LevelTweaks] No OldCar found, garage skipped."); return; }
        Transform car = carGO.transform;

        // The hood lies flat on the car (it used to stand open, tilted up).
        foreach (Transform t in car.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Hood_Open" && t.name != "Hood") continue;
            t.localRotation = Quaternion.identity;
            t.localPosition = new Vector3(1.55f, 1.03f, 0f);
            t.localScale = new Vector3(0.7f, 0.05f, 1.6f);
        }

        // The wall the gate goes on: the north wall of the wing (inner face).
        float faceZ = 29.85f;
        GameObject wall = GameObject.Find("Wall_WingNorth");
        if (wall != null)
        {
            Renderer[] rs = wall.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                faceZ = b.center.z - b.extents.z; // south face = inside the wing
            }
        }

        // Gate straight in front of the car, the car's front (the open hood, local +X) points at it.
        float gx = car.position.x;
        car.rotation = Quaternion.Euler(0f, -90f, 0f);

        Material metal = Mat(new Color(0.46f, 0.48f, 0.5f), false);
        Material dark = Mat(new Color(0.13f, 0.13f, 0.15f), false);
        Material stripe = Mat(new Color(0.9f, 0.72f, 0.1f), false);
        Material daylight = Mat(new Color(1f, 0.96f, 0.8f), true);

        var root = new GameObject("GarageGate").transform;
        float z = faceZ - 0.07f; // just in front of the wall
        const float width = 3.8f, height = 2.3f;

        // Frame.
        Box("PostL", root, new Vector3(gx - width / 2f - 0.12f, height / 2f, z), new Vector3(0.24f, height, 0.2f), dark);
        Box("PostR", root, new Vector3(gx + width / 2f + 0.12f, height / 2f, z), new Vector3(0.24f, height, 0.2f), dark);
        Box("Header", root, new Vector3(gx, height + 0.1f, z), new Vector3(width + 0.5f, 0.22f, 0.22f), dark);

        // Roll-up door: stacked slats with thin dark gaps between them.
        const int slats = 5;
        float slatH = (height - 0.12f) / slats;
        for (int i = 0; i < slats; i++)
            Box("Slat" + i, root, new Vector3(gx, 0.12f + slatH * (i + 0.5f), z + 0.02f), new Vector3(width, slatH - 0.04f, 0.1f), metal);
        // Daylight behind the slats: only switched on while the car drives out (CarEscape).
        Box("Outside", root, new Vector3(gx, 0.12f + (height - 0.12f) / 2f, z + 0.06f), new Vector3(width, height - 0.12f, 0.02f), daylight);
        root.Find("Outside").gameObject.SetActive(false);
        Box("Handle", root, new Vector3(gx, 0.45f, z - 0.06f), new Vector3(0.5f, 0.07f, 0.06f), dark);

        // Hazard stripe along the bottom and daylight leaking under the door.
        Box("Stripe", root, new Vector3(gx, 0.1f, z - 0.01f), new Vector3(width, 0.08f, 0.12f), stripe);
        Box("DaylightGap", root, new Vector3(gx, 0.03f, z + 0.03f), new Vector3(width, 0.06f, 0.06f), daylight);

        GateCenter = new Vector3(gx, 0f, z);
        HasGate = true;
        Debug.Log($"[LevelTweaks] Garage gate at x={gx:0.0}, z={z:0.00}; car turned to face it.");
    }
}
