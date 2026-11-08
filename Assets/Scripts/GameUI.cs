using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the HUD at runtime: stamina (4 segments), keys, turn indicator, action buttons
/// (Dash / Hide / Wait), prompt, danger indicator, red vignette, hide darkening,
/// pause menu, game over and win screens.
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Vignette")]
    [Tooltip("Killer closer than this starts the red vignette.")]
    public float dangerDistance = 10f;
    public float maxVignetteAlpha = 0.75f;
    [Tooltip("Screen darkening while hidden.")]
    public float hiddenDarkness = 0.45f;

    static readonly Color TextColor = new Color(0.9f, 0.88f, 0.82f);
    static readonly Color ButtonColor = new Color(0.22f, 0.06f, 0.06f, 0.95f);
    static readonly Color ButtonOnColor = new Color(0.75f, 0.4f, 0.08f, 1f);
    static readonly Color SegmentOn = new Color(0.85f, 0.82f, 0.7f, 0.95f);
    static readonly Color SegmentOff = new Color(0.25f, 0.24f, 0.22f, 0.6f);
    static readonly Color ButtonEmptyColor = new Color(0.16f, 0.12f, 0.12f, 0.8f);
    static readonly Color DeniedColor = new Color(1f, 0.05f, 0.05f, 1f);
    static readonly Color HideReadyColor = new Color(0.25f, 0.55f, 0.6f, 1f);

    Font font;
    Image vignette;
    Image hideOverlay;
    readonly Image[] staminaSegments = new Image[4];
    Text keysText;
    Text promptText;
    Text dangerText;
    Text turnText;
    Text loseMessage;
    Text winMessage;
    Button dashButton;
    Button hideButton;
    Button waitButton;
    Text dashLabel;
    Text hideLabel;
    GameObject losePanel;
    GameObject winPanel;
    GameObject pausePanel;
    Text volumeLabel;
    Text hideStatus;
    Text muteLabel;
    float hideAlpha;

    [Header("All keys found")]
    public float bannerTime = 4f;
    public float exitHintTime = 5f;
    [Tooltip("Distance (px) from the screen edge for exit arrows.")]
    public float hintEdgeMargin = 90f;

    class ExitHint
    {
        public ExitDoor door;
        public RectTransform arrow;
        public Image arrowImage;
        public Text label;
        public Vector2Int playerTile = new Vector2Int(-999, -999);
        public int pathTiles;
    }

    RectTransform hudRoot;
    Canvas hudCanvas;
    Text banner;
    Sprite arrowSprite;
    readonly System.Collections.Generic.List<ExitHint> exitHints = new System.Collections.Generic.List<ExitHint>();
    readonly System.Collections.Generic.Dictionary<Light, float> exitLightBase = new System.Collections.Generic.Dictionary<Light, float>();
    bool allKeysShown;
    float bannerLeft;
    float hintLeft;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildHud();
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        PlayerController player = PlayerController.Instance;
        KillerAI killer = KillerAI.Instance;
        if (gm == null || vignette == null) return;

        bool canAct = gm.CanPlayerAct && player && !player.IsBusy;

        if (player)
        {
            // 4 segments of 25%.
            int full = Mathf.FloorToInt(player.Stamina / (player.maxStamina / 4f) + 0.001f);
            for (int i = 0; i < 4; i++) staminaSegments[i].color = i < full ? SegmentOn : SegmentOff;

            hideAlpha = Mathf.MoveTowards(hideAlpha, player.IsHidden ? hiddenDarkness : 0f, Time.unscaledDeltaTime * 2f);
            hideOverlay.color = new Color(0f, 0f, 0f, hideAlpha);

            // Small status next to STAMINA after hiding: did he see you go in?
            if (player.IsHidden && killer != null)
            {
                hideStatus.text = killer.LastHideWitnessed ? "He saw you" : "Hidden";
                hideStatus.color = killer.LastHideWitnessed ? new Color(0.75f, 0.2f, 0.18f, 0.85f) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            }
            else hideStatus.text = "";

            // DASH stays clickable with low stamina so a refused dash gives feedback.
            dashButton.interactable = canAct && !player.IsHidden;
            dashButton.image.color = player.DashArmed ? ButtonOnColor : (player.CanDash ? ButtonColor : ButtonEmptyColor);
            dashLabel.text = player.DashArmed ? "DASH ON (Space)" : "DASH (Space)";

            // Red flash on button + stamina bar after a refused dash.
            float flash = 1f - (Time.unscaledTime - player.DashDeniedTime) / 0.6f;
            if (flash > 0f)
            {
                float pulse = flash * (0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 40f));
                dashButton.image.color = Color.Lerp(dashButton.image.color, DeniedColor, pulse);
                for (int i = 0; i < 4; i++) staminaSegments[i].color = Color.Lerp(staminaSegments[i].color, DeniedColor, pulse);
            }

            hideButton.interactable = canAct && (player.IsHidden || player.AvailableSpot != null);
            // Brighter HIDE button while a spot is in reach (matches the outline).
            hideButton.image.color = canAct && !player.IsHidden && player.AvailableSpot != null ? HideReadyColor : ButtonColor;
            hideLabel.text = player.IsHidden ? "EXIT (E)" : "HIDE (E)";
            waitButton.interactable = canAct;
        }

        keysText.text = $"KEYS {gm.Keys}/{gm.keysRequired}";
        promptText.text = GetPrompt(gm, player, canAct);

        bool playerTurn = gm.CurrentTurn == GameManager.Turn.Player;
        turnText.text = playerTurn ? "YOUR TURN" : "KILLER'S TURN";
        turnText.color = playerTurn ? new Color(0.6f, 0.95f, 0.7f) : new Color(1f, 0.3f, 0.25f);
        turnText.enabled = gm.IsPlaying;

        // Danger indicator + red vignette.
        bool chasing = killer && killer.CurrentState == KillerAI.State.Chase && gm.IsPlaying;
        dangerText.enabled = chasing;
        if (chasing) dangerText.color = new Color(1f, 0.1f, 0.1f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f));

        float alpha = 0f;
        if (killer && gm.IsPlaying)
        {
            alpha = Mathf.Clamp01(1f - killer.DistanceToPlayer / dangerDistance) * maxVignetteAlpha;
            if (chasing) alpha = Mathf.Max(alpha, 0.35f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f));
        }
        vignette.color = new Color(0.7f, 0f, 0f, alpha);

        // Menus.
        pausePanel.SetActive(gm.IsPaused);
        AudioManager audio = AudioManager.Instance;
        if (audio != null && gm.IsPaused)
        {
            volumeLabel.text = $"VOLUME {Mathf.RoundToInt(audio.MasterVolume * 100f)}%";
            muteLabel.text = audio.Muted ? "UNMUTE" : "MUTE";
        }
        UpdateAllKeysEvent(gm, player);
        losePanel.SetActive(gm.State == GameManager.GameState.Lost);
        winPanel.SetActive(gm.State == GameManager.GameState.Won);
        loseMessage.text = gm.EndMessage;
        winMessage.text = gm.EndMessage;
    }

    // ------------------------------------------------------------------ all keys found

    /// <summary>
    /// Once per run, when the last key is picked up: a green banner (4 s) and 5 s of exit hints
    /// (edge arrows, or a marker above an exit that is on screen) plus stronger exit lights.
    /// Timers use scaled time, so they stop in the pause menu. Cleared on win / death; Restart reloads the scene.
    /// </summary>
    void UpdateAllKeysEvent(GameManager gm, PlayerController player)
    {
        if (!allKeysShown && gm.IsPlaying && gm.Keys >= gm.keysRequired)
        {
            allKeysShown = true;
            bannerLeft = bannerTime;
            hintLeft = exitHintTime;
            BuildExitHints();
        }
        if (!gm.IsPlaying)
        {
            bannerLeft = 0f;
            hintLeft = 0f;
        }

        float dt = Time.deltaTime; // 0 while paused
        bannerLeft = Mathf.Max(0f, bannerLeft - dt);
        hintLeft = Mathf.Max(0f, hintLeft - dt);

        // Banner: fade in 0.5 s, fade out 0.8 s.
        float bAlpha = bannerLeft <= 0f ? 0f : Mathf.Min(1f, (bannerTime - bannerLeft) / 0.5f, bannerLeft / 0.8f);
        banner.enabled = bAlpha > 0f;
        banner.color = new Color(0.35f, 1f, 0.45f, bAlpha);

        float pulse01 = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
        float hAlpha = hintLeft <= 0f ? 0f : Mathf.Min(1f, (exitHintTime - hintLeft) / 0.3f, hintLeft / 1f);
        Camera cam = Camera.main;
        GridManager grid = GridManager.Instance;
        foreach (ExitHint h in exitHints)
        {
            bool show = hAlpha > 0f && h.door != null && cam != null && grid != null && player != null;
            h.arrow.gameObject.SetActive(show);
            h.label.enabled = show;
            if (!show) continue;

            if (h.playerTile != player.Tile)
            {
                h.playerTile = player.Tile;
                Vector2Int exitTile = grid.NearestWalkable(grid.WorldToTile(h.door.transform.position));
                var path = grid.FindPath(player.Tile, exitTile);
                h.pathTiles = path != null ? path.Count : Mathf.RoundToInt(Vector2Int.Distance(player.Tile, exitTile));
            }

            float scale = hudCanvas.scaleFactor;
            Vector3 sp = cam.WorldToScreenPoint(h.door.transform.position + Vector3.up * 1.2f);
            if (sp.z < 0f) sp = -sp;
            float w = Screen.width, ht = Screen.height, m = hintEdgeMargin;
            bool onScreen = sp.x > m && sp.x < w - m && sp.y > m && sp.y < ht - m;
            Vector2 pos;
            float angle;
            Vector2 labelOffset;
            if (onScreen)
            {
                // Marker above the exit, pointing down, bobbing.
                pos = new Vector2(sp.x, sp.y + 70f + 12f * pulse01);
                angle = -90f;
                labelOffset = new Vector2(0f, 55f);
            }
            else
            {
                Vector2 center = new Vector2(w * 0.5f, ht * 0.5f);
                Vector2 dir = ((Vector2)sp - center).normalized;
                float t = Mathf.Min((w * 0.5f - m) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (ht * 0.5f - m) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                pos = center + dir * t;
                angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                labelOffset = -dir * 70f;
            }
            h.arrow.anchoredPosition = pos / scale;
            h.arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            h.arrow.localScale = Vector3.one * (0.9f + 0.25f * pulse01);
            h.arrowImage.color = new Color(0.3f, 1f, 0.4f, hAlpha * (0.6f + 0.4f * pulse01));
            h.label.rectTransform.anchoredPosition = (pos + labelOffset) / scale;
            h.label.text = $"EXIT {h.pathTiles}";
            h.label.color = new Color(0.55f, 1f, 0.6f, hAlpha);
        }

        // Exit lights pulse stronger while the hints are up.
        foreach (ExitDoor door in ExitDoor.All)
        {
            if (door.doorLight == null) continue;
            if (!exitLightBase.ContainsKey(door.doorLight)) exitLightBase[door.doorLight] = door.doorLight.intensity;
            float baseI = exitLightBase[door.doorLight];
            door.doorLight.intensity = hAlpha > 0f ? baseI * (1f + hAlpha * (0.6f + 1.2f * pulse01)) : baseI;
        }
    }

    void BuildExitHints()
    {
        foreach (ExitDoor door in ExitDoor.All)
        {
            var h = new ExitHint { door = door };
            h.arrowImage = MakeImage("ExitArrow", hudRoot, Color.clear); // raycastTarget stays false: never blocks clicks
            h.arrowImage.sprite = arrowSprite;
            h.arrow = h.arrowImage.rectTransform;
            h.arrow.anchorMin = h.arrow.anchorMax = Vector2.zero;
            h.arrow.pivot = new Vector2(0.5f, 0.5f);
            h.arrow.sizeDelta = new Vector2(70f, 70f);
            h.label = MakeText("ExitLabel", hudRoot, "EXIT", 26, TextAnchor.MiddleCenter);
            h.label.rectTransform.anchorMin = h.label.rectTransform.anchorMax = Vector2.zero;
            h.label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            h.label.rectTransform.sizeDelta = new Vector2(160f, 34f);
            exitHints.Add(h);
        }
    }

    /// <summary>White arrow pointing right (+X), soft edges.</summary>
    static Sprite MakeArrowSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1), v = y / (float)(size - 1) - 0.5f;
                float halfWidth = 0.45f * (1f - u);           // triangle: wide at the left, point at the right
                float edge = halfWidth - Mathf.Abs(v);
                float a = Mathf.Clamp01(edge * 40f) * Mathf.Clamp01(u * 20f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    string GetPrompt(GameManager gm, PlayerController player, bool canAct)
    {
        if (!gm.IsPlaying || player == null || !canAct) return "";
        if (player.IsHidden) return "Hidden - E to leave, W to wait (+25% stamina)";
        if (ExitDoor.AnyPlayerNear && gm.Keys < gm.keysRequired)
            return $"Locked - find {gm.keysRequired - gm.Keys} more key(s)";
        if (player.AvailableSpot) return "Press E to hide";
        return "Click a highlighted tile to move";
    }

    // ------------------------------------------------------------------ building

    void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetParent(transform, false);
    }

    void BuildHud()
    {
        var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGO.transform;

        hideOverlay = MakeImage("HideOverlay", root, Color.clear);
        Stretch(hideOverlay.rectTransform);

        vignette = MakeImage("Vignette", root, Color.clear);
        vignette.sprite = MakeVignetteSprite();
        Stretch(vignette.rectTransform);

        // Stamina: 4 segments (bottom left).
        Text staminaLabel = MakeText("StaminaLabel", root, "STAMINA", 20, TextAnchor.LowerLeft);
        Place(staminaLabel.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 70f), new Vector2(300f, 26f));
        hideStatus = MakeText("HideStatus", root, "", 22, TextAnchor.LowerLeft);
        Place(hideStatus.rectTransform, new Vector2(0f, 0f), new Vector2(150f, 70f), new Vector2(300f, 26f));
        for (int i = 0; i < 4; i++)
        {
            staminaSegments[i] = MakeImage("Stamina" + (i + 1), root, SegmentOn);
            Place(staminaSegments[i].rectTransform, new Vector2(0f, 0f), new Vector2(40f + i * 82f, 40f), new Vector2(76f, 22f));
        }

        keysText = MakeText("Keys", root, "KEYS 0/3", 34, TextAnchor.UpperLeft);
        Place(keysText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(400f, 44f));

        turnText = MakeText("Turn", root, "YOUR TURN", 34, TextAnchor.UpperRight);
        Place(turnText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(500f, 44f));

        promptText = MakeText("Prompt", root, "", 30, TextAnchor.MiddleCenter);
        Place(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1000f, 50f));

        dangerText = MakeText("Danger", root, "! DANGER !", 52, TextAnchor.MiddleCenter);
        Place(dangerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(700f, 80f));
        dangerText.enabled = false;

        // "All keys found" banner + a layer for exit hints (under the menus, never blocks clicks).
        banner = MakeText("AllKeysBanner", root, "ALL KEYS FOUND - EXITS ARE NOW OPEN", 54, TextAnchor.MiddleCenter);
        Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1600f, 80f));
        banner.enabled = false;
        var hintLayer = new GameObject("ExitHints", typeof(RectTransform)).GetComponent<RectTransform>();
        hintLayer.SetParent(root, false);
        Stretch(hintLayer);
        hudRoot = hintLayer;
        hudCanvas = canvasGO.GetComponent<Canvas>();
        arrowSprite = MakeArrowSprite();

        // Action buttons (bottom right).
        dashButton = MakeButton(root, "DASH (Space)", new Vector2(1f, 0f), new Vector2(-560f, 40f), new Vector2(250f, 70f), out dashLabel,
            () => { if (PlayerController.Instance) PlayerController.Instance.ToggleDash(); });
        hideButton = MakeButton(root, "HIDE (E)", new Vector2(1f, 0f), new Vector2(-300f, 40f), new Vector2(200f, 70f), out hideLabel,
            () => { if (PlayerController.Instance) PlayerController.Instance.HideOrExit(); });
        waitButton = MakeButton(root, "WAIT (W)", new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(200f, 70f), out _,
            () => { if (PlayerController.Instance) PlayerController.Instance.Wait(); });

        pausePanel = MakePausePanel(root);
        losePanel = MakeEndPanel(root, "GameOver", "YOU DIED", new Color(0.85f, 0.1f, 0.1f), "Retry", out loseMessage);
        winPanel = MakeEndPanel(root, "Win", "YOU ESCAPED", new Color(0.3f, 0.9f, 0.4f), "Play Again", out winMessage);
    }

    GameObject MakePausePanel(RectTransform root)
    {
        Image panel = MakeImage("PausePanel", root, new Color(0f, 0f, 0f, 0.8f));
        Stretch(panel.rectTransform);
        panel.raycastTarget = true; // blocks clicks on tiles

        Text title = MakeText("Title", panel.rectTransform, "PAUSED", 96, TextAnchor.MiddleCenter);
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1000f, 130f));

        // Master volume slider + mute.
        volumeLabel = MakeText("VolumeLabel", panel.rectTransform, "VOLUME", 28, TextAnchor.MiddleCenter);
        Place(volumeLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(500f, 40f));
        Slider slider = MakeSlider(panel.rectTransform, new Vector2(-80f, 150f), new Vector2(360f, 34f));
        slider.value = AudioManager.Instance ? AudioManager.Instance.MasterVolume : 0.8f;
        slider.onValueChanged.AddListener(v => { if (AudioManager.Instance) AudioManager.Instance.SetMasterVolume(v); });
        MakeButton(panel.rectTransform, "MUTE", new Vector2(0.5f, 0.5f), new Vector2(190f, 150f), new Vector2(170f, 54f), out muteLabel,
            () => { if (AudioManager.Instance) AudioManager.Instance.ToggleMute(); });

        MakeButton(panel.rectTransform, "Resume", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(360f, 80f), out _,
            () => { if (GameManager.Instance) GameManager.Instance.SetPaused(false); });
        MakeButton(panel.rectTransform, "Restart", new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(360f, 80f), out _,
            () => { if (GameManager.Instance) GameManager.Instance.Restart(); });
        MakeButton(panel.rectTransform, "Quit", new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(360f, 80f), out _,
            () => { if (GameManager.Instance) GameManager.Instance.Quit(); });

        Text hint = MakeText("Hint", panel.rectTransform, "ESC to resume", 22, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -240f), new Vector2(400f, 30f));

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    GameObject MakeEndPanel(RectTransform root, string name, string title, Color titleColor, string buttonLabel, out Text message)
    {
        Image panel = MakeImage(name + "Panel", root, new Color(0f, 0f, 0f, 0.85f));
        Stretch(panel.rectTransform);
        panel.raycastTarget = true;

        Text titleText = MakeText("Title", panel.rectTransform, title, 96, TextAnchor.MiddleCenter);
        Place(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(1400f, 130f));
        titleText.color = titleColor;

        message = MakeText("Message", panel.rectTransform, "", 34, TextAnchor.MiddleCenter);
        Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 50f));

        MakeButton(panel.rectTransform, buttonLabel, new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(340f, 84f), out _,
            () => { if (GameManager.Instance) GameManager.Instance.Restart(); });

        Text hint = MakeText("Hint", panel.rectTransform, "or press R", 22, TextAnchor.MiddleCenter);
        Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(400f, 30f));

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    Button MakeButton(RectTransform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, out Text labelText, UnityEngine.Events.UnityAction onClick)
    {
        Image image = MakeImage(label + "Button", parent, ButtonColor);
        image.raycastTarget = true;
        Place(image.rectTransform, anchor, pos, size);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.5f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        labelText = MakeText("Label", image.rectTransform, label, 30, TextAnchor.MiddleCenter);
        Stretch(labelText.rectTransform);
        return button;
    }

    Slider MakeSlider(RectTransform parent, Vector2 pos, Vector2 size)
    {
        Image background = MakeImage("VolumeSlider", parent, new Color(0.15f, 0.14f, 0.13f, 1f));
        background.raycastTarget = true;
        Place(background.rectTransform, new Vector2(0.5f, 0.5f), pos, size);

        var fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(background.rectTransform, false);
        Stretch(fillArea);
        fillArea.offsetMin = new Vector2(5f, 5f);
        fillArea.offsetMax = new Vector2(-5f, -5f);
        Image fill = MakeImage("Fill", fillArea, ButtonOnColor);
        Stretch(fill.rectTransform);

        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(background.rectTransform, false);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);
        Image handle = MakeImage("Handle", handleArea, TextColor);
        handle.raycastTarget = true;
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(22f, 8f);

        Slider slider = background.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        return slider;
    }

    Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text MakeText(string name, Transform parent, string content, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = TextColor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Anchors, pivots and sizes a rect at one screen point.</summary>
    static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>White texture that is clear in the middle and solid at the edges.</summary>
    static Sprite MakeVignetteSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var center = new Vector2(size / 2f, size / 2f);
        float maxDist = center.magnitude;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float a = Mathf.SmoothStep(0f, 1f, (d - 0.45f) / 0.55f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
