# 00 - README

> **RO (rezumat):** Joc horror de ascunselea în timp real (fază de ascuns 60 s, apoi fază de căutare 30 s), pe o grilă de pătrățele 2x2, făcut doar din forme Unity.
> Te ascunzi 60 s, apoi clovnul verifică locurile de ascuns în ordine aleatorie; supraviețuiești 30 s.
> Citește întâi `09_WORKING_WITH_AI.md`: nu rula niciodată Tools > Hide And Seek peste scena existentă.

## What the game is

A real-time, point-and-click hide-and-seek horror game, in the spirit of *Scream 7 - Hide & Scream*.
You are a human in an abandoned warehouse/house at night. Two phases: **Hiding** (60 s to hide, the clown is not
there yet; it ends 2 s after you are hidden or when time runs out) and **Chase** (the clown spawns, visits every hiding
spot once in random order, raises a hand and fills a bar above each spot; if you are inside you are found). Survive 30 s
and you win. Keys/exits of the old goal are switched off (`GameManager.useKeysAndExits`).

- Look: fixed isometric camera (pitch 52 deg, yaw 45 deg), dark, steady warm lamps, light fog, all from Unity primitives.
- No downloaded assets: every model is primitives, every sound and all music is generated in code (`AudioManager`).
- Engine: Unity 6.6 (6000.6.8f1), URP, new Input System. Project folder: `<project root>` (the `jocfaculta` folder).

## How to open and play

1. Open the project in Unity Hub, open `Assets/Scenes/SampleScene.unity`.
2. Press **Play**. Click a highlighted tile to move. Controls:

| Input | Action |
|---|---|
| Left click on a tile | Walk there (real time, shortest path; click again to redirect) |
| Hold Space or Left Shift | Run (uses stamina, 4 s from full; refills when still / hidden) |
| E / HIDE button | Hide in a spot on or next to your tile / leave it |
| Right mouse button + drag | Orbit the camera around the player (max +/-50 deg left/right, 20 deg down / 15 deg up) |
| Mouse wheel | Zoom in / out (limited) |
| ESC | Pause menu (Resume, Restart, Quit, volume, mute) |
| R | Restart on the YOU DIED / YOU SURVIVED screen |

## How to read these docs

| File | Contents |
|---|---|
| `00_README.md` | This file |
| `01_GAME_DESIGN.md` | Exact rules: turns, moves, stamina, hiding, witnessed rule, killer, keys, exits, audio design |
| `02_ARCHITECTURE.md` | Every script: purpose, GameObject, fields, call graph; scene hierarchy |
| `03_MAP_AND_GRID.md` | Tile size, coordinates, rooms with ids and bounds, doors, hiding spots, keys, exits, waypoints, spawns |
| `04_AUDIO.md` | Every sound and music layer, how it is made, triggers, volumes, fields |
| `05_TUNING_GUIDE.md` | Every adjustable value: default, where it lives, safe range |
| `06_CHANGELOG.md` | Everything done so far, newest first |
| `07_PROTECTED_AND_TESTED.md` | Confirmed-working behavior and how to test each |
| `08_KNOWN_ISSUES_AND_TODO.md` | Known problems and ideas |
| `09_WORKING_WITH_AI.md` | Rules for anyone (human or AI) editing this project; MCP setup |
| `10_SCENE_SNAPSHOT.md` | Auto-generated live scene data (Tools > Hide And Seek > Export Project Memory Snapshot) |

Anything marked **unverified** was not checked against the live project.

## Folder map

```
jocfaculta/
  AI_READ_ME_FIRST.md          pointer to this folder
  Assets/
    Scenes/SampleScene.unity   the only game scene
    Scripts/                   runtime scripts (see 02_ARCHITECTURE.md)
    Scripts/Editor/            HideAndSeekBuilder.cs (scene builder), ProjectMemoryExporter.cs (snapshot)
    Materials/                 HS_*.mat materials (URP Lit / Unlit)
    Audio/Music/               optional royalty-free music clip for AudioManager.musicClipOverride (empty by default)
    ProjectMemory/             these docs
    Kenney/                    Kenney asset packs (imported earlier, NOT used by the game)
    Settings/, TutorialInfo/   URP template files
  Captures/                    screenshots taken during development
  Packages/, ProjectSettings/  Unity
```
