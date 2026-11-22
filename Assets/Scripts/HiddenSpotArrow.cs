using UnityEngine;

/// <summary>
/// A green arrow that floats above the hiding spot the player is in, so it is easy to see where you hid.
/// It appears when the player is hidden and disappears when he comes out.
/// No scene setup needed: the script installs itself when the game starts.
/// </summary>
public class HiddenSpotArrow : MonoBehaviour
{
    static readonly Color ArrowColor = new Color(0.2f, 1f, 0.35f);

    [Tooltip("How far above the hiding spot's top the arrow floats.")]
    public float height = 0.9f;
    [Tooltip("Up / down bounce size.")]
    public float bounce = 0.18f;
    public float bounceSpeed = 3.2f;
    public float spinSpeed = 90f;

    Transform arrow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<HiddenSpotArrow>() != null) return;
        var go = new GameObject("HiddenSpotArrow");
        go.AddComponent<HiddenSpotArrow>();
        DontDestroyOnLoad(go); // survives Restart (scene reload)
    }

    void Awake() { arrow = BuildArrow(); arrow.gameObject.SetActive(false); }

    void LateUpdate()
    {
        PlayerController p = PlayerController.Instance;
        GameManager gm = GameManager.Instance;
        bool show = p != null && gm != null && gm.IsPlaying && p.IsHidden && p.CurrentSpot != null;
        if (arrow.gameObject.activeSelf != show) arrow.gameObject.SetActive(show);
        if (!show) return;

        Vector3 anchor = p.CurrentSpot.BarAnchor;
        float y = anchor.y - 0.7f + height + 0.45f + Mathf.Sin(Time.unscaledTime * bounceSpeed) * bounce;
        arrow.position = new Vector3(anchor.x, y, anchor.z);
        arrow.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
    }

    // ------------------------------------------------------------------ the arrow (points down)

    Transform BuildArrow()
    {
        var root = new GameObject("Arrow").transform;
        root.SetParent(transform, false);

        Shader s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s == null) s = Shader.Find("Unlit/Color");
        var mat = new Material(s);
        mat.color = ArrowColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ArrowColor);

        // shaft
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(root, false);
        shaft.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        shaft.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
        Destroy(shaft.GetComponent<Collider>());
        shaft.GetComponent<Renderer>().sharedMaterial = mat;

        // head: a four-sided pyramid with the tip pointing down
        var head = new GameObject("Head");
        head.transform.SetParent(root, false);
        head.AddComponent<MeshFilter>().sharedMesh = BuildPyramid(0.6f, 0.5f);
        head.AddComponent<MeshRenderer>().sharedMaterial = mat;

        // green glow so it is visible in the dark
        var lg = new GameObject("Glow");
        lg.transform.SetParent(root, false);
        lg.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        Light l = lg.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = ArrowColor;
        l.range = 3.5f;
        l.intensity = 1.2f;
        l.shadows = LightShadows.None;
        return root;
    }

    /// <summary>Pyramid whose tip is at the origin and whose square base is 'height' above it.</summary>
    static Mesh BuildPyramid(float width, float height)
    {
        float h = width / 2f;
        Vector3 tip = Vector3.zero;
        Vector3 a = new Vector3(-h, height, -h), b = new Vector3(h, height, -h);
        Vector3 c = new Vector3(h, height, h), d = new Vector3(-h, height, h);
        var m = new Mesh { name = "ArrowHead" };
        m.vertices = new[] { tip, b, a, tip, c, b, tip, d, c, tip, a, d, a, b, c, d };
        m.triangles = new[] { 0, 2, 1, 3, 5, 4, 6, 8, 7, 9, 11, 10, 12, 14, 13, 12, 15, 14 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
