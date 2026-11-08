# 05 - Tuning guide

Change values in the Inspector on the named GameObject (scene values override script defaults).
Restart Play after changing audio generation values (root note, tempo for chord length).

| Value | What it does | Default | Where | Safe range |
|---|---|---|---|---|
| `normalRange` | tiles per normal move | 1 | Player / PlayerController | 1 |
| `dashRange` | tiles per dash move | 2 | Player / PlayerController | 2-3 |
| `dashCost` | stamina per dash | 25 | Player / PlayerController | 10-50 |
| `hiddenRegen` | stamina per turn hidden | 25 | Player / PlayerController | 10-50 |
| `maxStamina` | stamina pool | 100 | Player / PlayerController | 50-200 |
| `stepTime` (player) | seconds per tile animation | 0.32 | Player / PlayerController | 0.15-0.6 |
| `tilesPerTurn` | killer tiles per turn | 2 | Killer / KillerAI | 1-3 |
| `tilesPerKey` | killer speed added per key | 0.34 | Killer / KillerAI | 0-0.5 |
| `maxTilesPerTurn` | killer speed cap | 3 | Killer / KillerAI | 2-4 |
| `stepTime` (killer) | seconds per killer tile | 0.4 | Killer / KillerAI | 0.2-0.8 |
| `viewDistance` | vision range (world units; /2 = tiles) | 13 | Killer / KillerAI | 8-20 |
| `viewAngle` | vision cone width (deg) | 110 | Killer / KillerAI | 60-180 |
| `hearDashTiles` | dash heard within (tiles) | 3 | Killer / KillerAI | 0-6 |
| `sameRoomTiles` | "same room nearby" witness range | 5 | Killer / KillerAI | 0-8 |
| `blindFindChance` | chance to find an UNSEEN hidden player | 0 | Killer / KillerAI | 0-0.3 |
| `seenFindChance` | chance to find a SEEN hidden player | 0.95 | Killer / KillerAI | 0.5-1 |
| `chaseLostTurns` | turns without contact before searching | 2 | Killer / KillerAI | 1-4 |
| `searchRadiusTiles` | spots this close to last known tile are checked | 5 | Killer / KillerAI | 2-10 |
| `spotsToCheck` | spots opened per search | 3 | Killer / KillerAI | 1-5 |
| `checkDelay` | seconds to open a spot | 0.8 | Killer / KillerAI | 0.3-2 |
| `minSpawnDistance` | killer spawn min path distance (tiles) | 12 | Killer / KillerAI | 0-20 |
| `spawnSafetyChecks` | killer not in start room / not in sight | true | Killer / KillerAI | bool |
| `randomizeKeys` | random key tiles each run | true | Keys / KeyPlacer | bool |
| `minDistanceFromPlayerSpawn` | key min path distance from player spawn | 6 | Keys / KeyPlacer | 0-15 |
| `minDistanceFromKillerSpawn` | key min path distance from killer spawn | 8 | Keys / KeyPlacer | 0-15 |
| `minDistanceBetweenKeys` | key-to-key min path distance | 10 | Keys / KeyPlacer | 0-20 |
| `requireDifferentRooms` | 3 keys in 3 room ids | true | Keys / KeyPlacer | bool |
| `keysRequired` | keys to open the exits | 3 | GameManager | 1-3 (only 3 key objects exist) |
| `killerTurnDelay` | pause before the killer moves (s) | 0.15 | GameManager | 0-0.5 |
| `dangerDistance` | red vignette starts (world units) | 10 | GameUI | 6-16 |
| `bannerTime` / `exitHintTime` | all-keys banner / exit hints (s) | 4 / 5 | GameUI | 2-8 |
| `musicVolume` | all music layers | 0.55 | AudioManager | 0-1 |
| `calmMusicLevel` | calm background music | 0.35 | AudioManager | 0-0.6 |
| `musicTempo` | calm music BPM | 64 | AudioManager | 55-75 |
| `musicRootNote` | calm music root (MIDI, 57 = A3) | 57 | AudioManager | 50-62 |
| `musicClipOverride` | your own music clip | none | AudioManager | clip in Assets/Audio/Music/ |
| `baseLayerLevel` | old drone level | 0.28 | AudioManager | 0-1 |
| `tensionLevel` / `chaseMusicLevel` / `creepLevel` | layer levels | 0.45 / 0.7 / 0.4 | AudioManager | 0-1 |
| `ambientLevel` / `eventLevel` | ambient pad / random events | 0.5 / 0.35 | AudioManager | 0-1 |
| `heartbeatFarTiles` / `MidTiles` / `NearTiles` / `CloseTiles` | heartbeat curve distances | 16 / 10 / 5 / 2 | AudioManager | keep decreasing order |
| `heartbeatMidVolume` / `heartbeatNearVolume` | curve volumes | 0.25 / 0.6 | AudioManager | 0-1 |
| `heartbeatMinBpm` / `heartbeatMaxBpm` | tempo range | 60 / 150 | AudioManager | 40-180 |
| `heartbeatHiddenBoost` | extra while hidden | 1.3 | AudioManager | 1-2 |
| `heartbeatLevel` | heartbeat volume at 100% | 0.28 | AudioManager | 0.1-0.6 |
| `heartbeatDamping` | smoothing (s) | 0.3 | AudioManager | 0.1-1 |
| `tileSize`, `width`, `height`, `origin` | grid layout | 2, 30, 25, (-30,-20) | Grid / GridManager | only change together with the map |
| `probeHalfSize` / `propHalfSize` | wall / prop blocking test size (tiles) | 0.41 / 0.49 | Grid / GridManager | do not change |
| `size` + position | NavMesh bake bounds | (70,10,64) at (0,0,5) | NavMeshBaker | must cover the whole map |
