using UnityEngine;

/// <summary>
/// Takes props out of rooms before the walking grid is scanned, so a room can be crossed without a detour.
/// Called from GridManager.Awake. To clear another room add its area to <see cref="BarrelFreeAreas"/>
/// as (xMin, xMax, zMin, zMax) in world units.
/// </summary>
public static class PropCleaner
{
    struct Area
    {
        public string namePrefix; // props whose name starts with this are removed
        public float xMin, xMax, zMin, zMax;
        public Area(string prefix, float x0, float x1, float z0, float z1)
        {
            namePrefix = prefix;
            xMin = x0; xMax = x1; zMin = z0; zMax = z1;
        }
    }

    /// <summary>Props removed so a room can be crossed without a detour. Hiding-spot props are never touched.</summary>
    static readonly Area[] Areas =
    {
        new Area("Barrel", 10f, 30f, -20f, 3f),        // loading area: barrels
        new Area("OilBarrel", 10f, 30f, -20f, 3f),
        new Area("Crate", 26.5f, 28.1f, -8.2f, -6.2f), // loading area: crate stack by the locker, east wall
    };

    public static void Apply()
    {
        int removed = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.GetComponentInParent<HidingSpot>() != null) continue;
            Vector3 p = t.position;
            foreach (Area a in Areas)
            {
                if (!t.name.StartsWith(a.namePrefix)) continue;
                if (p.x < a.xMin || p.x > a.xMax || p.z < a.zMin || p.z > a.zMax) continue;
                t.gameObject.SetActive(false);
                removed++;
                Debug.Log($"[PropCleaner] Removed {t.name} at ({p.x:0.0}, {p.z:0.0})");
                break;
            }
        }
        if (removed == 0) Debug.Log("[PropCleaner] Nothing to remove.");
    }
}
