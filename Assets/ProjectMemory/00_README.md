# 00 - README

> **RO (rezumat):** Joc horror de ascunselea pe ture, pe o grilă de pătrățele 2x2, făcut doar din forme Unity.
> Găsești 3 chei, ieși pe una din cele 2 uși și te ascunzi de clovnul ucigaș.
> Citește întâi `09_WORKING_WITH_AI.md`: nu rula niciodată Tools > Hide And Seek peste scena existentă.

## What the game is

A turn-based, point-and-click hide-and-seek horror game, in the spirit of *Scream 7 - Hide & Scream*.
You are a human in an abandoned warehouse/house at night. A masked clown hunts you. Find 3 keys
(random positions every run), then reach one of the two exits. Hide in wardrobes, lockers, barrels,
under beds and more. The clown only finds a hidden player if he **saw** them hide.

- Look: fixed isometric camera (pitch 52 deg, yaw 45 deg), dark, steady warm lamps, light fog, all from Unity primitives.
- No downloaded assets: every model is primitives, every sound and all music is generated in code (`AudioManager`).
- Engine: Unity 6.6 (6000.6.8f1), URP, new Input System. Project folder: `<project root>` (the `jocfaculta` folder).

## How to open and play

1. Open the project in Unity Hub, open `Assets/Scenes/SampleScene.unity`.
2. Press **Play**. Click a highlighted tile to move. Controls:

| Input | Action |
|---|---|
| Left click on a tile | Move (1 tile; 2 with dash) |
| Space / DASH button | Arm dash (costs 25% stamina; buzzer if not enough) |
| E / HIDE button | Hide in a spot on or next to your tile / leave it |
| W / WAIT button | Skip the turn (+25% stamina while hidden) |
| ESC | Pause menu (Resume, Restart, Quit, volume, mute) |
| R | Restart on the YOU DIED / YOU ESCAPED screen |

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
