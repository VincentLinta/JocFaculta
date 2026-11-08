using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Turn-based player on the tile grid.
/// Click a highlighted tile to move (1 tile, or 2 with Dash). E hides / leaves a spot. W waits.
/// Every action ends the player's turn, then the killer moves.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Moves")]
    public int normalRange = 1;
    public int dashRange = 2;
    [Tooltip("Seconds to walk one tile.")]
    public float stepTime = 0.22f;
    [Tooltip("Height of the capsule's center above the floor.")]
    public float bodyHeight = 1f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    [Tooltip("Each dash costs this much (25 = 4 dashes from full).")]
    public float dashCost = 25f;
    [Tooltip("Gained per turn spent hidden.")]
    public float hiddenRegen = 25f;

    [Header("Hiding")]
    public float hideMoveTime = 0.35f;

    public Vector2Int Tile { get; private set; }
    public float Stamina { get; private set; }
    public bool DashArmed { get; private set; }
    public bool CanDash => Stamina >= dashCost - 0.01f;
    /// <summary>True if the last action was a dash (the killer can hear it).</summary>
    public bool DashedLastTurn { get; private set; }
    public bool IsHidden { get; private set; }
    /// <summary>True while an action animation is playing.</summary>
    public bool IsBusy { get; private set; }
    public HidingSpot CurrentSpot { get; private set; }
    /// <summary>A free hiding spot on or next to the player's tile, or null.</summary>
    public HidingSpot AvailableSpot { get; private set; }
    /// <summary>True while a dash move is being animated (used by the body animation).</summary>
    public bool IsDashMove { get; private set; }
    /// <summary>Unscaled time of the last refused dash (UI flashes red after it).</summary>
    public float DashDeniedTime { get; private set; } = -10f;

    GridManager grid;
    Camera cam;
    Renderer[] renderers;
    Light[] lights;

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
        Tile = grid.NearestWalkable(grid.WorldToTile(transform.position));
        transform.position = TilePosition(Tile);
    }

    Vector3 TilePosition(Vector2Int t) => grid.TileCenter(t) + Vector3.up * bodyHeight;

    bool CanActNow()
    {
        GameManager gm = GameManager.Instance;
        return gm != null && gm.CanPlayerAct && !IsBusy;
    }

    void Update()
    {
        if (grid == null) return;
        if (DashArmed && !CanDash) DashArmed = false;
        AvailableSpot = IsHidden ? null : FindSpot();

        if (!CanActNow())
        {
            grid.ClearHighlights();
            return;
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.spaceKey.wasPressedThisFrame) ToggleDash();
            if (kb.wKey.wasPressedThisFrame) { Wait(); return; }
            if (kb.eKey.wasPressedThisFrame) { HideOrExit(); return; }
        }

        UpdateMouse();
    }

    // ------------------------------------------------------------------ mouse + tiles

    void UpdateMouse()
    {
        if (IsHidden)
        {
            grid.ClearHighlights();
            return;
        }

        int range = DashArmed ? dashRange : normalRange;
        Vector2Int? killerTile = KillerAI.Instance ? KillerAI.Instance.Tile : (Vector2Int?)null;
        Dictionary<Vector2Int, Vector2Int> reach = grid.Reachable(Tile, range, killerTile);
        grid.ShowReach(reach.Keys, Tile);

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

        List<Vector2Int> path = null;
        if (hovered.HasValue && hovered.Value != Tile && reach.ContainsKey(hovered.Value))
            path = GridManager.PathTo(reach, Tile, hovered.Value);
        grid.ShowPath(path);

        if (path != null && mouse.leftButton.wasPressedThisFrame)
            StartCoroutine(MoveAction(path));
    }

    // ------------------------------------------------------------------ actions (buttons call these)

    /// <summary>Arms / disarms the dash for the next move.</summary>
    public void ToggleDash()
    {
        if (!CanActNow() || IsHidden) return;
        if (!CanDash)
        {
            // Not enough stamina: buzz + red flash, the turn is NOT used.
            DashArmed = false;
            DashDeniedTime = Time.unscaledTime;
            AudioManager.Play(AudioManager.Sfx.Denied);
            return;
        }
        DashArmed = !DashArmed;
    }

    /// <summary>Skips the turn. While hidden this refills stamina.</summary>
    public void Wait()
    {
        if (!CanActNow()) return;
        StartCoroutine(WaitAction());
    }

    /// <summary>Hides in the nearby spot, or leaves the current one.</summary>
    public void HideOrExit()
    {
        if (!CanActNow()) return;
        if (IsHidden) StartCoroutine(ExitAction());
        else if (AvailableSpot != null) StartCoroutine(EnterAction(AvailableSpot));
    }

    IEnumerator MoveAction(List<Vector2Int> path)
    {
        IsBusy = true;
        grid.ClearHighlights();

        bool dash = path.Count > normalRange;
        if (dash) Stamina = Mathf.Max(0f, Stamina - dashCost);
        DashArmed = false;
        DashedLastTurn = dash;
        IsDashMove = dash;

        foreach (Vector2Int t in path)
        {
            AudioManager.Play(AudioManager.Sfx.Footstep);
            yield return StepTo(t);
            Tile = t;
            yield return null; // let keys / exit react to the new tile
            if (!GameManager.Instance.IsPlaying)
            {
                IsBusy = false;
                IsDashMove = false;
                yield break;
            }
        }
        IsDashMove = false;
        FinishAction();
    }

    IEnumerator WaitAction()
    {
        IsBusy = true;
        grid.ClearHighlights();
        DashedLastTurn = false;
        DashArmed = false;
        if (IsHidden) Stamina = Mathf.Min(maxStamina, Stamina + hiddenRegen);
        yield return new WaitForSeconds(0.15f);
        FinishAction();
    }

    IEnumerator EnterAction(HidingSpot spot)
    {
        if (spot.isOccupied) yield break; // one person per spot
        IsBusy = true;
        grid.ClearHighlights();
        DashedLastTurn = false;
        DashArmed = false;

        // The killer decides how suspicious this spot is (did it see us go in?).
        float chance = KillerAI.Instance ? KillerAI.Instance.GetFindChanceFor(spot, Tile) : 0.15f;
        spot.Enter(chance);
        CurrentSpot = spot;
        AudioManager.Play(AudioManager.Sfx.Rustle);

        // Glide straight from where we stand into the hide point (we may be on a tile next to the object).
        Tile = spot.Tile;
        yield return MoveTo(spot.hidePoint.position, spot.hidePoint.rotation, hideMoveTime * 1.5f);
        SetVisible(false);
        IsHidden = true;
        FinishAction();
    }

    IEnumerator ExitAction()
    {
        HidingSpot spot = CurrentSpot;
        if (!TryGetExitTile(spot, out Vector2Int exitTile)) yield break; // killer is blocking the way out
        IsBusy = true;
        DashedLastTurn = false;

        SetVisible(true);
        IsHidden = false;
        AudioManager.Play(AudioManager.Sfx.Rustle);
        yield return MoveTo(TilePosition(exitTile), spot.enterPoint.rotation, hideMoveTime);
        spot.Exit();
        CurrentSpot = null;
        Tile = exitTile;
        FinishAction();
    }

    void FinishAction()
    {
        IsBusy = false;
        if (GameManager.Instance) GameManager.Instance.EndPlayerTurn();
    }

    // ------------------------------------------------------------------ helpers

    HidingSpot FindSpot()
    {
        // Same rule as the outline: on the spot's tile, next to it, or right next to the object.
        HidingSpot best = null;
        float bestDist = float.MaxValue;
        foreach (HidingSpot spot in HidingSpot.All)
        {
            if (spot.isOccupied || !spot.IsInReach(Tile, grid)) continue;
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

    IEnumerator StepTo(Vector2Int t)
    {
        Vector3 target = TilePosition(t);
        Vector3 dir = target - transform.position;
        dir.y = 0f;
        Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : transform.rotation;
        yield return MoveTo(target, rot, stepTime);
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
