using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the HUD at runtime: stamina (4 segments), phase timer, Hide button, prompt, danger indicator,
/// red vignette, hide darkening, the killer's check bar above the spot he is searching,
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
    static readonly Color HideReadyColor = new Color(0.25f, 0.55f, 0.6f, 1f);

    Font font;
    Image vignette;
    Image flashOverlay;
    Image hideOverlay;
    readonly Image[] staminaSegments = new Image[4];
    Text keysText;
    Text promptText;
    Text toastText;
    Text cardsText;

    // Card choice overlay (found a card while holding another).
    class CardView
    {
        public RectTransform rt;
        public CanvasGroup group;
        public Image border;
        public GameObject front, back;
        public Text tag, title, desc, key;
        public Image emblem;
        public float hover;
    }
    GameObject ccRoot;
    Image ccDim;
    Text ccTitle;
    CardView ccOld, ccNew;
    bool ccWasOn;

    // Fight Back coin flip overlay.
    GameObject fbRoot;
    Image fbDim, fbCoin;
    RectTransform fbCoinRT;
    Text fbFace, fbTitle, fbResult;
    Text dangerText;
    Text phaseText;
    Text phaseBanner;
    Text checkedText;
    Image checkBack;
    Image checkFill;
    int shownPhase = -1;
    float phaseBannerLeft;
    string phaseBannerText = "";
    Text loseMessage;
    Text winMessage;
    Button hideButton;
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

        UpdateFightBack(gm);
        UpdateCardChoice(player);
        bool canAct = gm.CanPlayerAct && player && !player.IsBusy;

        if (player)
        {
            // 4 segments of 25%; each one fills smoothly.
            float segment = player.maxStamina / 4f;
            for (int i = 0; i < 4; i++)
            {
                float fill = Mathf.Clamp01((player.Stamina - i * segment) / segment);
                staminaSegments[i].color = Color.Lerp(SegmentOff, SegmentOn, fill);
            }

            hideAlpha = Mathf.MoveTowards(hideAlpha, player.IsHidden ? hiddenDarkness : 0f, Time.unscaledDeltaTime * 2f);
            hideOverlay.color = new Color(0f, 0f, 0f, hideAlpha);

            // Small status next to STAMINA after hiding: did he see you go in?
            if (player.IsHidden)
            {
                bool saw = killer != null && killer.IsHunting && killer.LastHideWitnessed;
                hideStatus.text = (saw ? "He saw you" : "Hidden") + (killer != null && killer.CheckFortified ? " - FORTIFIED" : "");
                hideStatus.color = saw ? new Color(0.75f, 0.2f, 0.18f, 0.85f) : new Color(0.6f, 0.6f, 0.6f, 0.7f);
            }
            else hideStatus.text = "";

            bool canUse = player.AvailableSpot != null || player.AvailableSearchable != null;
            hideButton.interactable = canAct && ((player.IsHidden && !player.IsLockedIn) || (!player.IsHidden && canUse));
            // Brighter HIDE button while a spot is in reach (matches the outline).
            hideButton.image.color = canAct && !player.IsHidden && canUse ? HideReadyColor : ButtonColor;
            hideLabel.text = player.IsHidden ? (player.IsLockedIn ? "HIDDEN" : "EXIT (E)") : (player.AvailableSpot == null && player.AvailableSearchable != null ? "SEARCH (E)" : "HIDE (E)");
        }

        keysText.enabled = gm.useKeysAndExits;
        keysText.text = $"KEYS {gm.Keys}/{gm.keysRequired}";
        promptText.text = GetPrompt(gm, player, canAct);
        if (player != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in player.Cards)
                if (kv.Value > 0)
                {
                    string hex = ColorUtility.ToHtmlStringRGB(CardInfo.Tint(kv.Key));
                    string key = kv.Key == CardType.Escape && gm.CanUseEscape ? "  [Q]" : (kv.Key == CardType.Decoy || kv.Key == CardType.Girlfriend ? "  [F]" : (kv.Key == CardType.Flashbang ? "  [G]" : (kv.Key == CardType.CarKey || kv.Key == CardType.GarageRemote ? "  [E at the car]" : "")));
                    sb.Append("<color=#").Append(hex).Append(">CARD  ").Append(CardInfo.Name(kv.Key)).Append(key).Append("</color>\n");
                }
            if (CarEscape.KeyDelivered || CarEscape.RemoteDelivered)
                sb.Append("<color=#BFD9FF>CAR: key ").Append(CarEscape.KeyDelivered ? "[x]" : "[ ]").Append("  remote ").Append(CarEscape.RemoteDelivered ? "[x]" : "[ ]").Append("</color>\n");
            if (player.CanTriggerDecoy) sb.Append("<color=#BFF273>DECOY PLACED  [F] when hidden</color>\n");
            cardsText.text = sb.ToString().TrimEnd('\n');
            cardsText.color = Color.white;
            cardsText.enabled = !player.CardChoiceActive; // the card is on the choice screen
        }
        bool toast = Time.time < SearchableObject.ToastUntil && !string.IsNullOrEmpty(SearchableObject.ToastText);
        toastText.text = toast ? SearchableObject.ToastText : "";
        toastText.color = SearchableObject.ToastColor;
        UpdatePhaseHud(gm, killer);

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

        // Flashbang: the screen flashes white and fades out.
        if (flashOverlay != null)
        {
            float ft = Time.time - PlayerController.LastFlashTime;
            float fa = ft < 0f || ft > 1.4f ? 0f : (ft < 0.15f ? 1f : 1f - (ft - 0.15f) / 1.25f);
            flashOverlay.color = new Color(1f, 1f, 0.92f, fa * fa);
        }

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

    // ------------------------------------------------------------------ phases + check bar

    /// <summary>Phase timer, phase announcement, spots-checked counter and the bar above the spot the killer is checking.</summary>
    bool policeBannerShown;

    void UpdatePhaseHud(GameManager gm, KillerAI killer)
    {
        bool hiding = gm.CurrentPhase == GameManager.Phase.Hiding;
        int secs = Mathf.CeilToInt(gm.PhaseTimeLeft);

        phaseText.enabled = gm.IsPlaying;
        phaseText.text = hiding ? $"HIDE  {secs}s" : $"SURVIVE  {secs}s";
        if (hiding) phaseText.color = secs <= 10 ? new Color(1f, 0.35f, 0.25f) : new Color(0.6f, 0.95f, 0.7f);
        else phaseText.color = new Color(1f, 0.3f, 0.25f);
        if (gm.PoliceArriving)
        {
            phaseText.text = "POLICE ARRIVED";
            phaseText.color = new Color(0.45f, 0.7f, 1f);
        }

        // Big announcement for 3 seconds whenever the phase changes (also at the start of the game).
        if ((int)gm.CurrentPhase != shownPhase)
        {
            shownPhase = (int)gm.CurrentPhase;
            phaseBannerLeft = 3f;
            phaseBannerText = hiding ? "FIND A HIDING SPOT" : "THE CLOWN IS HERE";
        }
        if (gm.PoliceArriving && !policeBannerShown)
        {
            policeBannerShown = true;
            phaseBannerLeft = 3f;
            phaseBannerText = "THE POLICE ARE HERE";
        }
        phaseBannerLeft = Mathf.Max(0f, phaseBannerLeft - Time.deltaTime);
        float a = phaseBannerLeft <= 0f || !gm.IsPlaying ? 0f : Mathf.Min(1f, (3f - phaseBannerLeft) / 0.4f, phaseBannerLeft / 0.8f);
        phaseBanner.enabled = a > 0f;
        phaseBanner.text = phaseBannerText;
        phaseBanner.color = gm.PoliceArriving ? new Color(0.45f, 0.7f, 1f, a)
            : hiding ? new Color(0.6f, 0.95f, 0.7f, a) : new Color(1f, 0.25f, 0.2f, a);

        checkedText.enabled = gm.IsPlaying && !hiding && !gm.PoliceArriving;
        checkedText.text = $"SPOTS CHECKED {HidingSpot.CountChecked()}/{HidingSpot.All.Count}";

        // The killer's progress bar, drawn above the hiding spot he is checking.
        HidingSpot spot = killer != null && gm.IsPlaying ? killer.CheckingSpot : null;
        Camera cam = Camera.main;
        bool show = spot != null && cam != null;
        Vector3 sp = Vector3.zero;
        if (show)
        {
            sp = cam.WorldToScreenPoint(spot.BarAnchor);
            show = sp.z > 0f;
        }
        checkBack.gameObject.SetActive(show);
        if (show)
        {
            checkBack.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) / hudCanvas.scaleFactor;
            float p = Mathf.Clamp(killer.CheckProgress, 0.05f, 1f);
            checkFill.rectTransform.anchorMax = new Vector2(p, 1f);
            checkFill.color = killer.CheckFortified
                ? Color.Lerp(new Color(0.45f, 0.8f, 1f), new Color(0.9f, 0.1f, 0.1f), killer.CheckProgress)
                : Color.Lerp(new Color(0.95f, 0.75f, 0.2f), new Color(0.9f, 0.1f, 0.1f), killer.CheckProgress);
        }
    }

    // ------------------------------------------------------------------ all keys found

    /// <summary>
    /// Once per run, when the last key is picked up: a green banner (4 s) and 5 s of exit hints
    /// (edge arrows, or a marker above an exit that is on screen) plus stronger exit lights.
    /// Timers use scaled time, so they stop in the pause menu. Cleared on win / death; Restart reloads the scene.
    /// </summary>
    void UpdateAllKeysEvent(GameManager gm, PlayerController player)
    {
        if (!gm.useKeysAndExits && !gm.EscapeArmed) return; // neither keys nor an Escape card in play
        bool escape = gm.EscapeArmed && gm.IsPlaying;
        if (escape && exitHints.Count == 0) BuildExitHints();
        if (gm.useKeysAndExits && !allKeysShown && gm.IsPlaying && gm.Keys >= gm.keysRequired)
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
        if (escape) hAlpha = 1f; // the escape exit stays marked the whole time
        Camera cam = Camera.main;
        GridManager grid = GridManager.Instance;
        foreach (ExitHint h in exitHints)
        {
            bool show = hAlpha > 0f && h.door != null && cam != null && grid != null && player != null;
            if (escape && h.door != gm.EscapeDoor) show = false;
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
            h.label.text = escape ? "ESCAPE" : $"EXIT {h.pathTiles}";
            h.label.color = new Color(0.55f, 1f, 0.6f, hAlpha);
        }

        // Exit lights pulse stronger while the hints are up.
        foreach (ExitDoor door in ExitDoor.All)
        {
            if (door.doorLight == null) continue;
            if (!exitLightBase.ContainsKey(door.doorLight)) exitLightBase[door.doorLight] = door.doorLight.intensity;
            float baseI = exitLightBase[door.doorLight];
            bool lit = hAlpha > 0f && (!escape || door == gm.EscapeDoor);
            door.doorLight.intensity = lit ? baseI * (1f + hAlpha * (0.6f + 1.2f * pulse01)) : baseI;
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
        if (gm.EscapeActive) return "Running to the exit!";
        if (gm.EscapeArmed && player.IsHidden) return "Press Q to leave your hiding spot and run to the open door!";
        bool hiding = gm.CurrentPhase == GameManager.Phase.Hiding;
        if (player.CanFlash) return "Press G to FLASH the clown and run to another hiding spot!";
        if (player.IsHidden && player.CanTriggerDecoy) return "Press F to send the clown to your decoy";
        if (player.IsHidden) return hiding ? "Hidden - wait for the clown" : (player.IsLockedIn ? "Hidden - stay quiet and hope he misses you" : "Hidden - stay quiet (E to leave)");
        if (CarEscape.CanDeliver(player))
        {
            string what = player.HasCarKey ? "the car key" : "the garage remote";
            return "Press E to leave " + what + " in the car";
        }
        if (player.AvailableSpot) return player.CardCount(CardType.Decoy) > 0 ? "Press E to hide   |   Press F to place the decoy" : "Press E to hide";
        bool gf = player.CardCount(CardType.Girlfriend) > 0;
        if (player.AvailableSearchable) return gf ? "Press E to search   |   Press F to place your girlfriend" : "Press E to search";
        if (gf) return "Press F to place your girlfriend here (not next to a hiding spot)";
        if (CarEscape.IsNear(player) && !CarEscape.BothDelivered) return "The car needs the CAR KEY and the GARAGE REMOTE (hidden in the cabinets)";
        return hiding ? "Click to walk, hold Space to run - find a hiding spot" : "Stay out of his sight - hide!";
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

        flashOverlay = MakeImage("FlashOverlay", root, Color.clear);
        Stretch(flashOverlay.rectTransform);

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

        phaseText = MakeText("Phase", root, "HIDE  60s", 44, TextAnchor.UpperRight);
        Place(phaseText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -36f), new Vector2(600f, 56f));

        checkedText = MakeText("Checked", root, "", 26, TextAnchor.UpperLeft);
        Place(checkedText.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -90f), new Vector2(500f, 34f));
        checkedText.enabled = false;

        Text controls = MakeText("Controls", root, "Click: walk | Space/Shift: run | E: hide / search | Q: Escape card | F: place Decoy / Girlfriend (trap) | G: Flashbang | Right mouse + drag: camera | Wheel: zoom", 20, TextAnchor.MiddleCenter);
        Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(1400f, 28f));
        controls.color = new Color(0.9f, 0.88f, 0.82f, 0.55f);

        promptText = MakeText("Prompt", root, "", 30, TextAnchor.MiddleCenter);
        Place(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1000f, 50f));

        cardsText = MakeText("Cards", root, "", 24, TextAnchor.LowerLeft);
        Place(cardsText.rectTransform, new Vector2(0f, 0f), new Vector2(40f, 110f), new Vector2(700f, 70f));

        toastText = MakeText("Toast", root, "", 30, TextAnchor.MiddleCenter);
        Place(toastText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1000f, 50f));

        dangerText = MakeText("Danger", root, "! DANGER !", 52, TextAnchor.MiddleCenter);
        Place(dangerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(700f, 80f));
        dangerText.enabled = false;

        // Phase announcement (centered, near the top) and the killer's check bar.
        phaseBanner = MakeText("PhaseBanner", root, "", 64, TextAnchor.MiddleCenter);
        Place(phaseBanner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1600f, 90f));
        phaseBanner.enabled = false;
        checkBack = MakeImage("CheckBarBack", root, new Color(0f, 0f, 0f, 0.85f));
        checkBack.rectTransform.anchorMin = Vector2.zero;
        checkBack.rectTransform.anchorMax = Vector2.zero;
        checkBack.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        checkBack.rectTransform.sizeDelta = new Vector2(230f, 30f);
        checkFill = MakeImage("CheckBarFill", checkBack.rectTransform, new Color(0.95f, 0.75f, 0.2f));
        checkFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        checkFill.rectTransform.anchorMax = new Vector2(0.05f, 1f);
        checkFill.rectTransform.offsetMin = new Vector2(4f, 4f);
        checkFill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        checkBack.gameObject.SetActive(false);

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

        // Hide button (bottom right).
        hideButton = MakeButton(root, "HIDE (E)", new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(240f, 70f), out hideLabel,
            () => { if (PlayerController.Instance) PlayerController.Instance.HideOrExit(); });

        BuildFightBack(root);
        BuildCardChoice(root);
        pausePanel = MakePausePanel(root);
        losePanel = MakeEndPanel(root, "GameOver", "YOU DIED", new Color(0.85f, 0.1f, 0.1f), "Retry", out loseMessage);
        winPanel = MakeEndPanel(root, "Win", "YOU SURVIVED", new Color(0.3f, 0.9f, 0.4f), "Play Again", out winMessage);
    }

    void BuildFightBack(RectTransform root)
    {
        fbDim = MakeImage("FightBack", root, new Color(0f, 0f, 0f, 0f));
        Stretch(fbDim.rectTransform);
        fbRoot = fbDim.gameObject;

        fbTitle = MakeText("FBTitle", fbDim.rectTransform, "FIGHT BACK!", 80, TextAnchor.MiddleCenter);
        Place(fbTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1200f, 110f));
        fbTitle.color = new Color(1f, 0.45f, 0.4f);

        fbCoin = MakeImage("Coin", fbDim.rectTransform, new Color(0.95f, 0.78f, 0.2f));
        fbCoin.sprite = MakeCircleSprite();
        fbCoinRT = fbCoin.rectTransform;
        Place(fbCoinRT, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(200f, 200f));

        Image rim = MakeImage("Rim", fbCoinRT, new Color(0.62f, 0.45f, 0.08f));
        rim.sprite = MakeCircleSprite();
        Place(rim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(168f, 168f));

        fbFace = MakeText("Face", fbCoinRT, "YOU", 42, TextAnchor.MiddleCenter);
        Place(fbFace.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 60f));

        fbResult = MakeText("FBResult", fbDim.rectTransform, "", 64, TextAnchor.MiddleCenter);
        Place(fbResult.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1400f, 100f));
        fbRoot.SetActive(false);
    }


    // ------------------------------------------------------------------ card choice

    const float CardW = 340f, CardH = 480f;

    void BuildCardChoice(RectTransform root)
    {
        ccDim = MakeImage("CardChoice", root, Color.clear);
        Stretch(ccDim.rectTransform);
        ccRoot = ccDim.gameObject;

        ccTitle = MakeText("CCTitle", ccDim.rectTransform, "KEEP YOUR CARD OR TAKE THE NEW ONE", 56, TextAnchor.MiddleCenter);
        Place(ccTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(1600f, 90f));

        ccOld = BuildCardView(ccDim.rectTransform, "OldCard", false);
        ccNew = BuildCardView(ccDim.rectTransform, "NewCard", true);
        ccRoot.SetActive(false);
    }

    CardView BuildCardView(RectTransform parent, string name, bool isNew)
    {
        var v = new CardView();
        v.border = MakeImage(name, parent, Color.white);
        v.rt = v.border.rectTransform;
        Place(v.rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));
        v.group = v.border.gameObject.AddComponent<CanvasGroup>();

        // Back of the card (shown while the new card is mid-flip).
        var backGO = new GameObject("Back", typeof(RectTransform));
        backGO.transform.SetParent(v.rt, false);
        Stretch((RectTransform)backGO.transform);
        v.back = backGO;
        Image backFill = MakeImage("Fill", backGO.transform, new Color(0.16f, 0.1f, 0.1f));
        Stretch(backFill.rectTransform);
        backFill.rectTransform.offsetMin = new Vector2(10f, 10f);
        backFill.rectTransform.offsetMax = new Vector2(-10f, -10f);
        Text mark = MakeText("Mark", backGO.transform, "?", 160, TextAnchor.MiddleCenter);
        Stretch(mark.rectTransform);
        mark.color = new Color(0.7f, 0.6f, 0.5f);

        // Front of the card (clickable).
        var frontGO = new GameObject("Front", typeof(RectTransform));
        frontGO.transform.SetParent(v.rt, false);
        Stretch((RectTransform)frontGO.transform);
        v.front = frontGO;
        Image body = MakeImage("Body", frontGO.transform, new Color(0.1f, 0.075f, 0.075f, 1f));
        body.raycastTarget = true;
        Stretch(body.rectTransform);
        body.rectTransform.offsetMin = new Vector2(10f, 10f);
        body.rectTransform.offsetMax = new Vector2(-10f, -10f);
        Button btn = body.gameObject.AddComponent<Button>();
        btn.targetGraphic = body;
        btn.transition = Selectable.Transition.None;
        bool takeNew = isNew;
        btn.onClick.AddListener(() => { if (PlayerController.Instance) PlayerController.Instance.PickCard(takeNew); });

        v.tag = MakeText("Tag", frontGO.transform, isNew ? "FOUND" : "YOUR CARD", 30, TextAnchor.MiddleCenter);
        Place(v.tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(300f, 40f));

        v.emblem = MakeImage("Emblem", frontGO.transform, Color.white);
        v.emblem.sprite = MakeCircleSprite();
        Place(v.emblem.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 105f), new Vector2(120f, 120f));

        v.title = MakeText("Name", frontGO.transform, "", 46, TextAnchor.MiddleCenter);
        Place(v.title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(310f, 60f));

        v.desc = MakeText("Desc", frontGO.transform, "", 25, TextAnchor.UpperCenter);
        v.desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        Place(v.desc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -115f), new Vector2(290f, 150f));

        v.key = MakeText("Key", frontGO.transform, isNew ? "TAKE   [2] / [D]" : "KEEP   [1] / [A]", 28, TextAnchor.MiddleCenter);
        Place(v.key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(310f, 40f));
        return v;
    }

    void FillCardView(CardView v, CardType type)
    {
        Color tint = CardInfo.Tint(type);
        v.title.text = CardInfo.Name(type);
        v.title.color = tint;
        v.desc.text = CardInfo.Description(type);
        v.emblem.color = new Color(tint.r, tint.g, tint.b, 0.9f);
        v.key.color = tint;
    }

    static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - Mathf.Pow(1f - k, 3f); }

    /// <summary>
    /// Both cards fly in (the found one flips over), you pick one: the chosen card flies to the HUD, the other is thrown away.
    /// </summary>
    void UpdateCardChoice(PlayerController player)
    {
        if (ccRoot == null) return;
        bool on = player != null && player.CardChoiceActive;
        if (ccRoot.activeSelf != on) ccRoot.SetActive(on);
        if (!on) { ccWasOn = false; return; }

        if (!ccWasOn)
        {
            ccWasOn = true;
            ccOld.hover = ccNew.hover = 0f;
            FillCardView(ccOld, player.ChoiceHeld);
            FillCardView(ccNew, player.ChoiceFound);
        }

        float t = player.ChoiceTime;
        int picked = player.ChoicePicked;
        float u = picked >= 0 ? Mathf.Clamp01(player.ChoicePickTime / PlayerController.ChoiceOutro) : 0f;
        float uEase = u * u; // accelerates towards the end

        // Where the HUD card text sits (target of the flying card), in the overlay's local space.
        Vector2 hudPos = new Vector2(-760f, -400f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(ccDim.rectTransform,
                RectTransformUtility.WorldToScreenPoint(null, cardsText.rectTransform.position), null, out Vector2 local))
            hudPos = local + new Vector2(140f, 35f);

        float I = PlayerController.ChoiceIntro; // all fly-in times are fractions of it
        float dim = 0.65f * EaseOut(t / (0.47f * I)) * (1f - uEase);
        ccDim.color = new Color(0f, 0f, 0f, dim);
        float textA = EaseOut(t / (0.53f * I)) * (1f - Mathf.Clamp01(u * 2.5f));
        ccTitle.color = new Color(TextColor.r, TextColor.g, TextColor.b, textA);

        // Old card: slides out of the HUD to the left slot.
        float kOld = EaseOut(t / (0.73f * I));
        AnimateCard(ccOld, Vector2.Lerp(hudPos, new Vector2(-230f, 0f), kOld), Mathf.Lerp(0.35f, 1f, kOld), 1f, kOld, picked, 0, u, hudPos, player);

        // New card: flies up from the bottom, flipping over, to the right slot.
        float kNew = EaseOut((t - 0.2f * I) / (0.8f * I));
        Vector2 newPos = Vector2.Lerp(new Vector2(0f, -430f), new Vector2(230f, 0f), kNew) + Vector2.up * (Mathf.Sin(kNew * Mathf.PI) * 90f);
        float flip = Mathf.Cos((1f - kNew) * Mathf.PI * 2.5f);
        AnimateCard(ccNew, newPos, Mathf.Lerp(0.4f, 1f, kNew), flip, kNew, picked, 1, u, hudPos, player);
    }

    void AnimateCard(CardView v, Vector2 pos, float scale, float flipCos, float appear,
                     int picked, int index, float u, Vector2 hudPos, PlayerController player)
    {
        float alpha = Mathf.Clamp01(appear * 3f);
        float rot = 0f;
        bool interactive = player.ChoiceCanPick;

        // Hover (mouse over the card) lifts it a little.
        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool over = interactive && mouse != null
                    && RectTransformUtility.RectangleContainsScreenPoint(v.rt, mouse.position.ReadValue(), null);
        v.hover = Mathf.MoveTowards(v.hover, over ? 1f : 0f, Time.unscaledDeltaTime * 8f);

        if (picked < 0)
        {
            if (interactive) pos.y += Mathf.Sin(Time.unscaledTime * 2f + index * 1.7f) * 6f; // gentle float
            scale *= 1f + 0.07f * v.hover;
        }
        else if (picked == index)
        {
            // Chosen: shrinks and flies to the HUD card slot, fading at the very end.
            float k = u * u * (3f - 2f * u);
            pos = Vector2.Lerp(pos, hudPos, k);
            scale = Mathf.Lerp(scale * 1.07f, 0.28f, k);
            alpha = 1f - Mathf.Clamp01((u - 0.8f) / 0.2f);
        }
        else
        {
            // Not chosen: tossed away (tilts, falls, fades).
            pos += new Vector2(index == 0 ? -140f : 140f, -260f) * (u * u);
            rot = (index == 0 ? 25f : -25f) * u;
            scale *= Mathf.Lerp(1f, 0.6f, u);
            alpha = 1f - u;
        }

        bool frontSide = flipCos >= 0f;
        v.front.SetActive(frontSide);
        v.back.SetActive(!frontSide);
        Color tint = CardInfo.Tint(index == 0 ? player.ChoiceHeld : player.ChoiceFound);
        Color edge = frontSide ? Color.Lerp(tint, Color.white, 0.45f * v.hover) : new Color(0.5f, 0.42f, 0.35f);
        v.border.color = edge;

        v.rt.anchoredPosition = pos;
        v.rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        v.rt.localScale = new Vector3(scale * Mathf.Max(0.04f, Mathf.Abs(flipCos)), scale, 1f);
        v.group.alpha = alpha;
        v.group.blocksRaycasts = interactive;
    }

    static Sprite MakeCircleSprite()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d)));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>The coin flip: the coin is tossed up, spins (YOU on one side, CLOWN on the other) and lands on the result.</summary>
    void UpdateFightBack(GameManager gm)
    {
        if (fbRoot == null) return;
        bool on = gm.FightBackActive;
        if (fbRoot.activeSelf != on) fbRoot.SetActive(on);
        if (!on) return;

        fbDim.color = new Color(0f, 0f, 0f, Mathf.Clamp01(gm.FightBackElapsed / 0.3f) * 0.6f);

        float t = gm.FightBackT;
        float ease = 1f - Mathf.Pow(1f - t, 3f);
        float angle = gm.FightBackHalfTurns * Mathf.PI * ease;
        float c = Mathf.Cos(angle);
        float toss = Mathf.Sin(Mathf.PI * t) * 230f; // up and back down
        fbCoinRT.anchoredPosition = new Vector2(0f, 40f + toss);
        fbCoinRT.localScale = new Vector3(Mathf.Max(0.05f, Mathf.Abs(c)), 1f + 0.12f * Mathf.Sin(Mathf.PI * t), 1f);

        bool youSide = c >= 0f;
        fbFace.text = youSide ? "YOU" : "CLOWN";
        fbCoin.color = youSide ? new Color(0.95f, 0.78f, 0.2f) : new Color(0.85f, 0.35f, 0.3f);

        if (gm.FightBackRevealed)
        {
            bool won = gm.FightBackWon;
            fbResult.text = won ? "YOU BEAT THE CLOWN!" : "THE CLOWN WINS...";
            fbResult.color = won ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f);
            float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 10f);
            fbResult.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }
        else fbResult.text = "";
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
