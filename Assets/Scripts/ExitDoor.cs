using System.Collections.Generic;
using UnityEngine;

/// <summary>A way out. Wins the game when the player stands at it with all keys. There can be several.</summary>
public class ExitDoor : MonoBehaviour
{
    public static ExitDoor Instance { get; private set; }

    /// <summary>Every exit in the scene.</summary>
    public static readonly List<ExitDoor> All = new List<ExitDoor>();

    /// <summary>True while the player stands at any exit.</summary>
    public static bool AnyPlayerNear
    {
        get
        {
            foreach (ExitDoor e in All) if (e.PlayerNear) return true;
            return false;
        }
    }

    [Tooltip("Player tiles closer than this (in tiles) to the door count as 'at the door'.")]
    public float reachTiles = 0.8f;
    public Light doorLight;
    [Tooltip("Escape doors are spawned by SearchableSpawner. Only the one opened by the Escape card glows.")]
    public bool isEscapeDoor;
    [Tooltip("The escape point is a window (dark blue glass when closed).")]
    public bool isWindow;
    [Tooltip("Glowing panel of an escape door.")]
    public Renderer panel;
    public Color lockedColor = new Color(1f, 0.15f, 0.1f);
    public Color openColor = new Color(0.2f, 1f, 0.35f);

    /// <summary>True while the player stands on a tile next to the door.</summary>
    public bool PlayerNear { get; private set; }

    void Awake() { Instance = this; }

    void OnEnable() { All.Add(this); }

    void OnDisable() { All.Remove(this); }

    Material panelMat;

    void Update()
    {
        GameManager gm = GameManager.Instance;
        PlayerController player = PlayerController.Instance;
        GridManager grid = GridManager.Instance;
        if (gm == null || player == null || grid == null) return;

        // Use the player's tile (not the animated position) so the exit sits on fixed tiles.
        Vector3 offset = grid.TileCenter(player.Tile) - transform.position;
        offset.y = 0f;
        PlayerNear = !player.IsHidden && offset.magnitude < reachTiles * grid.tileSize;

        if (isEscapeDoor)
        {
            UpdateEscapeDoor(gm);
            return;
        }

        // Old goal (only when keys and exits are switched on): collect all keys, walk to an exit.
        if (!gm.useKeysAndExits) return;
        bool unlocked = gm.Keys >= gm.keysRequired;
        if (doorLight) doorLight.color = unlocked ? openColor : lockedColor;
        if (PlayerNear && unlocked && gm.IsPlaying && !player.IsBusy) gm.Win();
    }

    /// <summary>Closed: dark red panel, no light. Opened by the Escape card: bright green panel and light.</summary>
    void UpdateEscapeDoor(GameManager gm)
    {
        bool open = gm.EscapeArmed && gm.EscapeDoor == this;
        if (panel != null)
        {
            if (panelMat == null) panelMat = panel.material;
            Color c = open
                ? Color.Lerp(openColor * 0.7f, openColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 5f))
                : (isWindow ? new Color(0.04f, 0.07f, 0.11f) : new Color(0.22f, 0.04f, 0.04f));
            c.a = 1f;
            panelMat.color = c;
            if (panelMat.HasProperty("_BaseColor")) panelMat.SetColor("_BaseColor", c);
        }
        if (doorLight)
        {
            doorLight.color = openColor;
            doorLight.enabled = open;
        }
    }
}
