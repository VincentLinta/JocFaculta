# 07 - Protected and tested behavior

These behaviors were confirmed working by the owner (and/or tested in code by the AI). Do not change, rename or
"improve" them unless a task explicitly asks for it. After any change, re-test the affected rows.

| Behavior | How to test |
|---|---|
| Point-and-click turns on the 2x2 grid; hover, reachable tiles, path preview; YOUR TURN / KILLER'S TURN | Play, hover tiles, click a cyan tile; the killer moves after you |
| Walls/doors on the grid, each door one full tile, no half tiles | Look at doorways from the game camera; code check: no walkable tile overlapped by a prop |
| Move 1 tile, dash 2 tiles for 25% stamina | Space, then click a tile 2 away; one stamina segment goes |
| Dash refused under 25%: buzzer + red flash, turn not used (do not touch the buzzer) | Use 4 dashes, press Space again |
| Stamina only refills while hidden, +25% per WAIT | Hide (E), press W |
| ESC pause: Resume, Restart, Quit, volume slider, mute; freezes the killer's turn | Press ESC during the killer's move |
| YOU DIED with soft death sound, Retry / R; YOU ESCAPED with Play Again / R | Get caught / escape |
| Keys counter, key pickup by stepping on the tile, killer +speed per key | Walk onto a key |
| Hiding with E from the spot tile or an adjacent tile, EXIT (E), cyan outline in reach, all 28 spots | Walk next to a spot; outline shows; E in, E out |
| Witnessed rule: found only if seen (LOS through doorways, never walls; or same room within 5 tiles); "He saw you"/"Hidden"; Console log | Hide while the killer is in another room -> "Hidden", he never finds you; hide in front of him -> found |
| Random killer spawn (`minSpawnDistance`), Console log | Restart a few times; `[Killer] Spawn tile` changes |
| Killer: clown look, idle bob, patrol / chase / search, 2 tiles per turn (max 3), catching | Watch him; let him see you |
| Both exits open after 3 keys -> win | Collect 3 keys, walk to either exit (east x 30 z -13, west Storage x -30 z 7) |
| Danger indicator + red vignette, steady lights, player light circle, human player model + animations | Look while the killer approaches |
| Footstep / hide / key / win sounds, tension and chase music layers | Listen |
| Heartbeat rises with distance (16 -> 2 tiles), +30% hidden, silent in pause (v7) | Let the killer walk toward you |
| ALL KEYS FOUND banner, chime, exit arrows 5 s (pause stops the timer) (v7) | Collect the 3rd key, press ESC during the hints |
| Calm background music (v7) | Listen in a quiet area; it should change chords every ~4 s |
| North wing rooms reachable, 8 new hiding spots (v7) | Walk through the doors at x -15 / -7 / 21 in the north wall |
| Random keys every run, fairness rules, `[Keys]` log (v7) | Restart; keys move; counter 0/3 |

v7 rows were tested in code by the AI but not yet confirmed by the owner.
