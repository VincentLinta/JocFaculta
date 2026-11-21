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
