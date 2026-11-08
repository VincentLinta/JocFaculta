using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the game state: turns, keys, pause, win, lose and restart.
/// Restart reloads the scene, which resets everything (turns, stamina, keys, killer, positions).
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public enum GameState { Playing, Lost, Won }
    public enum Turn { Player, Killer }

    public static GameManager Instance { get; private set; }

    public int keysRequired = 3;
    [Tooltip("Short pause before the killer moves, so the turn change is readable.")]
    public float killerTurnDelay = 0.15f;

    public GameState State { get; private set; } = GameState.Playing;
    public Turn CurrentTurn { get; private set; } = Turn.Player;
    public int TurnNumber { get; private set; } = 1;
    public int Keys { get; private set; }
    public bool IsPaused { get; private set; }
    public string EndMessage { get; private set; } = "";

    public bool IsPlaying => State == GameState.Playing;
    /// <summary>True when the player may click / press action keys.</summary>
    public bool CanPlayerAct => IsPlaying && !IsPaused && CurrentTurn == Turn.Player;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame && IsPlaying) TogglePause();
        // R restarts quickly from the end screens.
        if (!IsPlaying && kb.rKey.wasPressedThisFrame) Restart();
    }

    // ------------------------------------------------------------------ turns

    /// <summary>Called by the player after every action. Runs the killer's turn, then gives the turn back.</summary>
    public void EndPlayerTurn()
    {
        if (!IsPlaying || CurrentTurn != Turn.Player) return;
        CurrentTurn = Turn.Killer;
        StartCoroutine(KillerPhase());
    }

    IEnumerator KillerPhase()
    {
        yield return new WaitForSeconds(killerTurnDelay);
        if (IsPlaying && KillerAI.Instance) yield return KillerAI.Instance.TakeTurn();
        if (!IsPlaying) yield break;
        TurnNumber++;
        CurrentTurn = Turn.Player;
    }

    // ------------------------------------------------------------------ pause

    public void TogglePause() { SetPaused(!IsPaused); }

    /// <summary>Freezes everything (timeScale 0), including the killer's move animation.</summary>
    public void SetPaused(bool paused)
    {
        if (!IsPlaying) return;
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    // ------------------------------------------------------------------ keys / end

    /// <summary>Called by a key when picked up. Makes the killer a bit faster.</summary>
    public void CollectKey()
    {
        if (!IsPlaying) return;
        Keys++;
        if (KillerAI.Instance) KillerAI.Instance.AddSpeedBonus();
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

    public void Win()
    {
        if (!IsPlaying) return;
        State = GameState.Won;
        EndMessage = "You found all the keys and got out.";
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
