# 01 - Game design (exact rules)

Values below are the defaults found in the scene on 2026-11-08 (see `10_SCENE_SNAPSHOT.md`).
"Tile" = one 2x2-unit grid square. Distances "in tiles" are path or straight-line tile distances as stated.

## 1. Turn order

1. **Player turn.** The player does exactly one action: move, dash-move, hide, leave a hiding spot, or wait.
   Arming the dash (Space) is free and does not end the turn. A refused dash does not end the turn.
2. After the action's animation ends, `GameManager.EndPlayerTurn()` starts the **killer turn**
   (after `killerTurnDelay` = 0.15 s). The player cannot act until the killer finishes.
3. The killer moves, then the turn returns to the player. `TurnNumber` counts up.
- The UI shows "YOUR TURN" (green) or "KILLER'S TURN" (red).
- ESC pauses everything (Time.timeScale = 0), including the killer's walk animation. No tile clicks while paused.

## 2. Movement, dash, stamina

- Player and killer always stand on a tile center and move tile to tile (4 directions), smoothly animated.
- **Normal move:** 1 tile (`normalRange` = 1). Click a highlighted reachable tile; the path is previewed.
- **Dash:** toggle with Space or the DASH button; the next move can go up to 2 tiles (`dashRange` = 2).
  A move longer than 1 tile costs `dashCost` = 25% stamina (4 dashes from full).
- **Dash refused:** below 25% stamina, pressing dash plays a low buzzer, flashes the DASH button and stamina bar red,
  and does not use the turn.
- **Dash noise:** a dash is heard by the killer within `hearDashTiles` = 3 tiles (straight-line tile distance;
  = 6 old 1x1 tiles = 6 world units). Hearing starts a chase.
- **Stamina** (`maxStamina` = 100) recharges **only while hidden**: +25 per WAIT turn spent hidden (`hiddenRegen`).
  Shown as 4 segments.
- The player cannot move onto the killer's tile.

## 3. Hiding

- 28 hiding spots (`HidingSpot_01` .. `HidingSpot_28`), types: Wardrobe, UnderBed, BehindCrate, Barrel, Locker,
  UnderTable, Curtain, Vent, UnderStairs.
- **Can hide** (E or HIDE) when the player stands on the spot's enter tile, on a tile next to it (no wall between),
  or on a tile right next to the hiding object (no wall between). Only free (not occupied) spots.
- In reach, the hiding object gets a pulsing cyan outline and the HIDE button turns brighter. Never while hidden,
  never during the killer's turn, only for spots in reach.
- Hiding takes the turn: the player glides into the hide point, the model and its light are hidden, the screen darkens.
- **EXIT (E)** while hidden takes the turn: the player steps out onto the spot's tile (or a free neighbour if the killer stands there).

### 3.1 Witnessed rule (when can a hidden player be found?)

Evaluated once, at the moment of hiding, for the tile the player stood on **and** the spot's tile
(`KillerAI.GetFindChanceFor(spot, fromTile)`):

- **(a) line of sight:** tile line of sight (supercover walk) from the killer's tile to the player's tile is clear,
  the distance is within `viewDistance / tileSize` = 13 / 2 = 6.5 tiles, and the tile is inside the killer's
  vision cone (`viewAngle` = 110 deg) or adjacent (distance <= 1 tile).
  Blocked tiles (props) and walls on tile edges stop the line; doorways are open edges, so sight passes through them.
- **(b) same room nearby:** same room id, within `sameRoomTiles` = 5 tiles, and the tile line is clear
  (no big prop in between). Works even from behind.
- Otherwise **not witnessed**.

Results:
- **Witnessed:** find chance `seenFindChance` = 0.95. The killer remembers the spot, switches to Chase, walks to it,
  then (in Search) opens that spot first.
- **Not witnessed:** find chance `blindFindChance` = 0. The killer may still search and open spots, but cannot find the player.
- Every hide logs `Hide witnessed: true/false, reason: line of sight | same room nearby | none, killer tile ..., player tile ...`.
- While hidden the UI shows "He saw you" (dim red) or "Hidden" (dim grey) next to STAMINA.

## 4. Killer

- Look: dark clown built from primitives (pale face, red nose with faint glow, big smile, red/blue hair tufts,
  ruffled collar, striped suit, big shoes), idle bob (`KillerBob`). No colliders.
- **Speed:** `tilesPerTurn` = 2; +`tilesPerKey` = 0.34 after each key (fractions add up over turns), max `maxTilesPerTurn` = 3.
- **Spawn:** random each game / Restart (seeded from the clock). See 03_MAP_AND_GRID.md section "Spawn rules".
- **States:**
  - *Patrol:* walks to random waypoints (15 waypoints, never the same one twice in a row; first one random).
  - *Chase:* starts when it sees the player (`CanSeePlayer`: physics line of sight from eye height 2.4, within 13 units,
    in the 110 deg cone, or touching tile) or hears a dash. Walks toward the last known tile. After `chaseLostTurns` = 2
    turns without knowing where the player is, or on reaching the last known tile without seeing them, it searches.
  - *Search:* goes to the last known tile, then opens up to `spotsToCheck` = 3 hiding spots within `searchRadiusTiles` = 5 tiles
    (a witnessed spot first). Opening a spot takes `checkDelay` = 0.8 s and ends its move. Then back to Patrol.
- **Catch:** a visible (not hidden) player is caught if the killer steps into their tile, or ends a step on or next to
  the player's tile while seeing them. -> YOU DIED.
- **Found in a spot:** when opening an occupied spot, `Random.value < findChance` -> YOU DIED ("The clown found your hiding spot.").

## 5. Keys and exits

- 3 keys (`keysRequired` = 3). Picked up by walking onto the key's tile (not while hidden). Each key: key sound,
  Console message `[Game] Key collected n/3`, killer speed bonus.
- **Random key placement** (`KeyPlacer` on the `Keys` object), every game start and Restart, seeded from the clock,
  after the killer's spawn. Each key tile must be: walkable, not a hiding-spot tile, not an exit tile (within 1.5 tiles
  of an exit door), not the player's spawn tile, not the killer's spawn tile, reachable on the grid and the NavMesh.
  Fairness (public fields):
  - `minDistanceFromPlayerSpawn` = 6 (path tiles)
  - `minDistanceFromKillerSpawn` = 8 (path tiles)
  - `minDistanceBetweenKeys` = 10 (path tiles)
  - `requireDifferentRooms` = true (3 different room ids)
  - If impossible: relax one at a time - rooms first, then halve the spacing, then halve the spawn distances - and log a warning.
  - `randomizeKeys` = false puts the keys at `originalPositions` (Storage, Bathroom, Boiler room).
  - Log: `[Keys] placed at tiles (x,y), (x,y), (x,y), rooms A, B, C, path distances from player spawn ...`.
- **All keys found** (3rd key): green banner "ALL KEYS FOUND - EXITS ARE NOW OPEN" (4 s, fade in/out), a mini-win
  chime + door-unlock clunk, and 5 s of exit hints: pulsing green edge arrows (or a marker above an exit that is on screen)
  labelled "EXIT n" (path tiles), plus stronger pulsing exit lights. Timers stop in the pause menu; cleared on win/death;
  Restart reloads the scene. Once per run.
- **Exits:** `ExitDoor` (east wall, x 30, z -13) and `ExitDoor_West` (west wall of the Storage room, x -30, z 7).
  Red light while locked, green when all keys are found. Standing on the tile in front of an exit with 3 keys (not hidden)
  = YOU ESCAPED. With fewer keys the prompt says "Locked - find n more key(s)".

## 6. Win / lose

- **Lose:** caught or found -> YOU DIED screen, soft death sound, Retry button or R.
- **Win:** exit with 3 keys -> YOU ESCAPED screen, win jingle, Play Again button or R.
- **Restart** (pause menu, end screens, R) reloads the scene: turn 1, stamina 100%, keys 0/3, new random killer spawn,
  new random keys. The AudioManager survives the reload (exactly one instance).

## 7. Audio design (summary; details in 04_AUDIO.md)

- Calm generated background music (A minor, 64 BPM: Am - Fmaj7 - Dm - E with occasional eerie chords, music-box melody).
- Distance-based heartbeat: silent at 16+ tiles, 25% at 10, 60% at 5, 100% at 2; 60 -> 150 BPM; +30% while hidden.
- Tension layer (10 -> 5 tiles), chase music (chasing or under 5 tiles), creeping tone while safely hidden.
- Subtle old drone under everything. Sound effects for steps, hiding, keys, danger, death, win, dash refused, all keys found.
