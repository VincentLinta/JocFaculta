using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the game state. The game has two real-time phases:
///   1. Hiding: the player has hideTime seconds to find a hiding spot (the killer is not in the level yet).
///      The phase ends when the player has been hidden for hiddenSettleTime seconds, or when the time runs out.
///   2. Chase: the killer spawns and checks the hiding spots one by one. If he has not found the player
///      after chaseTime seconds, the player wins.
/// Also handles pause, win, lose and restart. Restart reloads the scene, which resets everything.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Lost, Won }
    public enum Phase { Hiding, Chase }

    public static GameManager Instance { get; private set; }

    [Header("Phases")]
    [Tooltip("Seconds the player has to hide before the killer arrives.")]
    public float hideTime = 60f;
    [Tooltip("Seconds the killer has to find the player. Survive this long to win.")]
    public float chaseTime = 30f;
    [Tooltip("The hiding phase ends this many seconds after the player is fully hidden.")]
    public float hiddenSettleTime = 2f;

    [Header("Old key / exit goal")]
    [Tooltip("Off = the keys and exits of the old turn-based game are switched off (the goal is just to survive).")]
    public bool useKeysAndExits = false;
    public int keysRequired = 3;

    [Header("Brightness (applied when the game starts)")]
    [Tooltip("Brightens the scene at runtime. Untick to get the original dark look.")]
    public bool brightenScene = true;
    [Tooltip("Flat ambient light. The original was (0.07, 0.07, 0.09); higher = brighter shadows.")]
    public Color ambientColor = new Color(0.40f, 0.40f, 0.46f);
    [Tooltip("Directional light intensity. The original was 0.06.")]
    public float sunIntensity = 0.7f;
    [Tooltip("Multiplies the intensity of every lamp / point light.")]
    public float lampIntensityMultiplier = 2f;
    [Tooltip("Multiplies the range of every lamp / point light.")]
    public float lampRangeMultiplier = 1.4f;
    public Color fogColor = new Color(0.12f, 0.13f, 0.18f);
    public float fogStart = 45f;
    public float fogEnd = 130f;

    public GameState State { get; private set; } = GameState.Playing;
    public Phase CurrentPhase { get; private set; } = Phase.Hiding;

    /// <summary>True when an Escape card opened one of the escape doors.</summary>
    public bool EscapeArmed { get; private set; }
    /// <summary>True after the player pressed Q: he runs for the exit and the clown rushes him.</summary>
    public bool EscapeActive { get; private set; }
    /// <summary>The escape door that is open.</summary>
    public ExitDoor EscapeDoor { get; private set; }
    /// <summary>True during the short camera shot of the opened door, right before the clown spawns.</summary>
    public bool IsRevealing { get; private set; }
    /// <summary>Q works once a door is open and the run has not started yet.</summary>
    public bool CanUseEscape => IsPlaying && !IsPaused && EscapeArmed && !EscapeActive && !IsRevealing;

    /// <summary>Opens one random escape door and points the camera at it.</summary>
    public bool ArmEscape(float focusSeconds)
    {
        if (!IsPlaying || EscapeArmed) return false;
        var doors = new List<ExitDoor>();
        foreach (ExitDoor d in ExitDoor.All) if (d.isEscapeDoor) doors.Add(d);
        if (doors.Count == 0) return false;
        EscapeDoor = doors[Random.Range(0, doors.Count)];
        EscapeArmed = true;
        if (CameraFollow.Instance != null) CameraFollow.Instance.FocusOn(EscapeDoor.transform, focusSeconds);
        AudioManager.Play(AudioManager.Sfx.AllKeys);
        SearchableObject.ShowToast("ESCAPE - a door has opened! Press Q to run for it.", new Color(0.4f, 1f, 0.5f), 5f);
        return true;
    }

    /// <summary>Q pressed: the player runs for the open door and the clown rushes him.</summary>
    public bool ActivateEscape()
    {
        if (!CanUseEscape) return false;
        EscapeActive = true;
        if (KillerAI.Instance != null) KillerAI.Instance.BeginEscapeChase();
        SearchableObject.ShowToast("RUN! The clown is coming for you!", new Color(1f, 0.35f, 0.25f), 3f);
        return true;
    }

    /// <summary>Seconds left in the current phase.</summary>
    public float PhaseTimeLeft { get; private set; }
    public int Keys { get; private set; }
    public bool IsPaused { get; private set; }
    public string EndMessage { get; private set; } = "";

    public bool IsPlaying => State == GameState.Playing;
    /// <summary>True when the player may move and use action keys.</summary>
    public bool CanPlayerAct => IsPlaying && !IsPaused;

    float hiddenFor;
    int lastTickSecond = -1;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        PhaseTimeLeft = hideTime;

        if (brightenScene) ApplyBrightness();
    }

    void ApplyBrightness()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.type == LightType.Directional) l.intensity = sunIntensity;
            else
            {
                l.intensity *= lampIntensityMultiplier;
                l.range *= lampRangeMultiplier;
            }
        }

        Camera cam = Camera.main;
        if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = fogColor;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.escapeKey.wasPressedThisFrame && IsPlaying) TogglePause();
            // R restarts quickly from the end screens.
            if (!IsPlaying && kb.rKey.wasPressedThisFrame) Restart();
        }

        if (!IsPlaying || IsPaused || IsRevealing) return;

        PhaseTimeLeft = Mathf.Max(0f, PhaseTimeLeft - Time.deltaTime);
        if (CurrentPhase == Phase.Hiding) UpdateHidingPhase();
        else
        {
            // Found an Escape card during the chase (while not hidden): the door opens now.
            PlayerController p = PlayerController.Instance;
            if (!EscapeArmed && p != null && p.CardCount(CardType.Escape) > 0) ArmEscape(2f);
            if (PhaseTimeLeft <= 0f) Win("You survived. The clown did not find you.");
        }
    }

    // ------------------------------------------------------------------ phases

    void UpdateHidingPhase()
    {
        PlayerController player = PlayerController.Instance;
        hiddenFor = player != null && player.IsHidden ? hiddenFor + Time.deltaTime : 0f;

        // Tick every second during the last 10 seconds.
        int second = Mathf.CeilToInt(PhaseTimeLeft);
        if (second > 0 && second <= 10 && second != lastTickSecond)
        {
            lastTickSecond = second;
            AudioManager.Play(AudioManager.Sfx.Tick);
        }

        if (hiddenFor >= hiddenSettleTime || PhaseTimeLeft <= 0f) StartChasePhase();
    }

    void StartChasePhase()
    {
        // With an Escape card: first show the door that opened (camera + light), then the clown spawns.
        PlayerController p = PlayerController.Instance;
        if (!IsRevealing && !EscapeArmed && p != null && p.CardCount(CardType.Escape) > 0 && ArmEscape(2f))
        {
            StartCoroutine(RevealThenChase());
            return;
        }
        BeginChase();
    }

    System.Collections.IEnumerator RevealThenChase()
    {
        IsRevealing = true;
        yield return new WaitForSeconds(2f); // glide to the door, one second of looking at it
        IsRevealing = false;
        BeginChase();
    }

    void BeginChase()
    {
        CurrentPhase = Phase.Chase;
        PhaseTimeLeft = chaseTime;
        Debug.Log("[Game] Chase phase starts: the killer spawns.");
        if (KillerAI.Instance != null) KillerAI.Instance.BeginHunt();
    }

    // ------------------------------------------------------------------ pause

    public void TogglePause() { SetPaused(!IsPaused); }

    /// <summary>Freezes everything (timeScale 0), including the timers.</summary>
    public void SetPaused(bool paused)
    {
        if (!IsPlaying) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    // ------------------------------------------------------------------ keys / end

    /// <summary>Called by a key when picked up (only used when useKeysAndExits is on).</summary>
    public void CollectKey()
    {
        if (!IsPlaying) return;
        Keys++;
        if (Keys == keysRequired) AudioManager.Play(AudioManager.Sfx.AllKeys); // exits are open
        Debug.Log($"[Game] Key collected {Keys}/{keysRequired}");
    }

    public void Lose(string reason)
    {
        if (!IsPlaying) return;
        State = GameState.Lost;
        EndMessage = reason;
        AudioManager.Play(AudioManager.Sfx.Caught);
        EndGame();
    }

    /// <summary>Win through an exit (old goal).</summary>
    public void Win() { Win("You found all the keys and got out."); }

    public void Win(string message)
    {
        if (!IsPlaying) return;
        State = GameState.Won;
        EndMessage = message;
        AudioManager.Play(AudioManager.Sfx.Win);
        EndGame();
    }

    void EndGame()
    {
        IsPaused = false;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>Reloads the current scene. Works at any moment (pause menu or end screens).</summary>
    public void Restart()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    /// <summary>Stops Play mode in the editor, quits the app in a build.</summary>
    public void Quit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
