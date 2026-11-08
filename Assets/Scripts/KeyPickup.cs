using UnityEngine;

/// <summary>A floating, spinning key on one tile. Collected when the player steps onto that tile.</summary>
[DefaultExecutionOrder(100)] // after KeyPlacer (50) has moved the keys
public class KeyPickup : MonoBehaviour
{
    public float spinSpeed = 90f;
    public float bobHeight = 0.15f;

    /// <summary>The tile this key sits on.</summary>
    public Vector2Int Tile { get; private set; }

    Vector3 basePosition;

    void Start()
    {
        basePosition = transform.position;
        GridManager grid = GridManager.Instance;
        if (grid != null)
        {
            Tile = grid.ClaimNearest(transform.position); // one key per tile
            basePosition = grid.TileCenter(Tile) + Vector3.up;
        }
        transform.position = basePosition;
    }

    void Update()
    {
        // Idle animation so the key is easy to spot.
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = basePosition + Vector3.up * Mathf.Sin(Time.time * 2f) * bobHeight;

        PlayerController player = PlayerController.Instance;
        GameManager gm = GameManager.Instance;
        if (player == null || gm == null || !gm.IsPlaying || player.IsHidden) return;

        if (player.Tile == Tile)
        {
            AudioManager.Play(AudioManager.Sfx.Key);
            gm.CollectKey();
            Destroy(gameObject);
        }
    }
}
