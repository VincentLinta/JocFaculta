using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts cabinets and dressers against walls at random free tiles when the game starts.
/// Created automatically by PlayerController, so nothing has to be set up in the scene.
/// </summary>
public class SearchableSpawner : MonoBehaviour
{
    public int count = 26;
    [Tooltip("At least this many objects always hold a card.")]
    public int guaranteedCards = 9;
    [Tooltip("How many Decoy cards are hidden on the map. The game design is ONE per match; more is only for testing.")]
    public int decoyCount = 4;
    [Tooltip("Chance (0-1) that any other object holds a card.")]
    public float cardChance = 0.15f;
    [Tooltip("Chance (0-1) that any other object holds an energy drink.")]
    public float drinkChance = 0.3f;
    [Tooltip("Minimum distance between two objects, in tiles.")]
    public int minSpacing = 3;
    [Header("Escape doors")]
    [Tooltip("How many escape points (doors and windows) are put on the outer walls. The Escape card opens one of them.")]
    public int doorCount = 6;
    [Tooltip("Chance (0-1) that an escape point is a window instead of a door.")]
    public float windowChance = 0.4f;
    public int doorSpacing = 6;
    [Header("Light windows")]
    [Tooltip("Windows on the outer walls that let sunlight into the rooms.")]
    public int lightWindowCount = 10;
    [Tooltip("Minimum distance between two light windows, in tiles.")]
    public int lightWindowSpacing = 5;
    [Tooltip("Brightness of the light that comes through a window.")]
    public float windowLightIntensity = 6f;
    [Tooltip("Window light casts shadows (turn off if the game gets slow).")]
    public bool windowLightShadows = true;

    // Cards that can show up in several cabinets. The Decoy is special: exactly one per match.
    static readonly CardType[] Spawnable = { CardType.Fortify, CardType.Escape, CardType.FightBack, CardType.Girlfriend, CardType.Flashbang };

    /// <summary>A random card that can show up in several cabinets (never the Decoy).</summary>
    public static CardType RandomCard()
    {
        // Card types that were already used (Decoy / Girlfriend) can not be found any more.
        var pool = new List<CardType>();
        foreach (CardType t in Spawnable) if (!UsedCards.Contains(t)) pool.Add(t);
        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>Card types the player already used up. They never show up in cabinets again (cleared at every match start).</summary>
    public static readonly HashSet<CardType> UsedCards = new HashSet<CardType>();

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    IEnumerator Start()
    {
        yield return null; // let every hiding spot claim its tile first
        Spawn();
    }

    void Spawn()
    {
        UsedCards.Clear();
        GridManager grid = GridManager.Instance;
        PlayerController player = PlayerController.Instance;
        if (grid == null) return;

        // All (tile, wall direction) pairs where a cabinet fits against a wall.
        var candidates = new List<KeyValuePair<Vector2Int, Vector2Int>>();
        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                var t = new Vector2Int(x, y);
                if (!grid.IsWalkable(t)) continue;
                if (player != null && GridManager.Chebyshev(t, player.Tile) <= 1) continue;
                if (NearHidingSpot(grid, t)) continue;
                if (LevelTweaks.NearGate(grid.TileCenter(t))) continue; // the garage gate sits on this wall
                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int n = t + d;
                    if (grid.CanStep(t, n)) continue;
                    if (!grid.WallBetweenPoints(grid.TileCenter(t), grid.TileCenter(n))) continue;
                    candidates.Add(new KeyValuePair<Vector2Int, Vector2Int>(t, d));
                }
            }
        }

        // Shuffle, then take candidates that are far enough from each other.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        var used = new List<Vector2Int>();
        var placed = new List<SearchableObject>();
        foreach (var c in candidates)
        {
            if (used.Count >= count) break;
            bool tooClose = false;
            foreach (Vector2Int u in used)
                if (GridManager.Chebyshev(u, c.Key) < minSpacing) { tooClose = true; break; }
            if (tooClose) continue;
            bool card = used.Count < guaranteedCards;
            CardType forced = Spawnable[used.Count % Spawnable.Length]; // guaranteed cards cycle through every type
            used.Add(c.Key);
            placed.Add(Place(grid, c.Key, c.Value, card, forced));
        }

        // The Decoy cards (normally exactly one per match; decoyCount is higher only for testing).
        for (int i = placed.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (placed[i], placed[j]) = (placed[j], placed[i]);
        }
        for (int i = 0; i < Mathf.Min(decoyCount, placed.Count); i++)
        {
            placed[i].contents = SearchableObject.Contents.Card;
            placed[i].card = CardType.Decoy;
        }
        Debug.Log($"[Searchables] Placed {used.Count} searchable objects.");
        SpawnDoors(grid, candidates, used);
    }

    // ------------------------------------------------------------------ escape doors

    void SpawnDoors(GridManager grid, List<KeyValuePair<Vector2Int, Vector2Int>> candidates, List<Vector2Int> cabinetTiles)
    {
        var doorTiles = new List<Vector2Int>();
        foreach (var c in candidates) // already shuffled
        {
            if (doorTiles.Count >= doorCount) break;
            // Only on the outer walls of the map: the tile behind the wall lies outside the grid.
            if (grid.InBounds(c.Key + c.Value)) continue;
            bool bad = LevelTweaks.NearGate(grid.TileCenter(c.Key), 4.5f);
            foreach (ExitDoor e in ExitDoor.All) // keep clear of the old exits
                if (!e.isEscapeDoor && GridManager.Chebyshev(grid.WorldToTile(e.transform.position), c.Key) < 2) { bad = true; break; }
            foreach (Vector2Int u in cabinetTiles)
                if (GridManager.Chebyshev(u, c.Key) < 2) { bad = true; break; }
            if (!bad)
                foreach (Vector2Int u in doorTiles)
                    if (GridManager.Chebyshev(u, c.Key) < doorSpacing) { bad = true; break; }
            if (bad) continue;
            doorTiles.Add(c.Key);
            PlaceDoor(grid, c.Key, c.Value, Random.value < windowChance);
        }
        Debug.Log($"[EscapeDoors] Placed {doorTiles.Count} escape doors/windows on the outer walls.");
        SpawnLightWindows(grid, candidates, cabinetTiles, doorTiles);
    }

    // ------------------------------------------------------------------ light windows

    static Texture2D softTex;

    /// <summary>White rectangle with soft edges (used for the light patch on the floor and the beam).</summary>
    static Texture2D SoftTexture()
    {
        if (softTex != null) return softTex;
        const int n = 64;
        softTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float ex = Mathf.Min(x, n - 1 - x) / (n * 0.28f);
                float ey = Mathf.Min(y, n - 1 - y) / (n * 0.28f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(ex, ey)));
                softTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        softTex.Apply();
        return softTex;
    }

    static Transform MakeGlow(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localRotation = rot;
        q.transform.localScale = scale;
        Destroy(q.GetComponent<Collider>());
        Shader sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.mainTexture = SoftTexture();
        m.color = color;
        Renderer r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q.transform;
    }

    void SpawnLightWindows(GridManager grid, List<KeyValuePair<Vector2Int, Vector2Int>> candidates, List<Vector2Int> cabinetTiles, List<Vector2Int> doorTiles)
    {
        var tiles = new List<Vector2Int>();
        foreach (var c in candidates)
        {
            if (tiles.Count >= lightWindowCount) break;
            if (grid.InBounds(c.Key + c.Value)) continue; // outer walls only
            bool bad = LevelTweaks.NearGate(grid.TileCenter(c.Key), 4.5f);
            foreach (ExitDoor e in ExitDoor.All)
                if (!e.isEscapeDoor && GridManager.Chebyshev(grid.WorldToTile(e.transform.position), c.Key) < 2) { bad = true; break; }
            if (!bad) foreach (Vector2Int u in cabinetTiles) if (GridManager.Chebyshev(u, c.Key) < 2) { bad = true; break; }
            if (!bad) foreach (Vector2Int u in doorTiles) if (GridManager.Chebyshev(u, c.Key) < 3) { bad = true; break; }
            if (!bad) foreach (Vector2Int u in tiles) if (GridManager.Chebyshev(u, c.Key) < lightWindowSpacing) { bad = true; break; }
            if (bad) continue;
            tiles.Add(c.Key);
            PlaceLightWindow(grid, c.Key, c.Value);
        }
        Debug.Log($"[LightWindows] Placed {tiles.Count} windows that let the sun in.");
    }

    void PlaceLightWindow(GridManager grid, Vector2Int tile, Vector2Int wallDir)
    {
        var go = new GameObject("LightWindow");
        go.transform.SetParent(transform, false);
        Vector3 dir = new Vector3(wallDir.x, 0f, wallDir.y);
        go.transform.position = grid.TileCenter(tile) + dir * (grid.tileSize * 0.5f - 0.3f);
        go.transform.rotation = Quaternion.LookRotation(-dir); // local +Z points into the room

        Material frame = Mat(new Color(0.32f, 0.22f, 0.14f), false);
        MakeBox("FrameL", go.transform, new Vector3(-0.78f, 1.55f, 0f), new Vector3(0.14f, 1.5f, 0.22f), frame);
        MakeBox("FrameR", go.transform, new Vector3(0.78f, 1.55f, 0f), new Vector3(0.14f, 1.5f, 0.22f), frame);
        MakeBox("FrameTop", go.transform, new Vector3(0f, 2.3f, 0f), new Vector3(1.7f, 0.14f, 0.22f), frame);
        MakeBox("FrameBottom", go.transform, new Vector3(0f, 0.8f, 0.03f), new Vector3(1.85f, 0.14f, 0.34f), frame);
        MakeBox("Glass", go.transform, new Vector3(0f, 1.55f, 0.02f), new Vector3(1.42f, 1.42f, 0.05f), Mat(new Color(1f, 0.96f, 0.78f), true)); // bright pane
        MakeBox("BarV", go.transform, new Vector3(0f, 1.55f, 0.06f), new Vector3(0.06f, 1.42f, 0.04f), frame);
        MakeBox("BarH", go.transform, new Vector3(0f, 1.55f, 0.06f), new Vector3(1.42f, 0.06f, 0.04f), frame);

        // Sunlight coming in: a spot light into the room ...
        var lg = new GameObject("SunLight");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, 1.8f, 0.4f);
        lg.transform.localRotation = Quaternion.Euler(28f, 0f, 0f); // tilted down
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = new Color(1f, 0.93f, 0.75f);
        l.range = 16f;
        l.spotAngle = 70f;
        l.innerSpotAngle = 35f;
        l.intensity = windowLightIntensity;
        l.shadows = windowLightShadows ? LightShadows.Hard : LightShadows.None;

        // ... a bright patch on the floor and a faint beam of light from the window to it.
        MakeGlow("LightPatch", go.transform, new Vector3(0f, 0.03f, 3.3f), Quaternion.Euler(90f, 0f, 0f), new Vector3(1.7f, 2.8f, 1f), new Color(1f, 0.93f, 0.68f, 0.34f));
        Vector3 a = new Vector3(0f, 2.2f, 0.1f), b = new Vector3(0f, 0.05f, 3.8f);
        MakeGlow("LightBeam", go.transform, (a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, b - a), new Vector3(1.5f, (b - a).magnitude, 1f), new Color(1f, 0.95f, 0.75f, 0.12f));
    }

    void PlaceDoor(GridManager grid, Vector2Int tile, Vector2Int wallDir, bool window)
    {
        var go = new GameObject(window ? "EscapeWindow" : "EscapeDoor");
        go.transform.SetParent(transform, false);
        Vector3 dir = new Vector3(wallDir.x, 0f, wallDir.y);
        go.transform.position = grid.TileCenter(tile) + dir * (grid.tileSize * 0.5f - 0.3f);
        go.transform.rotation = Quaternion.LookRotation(-dir); // front faces the room

        Material frame = Mat(new Color(0.12f, 0.12f, 0.14f), false);
        Transform panelT;
        if (window)
        {
            // A window: a glass pane in a frame, with a sill.
            MakeBox("FrameL", go.transform, new Vector3(-0.62f, 1.5f, 0f), new Vector3(0.14f, 1.3f, 0.22f), frame);
            MakeBox("FrameR", go.transform, new Vector3(0.62f, 1.5f, 0f), new Vector3(0.14f, 1.3f, 0.22f), frame);
            MakeBox("FrameTop", go.transform, new Vector3(0f, 2.2f, 0f), new Vector3(1.38f, 0.14f, 0.22f), frame);
            MakeBox("FrameBottom", go.transform, new Vector3(0f, 0.85f, 0.03f), new Vector3(1.5f, 0.14f, 0.34f), frame);
            panelT = MakeBox("Panel", go.transform, new Vector3(0f, 1.52f, 0.02f), new Vector3(1.1f, 1.2f, 0.05f), Mat(new Color(0.04f, 0.07f, 0.11f), true));
            MakeBox("BarV", go.transform, new Vector3(0f, 1.52f, 0.06f), new Vector3(0.05f, 1.2f, 0.04f), frame);
            MakeBox("BarH", go.transform, new Vector3(0f, 1.52f, 0.06f), new Vector3(1.1f, 0.05f, 0.04f), frame);
        }
        else
        {
            MakeBox("FrameL", go.transform, new Vector3(-0.62f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.22f), frame);
            MakeBox("FrameR", go.transform, new Vector3(0.62f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.22f), frame);
            MakeBox("FrameTop", go.transform, new Vector3(0f, 2.3f, 0f), new Vector3(1.38f, 0.14f, 0.22f), frame);
            panelT = MakeBox("Panel", go.transform, new Vector3(0f, 1.12f, 0.02f), new Vector3(1.1f, 2.15f, 0.06f), Mat(new Color(0.22f, 0.04f, 0.04f), true));
        }

        var lg = new GameObject("DoorLight");
        lg.transform.SetParent(go.transform, false);
        lg.transform.localPosition = new Vector3(0f, 1.4f, 1.0f);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 7f;
        l.intensity = 2.2f;
        l.shadows = LightShadows.None;
        l.enabled = false;

        var door = go.AddComponent<ExitDoor>();
        door.isEscapeDoor = true;
        door.isWindow = window;
        door.doorLight = l;
        door.panel = panelT.GetComponent<Renderer>();
    }

    static Material Mat(Color c, bool unlit)
    {
        Shader s = unlit ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find(unlit ? "Unlit/Color" : "Standard");
        var m = new Material(s);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    static Transform MakeBox(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.SetParent(parent, false);
        b.transform.localPosition = pos;
        b.transform.localScale = scale;
        b.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(b.GetComponent<Collider>());
        return b.transform;
    }

    /// <summary>
    /// True if a cabinet on this tile would get tangled up with a hiding spot: the tile itself or any tile next to it
    /// (the places the player can stand to search it) is also in reach of a hiding spot, or the spot is only 1-2 tiles away.
    /// There E would hide the player instead of searching. Such tiles are never used for cabinets.
    /// </summary>
    static bool NearHidingSpot(GridManager grid, Vector2Int t)
    {
        foreach (HidingSpot s in HidingSpot.All)
        {
            if (GridManager.Manhattan(t, s.Tile) <= 2) return true;
            foreach (Vector2Int d in Dirs)
                if (s.IsInReach(t + d, grid)) return true;
            if (s.IsInReach(t, grid)) return true;
        }
        return false;
    }

    SearchableObject Place(GridManager grid, Vector2Int tile, Vector2Int wallDir, bool forceCard, CardType forcedType)
    {
        var go = new GameObject("Searchable");
        go.transform.SetParent(transform, false);
        Vector3 dir = new Vector3(wallDir.x, 0f, wallDir.y);
        go.transform.position = grid.TileCenter(tile) + dir * (grid.tileSize * 0.5f - 0.48f);
        go.transform.rotation = Quaternion.LookRotation(-dir); // front faces the room
        var so = go.AddComponent<SearchableObject>();
        float r = Random.value;
        SearchableObject.Contents contents = SearchableObject.Contents.Nothing;
        if (forceCard || r < cardChance) contents = SearchableObject.Contents.Card;
        else if (r < cardChance + drinkChance) contents = SearchableObject.Contents.EnergyDrink;
        CardType[] types = Spawnable;
        so.Init(grid, tile, Random.value < 0.5f, contents, forceCard ? forcedType : RandomCard());
        return so;
    }
}
