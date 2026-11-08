using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place where the player can hide (wardrobe, locker, barrel...).
/// The killer may check it while searching. Each spot owns one grid tile (its enter tile).
/// The player can hide from the enter tile, a tile next to it, or a tile right next to the object.
/// While the player is in reach, the object gets a pulsing outline.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class HidingSpot : MonoBehaviour
{
    public enum SpotType { Wardrobe, UnderBed, BehindCrate, Barrel, Locker, UnderTable, Curtain, Vent, UnderStairs }

    /// <summary>Every active hiding spot in the scene.</summary>
    public static readonly List<HidingSpot> All = new List<HidingSpot>();

    [Tooltip("What kind of hiding spot this is.")]
    public SpotType spotType;

    [Tooltip("Where the player stands to enter and where they come out.")]
    public Transform enterPoint;

    [Tooltip("Where the player is placed while hidden.")]
    public Transform hidePoint;

    [Tooltip("True while the player is inside.")]
    public bool isOccupied;

    [Header("Outline")]
    [Tooltip("Unlit material for the outline shell (created at runtime if empty).")]
    public Material outlineMaterial;
    public Color outlineColor = new Color(0.65f, 0.95f, 1f);
    [Tooltip("How much bigger the outline shell is than the object.")]
    public float outlineGrow = 0.09f;

    /// <summary>Chance (0-1) the killer finds the player when checking this spot. Set on enter.</summary>
    [HideInInspector] public float findChance;

    /// <summary>The grid tile you stand on to use this spot.</summary>
    public Vector2Int Tile { get; private set; }

    static Material sharedOutline;
    readonly List<Vector2Int> objectTiles = new List<Vector2Int>();
    readonly List<GameObject> outlineParts = new List<GameObject>();
    bool outlineOn;
    Vector2Int cachedPlayerTile = new Vector2Int(-999, -999);
    bool cachedInReach;

    void OnEnable() { All.Add(this); }

    void OnDisable()
    {
        All.Remove(this);
        isOccupied = false;
    }

    void Start()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || enterPoint == null) return;

        // Snap the enter point to the center of the nearest free walkable tile (one spot per tile).
        Tile = grid.ClaimNearest(enterPoint.position);
        enterPoint.position = grid.TileCenter(Tile) + Vector3.up;

        CollectObjectTiles(grid);
        BuildOutline();
    }

    void Reset()
    {
        // The collider is only a detection zone, never a wall.
        GetComponent<BoxCollider>().isTrigger = true;
    }

    void Update()
    {
        // Outline only for spots the player can use right now.
        PlayerController player = PlayerController.Instance;
        GameManager gm = GameManager.Instance;
        GridManager grid = GridManager.Instance;
        bool show = player != null && gm != null && grid != null
                    && gm.CanPlayerAct && !player.IsBusy && !player.IsHidden
                    && !isOccupied && IsInReach(player.Tile, grid);
        SetOutline(show);

        if (show && outlineMaterial != null)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
            Color c = outlineColor * Mathf.Lerp(0.45f, 1f, pulse);
            c.a = 1f;
            outlineMaterial.color = c;
            if (outlineMaterial.HasProperty("_BaseColor")) outlineMaterial.SetColor("_BaseColor", c);
        }
    }

    /// <summary>Called by the player when hiding here.</summary>
    public void Enter(float chance)
    {
        isOccupied = true;
        findChance = Mathf.Clamp01(chance);
    }

    /// <summary>Called by the player when leaving.</summary>
    public void Exit()
    {
        isOccupied = false;
        findChance = 0f;
    }

    /// <summary>
    /// True if a player on this tile can hide here: on the enter tile, next to it (no wall between),
    /// or right next to one of the object's tiles (no wall between).
    /// </summary>
    public bool IsInReach(Vector2Int playerTile, GridManager grid)
    {
        if (playerTile == cachedPlayerTile) return cachedInReach;
        cachedPlayerTile = playerTile;
        cachedInReach = ComputeInReach(playerTile, grid);
        return cachedInReach;
    }

    bool ComputeInReach(Vector2Int playerTile, GridManager grid)
    {
        if (playerTile == Tile) return true;
        if (GridManager.Manhattan(playerTile, Tile) == 1 && grid.CanStep(playerTile, Tile)) return true;
        foreach (Vector2Int t in objectTiles)
            if (GridManager.Manhattan(playerTile, t) == 1 && !WallBetween(grid, playerTile, t)) return true;
        return false;
    }

    /// <summary>True if a wall (or another object) stands between two tile centers. This spot's own parts are ignored.</summary>
    bool WallBetween(GridManager grid, Vector2Int a, Vector2Int b)
    {
        Vector3 from = grid.TileCenter(a) + Vector3.up * 0.9f;
        Vector3 to = grid.TileCenter(b) + Vector3.up * 0.9f;
        foreach (RaycastHit hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), grid.blockingLayers, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(transform) && GridManager.IsWall(hit.collider)) return true;
        return false;
    }

    void CollectObjectTiles(GridManager grid)
    {
        objectTiles.Clear();
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger) continue;
            Bounds b = col.bounds;
            Vector2Int min = grid.WorldToTile(b.min + new Vector3(0.05f, 0f, 0.05f));
            Vector2Int max = grid.WorldToTile(b.max - new Vector3(0.05f, 0f, 0.05f));
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                {
                    var t = new Vector2Int(x, y);
                    if (!objectTiles.Contains(t)) objectTiles.Add(t);
                }
        }
    }

    // ------------------------------------------------------------------ outline

    /// <summary>
    /// Outline = a slightly bigger copy of every visible part, drawn inside-out with an unlit bright material
    /// (only the rim around the object shows).
    /// </summary>
    void BuildOutline()
    {
        // One runtime copy shared by all spots, so pulsing never edits the material asset on disk.
        if (sharedOutline == null)
        {
            sharedOutline = outlineMaterial != null ? new Material(outlineMaterial) : MakeOutlineMaterial();
            if (sharedOutline.HasProperty("_Cull")) sharedOutline.SetFloat("_Cull", 1f);
        }
        outlineMaterial = sharedOutline;

        var parts = new List<MeshFilter>(GetComponentsInChildren<MeshFilter>());
        foreach (MeshFilter mf in parts)
        {
            if (mf.sharedMesh == null) continue;
            var go = new GameObject("Outline");
            go.transform.SetParent(mf.transform, false);
            Vector3 s = mf.transform.lossyScale;
            // Grow by a fixed world amount on each axis, so thin parts get a visible rim too.
            go.transform.localScale = new Vector3(
                1f + outlineGrow * 2f / Mathf.Max(0.01f, Mathf.Abs(s.x)),
                1f + outlineGrow * 2f / Mathf.Max(0.01f, Mathf.Abs(s.y)),
                1f + outlineGrow * 2f / Mathf.Max(0.01f, Mathf.Abs(s.z)));
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = outlineMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            outlineParts.Add(go);
        }
    }

    void SetOutline(bool on)
    {
        if (on == outlineOn) return;
        outlineOn = on;
        foreach (GameObject go in outlineParts) if (go) go.SetActive(on);
    }

    static Material MakeOutlineMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        var m = new Material(s) { name = "SpotOutline (runtime)" };
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 1f); // draw back faces = inside-out shell
        return m;
    }
}
