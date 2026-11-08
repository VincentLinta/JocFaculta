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
    public Color lockedColor = new Color(1f, 0.15f, 0.1f);
    public Color openColor = new Color(0.2f, 1f, 0.35f);

    /// <summary>True while the player stands on a tile next to the door.</summary>
    public bool PlayerNear { get; private set; }

    void Awake() { Instance = this; }

    void OnEnable() { All.Add(this); }

    void OnDisable() { All.Remove(this); }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        PlayerController player = PlayerController.Instance;
        GridManager grid = GridManager.Instance;
        if (gm == null || player == null || grid == null) return;

        bool unlocked = gm.Keys >= gm.keysRequired;
        if (doorLight) doorLight.color = unlocked ? openColor : lockedColor;

        // Use the player's tile (not the animated position) so the exit sits on fixed tiles.
        Vector3 offset = grid.TileCenter(player.Tile) - transform.position;
        offset.y = 0f;
        PlayerNear = !player.IsHidden && offset.magnitude < reachTiles * grid.tileSize;

        if (PlayerNear && unlocked && gm.IsPlaying && !player.IsBusy) gm.Win();
    }
}
