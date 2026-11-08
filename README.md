# Hide & seek & escape

A top-down isometric horror hide-and-seek game prototype made in Unity.

## Status

Prototype. What works now:

- Turn-based, point-and-click movement on a 2x2 tile grid, with dash and stamina.
- 28 hiding spots. The clown killer only finds you if he saw you hide.
- Clown killer that patrols, chases and searches, and spawns at a random tile.
- 3 keys at random fair positions each run. Two exits open after all keys are found.
- Generated music and sounds: calm music, a distance-based heartbeat, and chase layers.
- Pause menu with volume and mute. Win and death screens with restart.

Not yet tested with real mouse and keyboard play or by ear. See `Assets/ProjectMemory/08_KNOWN_ISSUES_AND_TODO.md`.

## Tech

- Unity 6.6 (6000.6.8f1)
- Universal Render Pipeline (URP), new Input System

## How to open

1. Install Unity 6000.6.8f1 with Unity Hub.
2. In Unity Hub: Add > Add project from disk, then select the repository folder.
3. Open `Assets/Scenes/SampleScene.unity` and press Play.

Project docs: start with `AI_READ_ME_FIRST.md`.

## Credits

- Kenney Furniture Kit and Building Kit (CC0, [kenney.nl](https://kenney.nl)), in `Assets/Kenney`.
