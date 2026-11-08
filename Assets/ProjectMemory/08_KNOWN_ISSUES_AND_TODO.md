# 08 - Known issues and TODO

## Known issues

1. **HidingSpot_01 and ExitDoor_West share a tile** (0,13). Standing there with 3 keys wins at once, so hiding in
   HidingSpot_01 is only possible before the keys are found. Fixing it means moving the locker or the west exit (owner decision).
2. **Two room ids for one room:** the Basement (ids 0 and 5) and the Loading area (ids 12 and 14). Props probably split them into
   pockets. Effect: "same room nearby" does not work across the two parts, and two keys can land in the same physical room.
   Cause **unverified**.
3. **CameraFollow has no map limits.** It only follows the player; the request to "extend camera limits" had nothing to extend.
4. **Old chase layer** (from v4) still has its own fast heartbeat at 28% during a chase, under the new distance heartbeat.
   It may sound doubled (not checked by ear).
5. The previous "ambient pad" and random-event layers still play under the new calm music; they may clash in key
   (the pad is around D/Eb, the music is A minor). Lower `ambientLevel` if it sounds muddy.
6. `musicRootNote` and the chord length (from `musicTempo`) are used when the clips are generated at Play start; changing them
   during Play only changes the bar timing.
7. When the Unity editor window is not focused, frames slow down, so animations and UI timers look slower in tests
   (not a game bug).
8. Harmless Console warning from the MCP package: `[WebSocket] Unexpected receive error: WebSocket is not initialised`.
9. A full rebuild with Tools > Hide And Seek > Build All Phases was **not** run after v3 (unverified that it reproduces the
   scene exactly; it is designed to).
10. Not tested by the AI: real mouse/keyboard play, how the sounds actually sound, the win flow through the west exit with
    random keys (the exit itself was tested in v6).
11. `Assets/Kenney` packs are in the project but unused.

## TODO / ideas

- Decide on issue 1 (move HidingSpot_01's locker one tile north, or move the west exit).
- Merge the split room ids (treat edges between pockets as normal edges) if "same room" should cover the whole basement.
- Optional camera bounds so the view does not show empty ground at the map edges.
- Optional: drop the old chase layer's heartbeat now that the distance heartbeat exists.
