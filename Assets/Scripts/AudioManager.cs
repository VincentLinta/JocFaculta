using System;
using UnityEngine;

/// <summary>
/// All game audio, generated in code (no downloaded files).
/// - Music: low evolving drone + slow heartbeat; a faster heartbeat and tense strings fade in during Chase.
/// - Sound effects: footstep, rustle, key chime, danger sting, caught, win, dash denied.
/// One instance survives scene reloads (Restart), so audio never doubles.
/// Master volume / mute use AudioListener.volume, so they control everything.
/// </summary>
[DefaultExecutionOrder(-300)]
public class AudioManager : MonoBehaviour
{
    public enum Sfx { Footstep, Rustle, Key, DangerSting, Caught, Win, Denied, AllKeys }

    public static AudioManager Instance { get; private set; }

    [Range(0f, 1f)] public float musicVolume = 0.55f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    [Tooltip("Music level while the pause menu is open.")]
    [Range(0f, 1f)] public float pausedMusicLevel = 0.3f;

    public float MasterVolume { get; private set; } = 0.8f;
    public bool Muted { get; private set; }

    const int Rate = 22050;
    const float TwoPi = Mathf.PI * 2f;

    AudioSource drone;
    AudioSource chaseLayer;
    AudioSource sfxSource;
    AudioSource stepSource;
    AudioClip[] clips;
    float chaseMix;
    float duck = 1f;

    [Header("New music mix")]
    [Tooltip("Old drone + heartbeat level under the new music (1 = as loud as before).")]
    [Range(0f, 1f)] public float baseLayerLevel = 0.28f;
    [Tooltip("Hidden player + killer this close (world units) = the old heartbeat comes back at full level.")]
    public float hiddenCloseDistance = 10f;
    [Range(0f, 1f)] public float ambientLevel = 0.5f;
    [Range(0f, 1f)] public float eventLevel = 0.35f;
    [Range(0f, 1f)] public float tensionLevel = 0.45f;
    [Range(0f, 1f)] public float chaseMusicLevel = 0.7f;
    [Range(0f, 1f)] public float creepLevel = 0.4f;
    [Tooltip("Seconds for a layer to fade in / to fade out.")]
    public float fadeInTime = 1.5f;
    public float fadeOutTime = 4f;

    AudioSource ambient;
    AudioSource tensionSrc;
    AudioSource chaseMusic;
    AudioSource creep;
    AudioSource eventSource;
    AudioSource hitSource;
    AudioClip[] eventClips;
    AudioClip turnHit;
    float baseScale = 0.28f;
    float tensionMix;
    float chaseMusicMix;
    float creepMix;
    float newDuck = 1f;
    float nextEventTime;
    GameManager.Turn lastTurn = GameManager.Turn.Player;

    [Header("Heartbeat (gets louder and faster as the killer gets closer)")]
    [Tooltip("Silent at this distance (tiles) or more.")]
    public float heartbeatFarTiles = 16f;
    public float heartbeatMidTiles = 10f;
    [Range(0f, 1f)] public float heartbeatMidVolume = 0.25f;
    public float heartbeatNearTiles = 5f;
    [Range(0f, 1f)] public float heartbeatNearVolume = 0.6f;
    [Tooltip("100% at this distance (tiles) or closer.")]
    public float heartbeatCloseTiles = 2f;
    public float heartbeatMinBpm = 60f;
    public float heartbeatMaxBpm = 150f;
    [Tooltip("Extra loudness while hidden (1.3 = up to 30% louder, clamped).")]
    [Range(1f, 2f)] public float heartbeatHiddenBoost = 1.3f;
    [Tooltip("Source volume at 100% (0.28 = same loudness as the old heartbeat while hidden).")]
    [Range(0f, 1f)] public float heartbeatLevel = 0.28f;
    [Tooltip("Smoothing time (s) for volume and tempo changes.")]
    public float heartbeatDamping = 0.3f;

    [Header("Background music (generated in code)")]
    [Tooltip("Calm music level (under the effects and the heartbeat). Overall music level = musicVolume.")]
    [Range(0f, 1f)] public float calmMusicLevel = 0.35f;
    [Tooltip("Beats per minute of the calm music.")]
    public float musicTempo = 64f;
    [Tooltip("MIDI root note of the music (57 = A3). Used when the clips are generated at Play start.")]
    public int musicRootNote = 57;
    [Tooltip("Optional royalty-free clip (put it in Assets/Audio/Music/). If set, it plays instead of the generated music.")]
    public AudioClip musicClipOverride;

    AudioSource heartSource;
    AudioClip heartLub;
    AudioClip heartDub;
    float heartLevel;
    float heartBpm = 60f;
    float nextBeatTime;
    float dubTime = -1f;

    AudioSource[] padSources;
    AudioSource[] melodySources;
    AudioSource bedSource;
    AudioSource overrideSource;
    AudioClip[] chordClips;
    System.Collections.Generic.Dictionary<int, AudioClip> noteClips;
    System.Random musicRng;
    double nextBarDsp;
    int barIndex;
    int melodyVoice;

    /// <summary>Current heartbeat loudness 0..1.3 and tempo (read by docs / tests).</summary>
    public float HeartbeatLevel01 => heartLevel;
    public float HeartbeatBpm => heartBpm;

    /// <summary>Plays a sound effect if the audio manager exists.</summary>
    public static void Play(Sfx s)
    {
        if (Instance != null) Instance.PlaySfx(s);
    }

    void Awake()
    {
        // Keep the first instance across Restart; drop the copy that comes with the reloaded scene.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        MasterVolume = PlayerPrefs.GetFloat("hs_volume", 0.8f);
        Muted = PlayerPrefs.GetInt("hs_mute", 0) == 1;
        ApplyVolume();

        drone = MakeLoop(MakeDrone());
        // The old fixed-tempo heartbeat loop is replaced by the distance-based heartbeat below
        // (same "lub-dub" sound, now one beat at a time so tempo and loudness can follow the killer).
        heartSource = gameObject.AddComponent<AudioSource>();
        heartSource.playOnAwake = false;
        heartSource.volume = 0f;
        heartLub = Build("HeartLub", 0.4f, t => 0.9f * Thump(t));
        heartDub = Build("HeartDub", 0.4f, t => 0.9f * 0.65f * Thump(t));
        chaseLayer = MakeLoop(MakeChaseLayer());
        chaseLayer.volume = 0f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        stepSource = gameObject.AddComponent<AudioSource>();
        stepSource.playOnAwake = false;

        // New music layers on top of the old drone + heartbeat.
        baseScale = baseLayerLevel;
        ambient = MakeLoop(MakeAmbientPad());
        tensionSrc = MakeLoop(MakeTensionLayer());
        chaseMusic = MakeLoop(MakeChaseMusic());
        creep = MakeLoop(MakeCreep());
        eventSource = gameObject.AddComponent<AudioSource>();
        eventSource.playOnAwake = false;
        hitSource = gameObject.AddComponent<AudioSource>();
        hitSource.playOnAwake = false;
        eventClips = new[]
        {
            MakeMusicBox(31), MakeMusicBox(47), MakeMusicBox(83),
            MakeScrape(5), MakeScrape(9), MakeCreak(12), MakeCreak(17), MakeWhisper(3), MakeWhisper(7),
        };
        turnHit = MakeTurnHit();
        nextEventTime = Time.unscaledTime + 5f;

        SetupMusic();

        clips = new AudioClip[8];
        clips[(int)Sfx.AllKeys] = MakeAllKeys();
        clips[(int)Sfx.Footstep] = MakeFootstep();
        clips[(int)Sfx.Rustle] = MakeRustle();
        clips[(int)Sfx.Key] = MakeKeyChime();
        clips[(int)Sfx.DangerSting] = MakeDangerSting();
        clips[(int)Sfx.Caught] = MakeCaught();
        clips[(int)Sfx.Win] = MakeWinJingle();
        clips[(int)Sfx.Denied] = MakeDenied();
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        KillerAI killer = KillerAI.Instance;

        // Chase layer fades in quickly, calms down slowly.
        bool chasing = gm != null && gm.IsPlaying && killer != null && killer.CurrentState == KillerAI.State.Chase;
        chaseMix = Mathf.MoveTowards(chaseMix, chasing ? 1f : 0f, Time.unscaledDeltaTime * (chasing ? 1.5f : 0.35f));

        // Lower the music in the pause menu and on the end screens.
        float targetDuck = 1f;
        if (gm != null)
        {
            if (gm.IsPaused) targetDuck = pausedMusicLevel;
            else if (!gm.IsPlaying) targetDuck = 0.15f;
        }
        duck = Mathf.MoveTowards(duck, targetDuck, Time.unscaledDeltaTime * 1.5f);

        // ---- New layers, driven by the killer's distance in tiles.
        PlayerController player = PlayerController.Instance;
        float tile = GridManager.Instance ? GridManager.Instance.tileSize : 2f;
        bool playing = gm != null && gm.IsPlaying;
        float distWorld = killer != null ? killer.DistanceToPlayer : 999f;
        float dTiles = distWorld / tile;
        bool hidden = player != null && player.IsHidden;
        bool hiddenSafe = hidden && killer != null && !killer.LastHideWitnessed;
        bool hiddenClose = playing && hidden && distWorld < hiddenCloseDistance;
        float dt = Time.unscaledDeltaTime;

        float tensionT = playing ? Mathf.Clamp01((10f - dTiles) / 5f) : 0f;
        if (hiddenSafe) tensionT *= 0.4f;
        float chaseT = playing && !hiddenSafe && (chasing || dTiles < 5f) ? 1f : 0f;
        float creepT = playing && hiddenSafe ? 1f : 0f;
        tensionMix = Fade(tensionMix, tensionT, dt);
        chaseMusicMix = Fade(chaseMusicMix, chaseT, dt);
        creepMix = Fade(creepMix, creepT, dt);

        // Old drone + heartbeat: a quiet layer, back to full level when hidden with the killer close.
        baseScale = Mathf.MoveTowards(baseScale, hiddenClose ? 1f : baseLayerLevel, dt * 0.6f);
        // New music steps back in that moment so the heartbeat stays clear.
        newDuck = Mathf.MoveTowards(newDuck, hiddenClose ? 0.55f : 1f, dt * 0.7f);

        float m = musicVolume * duck;
        drone.volume = m * 0.8f * baseScale;
        chaseLayer.volume = m * 0.75f * chaseMix * baseScale;

        UpdateHeartbeat(playing && !gm.IsPaused && killer != null, dTiles, hidden, dt);
        UpdateMusic(m * newDuck * (1f - 0.45f * chaseMusicMix));

        float n = m * newDuck;
        ambient.volume = n * ambientLevel * (1f - 0.4f * chaseMusicMix);
        tensionSrc.volume = n * tensionLevel * tensionMix * (1f - 0.5f * chaseMusicMix);
        chaseMusic.volume = n * chaseMusicLevel * chaseMusicMix;
        creep.volume = n * creepLevel * creepMix;

        // Random distant events (music box, scrapes, creaks, whispers): never the same order twice.
        if (playing && !gm.IsPaused && Time.unscaledTime > nextEventTime)
        {
            nextEventTime = Time.unscaledTime + UnityEngine.Random.Range(7f, 18f);
            eventSource.pitch = UnityEngine.Random.Range(0.85f, 1.1f);
            eventSource.panStereo = UnityEngine.Random.Range(-0.7f, 0.7f);
            AudioClip ev = eventClips[UnityEngine.Random.Range(0, eventClips.Length)];
            eventSource.PlayOneShot(ev, n * eventLevel * (1f - 0.6f * chaseMusicMix));
        }

        // Driving low hit at the start of each killer turn while the chase music plays.
        if (gm != null && gm.CurrentTurn != lastTurn)
        {
            lastTurn = gm.CurrentTurn;
            if (playing && lastTurn == GameManager.Turn.Killer && chaseMusicMix > 0.3f)
                hitSource.PlayOneShot(turnHit, n * chaseMusicLevel * chaseMusicMix);
        }
    }

    // ------------------------------------------------------------------ heartbeat

    /// <summary>0..1 from the distance curve: 0 at far, mid/near points, 1 at close.</summary>
    public float HeartbeatCurve(float tiles)
    {
        if (tiles >= heartbeatFarTiles) return 0f;
        if (tiles <= heartbeatCloseTiles) return 1f;
        if (tiles >= heartbeatMidTiles) return Mathf.Lerp(heartbeatMidVolume, 0f, Mathf.InverseLerp(heartbeatMidTiles, heartbeatFarTiles, tiles));
        if (tiles >= heartbeatNearTiles) return Mathf.Lerp(heartbeatNearVolume, heartbeatMidVolume, Mathf.InverseLerp(heartbeatNearTiles, heartbeatMidTiles, tiles));
        return Mathf.Lerp(1f, heartbeatNearVolume, Mathf.InverseLerp(heartbeatCloseTiles, heartbeatNearTiles, tiles));
    }

    /// <summary>
    /// One heartbeat source. Distance is read every frame (the killer's animated position), so it changes
    /// smoothly while he walks. Silent in the pause menu, fades out on the end screens.
    /// </summary>
    void UpdateHeartbeat(bool active, float tiles, bool hidden, float dt)
    {
        float target = active ? HeartbeatCurve(tiles) * (hidden ? heartbeatHiddenBoost : 1f) : 0f;
        float k = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, heartbeatDamping));
        heartLevel += (target - heartLevel) * k;
        float bpmTarget = Mathf.Lerp(heartbeatMinBpm, heartbeatMaxBpm, Mathf.InverseLerp(heartbeatFarTiles, heartbeatCloseTiles, tiles));
        heartBpm += (bpmTarget - heartBpm) * k;
        heartSource.volume = Mathf.Clamp01(heartbeatLevel * heartLevel);

        float now = Time.unscaledTime;
        if (heartLevel < 0.005f)
        {
            nextBeatTime = now; // start right away when it becomes audible again
            dubTime = -1f;
            return;
        }
        if (now >= nextBeatTime)
        {
            float period = 60f / Mathf.Max(20f, heartBpm);
            heartSource.PlayOneShot(heartLub, 1f);
            dubTime = now + Mathf.Min(0.22f, period * 0.35f);
            nextBeatTime = Mathf.Max(nextBeatTime + period, now + period * 0.5f);
        }
        if (dubTime > 0f && now >= dubTime)
        {
            heartSource.PlayOneShot(heartDub, 1f);
            dubTime = -1f;
        }
    }

    // ------------------------------------------------------------------ calm background music

    // Chords as semitones from the root (A minor when root = A3).
    static readonly int[][] ChordNotes =
    {
        new[] { 0, 3, 7 },        // Am
        new[] { -4, 0, 3, 7 },    // Fmaj7
        new[] { -7, -4, 0 },      // Dm
        new[] { -5, -1, 2 },      // E
        new[] { 1, 5, 8 },        // Bb  (flat second, eerie)
        new[] { -6, -3, 0 },      // D#dim (tritone, eerie)
    };
    static readonly int[] ChordBass = { -12, -16, -19, -17, -11, -18 };
    // Natural minor scale (semitones above root + 12) for the music box melody.
    static readonly int[] MelodyScale = { 0, 2, 3, 5, 7, 8, 10, 12, 14, 15 };
    static readonly int[] MotifBase = { 4, 2, 3, 1, 0 };
    static readonly float[] MotifBeats = { 0f, 1f, 1.5f, 2f, 3f };

    static float MidiToHz(float midi) => 440f * Mathf.Pow(2f, (midi - 69f) / 12f);

    float BarSeconds => 4f * 60f / Mathf.Max(30f, musicTempo);

    void SetupMusic()
    {
        musicRng = new System.Random(unchecked((int)DateTime.Now.Ticks));
        padSources = new[] { AddSource(), AddSource() };
        melodySources = new[] { AddSource(), AddSource(), AddSource(), AddSource() };
        overrideSource = AddSource();
        overrideSource.loop = true;

        chordClips = new AudioClip[ChordNotes.Length];
        for (int i = 0; i < ChordNotes.Length; i++) chordClips[i] = MakeChord(i);
        noteClips = new System.Collections.Generic.Dictionary<int, AudioClip>();
        foreach (int s in new[] { -2, 0, 1, 2, 3, 5, 6, 7, 8, 10, 12, 14, 15, 17 })
            noteClips[s] = MakeCelesta(s);
        bedSource = MakeLoop(MakeAirBed());
        nextBarDsp = AudioSettings.dspTime + 0.5;
    }

    AudioSource AddSource()
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.volume = 0f;
        return s;
    }

    /// <summary>Sequencer: one chord per bar, a varied music-box motif (with rests), never the same twice.</summary>
    void UpdateMusic(float level)
    {
        float calm = level * calmMusicLevel;
        if (musicClipOverride != null)
        {
            if (overrideSource.clip != musicClipOverride)
            {
                overrideSource.clip = musicClipOverride;
                overrideSource.Play();
            }
            overrideSource.volume = calm;
            foreach (AudioSource s in padSources) s.volume = 0f;
            foreach (AudioSource s in melodySources) s.volume = 0f;
            bedSource.volume = 0f;
            return;
        }
        if (overrideSource.isPlaying) overrideSource.Stop();

        foreach (AudioSource s in padSources) s.volume = calm;
        foreach (AudioSource s in melodySources) s.volume = calm * 0.8f;
        bedSource.volume = calm * 0.35f;

        double dsp = AudioSettings.dspTime;
        if (nextBarDsp < dsp + 0.05) nextBarDsp = dsp + 0.1; // fell behind (e.g. editor hiccup)
        if (nextBarDsp > dsp + 0.25) return;                 // schedule a bar shortly before it starts

        float bar = BarSeconds;
        float beat = bar / 4f;

        // Chord: Am - Fmaj7 - Dm - E, sometimes an eerie flat-second or tritone chord instead.
        int chord = barIndex % 4;
        if (barIndex % 4 != 0 && musicRng.NextDouble() < 0.12) chord = musicRng.NextDouble() < 0.5 ? 4 : 5;
        AudioSource pad = padSources[barIndex % 2];
        pad.clip = chordClips[chord];
        pad.PlayScheduled(nextBarDsp);

        // Melody: rest about 30% of bars, otherwise a varied version of the motif.
        if (musicRng.NextDouble() > 0.3)
        {
            int shift = chord == 2 ? -1 : chord == 3 ? 1 : 0;
            for (int i = 0; i < MotifBase.Length; i++)
            {
                if (musicRng.NextDouble() < 0.25) continue; // drop a note
                int idx = Mathf.Clamp(MotifBase[i] + shift + (musicRng.NextDouble() < 0.2 ? (musicRng.Next(2) * 2 - 1) : 0), 0, MelodyScale.Length - 1);
                int semis = MelodyScale[idx];
                if (musicRng.NextDouble() < 0.06) semis = musicRng.NextDouble() < 0.5 ? 1 : 6; // eerie touch
                float pos = MotifBeats[i] + (musicRng.NextDouble() < 0.15 ? 0.5f : 0f);
                AudioSource v = melodySources[melodyVoice++ % melodySources.Length];
                v.clip = noteClips[semis];
                v.PlayScheduled(nextBarDsp + pos * beat);
            }
        }

        nextBarDsp += bar;
        barIndex++;
    }

    /// <summary>Soft warm pad chord + low cello-like bass, one bar long with a 2 s release.</summary>
    AudioClip MakeChord(int index)
    {
        float bar = BarSeconds;
        float len = bar + 2f;
        int[] notes = ChordNotes[index];
        var f = new float[notes.Length];
        for (int i = 0; i < notes.Length; i++) f[i] = MidiToHz(musicRootNote + notes[i]);
        float bass = MidiToHz(musicRootNote + ChordBass[index]);
        float lp = 0f;
        return Build("Chord" + index, len, t =>
        {
            float env = Mathf.Clamp01(t / 1.0f) * (t < bar ? 1f : Mathf.Clamp01(1f - (t - bar) / 2f));
            float s = 0f;
            for (int i = 0; i < f.Length; i++)
                s += 0.055f * (Mathf.Sin(TwoPi * f[i] * t) + Mathf.Sin(TwoPi * f[i] * 1.003f * t) + 0.25f * Mathf.Sin(TwoPi * f[i] * 2f * t) + 0.08f * Mathf.Sin(TwoPi * f[i] * 3f * t));
            float vib = 1f + 0.003f * Mathf.Sin(TwoPi * 5f * t);
            float cello = 0f;
            for (int h = 1; h <= 5; h++) cello += Mathf.Sin(TwoPi * bass * vib * h * t) / h;
            float bassEnv = Mathf.Clamp01(t / 0.5f) * (t < bar ? 1f : Mathf.Clamp01(1f - (t - bar) / 1.5f));
            s += 0.07f * cello * bassEnv;
            lp += (s * env - lp) * 0.25f; // warm
            return lp;
        });
    }

    /// <summary>Lullaby celesta / music-box note.</summary>
    AudioClip MakeCelesta(int semis)
    {
        float f = MidiToHz(musicRootNote + 12 + semis);
        float lp = 0f;
        return Build("Celesta" + semis, 2.4f, t =>
        {
            float att = Mathf.Clamp01(t / 0.005f);
            float s = Mathf.Sin(TwoPi * f * t) + 0.3f * Mathf.Sin(TwoPi * f * 2f * t) * Mathf.Exp(-t * 6f) + 0.12f * Mathf.Sin(TwoPi * f * 4.02f * t) * Mathf.Exp(-t * 9f);
            lp += (0.22f * s * att * Mathf.Exp(-t * 1.8f) - lp) * 0.5f;
            return lp;
        });
    }

    /// <summary>Very soft airy noise bed (10 s loop with a cross-faded seam).</summary>
    static AudioClip MakeAirBed()
    {
        const float L = 10f;
        const float X = 1f;
        int n = Mathf.CeilToInt(L * Rate);
        int nx = Mathf.CeilToInt(X * Rate);
        var raw = new float[n + nx];
        var rng = new System.Random(5);
        float a = 0f, b = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float noise = (float)rng.NextDouble() * 2f - 1f;
            a += (noise - a) * 0.12f;
            b += (a - b) * 0.01f;
            float t = i / (float)Rate;
            raw[i] = 0.25f * (a - b) * (0.7f + 0.3f * Mathf.Sin(TwoPi * 0.1f * t));
        }
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = raw[i];
        for (int i = 0; i < nx; i++)
        {
            float w = i / (float)nx;
            data[i] = raw[i] * w + raw[n + i] * (1f - w); // the tail flows into the start
        }
        AudioClip clip = AudioClip.Create("AirBed", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>"Mini win": rising 4-note chime, then a soft door-unlock clunk (about 1.5 s).</summary>
    static AudioClip MakeAllKeys()
    {
        var rng = new System.Random(9);
        float lp = 0f;
        return Build("AllKeys", 1.6f, t =>
        {
            float chime = 0.22f * (Note(t, 659.3f, 5f) + Note(t - 0.14f, 784f, 5f) + Note(t - 0.28f, 987.8f, 5f) + Note(t - 0.42f, 1318.5f, 4f));
            float c = t - 0.95f;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.15f;
            float clunk = c < 0f ? 0f : 0.45f * Thump(c, 70f) + 0.25f * lp * Mathf.Exp(-c * 30f) + (c > 0.08f ? 0.3f * Thump(c - 0.08f, 95f) : 0f);
            return chime + clunk;
        });
    }

    float Fade(float current, float target, float dt)
    {
        float time = target > current ? fadeInTime : fadeOutTime;
        return Mathf.MoveTowards(current, target, dt / Mathf.Max(0.05f, time));
    }

    // ------------------------------------------------------------------ volume

    public void SetMasterVolume(float v)
    {
        MasterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("hs_volume", MasterVolume);
        ApplyVolume();
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        PlayerPrefs.SetInt("hs_mute", Muted ? 1 : 0);
        ApplyVolume();
    }

    void ApplyVolume() { AudioListener.volume = Muted ? 0f : MasterVolume; }

    public void PlaySfx(Sfx s)
    {
        AudioClip clip = clips[(int)s];
        if (s == Sfx.Footstep)
        {
            stepSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
            stepSource.PlayOneShot(clip, sfxVolume * 0.6f);
            return;
        }
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    AudioSource MakeLoop(AudioClip clip)
    {
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = 0f;
        src.Play();
        return src;
    }

    // ------------------------------------------------------------------ generation helpers

    static AudioClip Build(string name, float seconds, Func<float, float> sample)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
        AudioClip clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Rounds a frequency so it fits a whole number of cycles in the loop (seamless loop).</summary>
    static float Fit(float freq, float loop) => Mathf.Round(freq * loop) / loop;

    static float Sin(float freq, float t, float phase = 0f) => Mathf.Sin(TwoPi * freq * t + phase);

    static float Thump(float t, float freq = 48f) => t < 0f ? 0f : Mathf.Sin(TwoPi * freq * t) * Mathf.Exp(-t * 18f);

    static float Note(float t, float freq, float decay) =>
        t < 0f ? 0f : (Sin(freq, t) + 0.3f * Sin(freq * 2f, t)) * Mathf.Exp(-t * decay);

    // ------------------------------------------------------------------ music

    static AudioClip MakeDrone()
    {
        const float L = 16f;
        return Build("Drone", L, t =>
        {
            float wobble = 0.3f * Sin(Fit(0.25f, L), t);
            float s = 0.30f * Mathf.Sin(TwoPi * Fit(55f, L) * t + wobble) * (0.6f + 0.4f * Sin(1f / L, t));
            s += 0.20f * Sin(Fit(82.5f, L), t) * (0.5f + 0.5f * Sin(2f / L, t, 1f));
            s += 0.10f * Sin(Fit(110f, L), t) * (0.5f + 0.5f * Sin(3f / L, t));
            s += 0.07f * Sin(Fit(116.5f, L), t) * (0.5f + 0.5f * Sin(1f / L, t, 2f)); // slow dissonance
            s += 0.04f * Sin(Fit(233f, L), t) * (0.5f + 0.5f * Sin(5f / L, t));
            return s;
        });
    }

    /// <summary>Heartbeat "lub-dub" every period seconds, repeated beats times.</summary>
    static AudioClip MakeHeartbeat(string name, float period, int beats, float level)
    {
        return Build(name, period * beats, t =>
        {
            float local = t % period;
            return level * (Thump(local) + 0.65f * Thump(local - 0.22f));
        });
    }

    static AudioClip MakeChaseLayer()
    {
        const float period = 0.55f;
        const float L = period * 8f;
        float a = Fit(440f, L), b = Fit(466f, L), trem = Fit(7f, L), bass = Fit(41f, L);
        return Build("ChaseLayer", L, t =>
        {
            float local = t % period;
            float beat = Thump(local, 52f) + 0.7f * Thump(local - 0.16f, 52f);
            float strings = 0.09f * (Sin(a, t) + Sin(b, t)) * (0.6f + 0.4f * Sin(trem, t));
            float low = 0.12f * Sin(bass, t);
            return 0.9f * beat + strings + low;
        });
    }

    // ------------------------------------------------------------------ new music

    /// <summary>40 s of slow dissonant pads with a faint sub rumble (seamless loop).</summary>
    static AudioClip MakeAmbientPad()
    {
        const float L = 40f;
        float[] f = { Fit(73.4f, L), Fit(77.8f, L), Fit(110f, L), Fit(116.5f, L), Fit(155.6f, L), Fit(207.7f, L) };
        var f2 = new float[f.Length];
        for (int i = 0; i < f.Length; i++) f2[i] = Fit(f[i] * 1.004f, L); // slow beating = detune
        float[] rate = { 1f / L, 2f / L, 3f / L, 1f / L, 2f / L, 5f / L };
        float[] phase = { 0f, 2f, 1f, 4f, 3f, 5f };
        float sub = Fit(31f, L);
        var rng = new System.Random(11);
        float lp1 = 0f, lp2 = 0f;
        return Build("AmbientPad", L, t =>
        {
            float s = 0f;
            for (int i = 0; i < f.Length; i++)
            {
                float env = 0.5f + 0.5f * Mathf.Sin(TwoPi * rate[i] * t + phase[i]);
                env *= env;
                s += 0.07f * env * (Mathf.Sin(TwoPi * f[i] * t) + 0.7f * Mathf.Sin(TwoPi * f2[i] * t));
            }
            s += 0.07f * Sin(sub, t) * (0.6f + 0.4f * Sin(1f / L, t));
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp1 += (noise - lp1) * 0.02f;
            lp2 += (lp1 - lp2) * 0.02f;
            return s + 0.8f * lp2;
        });
    }

    /// <summary>Distant, slightly detuned music-box phrase with long ringing notes.</summary>
    static AudioClip MakeMusicBox(int seed)
    {
        var rng = new System.Random(seed);
        float[] scale = { 659.3f, 698.5f, 784f, 830.6f, 880f, 987.8f, 1046.5f, 1174.7f };
        int count = 4 + rng.Next(3);
        var times = new float[count];
        var freqs = new float[count];
        float now = 0.2f;
        for (int i = 0; i < count; i++)
        {
            times[i] = now;
            freqs[i] = scale[rng.Next(scale.Length)] * 0.99f;
            now += 0.45f + (float)rng.NextDouble() * 0.55f;
        }
        float lp = 0f;
        return Build("MusicBox" + seed, now + 2.5f, t =>
        {
            float s = 0f;
            float wobble = 1f + 0.004f * Mathf.Sin(TwoPi * 5f * t);
            for (int i = 0; i < count; i++)
            {
                float lt = t - times[i];
                if (lt < 0f) continue;
                float ring = Mathf.Sin(TwoPi * freqs[i] * wobble * lt) + 0.35f * Mathf.Sin(TwoPi * freqs[i] * 3.01f * lt) * Mathf.Exp(-lt * 6f);
                s += ring * Mathf.Exp(-lt * 2.2f);
            }
            lp += (s * 0.25f - lp) * 0.35f; // soft, far away
            return lp;
        });
    }

    /// <summary>Low metallic scrape: gliding inharmonic tone with grit and noise.</summary>
    static AudioClip MakeScrape(int seed)
    {
        var rng = new System.Random(seed);
        const float len = 2.8f;
        float baseF = 140f + (float)rng.NextDouble() * 80f;
        float ph1 = 0f, ph2 = 0f, ph3 = 0f, lp = 0f;
        return Build("Scrape" + seed, len, t =>
        {
            float env = Mathf.Sin(Mathf.PI * t / len);
            env *= env;
            float f = baseF * (1f + 0.15f * t / len) * (1f + 0.02f * ((float)rng.NextDouble() - 0.5f));
            ph1 += TwoPi * f / Rate;
            ph2 += TwoPi * f * 2.41f / Rate;
            ph3 += TwoPi * f * 5.13f / Rate;
            float tone = Mathf.Sin(ph1) + 0.6f * Mathf.Sin(ph2) + 0.3f * Mathf.Sin(ph3);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.15f;
            float grit = 0.5f + 0.5f * Mathf.PerlinNoise(t * 30f, seed);
            return 0.25f * env * (tone * grit * 0.6f + lp * 0.8f);
        });
    }

    /// <summary>Old wood creak: slowing / speeding click train through a low body tone.</summary>
    static AudioClip MakeCreak(int seed)
    {
        var rng = new System.Random(seed);
        const float len = 1.6f;
        float rate0 = 18f + (float)rng.NextDouble() * 10f;
        float ph = 0f, lp = 0f;
        return Build("Creak" + seed, len, t =>
        {
            float rate = rate0 * (1f + 0.6f * Mathf.Sin(Mathf.PI * t / len));
            ph += rate / Rate;
            float pulse = (ph % 1f) < 0.08f ? 1f : 0f;
            lp += (pulse - lp) * 0.3f;
            float body = lp * Mathf.Sin(TwoPi * 420f * t);
            return 0.35f * Mathf.Sin(Mathf.PI * t / len) * (lp * 0.6f + body * 0.5f);
        });
    }

    /// <summary>Distant whisper: band-passed noise in short syllables.</summary>
    static AudioClip MakeWhisper(int seed)
    {
        var rng = new System.Random(seed);
        const float len = 2.2f;
        float syllables = 3.5f + seed % 3;
        float a = 0f, b = 0f;
        return Build("Whisper" + seed, len, t =>
        {
            float noise = (float)rng.NextDouble() * 2f - 1f;
            a += (noise - a) * 0.5f;
            b += (a - b) * 0.08f;
            float band = a - b;
            float syl = Mathf.Max(0f, Mathf.Sin(TwoPi * syllables * t));
            return 0.3f * band * syl * syl * Mathf.Sin(Mathf.PI * t / len);
        });
    }

    /// <summary>Approaching layer: slow pulse, thin high dissonant tones, low throb (8 s loop).</summary>
    static AudioClip MakeTensionLayer()
    {
        const float L = 8f;
        float h1 = Fit(1244.5f, L), h2 = Fit(1318.5f, L), h3 = Fit(1396.9f, L), low = Fit(73.4f, L), trem = Fit(0.5f, L), throbRate = Fit(2f, L);
        return Build("TensionLayer", L, t =>
        {
            float local = t % 1f;
            float pulse = 0.5f * Thump(local, 58f) + 0.3f * Thump(local - 0.25f, 58f);
            float thin = 0.035f * (Sin(h1, t) + Sin(h2, t) * (0.5f + 0.5f * Sin(trem, t)) + 0.7f * Sin(h3, t) * (0.5f + 0.5f * Sin(trem, t, 2f)));
            float throb = 0.12f * Sin(low, t) * (0.6f + 0.4f * Sin(throbRate, t));
            return pulse + thin + throb;
        });
    }

    /// <summary>Chase music: fast pulse, low drums, distorted sub, harsh rising strings, pulsing riser.</summary>
    static AudioClip MakeChaseMusic()
    {
        const float beat = 0.36f;
        const float L = beat * 16f;
        float sub = Fit(41.2f, L), trem = Fit(8f, L);
        var rng = new System.Random(21);
        float phA = 0f, phB = 0f, lp = 0f;
        return Build("ChaseMusic", L, t =>
        {
            float local = t % beat;
            int idx = (int)(t / beat) % 16;
            float kick = Thump(local, 55f) + (idx % 2 == 1 ? 0.5f * Thump(local - 0.18f, 62f) : 0f);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.4f;
            float snare = idx % 4 == 2 ? 0.3f * lp * Mathf.Exp(-local * 25f) : 0f;
            float pump = 1f - 0.6f * Mathf.Exp(-local * 10f);
            float bass = (float)Math.Tanh(3f * Sin(sub, t)) * 0.2f * pump;
            float rise = Mathf.Pow(2f, t / L); // strings climb one octave per loop
            phA += TwoPi * 196f * rise / Rate;
            phB += TwoPi * 207.7f * rise / Rate;
            float saw = 0f;
            for (int h = 1; h <= 6; h++) saw += (Mathf.Sin(phA * h) + Mathf.Sin(phB * h)) / h;
            float progress = t / L;
            float strings = 0.045f * saw * (0.6f + 0.4f * Sin(trem, t)) * (0.4f + 0.6f * progress);
            float riser = 0.12f * progress * lp * (1f - local / beat);
            return 0.9f * kick + snare + bass + strings + riser;
        });
    }

    /// <summary>Hidden and safe: slow creeping beating tone with faint high whine (12 s loop).</summary>
    static AudioClip MakeCreep()
    {
        const float L = 12f;
        float a = Fit(98f, L), b = Fit(98.6f, L), hi = Fit(880f, L), hi2 = Fit(932f, L);
        return Build("Creep", L, t =>
        {
            float s = 0.18f * (Sin(a, t) + Sin(b, t)) * (0.6f + 0.4f * Sin(1f / L, t));
            s += 0.025f * (Sin(hi, t) * (0.5f + 0.5f * Sin(1f / L, t, 1f)) + Sin(hi2, t) * (0.5f + 0.5f * Sin(2f / L, t, 3f)));
            return s;
        });
    }

    /// <summary>One heavy low drum hit (start of each killer turn during the chase).</summary>
    static AudioClip MakeTurnHit()
    {
        var rng = new System.Random(77);
        float lp = 0f;
        return Build("TurnHit", 0.9f, t =>
        {
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.2f;
            return 0.8f * Thump(t, 45f) + 0.45f * Sin(38f, t) * Mathf.Exp(-t * 4f) + 0.25f * lp * Mathf.Exp(-t * 12f);
        });
    }

    // ------------------------------------------------------------------ effects

    static AudioClip MakeFootstep()
    {
        var rng = new System.Random(1);
        float lp = 0f;
        return Build("Footstep", 0.14f, t =>
        {
            lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.25f;
            return 0.6f * lp * Mathf.Exp(-t * 40f) + 0.5f * Sin(70f, t) * Mathf.Exp(-t * 30f);
        });
    }

    static AudioClip MakeRustle()
    {
        var rng = new System.Random(2);
        float lp = 0f;
        const float len = 0.45f;
        return Build("Rustle", len, t =>
        {
            lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.35f;
            return 0.7f * lp * Mathf.Sin(Mathf.PI * t / len) * (0.6f + 0.4f * Sin(22f, t));
        });
    }

    static AudioClip MakeKeyChime()
    {
        return Build("KeyChime", 0.8f, t => 0.3f * Note(t, 1318.5f, 6f) + 0.3f * Note(t - 0.12f, 1760f, 5f));
    }

    static AudioClip MakeDangerSting()
    {
        var rng = new System.Random(3);
        return Build("DangerSting", 1.1f, t =>
        {
            float hit = 0.7f * Sin(55f, t) * Mathf.Exp(-t * 4f);
            float noise = 0.4f * ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 25f);
            float cluster = 0.11f * (Sin(880f, t) + Sin(932f, t) + Sin(1245f, t) + Sin(1318f, t)) * Mathf.Exp(-t * 2.5f);
            return hit + noise + cluster;
        });
    }

    /// <summary>
    /// Soft death sound: low dull thud, a heartbeat that slows and drops in pitch, a muted low drone
    /// and a short, heavily filtered noise swell. All under ~2 kHz, gentle attack, slow fade, low peak.
    /// </summary>
    static AudioClip MakeCaught()
    {
        var rng = new System.Random(4);
        float lp1 = 0f, lp2 = 0f;
        const float len = 2.6f;
        return Build("Caught", len, t =>
        {
            float attack = Mathf.Clamp01(t / 0.04f);                   // no click
            float fadeOut = Mathf.Clamp01((len - t) / 1.2f);           // slow fade

            // Dull thud (two low sines, soft decay).
            float thud = (0.5f * Sin(46f, t) + 0.25f * Sin(70f, t)) * Mathf.Exp(-t * 3.5f) * attack;

            // Heartbeat drop: three beats, each later, slower and lower.
            float beats = 0.35f * Thump(t - 0.55f, 44f) + 0.28f * Thump(t - 1.15f, 40f) + 0.2f * Thump(t - 1.9f, 36f);

            // Muted low drone that swells in and fades.
            float droneEnv = Mathf.Clamp01(t / 0.6f) * fadeOut;
            float drone = 0.12f * (Sin(55f, t) + 0.6f * Sin(82.4f, t) + 0.3f * Sin(110f, t)) * droneEnv;

            // Short soft noise swell, low-passed twice (dark "whoosh").
            float n = (float)rng.NextDouble() * 2f - 1f;
            lp1 += (n - lp1) * 0.08f;
            lp2 += (lp1 - lp2) * 0.08f;
            float swell = 0.5f * lp2 * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.9f));

            return 0.8f * (thud + beats + drone + swell) * fadeOut;
        });
    }

    static AudioClip MakeWinJingle()
    {
        return Build("WinJingle", 1.4f, t =>
            0.28f * (Note(t, 523.3f, 4f) + Note(t - 0.15f, 659.3f, 4f) + Note(t - 0.3f, 784f, 4f) + Note(t - 0.45f, 1046.5f, 3f)));
    }

    static AudioClip MakeDenied()
    {
        // Two short low buzzes.
        return Build("Denied", 0.32f, t =>
        {
            bool on = (t < 0.12f) || (t > 0.16f && t < 0.3f);
            if (!on) return 0f;
            float sq = Mathf.Sign(Sin(98f, t)) + Mathf.Sign(Sin(104f, t));
            return 0.22f * sq;
        });
    }
}
