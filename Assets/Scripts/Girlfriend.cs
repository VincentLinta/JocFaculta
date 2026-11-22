using UnityEngine;

/// <summary>
/// The Girlfriend card: she stands where the player put her (never next to a hiding spot).
/// She is a trap: when the clown walks past her he is stunned for a couple of seconds and she
/// tells him off. She is used once.
/// Built from primitives, no colliders (never blocks the grid).
/// </summary>
public class Girlfriend : MonoBehaviour
{
    public const float StunSeconds = 2f;
    [Tooltip("The clown is stunned when he comes closer than this many tiles.")]
    public float triggerTiles = 1.3f;

    static readonly string[] Scolds =
    {
        "HEY! Where do you think you're going?!",
        "Look at me when I'm talking to you!",
        "You forgot our anniversary AGAIN!",
        "We need to talk. NOW.",
        "Take those big shoes off!",
        "You never listen to me!",
    };

    public bool Used { get; private set; }
    public Vector2Int Tile { get; private set; }

    TextMesh bubble;
    Transform body;
    float usedFor;
    float phase;

    /// <summary>Creates her on a tile.</summary>
    public static Girlfriend Create(Vector3 position, Vector2Int tile)
    {
        var go = new GameObject("Girlfriend");
        go.transform.position = position;
        var g = go.AddComponent<Girlfriend>();
        g.Tile = tile;
        g.Build();
        return g;
    }

    static Material Mat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        return m;
    }

    static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    void Build()
    {
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        Material dress = Mat(new Color(1f, 0.5f, 0.72f));
        Material skin = Mat(new Color(0.96f, 0.8f, 0.68f));
        Material hair = Mat(new Color(0.28f, 0.15f, 0.08f));

        Part(PrimitiveType.Cylinder, "Skirt", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.85f, 0.55f, 0.85f), dress);
        Part(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 1.3f, 0f), new Vector3(0.55f, 0.4f, 0.45f), dress);
        Part(PrimitiveType.Sphere, "Hair", body, new Vector3(0f, 1.95f, -0.05f), new Vector3(0.62f, 0.66f, 0.62f), hair);
        Part(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 1.9f, 0.06f), new Vector3(0.48f, 0.5f, 0.48f), skin);

        var glow = new GameObject("Glow");
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 1.6f, 0.6f);
        Light l = glow.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.8f);
        l.range = 4f;
        l.intensity = 0.8f;
        l.shadows = LightShadows.None;

        // Speech bubble (text over her head, always faces the camera).
        var tg = new GameObject("Bubble");
        tg.transform.SetParent(transform, false);
        tg.transform.localPosition = new Vector3(0f, 3.1f, 0f);
        bubble = tg.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bubble.font = font;
        tg.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        bubble.fontSize = 64;
        bubble.characterSize = 0.07f;
        bubble.anchor = TextAnchor.MiddleCenter;
        bubble.alignment = TextAlignment.Center;
        bubble.color = new Color(1f, 0.75f, 0.9f);
        bubble.text = "";
    }

    void Update()
    {
        phase += Time.deltaTime;
        if (body != null && !Used) body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(phase * 2.2f)) * 0.06f, 0f); // idle bounce

        GameManager gm = GameManager.Instance;
        KillerAI killer = KillerAI.Instance;
        GridManager grid = GridManager.Instance;
        if (Used)
        {
            usedFor += Time.deltaTime;
            if (usedFor > StunSeconds + 1.2f) Destroy(gameObject);
            return;
        }
        if (gm == null || !gm.IsPlaying || killer == null || !killer.IsHunting || grid == null) return;

        Vector3 d = killer.transform.position - transform.position;
        d.y = 0f;
        if (d.magnitude < triggerTiles * grid.tileSize) Trigger(killer);
    }

    void LateUpdate()
    {
        if (bubble == null) return;
        Camera cam = Camera.main;
        if (cam != null) bubble.transform.rotation = cam.transform.rotation;
    }

    /// <summary>The clown came close: he is stunned and she scolds him.</summary>
    public void Trigger(KillerAI killer)
    {
        if (Used || killer == null) return;
        Used = true;
        usedFor = 0f;
        killer.Stun(StunSeconds);

        Vector3 look = killer.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);

        string line = Scolds[Random.Range(0, Scolds.Length)];
        if (bubble != null) bubble.text = WrapBubble(line);
        AudioManager.Play(AudioManager.Sfx.Rustle);
        SearchableObject.ShowToast("Girlfriend: \"" + line + "\"  - the clown is stunned!", CardInfo.Tint(CardType.Girlfriend), 3.5f);
    }

    static string WrapBubble(string s)
    {
        // Two short lines so the bubble stays narrow.
        int mid = s.Length / 2;
        int cut = s.IndexOf(' ', mid);
        if (cut < 0) cut = s.LastIndexOf(' ', mid);
        return cut > 0 ? s.Substring(0, cut) + "\n" + s.Substring(cut + 1) : s;
    }
}
