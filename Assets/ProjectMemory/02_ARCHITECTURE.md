# 02 - Architecture

All scripts are plain MonoBehaviours in the default assembly (`Assembly-CSharp`), editor tools in `Assembly-CSharp-Editor`.
UI and audio are built at runtime in code. Defaults below are the scene values (2026-11-08).

## Execution order (matters!)

| Order | Script | Why |
|---|---|---|
| -300 | AudioManager | singleton survives Restart; drops the duplicate from the reloaded scene |
| -200 | RuntimeNavMeshBaker | NavMesh exists before anyone asks for paths |
| -150 | GridManager | tiles, edges, rooms ready in Awake |
| -100 | GameManager | game state ready |
| 0 | everything else (KillerAI spawns in Start; HidingSpot claims its tile in Start) | |
| 50 | KeyPlacer | after the killer spawn, before the keys claim tiles |
| 100 | KeyPickup | claims the tile the KeyPlacer chose |

## Scripts (`Assets/Scripts`)

### GridManager — on `Grid`
2x2-unit tile grid over the building. Scans colliders (layer Default) in Awake: a tile is blocked if any prop touches
any part of it (`propHalfSize` 0.49), or a wall enters its inner area (`probeHalfSize` 0.41); walls sit on tile lines.
Edges between neighbours are open unless a linecast at 0.9 m hits something. Isolated pockets are removed.
Room ids: flood fill that does not cross doorway edges (an open edge with wall pieces at both ends of its line).
Draws the grid line mesh and hover / reach / path highlights.
- Fields: `origin` (-30,-20), `width` 30, `height` 25, `tileSize` 2, `blockingLayers` Default, `probeHalfSize` 0.41,
  `propHalfSize` 0.49, `probeBottom` 0.15, `probeTop` 1.8, line/highlight materials, `lineWidth` 0.045.
- API: `TileCenter`, `WorldToTile`, `IsWalkable`, `CanStep`, `NearestWalkable`, `ClaimNearest` (one key/spot per tile),
  `Reachable` (BFS), `FindPath`, `LineOfSight` (tile supercover), `RoomOf`, `RoomCount`, `IsTileBlocked`, `IsWall`,
  `ShowReach/ShowHover/ShowPath/ClearHighlights`.
- Called by: PlayerController, KillerAI, HidingSpot, KeyPickup, KeyPlacer, ExitDoor, GameUI, AudioManager.

### PlayerController — on `Player`
Real-time. Left click = walk along `grid.FindPath` (tile centers) at `walkSpeed` 4.5; hold Space/Shift = run at `runSpeed` 8
(drains `runDrain` 25/s, regen `regenRate`, `hiddenRegenRate`, exhausted until `runRecover`). `Tile` follows the position.
E / HideOrExit: glide into the spot (calls `KillerAI.NotifyPlayerHid`), `IsHidden`; E again leaves. Spots being checked by the killer
cannot be entered. `IsDashMove` = running (used by PlayerAnimator).

### PlayerAnimator — on `Player/Model`
Procedural walk bob, arm/leg swing, dash lean/lunge, idle breathing. Fields: `swingAngle` 32, `strideSpeed` 9,
`bobHeight` 0.06, `dashLean` 16; part references (torso, legs, arms).

### KillerAI — on `Killer` (visual under `Killer/Visual`)
Parked invisible under the map until `BeginHunt()` (called by GameManager at the start of the Chase phase). Then spawns on a random
safe tile (`minSpawnDistance`, not in the player's room / sight), shuffles all hiding spots and visits each once:
Search (walks at `walkSpeed`) -> Check (hand up, `checkTime` 2.5 s, `CheckProgress` drives the HUD bar) -> if the spot is occupied: lose, else next.
`IsChecked` on the spot prevents rechecks. If `CanSeePlayer()` (player in the open) -> Chase at `chaseSpeed`, catch within `catchDistance`;
loses sight after `loseSightTime`. If he sees the player enter a spot (`NotifyPlayerHid`) he goes straight there. Sounds: steps, search creaks,
horn honks, breathing, spawn sting (AudioManager.PlayAt). The right arm is put under a runtime `ArmRPivot` for the raised hand.

### KillerBob — on `Killer/Visual`
Idle bob + sway. Fields `bobHeight` 0.06, `bobSpeed` 2.2, `swayAngle` 3. Uses scaled time (freezes in pause).

### HidingSpot — on each `HidingSpots/HidingSpot_NN`
Trigger box (layer Ignore Raycast), `spotType`, `enterPoint`, `hidePoint`, `isOccupied`, `findChance`, outline.
In Start it claims the nearest free tile for its enter point and builds the outline shell (inside-out copies of every
mesh with an unlit cyan material, `outlineGrow` 0.09). `IsInReach(tile)` = the hide rule. Static list `HidingSpot.All`.

### KeyPickup — on each `Keys/Key_N_*`
Spins/bobs; claims its tile in Start; when the player stands on it (not hidden): key sound, `GameManager.CollectKey()`, destroy.

### KeyPlacer — on `Keys`
Random fair key tiles (see 01_GAME_DESIGN.md §5). Fields `randomizeKeys` true, `minDistanceFromPlayerSpawn` 6,
`minDistanceFromKillerSpawn` 8, `minDistanceBetweenKeys` 10, `requireDifferentRooms` true, `originalPositions` (3).
Public helpers used by tests/docs: `ChooseTiles`, `Distances`, `ForbiddenTiles`, `PlayerSpawnTile`.

### ExitDoor — on `Map/Structure/ExitDoor` and `Map/Structure/ExitDoor_West`
Lock light color (red/green), `PlayerNear` (player tile within `reachTiles` 0.8 tiles of the door), calls `GameManager.Win()`.
Static `All` list and `AnyPlayerNear`. Fields `doorLight`, `lockedColor`, `openColor`.

### GameManager — on `GameManager`
Phases: Hiding (`hideTime` 60; ends `hiddenSettleTime` 2 s after the player is hidden, or at 0) -> Chase (`chaseTime` 30, calls `KillerAI.BeginHunt`);
survive = `Win`. `useKeysAndExits` false disables KeyPlacer/KeyPickup objects at Awake and `ExitDoor` wins. Pause, Lose, Win(msg), Restart, Quit.
No turns any more.

### GameUI — on `GameUI` (builds `GameUI/HUD` at runtime)
HUD: stamina segments, keys, turn indicator, DASH / HIDE / WAIT buttons, prompt, hide status, danger text, red vignette,
hide darkening, ALL KEYS banner + exit hints (`GameUI/HUD/ExitHints`), pause menu (volume slider, mute, Resume, Restart, Quit),
YOU DIED and YOU ESCAPED panels. Fields `dangerDistance` 10, `maxVignetteAlpha` 0.75, `hiddenDarkness` 0.45,
`bannerTime` 4, `exitHintTime` 5, `hintEdgeMargin` 90.

### AudioManager — on `AudioManager` (DontDestroyOnLoad)
Everything audible, generated in code at Awake. See 04_AUDIO.md. Static `AudioManager.Play(Sfx)`.

### CameraFollow — on `Main Camera`
Isometric camera following `target` (Player). Start angle `pitch` 52, `yaw` 45, `distance` 24, `smoothTime` 0.25.
Right mouse button + drag orbits (`rotationSpeed` 0.2 deg/px, `maxYawOffset` 30, `maxPitchOffset` 15 around the start angle).
Mouse wheel zooms (`zoomStep` 2.5, `minDistance` 12, `maxDistance` 34, `zoomSmoothing` 12). Input ignored when `Time.timeScale` is 0.
New Input System only (`Mouse.current`).
No map bounds exist (it only follows the player); only the orbit and zoom are limited.

### RuntimeNavMeshBaker — on `NavMeshBaker`
Bakes the NavMesh in Awake from colliders under `roots` (Map, HidingSpots), layer Default.
Bounds centered on the object: position (0,0,5), `size` (70,10,64). Used by spawn/key reachability checks.

## Editor scripts (`Assets/Scripts/Editor`)

### HideAndSeekBuilder
Menu **Tools > Hide And Seek**: `1 - Build Map` .. `5 - Build Game Loop`, `Build All Phases` (fresh scene build,
reproduces the current design incl. north wing, second exit, key placer), and `Upgrade v2/*` (old in-place upgrades).
Public in-place upgrade functions used through MCP: `AlignWallsToGrid`, `AddSecondExit`, `UpgradeNorthWing`,
`AddKeyPlacer`, `BuildHumanModel`. **Never run the build menus over the existing scene** (they delete and rebuild).

### ProjectMemoryExporter
Menu **Tools > Hide And Seek > Export Project Memory Snapshot**: writes `Assets/ProjectMemory/10_SCENE_SNAPSHOT.md`. Read-only.

## Scene hierarchy (`Assets/Scenes/SampleScene.unity`, verified 2026-11-08)

```
Main Camera            Camera, AudioListener, URP camera data, CameraFollow
Directional Light      very dim moonlight (0.06)
Global Volume          URP volume
Map
  Structure            walls (Wall_*), Floor, Ground_Outside, ExitDoor, ExitSign, ExitDoor_West, ExitSign_West
  Props                original clutter
  Lights               steady lamps (Lamp_*), table lamps, emergency light, ExitLight, ExitLight_West
  PlayerSpawn          (-25.5, 1, -1.5)
  Floors               per-room floor slabs (no colliders)
  Details              detailed props (furniture, puddles, stains, cobwebs...)
  Trim                 baseboards
  NorthWing            Structure, Floors, Props, Lights, Trim of the 4 new rooms
HidingSpots            HidingSpot_01 .. HidingSpot_28
Player                 PlayerController; PlayerLight (light circle), Model (PlayerAnimator, human primitives)
KillerWaypoints        15 waypoints
Killer                 KillerAI; Visual (clown primitives, KillerBob, NoseGlow light)
NavMeshBaker           RuntimeNavMeshBaker
Keys                   KeyPlacer; Key_1_Storage, Key_2_Bathroom, Key_3_Boiler
GameManager            GameManager
GameUI                 GameUI (+ runtime HUD, EventSystem)
Grid                   GridManager (+ runtime GridLines, highlights)
AudioManager           AudioManager (+ 19 runtime AudioSources)
```
