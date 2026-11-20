using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Real-time clown killer. He is not in the level during the hiding phase.
/// When the chase phase starts (BeginHunt) he spawns on a random tile far from the player and visits
/// EVERY hiding spot once, in random order. At each spot he stands still, raises a hand and a progress bar
/// fills above the spot (checkTime seconds). When the bar is full: if the player is inside, the player is
/// found; otherwise he walks to the next spot. A spot is never checked twice.
/// Two exceptions: if he sees the player in the open he chases and catches them, and if he sees the player
/// go into a spot he goes straight to that spot.
/// </summary>
public class KillerAI : MonoBehaviour
{
    public enum State { Waiting, Search, Check, Chase }

    public static KillerAI Instance { get; private set; }

    [Header("Movement (world units per second, one tile = 2 units)")]
    public float walkSpeed = 6.5f;
    public float chaseSpeed = 7.5f;

    [Header("Senses")]
    public float viewDistance = 13f;
    [Tooltip("Full width of the vision cone in degrees.")]
    public float viewAngle = 110f;
    public float eyeHeight = 2.4f;
    [Tooltip("What blocks line of sight.")]
    public LayerMask obstacleMask = 1;
    [Tooltip("Same room and this close (tiles) = he notices the player going into a spot, even from behind.")]
    public float sameRoomTiles = 5f;

    [Header("Checking a hiding spot")]
    [Tooltip("Seconds the progress bar takes to fill.")]
    public float checkTime = 2.5f;
    [Tooltip("How far the raised hand goes up (degrees).")]
    public float raiseAngle = 105f;

    [Header("Chasing a player he can see")]
    public float catchDistance = 2.2f;
    [Tooltip("Seconds without seeing the player before he goes back to checking spots.")]
    public float loseSightTime = 3f;

    [Header("Random spawn")]
    [Tooltip("Minimum path distance (tiles) from the player. 0 = anywhere.")]
    public int minSpawnDistance = 12;
    [Tooltip("Never spawn in the player's room or in the player's line of sight.")]
    public bool spawnSafetyChecks = true;

    [Header("Sounds")]
    [Tooltip("Distance walked per footstep sound.")]
    public float stepStride = 2.6f;
    [Tooltip("Seconds between clown horn honks (random between the two).")]
    public Vector2 honkInterval = new Vector2(6f, 12f);
    [Tooltip("Seconds between breaths (random between the two), only when he is close.")]
    public Vector2 breathInterval = new Vector2(3f, 5f);
    [Tooltip("He breathes when closer than this (world units).")]
    public float breathDistance = 26f;

    [Tooltip("Unused since the turn-based game was removed (kept so the scene keeps its data).")]
    public Transform[] waypoints;

    public State CurrentState { get; private set; } = State.Waiting;
    /// <summary>True once he has spawned (chase phase).</summary>
    public bool IsHunting => hunting;
    /// <summary>The tile he stands on (-999 before he spawns).</summary>
    public Vector2Int Tile => hunting && grid != null ? grid.WorldToTile(transform.position) : new Vector2Int(-999, -999);
    /// <summary>Flat distance to the player (999 before he spawns). Used by the UI vignette and the heartbeat.</summary>
    public float DistanceToPlayer { get; private set; } = 999f;
    /// <summary>The spot being checked right now, or null.</summary>
    public HidingSpot CheckingSpot { get; private set; }
    /// <summary>0..1 fill of the check bar.</summary>
    public float CheckProgress { get; private set; }
    /// <summary>Did he see the player go into the spot? (used by the UI and the music)</summary>
    public bool LastHideWitnessed { get; private set; }
    public string LastHideReason { get; private set; } = "none";

    GridManager grid;
    PlayerController player;
    Renderer[] renderers;
    Light[] lights;
    Transform armPivot;
    float armAngle;

    bool hunting;
    readonly List<HidingSpot> queue = new List<HidingSpot>();
    HidingSpot targetSpot;
    HidingSpot witnessedSpot;
    List<Vector2Int> path;
    int pathIndex;
    float checkTimer;
    float searchSoundTimer;
    float chaseLoseTimer;
    float repathTimer;
    Vector2Int lastSeenTile;
    float stepAccum;
    float nextHonk;
    float nextBreath;

    void Awake() { Instance = this; }

    void Start()
    {
        grid = GridManager.Instance;
        player = PlayerController.Instance;

        // New random result every game start / Restart (seeded from the clock).
        Random.InitState(unchecked((int)System.DateTime.Now.Ticks));

        renderers = GetComponentsInChildren<Renderer>(true);
        lights = GetComponentsInChildren<Light>(true);
        SetupArm();

        // Not in the level until the chase phase starts: invisible and parked under the map.
        SetVisible(false);
        transform.position = new Vector3(0f, -100f, 0f);
    }

    void Update()
    {
        if (player == null) return;
        if (!hunting)
        {
            DistanceToPlayer = 999f;
            return;
        }

        Vector3 flat = player.transform.position - transform.position;
        flat.y = 0f;
        DistanceToPlayer = flat.magnitude;

        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.IsPlaying || gm.IsPaused) return;
        float dt = Time.deltaTime;

        // Seeing the player in the open always starts a chase.
        bool sees = CanSeePlayer();
        if (sees && CurrentState != State.Chase) EnterChase();

        switch (CurrentState)
        {
            case State.Search: UpdateSearch(dt); break;
            case State.Check: UpdateCheck(dt); break;
            case State.Chase: UpdateChase(dt, sees); break;
        }

        UpdateArm(dt);
        UpdateIdleSounds();
    }

    // ------------------------------------------------------------------ hunt start

    /// <summary>Called by the GameManager when the chase phase starts.</summary>
    public void BeginHunt()
    {
        if (hunting || grid == null || player == null) return;
        hunting = true;

        Vector2Int spawn = PickSpawnTile(player.Tile);
        transform.position = grid.TileCenter(spawn);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);
        SetVisible(true);

        // Every hiding spot once, in random order.
        queue.Clear();
        foreach (HidingSpot s in HidingSpot.All) if (s.enterPoint != null) queue.Add(s);
        for (int i = queue.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }

        nextHonk = Time.time + Random.Range(honkInterval.x, honkInterval.y);
        nextBreath = Time.time + Random.Range(breathInterval.x, breathInterval.y);
        AudioManager.Play(AudioManager.Sfx.KillerSpawn);
        Debug.Log($"[Killer] Spawned on tile {spawn}; {queue.Count} hiding spots to check in random order.");
        GoToNextSpot();
    }

    // ------------------------------------------------------------------ search / check

    Vector2Int CurrentTile => grid.WorldToTile(transform.position);

    /// <summary>Picks the next unchecked spot and a path to it. Spots he cannot reach are skipped.</summary>
    void GoToNextSpot()
    {
        CheckingSpot = null;
        CheckProgress = 0f;
        targetSpot = null;
        path = null;
        pathIndex = 0;
        CurrentState = State.Search;

        while (queue.Count > 0)
        {
            HidingSpot s = queue[0];
            queue.RemoveAt(0);
            if (s == null || (s.IsChecked && s != witnessedSpot)) continue; // never the same spot twice

            List<Vector2Int> p = grid.FindPath(CurrentTile, s.Tile);
            if (p == null) continue; // unreachable
            targetSpot = s;
            path = p;
            return;
        }
        // No spot left: he just stands there until the timer ends.
    }

    void UpdateSearch(float dt)
    {
        if (targetSpot == null || path == null) return;
        if (Follow(walkSpeed, dt)) ArriveAtSpot();
    }

    void ArriveAtSpot()
    {
        CurrentState = State.Check;
        CheckingSpot = targetSpot;
        checkTimer = 0f;
        CheckProgress = 0f;
        searchSoundTimer = 0f;
        FaceTowards(targetSpot.transform.position);
    }

    void UpdateCheck(float dt)
    {
        if (CheckingSpot == null)
        {
            GoToNextSpot();
            return;
        }
        checkTimer += dt;
        CheckProgress = Mathf.Clamp01(checkTimer / checkTime);

        // Rummaging / creaking sound while the bar fills.
        searchSoundTimer -= dt;
        if (searchSoundTimer <= 0f)
        {
            AudioManager.PlayAt(AudioManager.Sfx.KillerSearch, transform.position);
            searchSoundTimer = 0.95f;
        }

        if (checkTimer >= checkTime) FinishCheck();
    }

    void FinishCheck()
    {
        HidingSpot spot = CheckingSpot;
        spot.MarkChecked();
        if (spot == witnessedSpot) witnessedSpot = null;
        CheckingSpot = null;
        CheckProgress = 0f;

        if (spot.isOccupied)
        {
            if (GameManager.Instance) GameManager.Instance.Lose("The clown found your hiding spot.");
            return;
        }
        GoToNextSpot();
    }

    // ------------------------------------------------------------------ chase (he sees the player in the open)

    void EnterChase()
    {
        // Remember the spot he was heading to / checking: it still has to be checked later.
        HidingSpot interrupted = CheckingSpot != null ? CheckingSpot : targetSpot;
        if (interrupted != null && (!interrupted.IsChecked || interrupted == witnessedSpot)) queue.Insert(0, interrupted);
        targetSpot = null;
        CheckingSpot = null;
        CheckProgress = 0f;

        CurrentState = State.Chase;
        chaseLoseTimer = loseSightTime;
        repathTimer = 0f;
        path = null;
        lastSeenTile = player.Tile;
        AudioManager.Play(AudioManager.Sfx.DangerSting); // spotted!
        AudioManager.PlayAt(AudioManager.Sfx.KillerHonk, transform.position);
    }

    void UpdateChase(float dt, bool sees)
    {
        if (sees)
        {
            chaseLoseTimer = loseSightTime;
            lastSeenTile = player.Tile;
        }
        else chaseLoseTimer -= dt;

        // He saw the player go into a spot: no need to wait, go and open it.
        bool sawHide = !sees && witnessedSpot != null && player.IsHidden;
        if (chaseLoseTimer <= 0f || sawHide)
        {
            GoToNextSpot(); // lost the player: back to checking spots
            return;
        }

        repathTimer -= dt;
        if (repathTimer <= 0f || path == null)
        {
            repathTimer = 0.3f;
            path = grid.FindPath(CurrentTile, lastSeenTile);
            pathIndex = 0;
        }
        if (path != null) Follow(chaseSpeed, dt);

        Vector3 flat = player.transform.position - transform.position;
        flat.y = 0f;
        if (sees && !player.IsHidden && flat.magnitude < catchDistance)
        {
            FaceTowards(player.transform.position);
            if (GameManager.Instance) GameManager.Instance.Lose("The clown caught you.");
        }
    }

    // ------------------------------------------------------------------ movement helpers

    /// <summary>Walks along the path. Returns true when the end of the path is reached.</summary>
    bool Follow(float speed, float dt)
    {
        if (path == null) return true;
        float move = speed * dt;
        Vector3 before = transform.position;

        while (move > 0f && pathIndex < path.Count)
        {
            Vector3 target = grid.TileCenter(path[pathIndex]);
            target.y = transform.position.y;
            Vector3 to = target - transform.position;
            to.y = 0f;
            float d = to.magnitude;
            if (d > 0.001f)
            {
                Quaternion want = Quaternion.LookRotation(to);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 540f * dt);
            }
            if (d <= move)
            {
                transform.position = target;
                move -= d;
                pathIndex++;
            }
            else
            {
                transform.position += to / d * move;
                move = 0f;
            }
        }

        // Heavy footsteps.
        stepAccum += Vector3.Distance(before, transform.position);
        if (stepAccum >= stepStride)
        {
            stepAccum = 0f;
            AudioManager.PlayAt(AudioManager.Sfx.KillerStep, transform.position);
        }

        return pathIndex >= path.Count;
    }

    void FaceTowards(Vector3 p)
    {
        Vector3 look = p - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
    }

    void UpdateIdleSounds()
    {
        float now = Time.time;
        if (now >= nextHonk)
        {
            nextHonk = now + Random.Range(honkInterval.x, honkInterval.y);
            if (CurrentState != State.Chase) AudioManager.PlayAt(AudioManager.Sfx.KillerHonk, transform.position);
        }
        if (now >= nextBreath)
        {
            nextBreath = now + Random.Range(breathInterval.x, breathInterval.y);
            if (DistanceToPlayer < breathDistance) AudioManager.PlayAt(AudioManager.Sfx.KillerBreath, transform.position);
        }
    }

    // ------------------------------------------------------------------ model: visibility + raised hand

    void SetVisible(bool visible)
    {
        if (renderers != null) foreach (Renderer r in renderers) if (r) r.enabled = visible;
        if (lights != null) foreach (Light l in lights) if (l) l.enabled = visible;
    }

    /// <summary>Puts the right arm and glove under a shoulder pivot so the hand can be raised.</summary>
    void SetupArm()
    {
        Transform visual = transform.Find("Visual");
        if (visual == null) return;
        Transform arm = visual.Find("ArmR");
        if (arm == null) return;
        Transform glove = visual.Find("GloveR");

        var pivot = new GameObject("ArmRPivot").transform;
        pivot.SetParent(visual, false);
        pivot.localPosition = new Vector3(0.6f, 2.0f, 0.02f); // shoulder
        pivot.localRotation = Quaternion.identity;
        arm.SetParent(pivot, true);
        if (glove != null) glove.SetParent(pivot, true);
        armPivot = pivot;
    }

    void UpdateArm(float dt)
    {
        if (armPivot == null) return;
        bool raised = CurrentState == State.Check;
        float target = raised ? -raiseAngle : 0f;
        armAngle = Mathf.MoveTowards(armAngle, target, 420f * dt);
        // Small searching wobble once the hand is up.
        float wobble = raised && Mathf.Abs(armAngle - target) < 1f ? Mathf.Sin(Time.time * 10f) * 9f : 0f;
        armPivot.localRotation = Quaternion.Euler(armAngle + wobble, 0f, 0f);
    }

    // ------------------------------------------------------------------ senses / rules

    /// <summary>True if the player is out in the open, inside the vision cone, with a clear line of sight.</summary>
    public bool CanSeePlayer()
    {
        if (!hunting || player == null || player.IsHidden || grid == null) return false;

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

    /// <summary>
    /// Called by the player when they start hiding (fromTile = where they stood).
    /// If the killer is hunting and saw them go in, he goes straight to that spot, even if it was checked before.
    /// </summary>
    public void NotifyPlayerHid(HidingSpot spot, Vector2Int fromTile)
    {
        LastHideWitnessed = false;
        LastHideReason = "none";
        if (!hunting || spot == null || grid == null) return;

        string reason = WitnessReason(fromTile) ?? WitnessReason(spot.Tile);
        LastHideWitnessed = reason != null;
        LastHideReason = reason ?? "none";
        Debug.Log($"Hide witnessed: {LastHideWitnessed.ToString().ToLower()}, reason: {LastHideReason}, killer tile {Tile}, player tile {fromTile} (spot {spot.name} tile {spot.Tile})");
        if (reason == null) return;

        witnessedSpot = spot;
        queue.Remove(spot);
        if (CurrentState == State.Search && targetSpot != null && targetSpot != spot) queue.Insert(0, targetSpot);
        queue.Insert(0, spot);
        // Busy checking another spot: he finishes that first. Otherwise he turns around now.
        if (CurrentState == State.Search || CurrentState == State.Chase) GoToNextSpot();
    }

    /// <summary>
    /// Why the killer notices the player on tile t, or null if he does not:
    /// "line of sight" = tile line of sight inside the vision range and cone,
    /// "same room nearby" = same room, within sameRoomTiles (even from behind).
    /// </summary>
    string WitnessReason(Vector2Int t)
    {
        if (grid == null) return null;
        Vector2Int me = Tile;
        float d = Vector2Int.Distance(me, t);
        if (!grid.LineOfSight(me, t)) return null;

        if (d <= viewDistance / grid.tileSize)
        {
            Vector3 dir = grid.TileCenter(t) - grid.TileCenter(me);
            dir.y = 0f;
            if (d <= 1.01f || Vector3.Angle(transform.forward, dir) <= viewAngle * 0.5f) return "line of sight";
        }

        int room = grid.RoomOf(t);
        if (d <= sameRoomTiles && room >= 0 && room == grid.RoomOf(me)) return "same room nearby";
        return null;
    }

    // ------------------------------------------------------------------ random spawn

    /// <summary>
    /// Random free, reachable tile that is not a hiding-spot / exit tile, at least minSpawnDistance path tiles
    /// from the player, and (if spawnSafetyChecks) not in the player's room and not in the player's line of sight.
    /// </summary>
    Vector2Int PickSpawnTile(Vector2Int start)
    {
        // Path distance (tiles) from the player.
        var dist = new Dictionary<Vector2Int, int> { { start, 0 } };
        var queueTiles = new Queue<Vector2Int>();
        queueTiles.Enqueue(start);
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        while (queueTiles.Count > 0)
        {
            Vector2Int c = queueTiles.Dequeue();
            foreach (Vector2Int d in dirs)
            {
                Vector2Int n = c + d;
                if (dist.ContainsKey(n) || !grid.CanStep(c, n)) continue;
                dist[n] = dist[c] + 1;
                queueTiles.Enqueue(n);
            }
        }

        var excluded = new HashSet<Vector2Int> { start };
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
        if (pool.Count == 0) return grid.NearestWalkable(start);
        Vector2Int pick = pool[Random.Range(0, pool.Count)];
        Debug.Log($"[Killer] Spawn tile {pick}, path distance {dist[pick]} from the player, safe candidates {safe.Count}");
        return pick;
    }
}
