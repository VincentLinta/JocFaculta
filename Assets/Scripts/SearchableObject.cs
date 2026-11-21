using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cabinet or dresser the player can search with E. Each one holds either nothing or an energy drink
/// (refills stamina). It can be searched once. Built from primitives, no colliders (never blocks the grid).
/// </summary>
public class SearchableObject : MonoBehaviour
{
    public enum Contents { Nothing, EnergyDrink }

    /// <summary>Every searchable object in the scene.</summary>
    public static readonly List<SearchableObject> All = new List<SearchableObject>();

    // Message shown by the HUD after a search.
    public static string ToastText = "";
    public static Color ToastColor = Color.white;
    public static float ToastUntil;

    public Contents contents = Contents.Nothing;
    public float searchTime = 1.6f;
    public bool IsSearched { get; private set; }
    public Vector2Int Tile { get; private set; }

    bool dresser;
    Transform drawer, doorL, doorR;
    Light glow;
    Vector2Int cachedPlayerTile = new Vector2Int(-999, -999);
    bool cachedReach;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Builds the model and sets the tile. Called by the spawner. Forward (+Z) is the open front.</summary>
    public void Init(GridManager grid, Vector2Int tile, bool isDresser, Contents c)
    {
        Tile = tile;
        dresser = isDresser;
        contents = c;
        BuildModel();
    }

    public bool IsInReach(Vector2Int playerTile, GridManager grid)
    {
        if (playerTile == cachedPlayerTile) return cachedReach;
        cachedPlayerTile = playerTile;
        cachedReach = playerTile == Tile
                      || (GridManager.Manhattan(playerTile, Tile) == 1 && grid.CanStep(playerTile, Tile));
        return cachedReach;
    }

    void Update()
    {
        if (glow == null) return;
        PlayerController p = PlayerController.Instance;
        GameManager gm = GameManager.Instance;
        GridManager grid = GridManager.Instance;
        bool show = !IsSearched && p != null && gm != null && grid != null
                    && gm.CanPlayerAct && !p.IsBusy && !p.IsHidden && IsInReach(p.Tile, grid);
        glow.enabled = show;
        if (show) glow.intensity = 0.5f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
    }

    /// <summary>Plays the opening animation, then gives the contents to the player.</summary>
    public IEnumerator SearchRoutine(PlayerController player)
    {
        IsSearched = true;
        if (glow) glow.enabled = false;
        AudioManager.Play(AudioManager.Sfx.Rustle);

        float t = 0f;
        float open = searchTime * 0.35f;
        while (t < searchTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / open));
            SetOpen(k);
            yield return null;
        }

        if (contents == Contents.EnergyDrink)
        {
            player.RestoreStamina(player.maxStamina);
            AudioManager.Play(AudioManager.Sfx.Key);
            Toast("Found an energy drink - stamina restored", new Color(0.5f, 1f, 0.55f));
        }
        else Toast("Nothing but dust.", new Color(0.85f, 0.82f, 0.75f));
    }

    static void Toast(string text, Color color)
    {
        ToastText = text;
        ToastColor = color;
        ToastUntil = Time.time + 2.5f;
    }

    void SetOpen(float k)
    {
        if (drawer) drawer.localPosition = new Vector3(0f, 0.78f, 0.285f + 0.35f * k);
        if (doorL) doorL.localRotation = Quaternion.Euler(0f, -100f * k, 0f);
        if (doorR) doorR.localRotation = Quaternion.Euler(0f, 100f * k, 0f);
    }

    // ------------------------------------------------------------------ model

    static Material Mat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    static Transform Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    void BuildModel()
    {
        Material wood = Mat(new Color(0.2f, 0.125f, 0.08f));
        Material front = Mat(new Color(0.3f, 0.19f, 0.11f));
        Material metal = Mat(new Color(0.62f, 0.56f, 0.4f));

        if (dresser)
        {
            Box("Body", transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 0.55f), wood);
            Box("DrawerLow", transform, new Vector3(0f, 0.22f, 0.285f), new Vector3(1.2f, 0.28f, 0.04f), front);
            Box("DrawerMid", transform, new Vector3(0f, 0.5f, 0.285f), new Vector3(1.2f, 0.26f, 0.04f), front);
            Box("HandleLow", transform, new Vector3(0f, 0.22f, 0.32f), new Vector3(0.25f, 0.04f, 0.03f), metal);
            Box("HandleMid", transform, new Vector3(0f, 0.5f, 0.32f), new Vector3(0.25f, 0.04f, 0.03f), metal);

            drawer = new GameObject("Drawer").transform; // top drawer slides out
            drawer.SetParent(transform, false);
            drawer.localPosition = new Vector3(0f, 0.78f, 0.285f);
            Box("Front", drawer, Vector3.zero, new Vector3(1.2f, 0.24f, 0.04f), front);
            Box("Handle", drawer, new Vector3(0f, 0f, 0.035f), new Vector3(0.25f, 0.04f, 0.03f), metal);
            Box("Inside", drawer, new Vector3(0f, -0.06f, -0.2f), new Vector3(1.1f, 0.1f, 0.4f), Mat(new Color(0.08f, 0.05f, 0.03f)));
        }
        else
        {
            Box("Body", transform, new Vector3(0f, 0.85f, 0f), new Vector3(1f, 1.7f, 0.55f), wood);
            doorL = new GameObject("DoorL").transform;
            doorL.SetParent(transform, false);
            doorL.localPosition = new Vector3(-0.47f, 0.85f, 0.285f);
            Box("Panel", doorL, new Vector3(0.235f, 0f, 0f), new Vector3(0.47f, 1.55f, 0.04f), front);
            Box("Knob", doorL, new Vector3(0.42f, 0f, 0.035f), new Vector3(0.04f, 0.12f, 0.03f), metal);
            doorR = new GameObject("DoorR").transform;
            doorR.SetParent(transform, false);
            doorR.localPosition = new Vector3(0.47f, 0.85f, 0.285f);
            Box("Panel", doorR, new Vector3(-0.235f, 0f, 0f), new Vector3(0.47f, 1.55f, 0.04f), front);
            Box("Knob", doorR, new Vector3(-0.42f, 0f, 0.035f), new Vector3(0.04f, 0.12f, 0.03f), metal);
        }

        // Warm glow shown while the player can search it.
        var lg = new GameObject("SearchGlow");
        lg.transform.SetParent(transform, false);
        lg.transform.localPosition = new Vector3(0f, 1.1f, 0.9f);
        glow = lg.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.8f, 0.4f);
        glow.range = 3.2f;
        glow.intensity = 0.6f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }
}
