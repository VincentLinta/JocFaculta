# 04 - Audio

Everything is generated in code by `AudioManager` (mono, 22050 Hz) at Awake. No audio files are used,
except the optional `musicClipOverride`. One AudioManager survives Restart (DontDestroyOnLoad; the copy in the reloaded
scene destroys itself), so there are never duplicate sources: **19 AudioSources** on the AudioManager (verified).

## Global controls

- **Master volume / mute:** pause-menu slider and MUTE button -> `AudioListener.volume` (saved in PlayerPrefs
  `hs_volume`, `hs_mute`). Controls every layer and effect.
- `musicVolume` 0.55 scales all music layers (`m = musicVolume * duck`).
- `duck`: 1 in play, `pausedMusicLevel` 0.3 in the ESC menu, 0.15 on the end screens (moves at 1.5/s).
- `newDuck`: 0.55 while the player is hidden and the killer is within `hiddenCloseDistance` 10 world units, else 1.
- Fades: `fadeInTime` 1.5 s, `fadeOutTime` 4 s for tension / chase / creep layers.

## Music layers

| Layer | How it is made | When / level |
|---|---|---|
| **Calm music** (new) | Sequencer, one bar per chord at `musicTempo` 64 BPM (bar 3.75 s). Pad chords (2 detuned sines + soft harmonics, 1 s attack, 2 s release) + cello-like bass (5 harmonics, 5 Hz vibrato) per chord, generated for root `musicRootNote` 57 (A3). Progression Am - Fmaj7 - Dm - E; 12% of non-first bars swap in Bb (flat II) or D#dim (tritone). Music-box/celesta melody: motif (scale degrees 4-2-3-1-0 on beats 0,1,1.5,2,3) with random note drops, +/-1 steps, half-beat shifts, 6% eerie b2/tritone notes, 30% of bars rest. Soft airy noise bed (10 s cross-faded loop). Random seed from the clock, so it never repeats as a short loop. | Always. Level `m * newDuck * (1 - 0.45 * chase) * calmMusicLevel` (0.35); pads 1x, melody 0.8x, bed 0.35x. If `musicClipOverride` is set (put the file in `Assets/Audio/Music/`), that clip loops at the same level instead. |
| Drone (old) | 16 s loop of low detuned sines | Always, `m * 0.8 * baseScale`; baseScale = `baseLayerLevel` 0.28, rises to 1 when hidden with the killer close. |
| Old chase layer | 4.4 s loop: fast heartbeat + tense strings | `m * 0.75 * chaseMix * baseScale` while Chase (legacy). |
| Ambient pad (previous "new" music) | 40 s dissonant pads + sub rumble | `m * newDuck * ambientLevel` 0.5, x0.6 during chase |
| Random events | music box phrases (3), scrapes (2), creaks (2), whispers (2), random pitch/pan | every 7-18 s in play, `eventLevel` 0.35 |
| Tension | 8 s loop: slow pulse, thin high dissonant tones, low throb | killer 10 -> 5 tiles away (`tensionLevel` 0.45); x0.4 when hidden unseen |
| Chase music | 5.76 s loop: 166 BPM drums, distorted sub, rising saw strings, noise riser; plus a heavy hit at the start of each killer turn | killer chasing or under 5 tiles, not while hidden unseen (`chaseMusicLevel` 0.7) |
| Creep | 12 s loop: slow beating low tone + faint high whine | hidden and NOT witnessed (`creepLevel` 0.4) |

## Heartbeat (one source, replaces the old fixed-tempo heartbeat loop)

- Same "lub-dub" sound as before (`Thump` 48 Hz, dub at 65%), played one beat at a time.
- Distance = `KillerAI.DistanceToPlayer` / tile size, updated every frame (smooth while the killer walks).
- Volume curve (`HeartbeatCurve`): 0 at >= `heartbeatFarTiles` 16, `heartbeatMidVolume` 0.25 at `heartbeatMidTiles` 10,
  `heartbeatNearVolume` 0.6 at `heartbeatNearTiles` 5, 1.0 at <= `heartbeatCloseTiles` 2 (linear in between).
- Tempo: `heartbeatMinBpm` 60 (at 16 tiles) -> `heartbeatMaxBpm` 150 (at 2 tiles). Dub delay = min(0.22 s, 35% of the period).
- Hidden: x`heartbeatHiddenBoost` 1.3. Source volume = clamp01(`heartbeatLevel` 0.28 x level) -> 100% equals the old
  "hidden + killer close" heartbeat loudness.
- Damping `heartbeatDamping` 0.3 s on volume and tempo. 0 in the pause menu and on the end screens (fades out).
- Measured in Play: 5 tiles -> level 0.56, 126 BPM; 1 tile -> 0.90 (rising to 1), 145 BPM; paused -> 0.

## Sound effects (`AudioManager.Play(Sfx)`, `sfxVolume` 0.9)

| Sfx | Sound | Trigger |
|---|---|---|
| Footstep | short low-passed thump, random pitch 0.85-1.15 | each tile the player moves |
| Rustle | filtered noise swell | entering or leaving a hiding spot |
| Key | two-note chime | key pickup |
| AllKeys | rising 4-note chime (E5 G5 B5 E6) + soft door-unlock clunk, 1.6 s | 3rd key |
| DangerSting | low hit + noise + high cluster | killer first sees the player / sees them hide |
| Caught | soft thud, slowing heartbeat drop, muted drone, filtered swell (< ~2 kHz) | YOU DIED |
| Win | 4-note major jingle | YOU ESCAPED |
| Denied | two short low square buzzes | dash with < 25% stamina |

Not verified by ear by the AI: how the new calm music, heartbeat and all-keys chime actually sound.
