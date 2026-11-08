# 06 - Changelog (newest first)

Reconstructed from the chat sessions of 2026-11-07 and 2026-11-08. All work done by the AI (Kiro) through the
Unity MCP bridge, at the owner's request. Dates are the session dates.

## 2026-11-08 - Git baseline
- **What:** removed an empty, half-made `.git` (0 commits, nothing staged); added a Unity `.gitignore` (Library, Temp, Obj,
  Logs, UserSettings, Builds, MemoryCaptures, Recordings, Captures, IDE files) and `.gitattributes` (text=auto, LF for
  source and Unity YAML); `git init -b main`; replaced private absolute paths in the docs with `<project root>` and
  `%USERPROFILE%`; scanned committed files for secrets (none) and files over 50 MB (none);
  first commit "Baseline v7 - hide, seek and escape (stable)" (687 files); remote `origin` added (not pushed).
  Git section added to 09_WORKING_WITH_AI.md.
- **Files:** .gitignore (new), .gitattributes (new), 00_README.md, 06_CHANGELOG.md, 09_WORKING_WITH_AI.md, 10_SCENE_SNAPSHOT.md.
  No game logic, script, scene or asset changed.
- **Why:** owner request (version history and a safe way back).
- **Verified:** `git status` shows nothing from Library, Temp, Logs, UserSettings or Captures tracked.

## 2026-11-08 - v7: heartbeat, all-keys event, calm music, north wing, random keys, project memory
- **What:**
  - Distance-based heartbeat (silent at 16+ tiles -> 100% at 2 tiles, 60 -> 150 BPM, +30% hidden, 0.3 s damping,
    silent when paused, fades on end screens). Replaces the old fixed-tempo heartbeat loop; same sound.
  - "ALL KEYS FOUND - EXITS ARE NOW OPEN" banner, mini-win chime + unlock clunk, 5 s exit arrows/markers with path
    distance, pulsing exit lights; timers pause in ESC.
  - Calm generated background music (A minor, 64 BPM, Am-Fmaj7-Dm-E + eerie chords, music-box motif, cello bass, air bed),
    ducks under the chase; `musicClipOverride` + `Assets/Audio/Music/` folder.
  - North wing: Garage, Kitchen, Bedroom 2, Cellar corridor (z 20..30), 3 one-tile doorways cut in the old north wall
    (x -15, -7, 21), 3 inner doors, props, floors, steady lamps, HidingSpot_21..28, 4 waypoints, grid 30x25,
    NavMesh bounds (70,10,64) at (0,0,5). Walkable tiles 412 -> 524 (+27%).
  - Random key placement (`KeyPlacer`) with fairness rules and fallback, Console log; `randomizeKeys` keeps original positions.
  - `GridManager.ClaimNearest` no longer gives a spot a tile through a wall (fixed HidingSpot_08 after the new doorway).
  - Project memory docs + `Export Project Memory Snapshot` menu.
- **Files:** AudioManager.cs, GameManager.cs, GameUI.cs, GridManager.cs, KeyPickup.cs (execution order only),
  KeyPlacer.cs (new), Editor/HideAndSeekBuilder.cs, Editor/ProjectMemoryExporter.cs (new), SampleScene.unity,
  Assets/ProjectMemory/*, AI_READ_ME_FIRST.md.
- **Why:** owner request (more tension feedback, real music, bigger map, replayability, durable project knowledge).
- **Verified in code:** curve values, heartbeat levels/BPM, banner/hint timers and pause, 524/524 tiles reachable,
  0 half-covered tiles, 16 rooms, all doors passable, 28/28 hiding spots, 15/15 waypoints on NavMesh, witnessed rule in
  the kitchen, 50 key placements with 0 rule failures, new key set after Restart, 1 AudioManager with 19 sources.

## 2026-11-08 - v6: hidden = safe unless seen, music layers, random killer spawn, second exit
- **What:** witnessed rule (tile line of sight in range + cone, or same room within 5 tiles), `blindFindChance` 0,
  "He saw you" / "Hidden" text, Console log; room ids + tile line of sight in GridManager. New music: ambient pad,
  random events, tension layer, chase music, creep layer; old drone/heartbeat at 28%. Random killer spawn with
  `minSpawnDistance` 12 and safety checks. Second exit in the west wall of the Storage room (z 7).
- **Files:** GridManager.cs, KillerAI.cs, PlayerController.cs, GameUI.cs, ExitDoor.cs, AudioManager.cs, HideAndSeekBuilder.cs, scene.
- **Why:** killer found players it never saw; owner wanted real music, replayability and a second way out.
- Note: the first west exit spot (z 17) was a closed pocket; moved to z 7 without moving props.

## 2026-11-08 - v5: walls on the grid, hiding fix, outline, soft death sound
- **What:** all walls moved onto tile lines (x +/-15 -> +/-14), every door exactly one 2x2 tile, exit at z -13;
  a few props nudged; whole-tile blocking rule; isolated tiles hidden. Hiding works from the spot tile, an adjacent tile
  or next to the object (cause of the bug: enter tile 2 tiles away after the 2x2 change). Cyan pulsing outline on spots in
  reach, brighter HIDE button. Softer death sound under ~2 kHz.
- **Files:** GridManager.cs, HidingSpot.cs, PlayerController.cs, GameUI.cs, AudioManager.cs, HideAndSeekBuilder.cs, scene.

## 2026-11-08 - v4: 2x2 tiles, dash buzzer, audio, human player
- **What:** tiles 1x1 -> 2x2 (grid 30x20), speeds and ranges rescaled; dash refused buzzer + red flash (turn not used);
  `AudioManager` with drone, heartbeat, chase layer, footstep/rustle/key/sting/caught/win/denied sounds, volume slider +
  mute in pause; human player model + `PlayerAnimator`.
- **Files:** GridManager.cs, PlayerController.cs, KillerAI.cs, GameManager.cs, GameUI.cs, KeyPickup.cs, HidingSpot.cs,
  ExitDoor.cs, AudioManager.cs (new), PlayerAnimator.cs (new), HideAndSeekBuilder.cs, scene.

## 2026-11-08 - v3: grid + turns, clown, detailed map, pause menu
- **What:** removed flicker (FlickerLight.cs deleted); 1x1 tile grid with hover / reach / path; point-and-click turns,
  dash, stamina only while hidden, WAIT; killer 2 tiles per turn; clown look + `KillerBob`; detailed props, room floors,
  baseboards, steady lamps; ESC pause menu with Resume / Restart / Quit; fixed: killer now chases a player it saw hide.
- **Files:** GridManager.cs (new), PlayerController.cs, KillerAI.cs, GameManager.cs, GameUI.cs, HidingSpot.cs, KeyPickup.cs,
  ExitDoor.cs, KillerBob.cs (new), HideAndSeekBuilder.cs, scene.

## 2026-11-08 - v2 (first game): hide-and-seek horror, 5 phases
- **What:** deleted the test map; built a 60x40 building with 9 rooms and corridors, 20 hiding spots, WASD player with
  sprint, NavMesh killer (patrol / chase / search), keys + exit, UI, game manager; NavMesh bake bug fixed
  (CollectSources clears its list); lights made brighter.
- **Files:** all scripts created; Editor/HideAndSeekBuilder.cs; Materials HS_*; scene.

## 2026-11-07 / 08 - v1: MCP connection and test map
- **What:** found the Unity MCP config (`%USERPROFILE%\.kiro\settings\mcp.json`, server `unityMCP` at
  http://127.0.0.1:8090/mcp, separate from the Roblox MCP), connected after the owner pressed Start Session,
  created TestCube, then a simple test map (ground, 4 walls, platforms, obstacles, PlayerSpawn).
- **Files:** scene only.
