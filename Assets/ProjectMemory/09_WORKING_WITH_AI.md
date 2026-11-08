# 09 - Working with AI on this project

## Hard rules

1. **Never run Tools > Hide And Seek > 1..5 / Build All Phases / Upgrade v2 over the existing scene.** They delete and
   rebuild objects. Change the existing objects and scripts instead, and update the builder so a rebuild *would*
   reproduce the change (but do not run it).
2. **Back up before big changes:** copy `Assets/Scenes/SampleScene.unity` (and `.meta`) and the `Assets/Scripts` folder,
   or commit with Unity Version Control / git first.
3. Read `07_PROTECTED_AND_TESTED.md` before editing. Do not change protected behavior unless asked.
4. After each change: check the Console for errors, fix them, save the scene (Ctrl+S, or `EditorSceneManager.SaveScene`).
5. Do not claim something works unless it was tested in Play mode; say what was not tested.
6. No downloaded assets; primitives and generated audio only (unless the owner adds a file, e.g. `Assets/Audio/Music/`).
7. **Standing rule:** at the end of every task, append an entry to `06_CHANGELOG.md`, update any doc that changed,
   run **Tools > Hide And Seek > Export Project Memory Snapshot**, and tell the owner which docs were updated.

## Git

- Repo: `<project root>`, branch `main`, remote `origin` = https://github.com/VincentLinta/JocFaculta.git.
- Tracked: `Assets` (with every `.meta`), `Packages`, `ProjectSettings`, `AI_READ_ME_FIRST.md`, `.gitignore`, `.gitattributes`.
- **Never commit `Library`** (or `Temp`, `Logs`, `UserSettings`, `Captures`, `*.csproj`, `*.sln`). `.gitignore` blocks them;
  check `git status` before every commit.
- **Commit after every task** (after the changelog entry and the snapshot export):
  `git add -- Assets Packages ProjectSettings AI_READ_ME_FIRST.md .gitignore .gitattributes` then `git commit -m "<what changed>"`.
- **Go back to a previous commit:** close Unity first, then
  - see history: `git log --oneline`
  - look at an old version without losing work: `git switch --detach <commit>` (return with `git switch main`)
  - undo one commit safely (new commit that reverses it): `git revert <commit>`
  - restore one file from an old commit: `git restore --source <commit> -- <path>`
- Line endings: `.gitattributes` stores Unity YAML (`.unity`, `.prefab`, `.asset`, `.mat`, `.meta`) and source files with LF.
- Push is done by the owner: `git push -u origin main` (the AI does not push).

## How the MCP connection works

- Package: **MCP for Unity** (`com.coplaydev.unity-mcp`, in `Packages/manifest.json`).
- Server: HTTP at **http://127.0.0.1:8090/mcp**, configured as `unityMCP` in `%USERPROFILE%\.kiro\settings\mcp.json`
  (the Roblox Studio MCP is a separate server in `%USERPROFILE%\.kiro\crew\mcp.json`; keep them separate).
- In Unity: **Window > MCP for Unity**, transport HTTP, port 8090, press **Start Session / Connect**
  (the window can also start the server, so no terminal is needed). "Unity session not available" = the editor is not connected.
- The AI then uses tools such as `manage_scene`, `manage_gameobject`, `execute_code`, `read_console`, `manage_editor` (play/stop),
  `refresh_unity` (recompile), `manage_camera` (screenshots).
- Tips: after writing scripts, request a recompile and wait until it finishes before calling new code (otherwise the old
  assembly runs). `execute_code` compiles with C# 6 (CodeDom): avoid `?.` in some places, escape quotes carefully.
  In Play, set `Application.runInBackground = true` so the game keeps running while the editor is not focused.
  Tests that need the live game must run in Play mode (the grid, rooms and spawns only exist at runtime).

## Writing good prompts

- Start with a **PROTECTED** list of what must not change.
- Split the work into numbered parts with exact rules and numbers (tiles, seconds, percentages).
- Say "modify existing objects only, do not rebuild" and "update the builder but do not run it".
- Ask for verification in code and for a list of what was not tested.

## Naming conventions

- GameObjects: `Wall_<Name>_<n>` (walls; the grid treats `Wall_*` and `ExitDoor*` as walls on tile lines),
  `HidingSpot_NN` (two digits), `Key_<n>_<Room>`, `WP_<Room>` (waypoints), `Lamp_<Room>`, `ExitDoor`, `ExitDoor_West`.
- Materials: `Assets/Materials/HS_<Name>.mat` (URP Lit; grid/outline materials URP Unlit).
- Scripts: one MonoBehaviour per file in `Assets/Scripts`, editor tools in `Assets/Scripts/Editor`, comments in English.
- Tiles: 2x2 units, centers on odd world coordinates, walls on even coordinates.
