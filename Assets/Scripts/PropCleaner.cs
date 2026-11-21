using UnityEngine;

/// <summary>
/// Takes props out of rooms before the walking grid is scanned, so a room can be crossed without a detour.
/// Called from GridManager.Awake. To clear another room add its area to <see cref="BarrelFreeAreas"/>
/// as (xMin, xMax, zMin, zMax) in world units.
/// </summary>
public static class PropCleaner
{
    /// <summary>Barrels inside these areas are removed (hiding-spot barrels are never touched).</summary>
    static readonly Vector4[] BarrelFreeAreas =
    {
        new Vector4(10f, 30f, -20f, 3f), // loading area (east side)
    };

    public static void Apply()
    {
        int removed = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = t.name;
            if (!n.StartsWith("Barrel") && !n.StartsWith("OilBarrel")) continue;
            if (t.GetComponentInParent<HidingSpot>() != null) continue;

            Vector3 p = t.position;
            foreach (Vector4 a in BarrelFreeAreas)
            {
                if (p.x < a.x || p.x > a.y || p.z < a.z || p.z > a.w) continue;
                t.gameObject.SetActive(false);
                removed++;
                Debug.Log($"[PropCleaner] Removed {n} at ({p.x:0.0}, {p.z:0.0})");
                break;
            }
        }
        if (removed == 0) Debug.Log("[PropCleaner] No barrels to remove.");
    }
}
