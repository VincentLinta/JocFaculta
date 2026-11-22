using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Real-time player on the tile grid.
/// Left click a tile: walk there along the shortest path (click again to change your mind).
/// Hold Space or Left Shift: run (uses stamina). E: hide in a nearby spot / leave it.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement (world units per second, one tile = 2 units)")]
    public float walkSpeed = 4.5f;
    public float runSpeed = 8f;
    [Tooltip("Degrees per second the body turns towards the walking direction.")]
    public float turnSpeed = 720f;
    [Tooltip("Height of the capsule's center above the floor.")]
    public float bodyHeight = 1f;
    [Tooltip("Distance walked per footstep sound.")]
    public float stride = 2.2f;

    [Header("Stamina (used for running)")]
    public float maxStamina = 100f;
    [Tooltip("Stamina lost per second while running (25 = 4 seconds from full).")]
    public float runDrain = 25f;
    [Tooltip("Stamina gained per second while standing still.")]
    public float regenRate = 12f;
    [Tooltip("Stamina gained per second while hidden.")]
    public float hiddenRegenRate = 30f;
    [Tooltip("After running out you can run again once stamina is back to this level.")]
    public float runRecover = 25f;

    [Header("Hiding")]
    public float hideMoveTime = 0.35f;
    [Tooltip("If on, once the player is hidden he can never leave the hiding spot.")]
    public bool lockInHidingSpot = true;
    /// <summary>True when the player is hidden and not allowed to leave.</summary>
    public bool IsLockedIn => IsHidden && lockInHidingSpot;

    /// <summary>The tile the player stands on (the spot's tile while hidden).</summary>
    public Vector2Int Tile { get; private set; }
    public float Stamina { get; private set; }
    public bool IsRunning { get; private set; }
    /// <summary>Used by the body animation (leans forward while running).</summary>
    public bool IsDashMove => IsRunning;
    public bool IsHidden { get; private set; }
    /// <summary>True while the hide / leave animation plays.</summary>
    public bool IsBusy { get; private set; }
    public HidingSpot CurrentSpot { get; private set; }
    /// <summary>A free hiding spot on or next to the player's tile, or null.</summary>
    public HidingSpot AvailableSpot { get; private set; }
    /// <summary>A cabinet / dresser in reach that can be searched with E (only when no hiding spot is in reach), or null.</summary>
    public SearchableObject AvailableSearchable { get; private set; }
    /// <summary>True while the player runs to the escape exit by himself (no clicking).</summary>
    public bool IsEscaping => autoEscape;
    bool autoEscape;

    // The player holds ONE card at a time: finding another one lets him choose which to keep.
    CardType? heldCard;
    public CardType? HeldCard => heldCard;
    /// <summary>The card the player holds (as a list of one, or empty).</summary>
    public IEnumerable<KeyValuePair<CardType, int>> Cards
    {
        get { if (heldCard.HasValue) yield return new KeyValuePair<CardType, int>(heldCard.Value, 1); }
    }
    public int CardCount(CardType t) => heldCard.HasValue && heldCard.Value == t ? 1 : 0;
    /// <summary>Takes a card. Returns the card that was replaced, or null if the hands were empty.</summary>
    public CardType? AddCard(CardType t)
    {
        CardType? old = heldCard;
        heldCard = t;
        return old;
    }
    // ---- Card choice: found a card while already holding another one ----
    public const float ChoiceIntro = 0.4f;    // seconds the cards need to fly in
    public const float ChoiceOutro = 0.3f;    // seconds of the pick animation
    /// <summary>True while the "keep yours or take the new one" screen is open.</summary>
    public bool CardChoiceActive { get; private set; }
    public CardType ChoiceHeld { get; private set; }
    public CardType ChoiceFound { get; private set; }
    /// <summary>Seconds since the choice opened (real time, drives the UI animation).</summary>
    public float ChoiceTime { get; private set; }
    /// <summary>-1 = still deciding, 0 = keep the card in hand, 1 = take the found card.</summary>
    public int ChoicePicked { get; private set; } = -1;
    /// <summary>Seconds since the pick (0..ChoiceOutro).</summary>
    public float ChoicePickTime { get; private set; }
    public bool ChoiceCanPick => CardChoiceActive && ChoicePicked < 0 && ChoiceTime >= ChoiceIntro;

    /// <summary>
    /// Shows both cards and waits for the player to decide (1 / Left / A = keep, 2 / Right / D = take the new one,
    /// or click a card). The game keeps running meanwhile. onDone(true) = the found card was taken.
    /// </summary>
    public IEnumerator ChooseCard(CardType found, System.Action<bool> onDone)
    {
        GameManager gm = GameManager.Instance;
        if (!heldCard.HasValue) { heldCard = found; onDone?.Invoke(true); yield break; }

        ChoiceHeld = heldCard.Value;
        ChoiceFound = found;
        ChoiceTime = 0f;
        ChoicePickTime = 0f;
        ChoicePicked = -1;
        CardChoiceActive = true;

        while (gm != null && gm.IsPlaying && ChoicePicked < 0)
        {
            ChoiceTime += Time.unscaledDeltaTime;
            Keyboard kb = Keyboard.current;
            if (kb != null && !gm.IsPaused)
            {
                if (kb.digit1Key.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) PickCard(false);
                else if (kb.digit2Key.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) PickCard(true);
            }
            yield return null;
        }

        // Pick animation (cards fly to the HUD / get thrown away).
        while (ChoicePicked >= 0 && ChoicePickTime < ChoiceOutro && gm != null && gm.IsPlaying)
        {
            ChoicePickTime += Time.unscaledDeltaTime;
            yield return null;
        }

        bool tookNew = ChoicePicked == 1;
        if (tookNew) heldCard = found;
        CardChoiceActive = false;
        ChoicePicked = -1;
        onDone?.Invoke(tookNew);
    }

    /// <summary>Called by the keys above and by the card buttons. takeNew = true takes the found card.</summary>
    public void PickCard(bool takeNew)
    {
        GameManager gm = GameManager.Instance;
        if (!ChoiceCanPick || gm == null || gm.IsPaused) return;
        ChoicePicked = takeNew ? 1 : 0;
        ChoicePickTime = 0f;
        AudioManager.Play(takeNew ? AudioManager.Sfx.AllKeys : AudioManager.Sfx.Key);
    }

    /// <summary>Uses up the held card if it is this type. Returns false otherwise.</summary>
    public bool TryUseCard(CardType t)
    {
        if (!heldCard.HasValue || heldCard.Value != t) return false;
        heldCard = null;
        return true;
    }

    GridManager grid;
    Camera cam;
    Renderer[] renderers;
    Light[] lights;

    List<Vector2Int> path;
    int pathIndex;
    bool exhausted;
    float stepAccum;

    Vector2Int previewGoal = new Vector2Int(-999, -999);
    Vector2Int previewFrom = new Vector2Int(-999, -999);
    List<Vector2Int> previewPath;

    void Awake()
    {
        Instance = this;
        renderers = GetComponentsInChildren<Renderer>();
        lights = GetComponentsInChildren<Light>();
        Stamina = maxStamina;
    }

    void Start()
    {
        cam = Camera.main;
        grid = GridManager.Instance;
        if (FindFirstObjectByType<SearchableSpawner>() == null)
            new GameObject("Searchables").AddComponent<SearchableSpawner>(); // cabinets to search with E
        Tile = grid.NearestWalkable(grid.WorldToTile(transform.position));
        transform.position = TilePosition(Tile);
    }

    Vector3 TilePosition(Vector2Int t) => grid.TileCenter(t) + Vector3.up * bodyHeight;

    void Update()
    {
        if (grid == null) return;
        GameManager gm = GameManager.Instance;
        AvailableSpot = IsHidden ? null : FindSpot();
        AvailableSearchable = (IsHidden || AvailableSpot != null) ? null : FindSearchable();

        bool canAct = gm != null && gm.CanPlayerAct && !IsBusy;
        if (!canAct)
        {
            grid.ClearHighlights();
            IsRunning = false;
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            HideOrExit();
            return;
        }
        if (kb != null && kb.qKey.wasPressedThisFrame)
        {
            UseEscapeCard();
            return;
        }
        if (kb != null && kb.fKey.wasPressedThisFrame)
        {
            if (IsHidden) TriggerDecoy();
            else if (CardCount(CardType.Girlfriend) > 0) PlaceGirlfriend();
            else PlaceDecoy();
            return;
        }

        if (IsHidden)
        {
            grid.ClearHighlights();
            IsRunning = false;
            Stamina = Mathf.Min(maxStamina, Stamina + hiddenRegenRate * Time.deltaTime);
            return;
        }

        // Escape run: follow the path to the exit on his own (runs while stamina lasts).
        if (autoEscape)
        {
            grid.ClearHighlights();
            Move(true);
            if (path == null)
            {
                autoEscape = false;
                gm.Win("You escaped! The clown was too late.");
            }
            return;
        }

        bool runKey = kb != null && (kb.spaceKey.isPressed || kb.leftShiftKey.isPressed);
        UpdateMouse();
        Move(runKey);
    }

    // ------------------------------------------------------------------ mouse + walking

    void UpdateMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || cam == null) return;

        // Clicks on buttons must not move the player.
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector2Int? hovered = null;
        if (!overUI)
        {
            Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist))
            {
                Vector2Int t = grid.WorldToTile(ray.GetPoint(dist));
                if (grid.IsWalkable(t)) hovered = t;
            }
        }
        grid.ShowHover(hovered);

        // While walking show the rest of the path; otherwise the path to the tile under the mouse.
        if (path != null)
        {
            grid.ShowPath(path.GetRange(pathIndex, path.Count - pathIndex));
        }
        else if (hovered.HasValue && hovered.Value != Tile)
        {
            if (previewGoal != hovered.Value || previewFrom != Tile)
            {
                previewGoal = hovered.Value;
                previewFrom = Tile;
                previewPath = grid.FindPath(Tile, hovered.Value);
            }
            grid.ShowPath(previewPath);
        }
        else grid.ShowPath(null);

        if (hovered.HasValue && hovered.Value != Tile && mouse.leftButton.wasPressedThisFrame)
            StartWalk(hovered.Value);
    }

    void StartWalk(Vector2Int goal)
    {
        List<Vector2Int> p = grid.FindPath(Tile, goal);
        if (p == null || p.Count == 0) return;
        path = p;
        pathIndex = 0;
    }

    void Move(bool runKey)
    {
        float dt = Time.deltaTime;
        bool walking = path != null && pathIndex < path.Count;
        IsRunning = false;

        if (!walking)
        {
            path = null;
            Regen(regenRate * dt);
            return;
        }

        bool run = runKey && !exhausted && Stamina > 0f;
        float speed = walkSpeed;
        if (run)
        {
            speed = runSpeed;
            IsRunning = true;
            Stamina = Mathf.Max(0f, Stamina - runDrain * dt);
            if (Stamina <= 0f) exhausted = true;
        }
        else Regen(regenRate * 0.5f * dt); // slow recovery while walking

        Vector3 pos = transform.position;
        Vector3 target = TilePosition(path[pathIndex]);
        Vector3 next = Vector3.MoveTowards(pos, target, speed * dt);

        Vector3 dir = target - pos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * dt);
        transform.position = next;
        Tile = grid.WorldToTile(next);

        stepAccum += Vector3.Distance(pos, next);
        float strideNow = run ? stride * 1.3f : stride;
        if (stepAccum >= strideNow)
        {
            stepAccum = 0f;
            AudioManager.Play(AudioManager.Sfx.Footstep);
        }

        if ((next - target).sqrMagnitude < 0.0004f)
        {
            pathIndex++;
            if (pathIndex >= path.Count) path = null;
        }
    }

    void Regen(float amount)
    {
        Stamina = Mathf.Min(maxStamina, Stamina + amount);
        if (exhausted && Stamina >= runRecover) exhausted = false;
    }

    // ------------------------------------------------------------------ hiding (E key / HIDE button)

    /// <summary>Hides in the nearby spot, or leaves the current one.</summary>
    public void HideOrExit()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.CanPlayerAct || IsBusy) return;

        // Escape card active and standing at the opened exit: E gets you out.
        if (gm.EscapeActive && gm.EscapeDoor != null && gm.EscapeDoor.PlayerNear)
        {
            gm.Win("You escaped! The clown was too late.");
            return;
        }
        if (autoEscape) return; // already running for the exit

        if (IsHidden)
        {
            if (!lockInHidingSpot) StartCoroutine(ExitAction());
        }
        else if (AvailableSpot != null) StartCoroutine(EnterAction(AvailableSpot));
        else if (AvailableSearchable != null) StartCoroutine(SearchAction(AvailableSearchable));
    }

    /// <summary>
    /// Q: uses the Escape card. The player leaves his hiding spot (even though it is locked) and runs to the
    /// open exit by himself, while the clown rushes him.
    /// </summary>
    public void UseEscapeCard()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.CanPlayerAct || IsBusy || autoEscape || gm.EscapeActive || gm.IsRevealing) return;
        if (CardCount(CardType.Escape) <= 0) { EscapeHint("You need an Escape card to escape."); return; }
        if (!gm.EscapeArmed) { EscapeHint("The escape door opens when the clown starts searching."); return; }
        if (IsHidden && !TryGetExitTile(CurrentSpot, out Vector2Int _)) { EscapeHint("The clown blocks your way out!"); return; }

        Debug.Log("[Escape] Q pressed: using the Escape card.");
        TryUseCard(CardType.Escape);
        StartCoroutine(EscapeRun(gm));
        gm.ActivateEscape();
    }

    // ------------------------------------------------------------------ Decoy card

    HidingSpot placedDecoy;
    bool decoyTriggered;

    /// <summary>True while a placed decoy is waiting: from a hiding spot, F sends the clown to it.</summary>
    public bool CanTriggerDecoy => placedDecoy != null && placedDecoy.HasDecoy && !decoyTriggered;

    /// <summary>
    /// F next to a hiding spot (not hidden): puts the Decoy card there. The card is used up, but nothing happens yet:
    /// hide somewhere, then press F again to make the clown go to the decoy.
    /// </summary>
    public void PlaceDecoy()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.CanPlayerAct || IsBusy || IsHidden || autoEscape || CardCount(CardType.Decoy) <= 0) return;
        if (AvailableSpot == null)
        {
            SearchableObject.ShowToast("Go next to a hiding spot to place the decoy.", new Color(0.75f, 0.95f, 0.45f), 2.5f);
            return;
        }
        if (CanTriggerDecoy)
        {
            SearchableObject.ShowToast("Your other decoy is still waiting.", new Color(0.75f, 0.95f, 0.45f), 2.5f);
            return;
        }
        HidingSpot spot = AvailableSpot;
        TryUseCard(CardType.Decoy); // used up when placed
        spot.PlaceDecoyMarker();
        placedDecoy = spot;
        decoyTriggered = false;
        SearchableObject.RemoveDecoyCards(); // no more decoys to find in the cabinets
        AudioManager.Play(AudioManager.Sfx.Rustle);
        SearchableObject.ShowToast("Decoy placed. Hide, then press F to send the clown to it.", new Color(0.75f, 0.95f, 0.45f), 4f);
    }

    /// <summary>F while hidden: the clown goes straight to the decoy, then carries on searching normally. Returns true if it did.</summary>
    public bool TriggerDecoy()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.CanPlayerAct || IsBusy || !IsHidden || autoEscape || gm.EscapeActive) return false;
        if (placedDecoy == null || decoyTriggered) return false; // nothing to trigger
        if (!placedDecoy.HasDecoy)
        {
            SearchableObject.ShowToast("The decoy is gone.", new Color(0.75f, 0.95f, 0.45f), 2.5f);
            return false;
        }
        decoyTriggered = true;
        if (KillerAI.Instance != null) KillerAI.Instance.LureTo(placedDecoy);
        AudioManager.Play(AudioManager.Sfx.Rustle);
        SearchableObject.ShowToast("Decoy triggered! The clown is heading there.", new Color(0.75f, 0.95f, 0.45f), 3.5f);
        return true;
    }

    // ------------------------------------------------------------------ Girlfriend card

    bool NearAnyHidingSpot(Vector2Int t)
    {
        foreach (HidingSpot s in HidingSpot.All)
            if (s.enterPoint != null && s.IsInReach(t, grid)) return true;
        return false;
    }

    /// <summary>
    /// F with a Girlfriend card (not hidden): puts her down in front of the player, anywhere except next to a
    /// hiding spot. She is a trap: when the clown comes close he is stunned.
    /// </summary>
    public void PlaceGirlfriend()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.CanPlayerAct || IsBusy || IsHidden || autoEscape || CardCount(CardType.Girlfriend) <= 0) return;
        Color pink = CardInfo.Tint(CardType.Girlfriend);

        // One tile in front of the player if possible, otherwise on his own tile.
        Vector2Int tile = Tile;
        Vector3 f = transform.forward;
        Vector2Int dir = Mathf.Abs(f.x) > Mathf.Abs(f.z)
            ? new Vector2Int(f.x > 0f ? 1 : -1, 0)
            : new Vector2Int(0, f.z > 0f ? 1 : -1);
        Vector2Int ahead = Tile + dir;
        if (grid.CanStep(Tile, ahead) && !NearAnyHidingSpot(ahead)) tile = ahead;

        if (NearAnyHidingSpot(tile))
        {
            SearchableObject.ShowToast("Too close to a hiding spot - put her somewhere else.", pink, 2.5f);
            return;
        }

        TryUseCard(CardType.Girlfriend); // used up when placed
        Girlfriend.Create(grid.TileCenter(tile), tile);
        SearchableObject.RemoveCardsOfType(CardType.Girlfriend); // no more Girlfriend cards to find
        AudioManager.Play(AudioManager.Sfx.Rustle);
        SearchableObject.ShowToast("Girlfriend placed. If the clown walks past her, he gets stunned.", pink, 4f);
    }

    static void EscapeHint(string message)
    {
        Debug.Log("[Escape] " + message);
        SearchableObject.ShowToast(message, new Color(1f, 0.8f, 0.4f), 2.5f);
    }

    IEnumerator EscapeRun(GameManager gm)
    {
        if (IsHidden) yield return StartCoroutine(ExitAction());

        // Go to the reachable tile that is closest to the open exit.
        Vector3 door = gm.EscapeDoor.transform.position;
        Dictionary<Vector2Int, Vector2Int> reach = grid.Reachable(Tile, int.MaxValue);
        Vector2Int goal = Tile;
        float best = float.MaxValue;
        foreach (KeyValuePair<Vector2Int, Vector2Int> kv in reach)
        {
            Vector3 c = grid.TileCenter(kv.Key);
            float d = (c.x - door.x) * (c.x - door.x) + (c.z - door.z) * (c.z - door.z);
            if (d < best)
            {
                best = d;
                goal = kv.Key;
            }
        }

        List<Vector2Int> p = grid.FindPath(Tile, goal);
        if (p == null)
        {
            SearchableObject.ShowToast("No way to the exit!", Color.red, 3f);
            yield break;
        }
        path = p;
        pathIndex = 0;
        autoEscape = true;
    }

    IEnumerator SearchAction(SearchableObject obj)
    {
        IsBusy = true;
        IsRunning = false;
        path = null;
        grid.ClearHighlights();

        // Turn towards the object, then stand still while searching.
        Vector3 look = obj.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
            yield return MoveTo(transform.position, Quaternion.LookRotation(look), 0.2f);
        yield return obj.SearchRoutine(this);
        IsBusy = false;
    }

    /// <summary>Adds stamina (used by items found in cabinets).</summary>
    public void RestoreStamina(float amount)
    {
        Stamina = Mathf.Min(maxStamina, Stamina + amount);
        exhausted = false;
    }

    SearchableObject FindSearchable()
    {
        SearchableObject best = null;
        float bestDist = float.MaxValue;
        foreach (SearchableObject o in SearchableObject.All)
        {
            if (o.IsSearched || !o.IsInReach(Tile, grid)) continue;
            float d = Vector3.Distance(transform.position, o.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = o;
            }
        }
        return best;
    }

    IEnumerator EnterAction(HidingSpot spot)
    {
        if (spot.isOccupied) yield break; // one person per spot
        IsBusy = true;
        IsRunning = false;
        path = null;
        grid.ClearHighlights();

        // The killer (if he is already hunting) decides whether he saw us go in.
        if (KillerAI.Instance != null) KillerAI.Instance.NotifyPlayerHid(spot, Tile);
        spot.Enter();
        CurrentSpot = spot;
        AudioManager.Play(AudioManager.Sfx.Rustle);

        // Glide straight from where we stand into the hide point (we may be on a tile next to the object).
        Tile = spot.Tile;
        yield return MoveTo(spot.hidePoint.position, spot.hidePoint.rotation, hideMoveTime * 1.5f);
        SetVisible(false);
        IsHidden = true;
        IsBusy = false;
    }

    IEnumerator ExitAction()
    {
        HidingSpot spot = CurrentSpot;
        if (!TryGetExitTile(spot, out Vector2Int exitTile)) yield break; // killer is blocking the way out
        IsBusy = true;

        SetVisible(true);
        IsHidden = false;
        AudioManager.Play(AudioManager.Sfx.Rustle);
        yield return MoveTo(TilePosition(exitTile), spot.enterPoint.rotation, hideMoveTime);
        spot.Exit();
        CurrentSpot = null;
        Tile = exitTile;
        IsBusy = false;
    }

    // ------------------------------------------------------------------ helpers

    HidingSpot FindSpot()
    {
        // Same rule as the outline: on the spot's tile, next to it, or right next to the object.
        KillerAI killer = KillerAI.Instance;
        HidingSpot best = null;
        float bestDist = float.MaxValue;
        foreach (HidingSpot spot in HidingSpot.All)
        {
            if (spot.isOccupied || !spot.IsInReach(Tile, grid)) continue;
            if (killer != null && killer.CheckingSpot == spot) continue; // the killer is standing there
            float d = spot.Tile == Tile ? -1f : Vector3.Distance(transform.position, spot.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = spot;
            }
        }
        return best;
    }

    bool TryGetExitTile(HidingSpot spot, out Vector2Int tile)
    {
        Vector2Int killerTile = KillerAI.Instance ? KillerAI.Instance.Tile : new Vector2Int(-999, -999);
        tile = spot.Tile;
        if (tile != killerTile) return true;
        foreach (Vector2Int d in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
        {
            Vector2Int n = spot.Tile + d;
            if (n != killerTile && grid.CanStep(spot.Tile, n))
            {
                tile = n;
                return true;
            }
        }
        return false;
    }

    IEnumerator MoveTo(Vector3 position, Quaternion rotation, float duration)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(startPos, position, k), Quaternion.Slerp(startRot, rotation, k));
            yield return null;
        }
        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>Shows or hides the player's mesh and personal light.</summary>
    public void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
        foreach (Light l in lights) l.enabled = visible;
    }
}
