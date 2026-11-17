# Aruun in Unity

Aruun's game project. (The Godot prototype it replaces is retired; its last version is
commit `91ea5ff`, kept for reference while its rules are ported.) Plan and audit:
https://claude.ai/artifact/PxBD5NR3jihiH37U3573fA

**Status: first slice.** Seeded 0.25 m terrain (meadows, lakes, mesas, trees), a flying
camera, digging and building through edit ops, a save journal, destruction and language
rules. Runs at about 100 fps on an RTX 3070 (Unity 6000.3.28f1, Linux).

## Layout

| Folder | What it is |
| --- | --- |
| `Assets/Aruun/Core` | The rules: voxels, worldgen, mesher, edit ops, journal, content loading. Plain C# with no Unity dependency, so it is tested without Unity and behaves the same on host, client and server. |
| `Assets/Aruun/Game` | The Unity side: chunk meshes, camera, input, overlay. Thin on purpose. |
| `Tests/Aruun.Core.Tests` | Tests for Core. Run with plain .NET. Outside `Assets`, so Unity ignores it. |
| `../content` | Blocks, items, models and the rest. Read straight from the repo in the editor; copied into builds. |

Rules of the house:

- **Game rules go in Core, not in MonoBehaviours.** If it decides something (can this be dug,
  what does this word do), it belongs in Core with a test.
- **Core worldgen is integer maths only.** Every machine must build the same world from the
  same seed. A pinned test fails if generation changes by accident; if you mean to change it,
  update the pin and bump `WorldConfig.GenerationVersion`.
- **The world changes only through `EditOp`s.** They are what is saved and what co-op will send.
- **No hand-wired scenes.** `Bootstrap` builds everything from code, so any scene (even an
  empty one) runs the game and it can be started from a terminal.
- Core is kept to C# 9, which is what Unity compiles.

## First run

1. Install the Unity CLI and sign in (once):
   ```
   curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
   unity auth login
   ```
2. Install the newest Unity 6 LTS editor: `unity install <version>`.
3. Open this folder as a project: `unity open unity` from the repo root. Unity creates
   `Packages/`, `ProjectSettings/` and the `.meta` files on first open. **Commit them**: from
   then on the editor version and packages are pinned for everyone.
4. Press Play in any scene. You should be flying over green land with lakes and red mesas.

If nothing moves when you press keys, the project is using the new Input System without
the package: install `com.unity.inputsystem`, or set Player > Active Input Handling to Both.

Launch flags (after `--` on the command line, or in a built game): `--seed=1234` or
`--seed=anyword`, `--view=160` (metres).

## Controls (dev build)

WASD fly, Space and Ctrl up and down, Shift fast, mouse look, left click dig, right click
place, 1 to 7 choose a block, F3 overlay, F5 save, Esc free the mouse.

Edits are saved per seed under Unity's persistent data folder (`worlds/dev-<seed>/journal.txt`)
and replayed on the next start.

## Build and screenshot from a terminal

```
U=~/Unity/Hub/Editor/<version>/Editor/Unity
$U -batchmode -nographics -projectPath unity -executeMethod Aruun.EditorTools.DevBuild.Linux -quit -logFile -
unity/Builds/linux/Aruun --seed=42 --shot=/tmp/aruun.png --shot-after=10
```

`--pos=x,y,z,yaw,pitch` places the camera (metres, degrees) for repeatable shots.

## Tests

```
dotnet test unity/Tests/Aruun.Core.Tests
```

Needs the .NET 8 SDK only. GitHub runs the same command on every pull request.
To see the land a seed makes without opening Unity:

```
ARUUN_MAP=/tmp/map.ppm dotnet test unity/Tests/Aruun.Core.Tests --filter WriteMapPicture
```
