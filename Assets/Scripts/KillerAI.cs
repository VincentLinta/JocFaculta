using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turn-based clown killer on the tile grid. Moves 2 tiles per turn (up to 3 after keys).
/// Patrol: walks between waypoints.
/// Chase: sees the player (vision cone + line of sight) or hears a dash within a few tiles.
/// Search: goes to the last known tile, then checks nearby hiding spots one by one.
/// </summary>
public class KillerAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Search }

    public static KillerAI Instance { get; private set; }

    [Header("Movement")]
    public float tilesPerTurn = 2f;
    [Tooltip("Added after each key. Fractions add up over turns (2.34 = sometimes 3 tiles).")]
    public float tilesPerKey = 0.34f;
    public float maxTilesPerTurn = 3f;
    [Tooltip("Seconds to walk one tile.")]
    public float stepTime = 0.28f;

    [Header("Senses")]
    public float viewDistance = 13f;
    [Tooltip("Full width of the vision cone in degrees.")]
    public float viewAngle = 110f;
    public float eyeHeight = 2.4f;
    [Tooltip("A dash is heard within this many tiles.")]
    public float hearDashTiles = 6f;
    [Tooltip("What blocks line of sight.")]
    public LayerMask obstacleMask = 1;

    [Header("Search")]
    [Tooltip("Turns without seeing the player before a chase becomes a search.")]
    public int chaseLostTurns = 2;
    [Tooltip("Hiding spots this close (tiles) to the last known tile get checked.")]
    public float searchRadiusTiles = 10f;
    public int spotsToCheck = 3;
    [Tooltip("Seconds spent opening a hiding spot.")]
    public float checkDelay = 0.8f;

    [Header("Find chances")]
    public float baseFindChance = 0.15f;
    public float chasedFindChance = 0.7f;
    public float seenFindChance = 0.95f;
    [Tooltip("Hiding this close (tiles) to the killer counts as 'close'.")]
    public float closeTiles = 4f;

    [Header("Hiding rules")]
    [Tooltip("Chance to find a hidden player the killer did NOT see hide. 0 = never.")]
    [Range(0f, 1f)] public float blindFindChance = 0f;
    [Tooltip("Same room and this close (tiles) = notices the player hiding, even from behind.")]
    public float sameRoomTiles = 5f;

    [Header("Random spawn")]
    [Tooltip("Minimum path distance (tiles) from the player's spawn. 0 = anywhere.")]
    public int minSpawnDistance = 12;
    [Tooltip("Never spawn in the player's starting room or in the player's line of sight. Off + distance 0 = fully random.")]
    public bool spawnSafetyChecks = true;

    public Transform[] waypoints;

    /// <summary>Result of the last hide check (used by the UI and the music).</summary>
    public bool LastHideWitnessed { get; private set; }
    public string LastHideReason { get; private set; } = "none";

    public State CurrentState { get; private set; }
    public Vector2Int Tile { get; private set; }
    /// <summary>Flat distance to the player (used by the UI vignette).</summary>
    public float DistanceToPlayer { get; private set; } = 999f;
    /// <summary>The spot being checked right now, or null.</summary>
    public HidingSpot CheckingSpot { get; private set; }
    public int CurrentTilesPerTurn => Mathf.FloorToInt(Mathf.Min(tilesPerTurn, maxTilesPerTurn));

    GridManager grid;
    PlayerController player;
    Vector2Int[] waypointTiles;
    int waypointIndex = -1;
    Vector2Int lastKnown;
    HidingSpot witnessedSpot;
    float moveBudget;
    int unseenTurns;
    bool knewThisTurn;
    readonly List<(Vector2Int tile, HidingSpot spot)> searchQueue = new List<(Vector2Int, HidingSpot)>();

    void Awake() { Instance = this; }

    void Start()
    {
        grid = GridManager.Instance;
        player = PlayerController.Instance;

        // New random result every game start / Restart (seeded from the clock).
        Random.InitState(unchecked((int)System.DateTime.Now.Ticks));
        Tile = PickSpawnTile();
        transform.position = grid.TileCenter(Tile);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);

        int n = waypoints != null ? waypoints.Length : 0;
        waypointTiles = new Vector2Int[n];
        for (int i = 0; i < n; i++)
            waypointTiles[i] = grid.NearestWalkable(grid.WorldToTile(waypoints[i].position));

        CurrentState = State.Patrol;
        PickNextWaypoint();
    }

    void Update()
    {
        if (player == null) return;
        Vector3 flat = player.transform.position - transform.position;
        flat.y = 0f;
        DistanceToPlayer = flat.magnitude;
    }

    static bool Playing => GameManager.Instance == null || GameManager.Instance.IsPlaying;

    // ------------------------------------------------------------------ the turn

    /// <summary>Runs one full killer turn (called by the GameManager).</summary>
    public IEnumerator TakeTurn()
    {
        knewThisTurn = false;
        Hear();
        Look();
        if (TryCatch()) yield break;

        moveBudget += Mathf.Min(tilesPerTurn, maxTilesPerTurn);
        int steps = Mathf.FloorToInt(moveBudget + 0.001f);
        moveBudget -= steps;

        int guard = 0;
        while (steps > 0 && guard++ < 30 && Playing)
        {
            if (!GetTarget(out Vector2Int target, out HidingSpot spot)) break;

            if (Tile == target)
            {
                if (spot != null)
                {
                    // Opening a hiding spot ends the move.
                    yield return CheckSpot(spot);
                    if (searchQueue.Count > 0) searchQueue.RemoveAt(0);
                    break;
                }
                OnArrived();
                continue;
            }

            List<Vector2Int> path = grid.FindPath(Tile, target);
            if (path == null || path.Count == 0)
            {
                OnArrived(); // unreachable target: skip it
                continue;
            }

            // Walking into the player = caught.
            if (path[0] == player.Tile && !player.IsHidden)
            {
                Catch();
                yield break;
            }

            yield return StepTo(path[0]);
            Tile = path[0];
            steps--;

            Look();
            if (TryCatch()) yield break;
        }

        // Lost track of the player during a chase?
        if (CurrentState == State.Chase && !knewThisTurn)
        {
            unseenTurns++;
            if (unseenTurns >= chaseLostTurns) StartSearch();
        }
    }

    void Look()
    {
        if (!CanSeePlayer()) return;
        lastKnown = player.Tile;
        knewThisTurn = true;
        unseenTurns = 0;
        if (CurrentState != State.Chase)
        {
            SetState(State.Chase);
            AudioManager.Play(AudioManager.Sfx.DangerSting); // spotted!
        }
    }

    void Hear()
    {
        if (player == null || !player.DashedLastTurn || player.IsHidden) return;
        if (Vector2Int.Distance(Tile, player.Tile) > hearDashTiles) return;
        lastKnown = player.Tile;
        knewThisTurn = true;
        unseenTurns = 0;
        if (CurrentState != State.Chase) SetState(State.Chase);
    }

    bool TryCatch()
    {
        if (player == null || player.IsHidden) return false;
        if (GridManager.Chebyshev(Tile, player.Tile) > 1 || !CanSeePlayer()) return false;
        Catch();
        return true;
    }

    void Catch()
    {
        FaceTowards(player.transform.position);
        if (GameManager.Instance) GameManager.Instance.Lose("The clown caught you.");
    }

    bool GetTarget(out Vector2Int target, out HidingSpot spot)
    {
        spot = null;
        target = Tile;
        switch (CurrentState)
        {
            case State.Chase:
                target = lastKnown;
                return true;
            case State.Search:
                if (searchQueue.Count == 0)
                {
                    SetState(State.Patrol);
                    return GetTarget(out target, out spot);
                }
                target = searchQueue[0].tile;
                spot = searchQueue[0].spot;
                return true;
            default:
                if (waypointTiles.Length == 0) return false;
                target = waypointTiles[waypointIndex];
                return true;
        }
    }

    void OnArrived()
    {
        switch (CurrentState)
        {
            case State.Patrol:
                PickNextWaypoint();
                break;
            case State.Chase:
                // Reached the last known tile and the player is not in sight.
                if (!CanSeePlayer()) StartSearch();
                break;
            case State.Search:
                if (searchQueue.Count > 0) searchQueue.RemoveAt(0);
                break;
        }
    }

    void SetState(State s)
    {
        CurrentState = s;
        CheckingSpot = null;
        if (s == State.Patrol) PickNextWaypoint();
    }

    void StartSearch()
    {
        SetState(State.Search);
        searchQueue.Clear();
        searchQueue.Add((lastKnown, null));

        var spots = new List<HidingSpot>();
        foreach (HidingSpot s in HidingSpot.All)
            if (Vector2Int.Distance(s.Tile, lastKnown) <= searchRadiusTiles) spots.Add(s);
        spots.Sort((a, b) => SpotScore(a).CompareTo(SpotScore(b)));

        for (int i = 0; i < Mathf.Min(spotsToCheck, spots.Count); i++)
            searchQueue.Add((spots[i].Tile, spots[i]));
    }

    float SpotScore(HidingSpot spot)
    {
        float score = Vector2Int.Distance(spot.Tile, lastKnown);
        if (spot == witnessedSpot) score -= 1000f; // saw the player go in: check it first
        return score;
    }

    void PickNextWaypoint()
    {
        if (waypointTiles == null || waypointTiles.Length == 0) return;
        int next = Random.Range(0, waypointTiles.Length);
        if (waypointTiles.Length > 1 && next == waypointIndex) next = (next + 1) % waypointTiles.Length;
        waypointIndex = next;
    }

    IEnumerator CheckSpot(HidingSpot spot)
    {
        CheckingSpot = spot;
        FaceTowards(spot.transform.position);
        yield return new WaitForSeconds(checkDelay);
        CheckingSpot = null;

        if (spot.isOccupied && Random.value < spot.findChance)
        {
            if (GameManager.Instance) GameManager.Instance.Lose("The clown found your hiding spot.");
            yield break;
        }
        if (spot == witnessedSpot) witnessedSpot = null;
    }

    IEnumerator StepTo(Vector2Int t)
    {
        Vector3 start = transform.position;
        Vector3 end = grid.TileCenter(t);
        Quaternion startRot = transform.rotation;
        Vector3 dir = end - start;
        dir.y = 0f;
        Quaternion endRot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : startRot;

        for (float k = 0f; k < 1f; k += Time.deltaTime / stepTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, k);
            transform.SetPositionAndRotation(Vector3.Lerp(start, end, s), Quaternion.Slerp(startRot, endRot, Mathf.Min(1f, k * 2f)));
            yield return null;
        }
        transform.SetPositionAndRotation(end, endRot);
    }

    void FaceTowards(Vector3 p)
    {
        Vector3 look = p - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
    }

    // ------------------------------------------------------------------ senses / rules

    /// <summary>True if the player is visible inside the vision cone with a clear line of sight.</summary>
    public bool CanSeePlayer()
    {
        if (player == null || player.IsHidden) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = grid.TileCenter(player.Tile) + Vector3.up * 1.5f;
        Vector3 toTarget = target - eye;
        if (toTarget.magnitude > viewDistance) return false;

        Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
        bool veryClose = flat.magnitude < 1.6f * grid.tileSize; // senses you on any touching tile
        if (!veryClose && Vector3.Angle(transform.forward, flat) > viewAngle * 0.5f) return false;

        // Walls and big props block sight.
        if (Physics.Linecast(eye, target, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore)
            && hit.transform != player.transform)
            return false;

        return true;
    }

    /// <summary>Old entry point: uses the player's current tile as the tile they hid from.</summary>
    public float GetFindChanceFor(HidingSpot spot) => GetFindChanceFor(spot, player != null ? player.Tile : spot.Tile);

    /// <summary>
    /// Called by the player when they hide (fromTile = where they stood). Returns how likely the killer is
    /// to find them there. Only a WITNESSED hide can be found; otherwise blindFindChance (default 0).
    /// </summary>
    public float GetFindChanceFor(HidingSpot spot, Vector2Int fromTile)
    {
        string reason = WitnessReason(fromTile) ?? WitnessReason(spot.Tile);
        LastHideWitnessed = reason != null;
        LastHideReason = reason ?? "none";
        Debug.Log($"Hide witnessed: {LastHideWitnessed.ToString().ToLower()}, reason: {LastHideReason}, killer tile {Tile}, player tile {fromTile} (spot {spot.name} tile {spot.Tile})");

        if (LastHideWitnessed)
        {
            // Saw the player go in: walk there, then open that spot first.
            witnessedSpot = spot;
            lastKnown = spot.Tile;
            unseenTurns = 0;
            if (CurrentState != State.Chase)
            {
                SetState(State.Chase);
                AudioManager.Play(AudioManager.Sfx.DangerSting);
            }
            return seenFindChance;
        }
        // Not witnessed: the killer may still search around (and open spots), but cannot find the player.
        return blindFindChance;
    }

    /// <summary>
    /// Why the killer notices the player on tile t, or null if it does not:
    /// "line of sight" = tile line of sight inside the vision range and cone,
    /// "same room nearby" = same room, within sameRoomTiles, nothing big in between (even from behind).
    /// </summary>
    string WitnessReason(Vector2Int t)
    {
        if (grid == null) return null;
        float d = Vector2Int.Distance(Tile, t);
        bool los = grid.LineOfSight(Tile, t);
        if (!los) return null;

        if (d <= viewDistance / grid.tileSize)
        {
            Vector3 dir = grid.TileCenter(t) - grid.TileCenter(Tile);
            dir.y = 0f;
            if (d <= 1.01f || Vector3.Angle(transform.forward, dir) <= viewAngle * 0.5f) return "line of sight";
        }

        int room = grid.RoomOf(t);
        if (d <= sameRoomTiles && room >= 0 && room == grid.RoomOf(Tile)) return "same room nearby";
        return null;
    }

    // ------------------------------------------------------------------ random spawn

    /// <summary>
    /// Random free, reachable tile that is not a key / exit / hiding-spot / player-spawn tile,
    /// at least minSpawnDistance path tiles from the player, and (if spawnSafetyChecks) not in the
    /// player's starting room and not in the player's line of sight.
    /// </summary>
    Vector2Int PickSpawnTile()
    {
        Vector2Int start = grid.NearestWalkable(grid.WorldToTile(player != null ? player.transform.position : Vector3.zero));

        // Path distance (tiles) from the player's spawn.
        var dist = new Dictionary<Vector2Int, int> { { start, 0 } };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (Vector2Int d in dirs)
            {
                Vector2Int n = c + d;
                if (dist.ContainsKey(n) || !grid.CanStep(c, n)) continue;
                dist[n] = dist[c] + 1;
                queue.Enqueue(n);
            }
        }

        var excluded = new HashSet<Vector2Int> { start };
        foreach (KeyPickup k in FindObjectsByType<KeyPickup>())
            excluded.Add(grid.NearestWalkable(grid.WorldToTile(k.transform.position)));
        foreach (HidingSpot s in HidingSpot.All)
        {
            if (s.enterPoint == null) continue;
            Vector2Int st = grid.NearestWalkable(grid.WorldToTile(s.enterPoint.position));
            excluded.Add(st);
            foreach (Vector2Int d in dirs) excluded.Add(st + d); // spot tiles can shift by one when claimed
        }
        foreach (ExitDoor e in ExitDoor.All)
            foreach (Vector2Int t in dist.Keys)
            {
                Vector3 off = grid.TileCenter(t) - e.transform.position;
                off.y = 0f;
                if (off.magnitude < grid.tileSize * 1.5f) excluded.Add(t);
            }

        int startRoom = grid.RoomOf(start);
        var safe = new List<Vector2Int>();
        var anyFree = new List<Vector2Int>();
        foreach (var kv in dist)
        {
            if (excluded.Contains(kv.Key)) continue;
            anyFree.Add(kv.Key);
            if (kv.Value < minSpawnDistance) continue;
            if (spawnSafetyChecks && (grid.RoomOf(kv.Key) == startRoom || grid.LineOfSight(kv.Key, start))) continue;
            safe.Add(kv.Key);
        }

        List<Vector2Int> pool = safe.Count > 0 ? safe : anyFree;
        // Shuffle, then take the first tile the NavMesh can also reach.
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        Vector3 from = grid.TileCenter(start);
        var path = new UnityEngine.AI.NavMeshPath();
        foreach (Vector2Int t in pool)
        {
            if (UnityEngine.AI.NavMesh.CalculatePath(from, grid.TileCenter(t), UnityEngine.AI.NavMesh.AllAreas, path)
                && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
            {
                Debug.Log($"[Killer] Spawn tile {t} (world {grid.TileCenter(t)}), path distance {dist[t]} from player spawn {start}, safe candidates {safe.Count}");
                return t;
            }
        }
        Vector2Int fallback = pool.Count > 0 ? pool[0] : grid.NearestWalkable(grid.WorldToTile(transform.position));
        Debug.Log($"[Killer] Spawn tile {fallback} (fallback, NavMesh check failed)");
        return fallback;
    }

    /// <summary>Called by the GameManager after each key.</summary>
    public void AddSpeedBonus()
    {
        tilesPerTurn = Mathf.Min(maxTilesPerTurn, tilesPerTurn + tilesPerKey);
    }
}
