# 03 - Map and grid

All numbers verified in Play mode on 2026-11-08 unless marked otherwise.

## Grid conventions

- Tile size: **2 x 2 world units**. Grid: **30 x 25 tiles**, origin (bottom-left corner) at world (-30, -20).
- Tile (x, y) covers world X `-30 + 2x .. -28 + 2x` and Z `-20 + 2y .. -18 + 2y`.
- **Tile center** = (`-29 + 2x`, 0, `-19 + 2y`): tile centers are at odd world coordinates.
- `WorldToTile(p)` = (floor((p.x + 30) / 2), floor((p.z + 20) / 2)).
- Walls lie on **even** world coordinates (the lines between tiles), 0.3 thick. Doors are exactly one tile (2 units),
  centered on an odd coordinate.
- A tile is either fully free or fully blocked: any prop touching it blocks it; walls on its border do not.
- Walkable tiles: **524**, all reachable from the player spawn (412 before the north wing, +27%; 112 tiles in the wing).
- Player spawn: `Map/PlayerSpawn` (-25.5, 1, -1.5) -> tile **(2, 9)** in the Safe room.

## Rooms (ids from `GridManager.RoomOf`)

Ids are assigned by flood fill in scan order, so they stay the same while the map does not change.
Bounds = bounding box of the room's walkable tiles (world).

| Id | Room | Walkable tiles | World X | World Z |
|---|---|---|---|---|
| 0 | Basement (west part) | 13 | -30..-20 | -18..-8 |
| 1 | Safe room (player start) | 22 | -30..-18 | -8..2 |
| 2 | Storage | 29 | -30..-14 | 6..20 |
| 3 | Garage (north wing) | 32 | -30..-12 | 20..30 |
| 4 | Corridor | 56 | -28..28 | 2..6 |
| 5 | Basement (east pocket) | 6 | -22..-18 | -18..-10 |
| 6 | Boiler room | 50 | -18..-6 | -18..2 |
| 7 | Office | 31 | -14..0 | 6..20 |
| 8 | Kitchen (north wing) | 28 | -12..4 | 20..30 |
| 9 | Main hall | 85 | -6..14 | -20..2 |
| 10 | Bedroom | 27 | 0..12 | 6..20 |
| 11 | Bedroom 2 (north wing) | 29 | 4..18 | 20..30 |
| 12 | Loading area | 40 | 14..30 | -20..-2 |
| 13 | Bathroom | 39 | 14..30 | 6..20 |
| 14 | Loading area (north part, 26..30 x -6..2 region) | 14 | 18..30 | -6..2 |
| 15 | Cellar corridor (north wing) | 23 | 18..30 | 20..30 |

16 room ids. The basement and the loading area each get **two** ids (0/5 and 12/14). The props probably split
them into pockets that only meet at a doorway-like edge (cause **unverified**, see 08_KNOWN_ISSUES_AND_TODO.md).

## Doors (one tile each)

| Wall | Door center (world) | Connects |
|---|---|---|
| Corridor north z=6 | x -23, -7, 7, 23 | Corridor <-> Storage / Office / Bedroom / Bathroom |
| x=-14 | z 17 | Storage <-> Office |
| x=0 | z 17 | Office <-> Bedroom |
| Corridor south z=2 | x -23, -11, 3, 21 | Corridor <-> Safe room / Boiler / Main hall / Loading |
| x=-18 | z -3, -13 | Safe room <-> Boiler, Basement <-> Boiler |
| z=-8 | x -23 | Safe room <-> Basement |
| x=-6 | z -9 | Boiler <-> Main hall |
| x=14 | z -7, -15 | Main hall <-> Loading |
| North wall z=20 (new) | x -15, -7, 21 | Storage <-> Garage, Office <-> Kitchen, Bathroom <-> Cellar |
| x=-12 (new) | z 25 | Garage <-> Kitchen |
| x=4 (new) | z 23 | Kitchen <-> Bedroom 2 |
| x=18 (new) | z 27 | Bedroom 2 <-> Cellar |
| East outer x=30 | z -13 | **ExitDoor** |
| West outer x=-30 | z 7 | **ExitDoor_West** |

## Exits

| Name | Door position | Tile in front | Room |
|---|---|---|---|
| ExitDoor | (30, 1.2, -13) | (29, 3) | 12 Loading |
| ExitDoor_West | (-30, 1.2, 7) | (0, 13) | 2 Storage |

## Hiding spots (tile = the enter tile after the runtime tile claim)

| Name | Type | Tile | Room |
|---|---|---|---|
| HidingSpot_01 | Locker | (0, 13) | 2 Storage |
| HidingSpot_02 | BehindCrate | (5, 17) | 2 Storage |
| HidingSpot_03 | Barrel | (2, 18) | 2 Storage |
| HidingSpot_04 | UnderTable | (11, 15) | 7 Office |
| HidingSpot_05 | Wardrobe | (12, 19) | 7 Office |
| HidingSpot_06 | UnderBed | (17, 17) | 10 Bedroom |
| HidingSpot_07 | Wardrobe | (20, 16) | 10 Bedroom |
| HidingSpot_08 | Curtain | (19, 18) | 10 Bedroom |
| HidingSpot_09 | Curtain | (28, 18) | 13 Bathroom |
| HidingSpot_10 | Vent | (28, 16) | 13 Bathroom |
| HidingSpot_11 | Vent | (1, 11) | 4 Corridor |
| HidingSpot_12 | Wardrobe | (0, 6) | 1 Safe room |
| HidingSpot_13 | UnderStairs | (1, 1) | 0 Basement |
| HidingSpot_14 | Barrel | (5, 1) | 5 Basement pocket |
| HidingSpot_15 | Locker | (9, 1) | 6 Boiler |
| HidingSpot_16 | Vent | (10, 8) | 6 Boiler |
| HidingSpot_17 | UnderTable | (17, 6) | 9 Main hall |
| HidingSpot_18 | Curtain | (15, 1) | 9 Main hall |
| HidingSpot_19 | BehindCrate | (26, 9) | 14 Loading north |
| HidingSpot_20 | Locker | (28, 8) | 14 Loading north |
| HidingSpot_21 | Locker | (0, 21) | 3 Garage |
| HidingSpot_22 | BehindCrate | (6, 22) | 3 Garage |
| HidingSpot_23 | UnderTable | (12, 23) | 8 Kitchen |
| HidingSpot_24 | Wardrobe | (15, 23) | 8 Kitchen |
| HidingSpot_25 | UnderBed | (19, 23) | 11 Bedroom 2 |
| HidingSpot_26 | Curtain | (17, 24) | 11 Bedroom 2 |
| HidingSpot_27 | Barrel | (29, 23) | 15 Cellar |
| HidingSpot_28 | UnderStairs | (27, 23) | 15 Cellar |

All 28 tested in code: enter, +25% stamina on WAIT, exit (28/28 OK).
`GridManager.ClaimNearest` never gives a spot a tile on the other side of a wall. (After the new north doorway,
HidingSpot_08 had briefly claimed the north-wing tile (20,20) through the wall; fixed, now (19,18), retested.)
Note: `HidingSpot_01`'s tile (0,13) is also the tile in front of `ExitDoor_West` (see known issues).

## Keys

- Scene positions (`KeyPlacer.originalPositions`, used when `randomizeKeys` = false):
  Key_1_Storage (-19.5, 1, 12.5), Key_2_Bathroom (21.5, 1, 10.5), Key_3_Boiler (-11.5, 1, -12.5).
- At Play/Restart the `KeyPlacer` moves them to random fair tiles: walkable, reachable (grid + NavMesh), not a
  hiding-spot / exit / spawn tile, `minDistanceFromPlayerSpawn` 6, `minDistanceFromKillerSpawn` 8,
  `minDistanceBetweenKeys` 10 (path tiles), `requireDifferentRooms` true; rules relaxed one by one if impossible
  (warning logged). Keys sit on the tile center at y = 1 (+/- 0.15 bob).
- 50 simulated placements (2026-11-08): 0 rule failures, 0 relaxations, all 16 rooms used, 36 of 150 keys in the north wing.

## Killer waypoints

| Name | World | Tile | Room |
|---|---|---|---|
| WP_Storage | (-22, 0, 12) | (4, 16) | 2 |
| WP_Office | (-7.5, 0, 11) | (11, 15) | 7 |
| WP_Bedroom | (8, 0, 11) | (19, 15) | 10 |
| WP_Bathroom | (22, 0, 11) | (26, 15) | 13 |
| WP_CorridorWest | (-20, 0, 4) | (5, 12) | 4 |
| WP_CorridorEast | (15, 0, 4) | (22, 12) | 4 |
| WP_Basement | (-24, 0, -13) | (3, 3) | 0 |
| WP_Boiler | (-12, 0, -8) | (9, 6) | 6 |
| WP_HallNorth | (4, 0, -4) | (17, 8) | 9 |
| WP_HallSouth | (4, 0, -15) | (17, 2) | 9 |
| WP_Loading | (22, 0, -10) | (26, 4) | 12 |
| WP_Garage | (-21, 0, 22) | (4, 21) | 3 |
| WP_Kitchen | (-5, 0, 23) | (12, 21) | 8 |
| WP_Bedroom2 | (9, 0, 24) | (19, 22) | 11 |
| WP_Cellar | (23, 0, 24) | (26, 22) | 15 |

All reachable on the grid and the NavMesh. No waypoint in the Safe room (player start).

## Spawn rules

- **Player:** always `PlayerSpawn`, tile (2, 9), Safe room.
- **Killer** (`KillerAI.PickSpawnTile`, every game start / Restart, clock seed): random walkable tile reachable from the
  player spawn, not the player spawn tile, not a key tile, not a hiding-spot tile (or its 4 neighbours), not within 1.5 tiles
  of an exit, path distance >= `minSpawnDistance` 12, and with `spawnSafetyChecks` = true not in the player's starting room
  and not in the player's tile line of sight; NavMesh path must be complete. Falls back to any free tile if none qualifies.
  Log: `[Killer] Spawn tile (x, y) ...`. `minSpawnDistance` = 0 and `spawnSafetyChecks` = false -> fully random.
  First patrol waypoint is random.

## North wing (added 2026-11-08)

Rooms along z 20..30: Garage (x -30..-12, garage floor, old car, workbench, tires, oil barrels), Kitchen (x -12..4, tiles,
counter, sink, stove, fridge, table), Bedroom 2 (x 4..18, wood, bed, curtain, dresser, nightstand + lamp, rug),
Cellar corridor (x 18..30, dark concrete, barrels, crates, shelf, pipes, stairs). Steady lamps only.
Built by `HideAndSeekBuilder.BuildNorthWing` (objects under `Map/NorthWing`), spots by `BuildWingSpots`.
