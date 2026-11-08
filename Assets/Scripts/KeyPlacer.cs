using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Places the 3 keys on random fair tiles at every game start / Restart (seeded from the clock).
/// Runs after the killer's random spawn (KillerAI.Start, order 0) and before KeyPickup.Start (order 100),
/// so each key still claims its own tile exactly as before.
/// Sits on the "Keys" root object; the keys are its children.
/// </summary>
[DefaultExecutionOrder(50)]
public class KeyPlacer : MonoBehaviour
{
    [Tooltip("Off = keys go back to their original fixed positions (below).")]
    public bool randomizeKeys = true;
    [Tooltip("Minimum path distance (tiles) from the player's spawn.")]
    public int minDistanceFromPlayerSpawn = 6;
    [Tooltip("Minimum path distance (tiles) from the killer's spawn.")]
    public int minDistanceFromKillerSpawn = 8;
    [Tooltip("Minimum path distance (tiles) between any two keys.")]
    public int minDistanceBetweenKeys = 10;
    [Tooltip("Put every key in a different room (room ids from the GridManager).")]
    public bool requireDifferentRooms = true;
    [Tooltip("Original fixed key positions (world), in child order. Filled once; used when randomizeKeys is off.")]
    public List<Vector3> originalPositions = new List<Vector3>();

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    void Start()
    {
        GridManager grid = GridManager.Instance;
        var keys = new List<Transform>();
        foreach (Transform t in transform) if (t.GetComponent<KeyPickup>() != null) keys.Add(t);
        if (grid == null || keys.Count == 0) return;

        if (originalPositions.Count < keys.Count)
        {
            originalPositions.Clear();
            foreach (Transform k in keys) originalPositions.Add(k.position);
        }

        if (!randomizeKeys)
        {
            for (int i = 0; i < keys.Count; i++) keys[i].position = originalPositions[i];
            return;
        }

        Vector2Int playerSpawn = PlayerSpawnTile(grid);
        Vector2Int killerSpawn = KillerAI.Instance ? KillerAI.Instance.Tile : playerSpawn;
        var rng = new System.Random(unchecked((int)System.DateTime.Now.Ticks));
        List<Vector2Int> tiles = ChooseTiles(grid, rng, keys.Count, playerSpawn, killerSpawn, out string note);

        var fromPlayer = Distances(grid, playerSpawn);
        var parts = new List<string>();
        var rooms = new List<string>();
        var dists = new List<string>();
        for (int i = 0; i < keys.Count && i < tiles.Count; i++)
        {
            keys[i].position = grid.TileCenter(tiles[i]) + Vector3.up; // same height as before (1 unit)
            parts.Add($"({tiles[i].x},{tiles[i].y})");
            rooms.Add(grid.RoomOf(tiles[i]).ToString());
            dists.Add(fromPlayer.TryGetValue(tiles[i], out int d) ? d.ToString() : "?");
        }
        Debug.Log($"[Keys] placed at tiles {string.Join(", ", parts)}, rooms {string.Join(", ", rooms)}, path distances from player spawn {string.Join(", ", dists)}{note}");
    }

    public static Vector2Int PlayerSpawnTile(GridManager grid)
    {
        PlayerController p = PlayerController.Instance;
        return grid.NearestWalkable(grid.WorldToTile(p != null ? p.transform.position : Vector3.zero));
    }

    /// <summary>Path distance (tiles) from a tile to every reachable tile.</summary>
    public static Dictionary<Vector2Int, int> Distances(GridManager grid, Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int> { { from, 0 } };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int n = c + d;
                if (dist.ContainsKey(n) || !grid.CanStep(c, n)) continue;
                dist[n] = dist[c] + 1;
                q.Enqueue(n);
            }
        }
        return dist;
    }

    /// <summary>Tiles a key may never use: hiding-spot tiles, exit tiles, both spawns.</summary>
    public static HashSet<Vector2Int> ForbiddenTiles(GridManager grid, Vector2Int playerSpawn, Vector2Int killerSpawn)
    {
        var bad = new HashSet<Vector2Int> { playerSpawn, killerSpawn };
        foreach (HidingSpot s in HidingSpot.All) bad.Add(s.Tile);
        foreach (ExitDoor e in ExitDoor.All)
            for (int x = 0; x < grid.width; x++)
                for (int y = 0; y < grid.height; y++)
                {
                    var t = new Vector2Int(x, y);
                    Vector3 off = grid.TileCenter(t) - e.transform.position;
                    off.y = 0f;
                    if (off.magnitude < grid.tileSize * 1.5f) bad.Add(t);
                }
        return bad;
    }

    /// <summary>
    /// Picks count tiles that follow the fairness rules. If impossible, relaxes them one at a time
    /// (different rooms first, then spacing between keys, then distance from the spawns) and logs a warning.
    /// </summary>
    public List<Vector2Int> ChooseTiles(GridManager grid, System.Random rng, int count, Vector2Int playerSpawn, Vector2Int killerSpawn, out string note)
    {
        note = "";
        var fromPlayer = Distances(grid, playerSpawn);
        var fromKiller = Distances(grid, killerSpawn);
        HashSet<Vector2Int> forbidden = ForbiddenTiles(grid, playerSpawn, killerSpawn);
        var navPath = new NavMeshPath();

        // Reachable from the player's spawn on the grid AND on the NavMesh, never forbidden.
        var basePool = new List<Vector2Int>();
        foreach (Vector2Int t in fromPlayer.Keys)
        {
            if (forbidden.Contains(t) || !grid.IsWalkable(t)) continue;
            if (!NavMesh.CalculatePath(grid.TileCenter(playerSpawn), grid.TileCenter(t), NavMesh.AllAreas, navPath)
                || navPath.status != NavMeshPathStatus.PathComplete) continue;
            basePool.Add(t);
        }

        bool rooms = requireDifferentRooms;
        int spacing = minDistanceBetweenKeys;
        int fromP = minDistanceFromPlayerSpawn;
        int fromK = minDistanceFromKillerSpawn;
        var distCache = new Dictionary<Vector2Int, Dictionary<Vector2Int, int>>();

        for (int stage = 0; stage < 12; stage++)
        {
            var pool = basePool.FindAll(t => fromPlayer[t] >= fromP && (!fromKiller.TryGetValue(t, out int dk) || dk >= fromK));
            for (int attempt = 0; attempt < 60 && pool.Count >= count; attempt++)
            {
                Shuffle(pool, rng);
                var picked = new List<Vector2Int>();
                foreach (Vector2Int t in pool)
                {
                    if (!Fits(grid, t, picked, rooms, spacing, distCache)) continue;
                    picked.Add(t);
                    if (picked.Count == count) break;
                }
                if (picked.Count == count)
                {
                    if (stage > 0)
                    {
                        note = $" (relaxed rules: differentRooms={rooms}, spacing={spacing}, fromPlayer={fromP}, fromKiller={fromK})";
                        Debug.LogWarning("[Keys] Fairness rules could not all be met;" + note);
                    }
                    return picked;
                }
            }
            // Relax one rule at a time: rooms first, then spacing, then the spawn distances.
            if (rooms) rooms = false;
            else if (spacing > 0) spacing /= 2;
            else if (fromP > 0 || fromK > 0) { fromP /= 2; fromK /= 2; }
            else break;
        }
        Debug.LogWarning("[Keys] Could not place keys randomly; using any free reachable tiles.");
        note = " (fallback)";
        Shuffle(basePool, rng);
        return basePool.GetRange(0, Mathf.Min(count, basePool.Count));
    }

    static bool Fits(GridManager grid, Vector2Int t, List<Vector2Int> picked, bool rooms, int spacing, Dictionary<Vector2Int, Dictionary<Vector2Int, int>> cache)
    {
        foreach (Vector2Int p in picked)
        {
            if (rooms && grid.RoomOf(p) == grid.RoomOf(t)) return false;
            if (spacing > 0)
            {
                if (!cache.TryGetValue(p, out var dp)) cache[p] = dp = Distances(grid, p);
                if (!dp.TryGetValue(t, out int d) || d < spacing) return false;
            }
        }
        return true;
    }

    static void Shuffle(List<Vector2Int> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
