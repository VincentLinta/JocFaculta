using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts cabinets and dressers against walls at random free tiles when the game starts.
/// Created automatically by PlayerController, so nothing has to be set up in the scene.
/// </summary>
public class SearchableSpawner : MonoBehaviour
{
    public int count = 10;
    [Tooltip("At least this many objects always hold a card.")]
    public int guaranteedCards = 4;
    [Tooltip("Chance (0-1) that any other object holds a card.")]
    public float cardChance = 0.15f;
    [Tooltip("Chance (0-1) that any other object holds an energy drink.")]
    public float drinkChance = 0.3f;
    [Tooltip("Minimum distance between two objects, in tiles.")]
    public int minSpacing = 4;
    [Header("Escape doors")]
    [Tooltip("How many escape points (doors and windows) are put on the outer walls. The Escape card opens one of them.")]
    public int doorCount = 6;
    [Tooltip("Chance (0-1) that an escape point is a window instead of a door.")]
    public float windowChance = 0.4f;
    public int doorSpacing = 6;

    static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    IEnumerator Start()
    {
        yield return null; // let every hiding spot claim its tile first
        Spawn();
    }

    void Spawn()
    {
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
                if (NearHidingSpot(t)) continue;
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
        foreach (var c in candidates)
        {
            if (used.Count >= count) break;
            bool tooClose = false;
            foreach (Vector2Int u in used)
                if (GridManager.Chebyshev(u, c.Key) < minSpacing) { tooClose = true; break; }
            if (tooClose) continue;
            bool card = used.Count < guaranteedCards;
            var all = (CardType[])System.Enum.GetValues(typeof(CardType));
            CardType forced = all[used.Count % all.Length]; // guaranteed cards cycle through every type
            used.Add(c.Key);
            Place(grid, c.Key, c.Value, card, forced);
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
            bool bad = false;
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

    static bool NearHidingSpot(Vector2Int t)
    {
        foreach (HidingSpot s in HidingSpot.All)
            if (GridManager.Manhattan(t, s.Tile) <= 1) return true;
        return false;
    }

    void Place(GridManager grid, Vector2Int tile, Vector2Int wallDir, bool forceCard, CardType forcedType)
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
        var types = (CardType[])System.Enum.GetValues(typeof(CardType));
        so.Init(grid, tile, Random.value < 0.5f, contents, forceCard ? forcedType : types[Random.Range(0, types.Length)]);
    }
}
