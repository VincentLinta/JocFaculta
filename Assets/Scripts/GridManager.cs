using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Square tile grid over the building (1x1 tiles).
/// - Works out which tiles are walkable from the colliders in the scene.
/// - Works out which neighbour moves are open (walls sit on tile edges).
/// - Draws subtle outlines on walkable tiles and shows hover / reach / path highlights.
/// - Finds paths (4 directions) for the player and the killer.
/// </summary>
[DefaultExecutionOrder(-150)]
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Area (world X/Z of the bottom-left corner)")]
    public Vector2 origin = new Vector2(-30f, -20f);
    public int width = 60;
    public int height = 40;
    public float tileSize = 1f;

    [Header("Blocking")]
    [Tooltip("Layers whose colliders block tiles and edges.")]
    public LayerMask blockingLayers = 1;
    [Tooltip("Inner area (half size, in tiles) a WALL must not enter. Walls sit on tile lines with half thickness 0.15, so keep this just under 0.425.")]
    public float probeHalfSize = 0.41f;
    [Tooltip("Area (half size, in tiles) a PROP must not touch at all. 0.49 = the whole tile.")]
    public float propHalfSize = 0.49f;
    public float probeBottom = 0.15f;
    [Tooltip("Things higher than this (hanging lamps, pipes) do not block.")]
    public float probeTop = 1.8f;

    [Header("Look")]
    public Material lineMaterial;
    public Material reachMaterial;
    public Material hoverMaterial;
    public Material pathMaterial;
    public float lineWidth = 0.03f;
    public float lineY = 0.025f;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    bool[,] walkable;
    bool[,] openEast;
    bool[,] openNorth;

    Mesh outlineMesh;
    Mesh hoverMesh;
    Mesh dotMesh;
    GameObject hoverObject;
    readonly List<GameObject> reachPool = new List<GameObject>();
    readonly List<GameObject> pathPool = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        Physics.SyncTransforms();
        ScanTiles();
        BuildVisuals();
    }

    // ------------------------------------------------------------------ coordinates

    public Vector3 TileCenter(Vector2Int t) =>
        new Vector3(origin.x + (t.x + 0.5f) * tileSize, 0f, origin.y + (t.y + 0.5f) * tileSize);

    public Vector2Int WorldToTile(Vector3 p) =>
        new Vector2Int(Mathf.FloorToInt((p.x - origin.x) / tileSize), Mathf.FloorToInt((p.z - origin.y) / tileSize));

    public bool InBounds(Vector2Int t) => t.x >= 0 && t.y >= 0 && t.x < width && t.y < height;

    public bool IsWalkable(Vector2Int t) => InBounds(t) && walkable[t.x, t.y];

    public static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    /// <summary>True if you can walk straight from tile a to its neighbour b.</summary>
    public bool CanStep(Vector2Int a, Vector2Int b)
    {
        if (!IsWalkable(a) || !IsWalkable(b)) return false;
        Vector2Int d = b - a;
        if (d == Vector2Int.right) return openEast[a.x, a.y];
        if (d == Vector2Int.left) return openEast[b.x, b.y];
        if (d == Vector2Int.up) return openNorth[a.x, a.y];
        if (d == Vector2Int.down) return openNorth[b.x, b.y];
        return false;
    }

    /// <summary>The closest walkable tile to t (t itself if it is walkable).</summary>
    public Vector2Int NearestWalkable(Vector2Int t, int maxRadius = 6)
    {
        if (IsWalkable(t)) return t;
        Vector2Int best = t;
        float bestDist = float.MaxValue;
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = new Vector2Int(t.x + dx, t.y + dy);
                    float d = dx * dx + dy * dy;
                    if (IsWalkable(c) && d < bestDist)
                    {
                        bestDist = d;
                        best = c;
                    }
                }
            }
            if (bestDist < float.MaxValue) return best;
        }
        return best;
    }

    readonly HashSet<Vector2Int> claimed = new HashSet<Vector2Int>();

    /// <summary>
    /// Reserves the closest free walkable tile for a key or hiding spot, so each one gets its own tile.
    /// </summary>
    public Vector2Int ClaimNearest(Vector3 worldPos, int maxRadius = 6)
    {
        Vector2Int t = WorldToTile(worldPos);
        Vector2Int best = NearestWalkable(t, maxRadius);
        // Never claim a tile on the other side of a wall (e.g. a spot next to a wall would "leak" into the next room).
        if (claimed.Contains(best) || WallBetweenPoints(worldPos, TileCenter(best)))
        {
            float bestDist = float.MaxValue;
            for (int dx = -maxRadius; dx <= maxRadius; dx++)
            {
                for (int dy = -maxRadius; dy <= maxRadius; dy++)
                {
                    var c = new Vector2Int(t.x + dx, t.y + dy);
                    if (!IsWalkable(c) || claimed.Contains(c) || WallBetweenPoints(worldPos, TileCenter(c))) continue;
                    float d = (TileCenter(c) - new Vector3(worldPos.x, 0f, worldPos.z)).sqrMagnitude;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = c;
                    }
                }
            }
        }
        claimed.Add(best);
        return best;
    }

    /// <summary>True if a wall piece (Wall_* / ExitDoor*) lies on the straight line between two points (at 0.9 m).</summary>
    public bool WallBetweenPoints(Vector3 a, Vector3 b)
    {
        a.y = 0.9f;
        b.y = 0.9f;
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.0001f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(a, d.normalized, d.magnitude, blockingLayers, QueryTriggerInteraction.Ignore))
            if (IsWall(hit.collider)) return true;
        return false;
    }

    // ------------------------------------------------------------------ path finding

    /// <summary>Breadth-first search. Returns tile -> parent for every tile reachable within maxSteps.</summary>
    public Dictionary<Vector2Int, Vector2Int> Reachable(Vector2Int start, int maxSteps, Vector2Int? blocked = null)
    {
        var parents = new Dictionary<Vector2Int, Vector2Int> { { start, start } };
        var depth = new Dictionary<Vector2Int, int> { { start, 0 } };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            int d = depth[c];
            if (d >= maxSteps) continue;
            foreach (Vector2Int dir in Dirs)
            {
                Vector2Int n = c + dir;
                if (parents.ContainsKey(n) || !CanStep(c, n)) continue;
                if (blocked.HasValue && n == blocked.Value) continue;
                parents[n] = c;
                depth[n] = d + 1;
                queue.Enqueue(n);
            }
        }
        return parents;
    }

    /// <summary>Path from start to goal using a parents map (start excluded). Null if unreachable.</summary>
    public static List<Vector2Int> PathTo(Dictionary<Vector2Int, Vector2Int> parents, Vector2Int start, Vector2Int goal)
    {
        if (!parents.ContainsKey(goal)) return null;
        var path = new List<Vector2Int>();
        Vector2Int c = goal;
        while (c != start)
        {
            path.Add(c);
            c = parents[c];
        }
        path.Reverse();
        return path;
    }

    /// <summary>Shortest path from start to goal (start excluded). Null if unreachable.</summary>
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (start == goal) return new List<Vector2Int>();
        return PathTo(Reachable(start, int.MaxValue), start, goal);
    }

    // ------------------------------------------------------------------ scanning

    void ScanTiles()
    {
        walkable = new bool[width, height];
        openEast = new bool[width, height];
        openNorth = new bool[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                walkable[x, y] = !IsTileBlocked(new Vector2Int(x, y));

        // Walls sit on tile edges: a line between two tile centers tells if the edge is open.
        Vector3 eye = Vector3.up * 0.9f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!walkable[x, y]) continue;
                Vector3 a = TileCenter(new Vector2Int(x, y)) + eye;
                if (x + 1 < width && walkable[x + 1, y])
                    openEast[x, y] = !Physics.Linecast(a, TileCenter(new Vector2Int(x + 1, y)) + eye, blockingLayers, QueryTriggerInteraction.Ignore);
                if (y + 1 < height && walkable[x, y + 1])
                    openNorth[x, y] = !Physics.Linecast(a, TileCenter(new Vector2Int(x, y + 1)) + eye, blockingLayers, QueryTriggerInteraction.Ignore);
            }
        }

        RemoveIsolatedTiles();
        BuildRooms();
    }

    /// <summary>True for wall pieces and the exit door (they sit on tile lines, not on tiles).</summary>
    public static bool IsWall(Collider col) => col.name.StartsWith("Wall_") || col.name.StartsWith("ExitDoor");

    // ------------------------------------------------------------------ rooms + line of sight

    int[,] roomIds;
    public int RoomCount { get; private set; }

    /// <summary>Room id of a walkable tile (-1 for blocked / outside).</summary>
    public int RoomOf(Vector2Int t) => InBounds(t) && roomIds != null ? roomIds[t.x, t.y] : -1;

    /// <summary>
    /// Flood fill over open edges, but never through a doorway, so each room gets its own id.
    /// A doorway is an open edge with wall pieces on its line at both ends.
    /// </summary>
    void BuildRooms()
    {
        roomIds = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                roomIds[x, y] = -1;

        int id = 0;
        var queue = new Queue<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!walkable[x, y] || roomIds[x, y] >= 0) continue;
                roomIds[x, y] = id;
                queue.Enqueue(new Vector2Int(x, y));
                while (queue.Count > 0)
                {
                    Vector2Int c = queue.Dequeue();
                    foreach (Vector2Int d in Dirs)
                    {
                        Vector2Int n = c + d;
                        if (!InBounds(n) || roomIds[n.x, n.y] >= 0 || !CanStep(c, n) || IsDoorEdge(c, n)) continue;
                        roomIds[n.x, n.y] = id;
                        queue.Enqueue(n);
                    }
                }
                id++;
            }
        }
        RoomCount = id;
    }

    bool IsDoorEdge(Vector2Int a, Vector2Int b)
    {
        Vector3 mid = (TileCenter(a) + TileCenter(b)) * 0.5f + Vector3.up;
        Vector3 along = (b - a).x != 0 ? Vector3.forward : Vector3.right; // direction of the edge line
        float off = tileSize * 0.75f;
        return WallAt(mid + along * off) && WallAt(mid - along * off);
    }

    bool WallAt(Vector3 p)
    {
        foreach (Collider c in Physics.OverlapBox(p, new Vector3(0.1f, 0.4f, 0.1f), Quaternion.identity, blockingLayers, QueryTriggerInteraction.Ignore))
            if (IsWall(c)) return true;
        return false;
    }

    /// <summary>
    /// Tile line of sight (supercover walk from a to b). Blocked tiles stop it, and so do walls on tile edges.
    /// Doorways are open edges, so sight passes through them.
    /// </summary>
    public bool LineOfSight(Vector2Int a, Vector2Int b)
    {
        if (!IsWalkable(a) || !IsWalkable(b)) return false;
        int dx = b.x - a.x, dy = b.y - a.y;
        int nx = Mathf.Abs(dx), ny = Mathf.Abs(dy);
        int sx = dx > 0 ? 1 : -1, sy = dy > 0 ? 1 : -1;
        Vector2Int c = a;
        int ix = 0, iy = 0;
        while (ix < nx || iy < ny)
        {
            long lhs = (1L + 2L * ix) * ny;
            long rhs = (1L + 2L * iy) * nx;
            if (lhs == rhs)
            {
                // Exactly through a corner: one of the two side ways must be open.
                var px = new Vector2Int(c.x + sx, c.y);
                var py = new Vector2Int(c.x, c.y + sy);
                var n = new Vector2Int(c.x + sx, c.y + sy);
                if (!((CanStep(c, px) && CanStep(px, n)) || (CanStep(c, py) && CanStep(py, n)))) return false;
                c = n;
                ix++;
                iy++;
            }
            else if (lhs < rhs)
            {
                var n = new Vector2Int(c.x + sx, c.y);
                if (!CanStep(c, n)) return false;
                c = n;
                ix++;
            }
            else
            {
                var n = new Vector2Int(c.x, c.y + sy);
                if (!CanStep(c, n)) return false;
                c = n;
                iy++;
            }
        }
        return true;
    }

    /// <summary>
    /// A tile is blocked if any prop touches ANY part of it (so no tile is ever half covered),
    /// or if a wall reaches into its inner area (walls on the tile lines only touch the thin border).
    /// </summary>
    public bool IsTileBlocked(Vector2Int t)
    {
        float midY = (probeBottom + probeTop) * 0.5f;
        float halfY = (probeTop - probeBottom) * 0.5f;
        Vector3 center = TileCenter(t) + Vector3.up * midY;
        var full = new Vector3(propHalfSize * tileSize, halfY, propHalfSize * tileSize);
        var inner = new Vector3(probeHalfSize * tileSize, halfY, probeHalfSize * tileSize);

        foreach (Collider col in Physics.OverlapBox(center, full, Quaternion.identity, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            // The player and the killer never block tiles.
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (col.GetComponentInParent<KillerAI>() != null) continue;
            if (!IsWall(col)) return true;
        }
        foreach (Collider col in Physics.OverlapBox(center, inner, Quaternion.identity, blockingLayers, QueryTriggerInteraction.Ignore))
            if (IsWall(col)) return true;
        return false;
    }

    /// <summary>Walkable tiles that are cut off from the main area are treated as blocked (no grid lines).</summary>
    void RemoveIsolatedTiles()
    {
        var best = new HashSet<Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var t = new Vector2Int(x, y);
                if (!walkable[x, y] || seen.Contains(t)) continue;
                var region = new HashSet<Vector2Int>(Reachable(t, int.MaxValue).Keys);
                seen.UnionWith(region);
                if (region.Count > best.Count) best = region;
            }
        }
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (walkable[x, y] && !best.Contains(new Vector2Int(x, y))) walkable[x, y] = false;
    }

    // ------------------------------------------------------------------ visuals

    void BuildVisuals()
    {
        lineMaterial = lineMaterial ? lineMaterial : MakeUnlit(new Color(0.2f, 0.19f, 0.17f));
        reachMaterial = reachMaterial ? reachMaterial : MakeUnlit(new Color(0.1f, 0.55f, 0.5f));
        hoverMaterial = hoverMaterial ? hoverMaterial : MakeUnlit(new Color(1f, 0.85f, 0.4f));
        pathMaterial = pathMaterial ? pathMaterial : MakeUnlit(new Color(0.95f, 0.55f, 0.15f));

        // Highlight sizes scale with the tile size.
        outlineMesh = MakeOutlineMesh(tileSize * 0.9f, 0.05f * tileSize);
        hoverMesh = MakeOutlineMesh(tileSize * 0.98f, 0.07f * tileSize);
        dotMesh = MakeSquareMesh(0.2f * tileSize);

        // One combined mesh for every walkable tile outline.
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (walkable[x, y])
                    AddOutline(verts, tris, TileCenter(new Vector2Int(x, y)) + Vector3.up * lineY, tileSize * 0.96f, lineWidth);

        var mesh = new Mesh { name = "GridLines", indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        CreateRenderer("GridLines", mesh, lineMaterial);

        hoverObject = CreateRenderer("Hover", hoverMesh, hoverMaterial);
        hoverObject.SetActive(false);
    }

    /// <summary>Outlines every tile in the set (except one) in the reach color.</summary>
    public void ShowReach(IEnumerable<Vector2Int> tiles, Vector2Int except)
    {
        int i = 0;
        foreach (Vector2Int t in tiles)
        {
            if (t == except) continue;
            GameObject go = Pooled(reachPool, i++, "Reach", outlineMesh, reachMaterial);
            go.transform.position = TileCenter(t) + Vector3.up * (lineY + 0.004f);
        }
        HideFrom(reachPool, i);
    }

    public void ShowHover(Vector2Int? tile)
    {
        hoverObject.SetActive(tile.HasValue);
        if (tile.HasValue) hoverObject.transform.position = TileCenter(tile.Value) + Vector3.up * (lineY + 0.008f);
    }

    public void ShowPath(List<Vector2Int> path)
    {
        int i = 0;
        if (path != null)
        {
            foreach (Vector2Int t in path)
            {
                GameObject go = Pooled(pathPool, i++, "Path", dotMesh, pathMaterial);
                go.transform.position = TileCenter(t) + Vector3.up * (lineY + 0.012f);
            }
        }
        HideFrom(pathPool, i);
    }

    public void ClearHighlights()
    {
        HideFrom(reachPool, 0);
        HideFrom(pathPool, 0);
        if (hoverObject) hoverObject.SetActive(false);
    }

    GameObject Pooled(List<GameObject> pool, int index, string name, Mesh mesh, Material mat)
    {
        while (pool.Count <= index) pool.Add(CreateRenderer(name, mesh, mat));
        GameObject go = pool[index];
        go.SetActive(true);
        return go;
    }

    static void HideFrom(List<GameObject> pool, int start)
    {
        for (int i = start; i < pool.Count; i++)
            if (pool[i].activeSelf) pool[i].SetActive(false);
    }

    GameObject CreateRenderer(string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static Material MakeUnlit(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        var m = new Material(s);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    static Mesh MakeOutlineMesh(float size, float w)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        AddOutline(verts, tris, Vector3.zero, size, w);
        var mesh = new Mesh { name = "TileOutline" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    static Mesh MakeSquareMesh(float size)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float h = size * 0.5f;
        AddQuad(verts, tris, new Vector3(-h, 0f, -h), new Vector3(h, 0f, h));
        var mesh = new Mesh { name = "TileDot" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        return mesh;
    }

    static void AddOutline(List<Vector3> v, List<int> t, Vector3 c, float size, float w)
    {
        float h = size * 0.5f;
        AddQuad(v, t, c + new Vector3(-h, 0f, h - w), c + new Vector3(h, 0f, h));               // top
        AddQuad(v, t, c + new Vector3(-h, 0f, -h), c + new Vector3(h, 0f, -h + w));             // bottom
        AddQuad(v, t, c + new Vector3(-h, 0f, -h + w), c + new Vector3(-h + w, 0f, h - w));     // left
        AddQuad(v, t, c + new Vector3(h - w, 0f, -h + w), c + new Vector3(h, 0f, h - w));       // right
    }

    /// <summary>Flat quad facing up, from min corner to max corner (x/z).</summary>
    static void AddQuad(List<Vector3> v, List<int> t, Vector3 min, Vector3 max)
    {
        int i = v.Count;
        v.Add(new Vector3(min.x, min.y, min.z));
        v.Add(new Vector3(min.x, min.y, max.z));
        v.Add(new Vector3(max.x, min.y, max.z));
        v.Add(new Vector3(max.x, min.y, min.z));
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }
}
