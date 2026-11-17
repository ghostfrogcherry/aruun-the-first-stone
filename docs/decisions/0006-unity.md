# 0006: Move to Unity

Status: accepted. Date: 2026-11-17. Supersedes 0001.

## Decision
- The game is built in Unity (6000.3 LTS line, pinned in `unity/ProjectSettings`), driven
  from the Unity CLI. The Godot prototype is retired; its last version is `91ea5ff`.
- Game rules live in `Aruun.Core`: plain C# 9 with no Unity dependency, tested with .NET in CI.
  `Aruun.Game` is a thin Unity layer that draws and takes input.
- Our own chunked voxel engine (32^3 chunks of 0.25 m voxels, greedy meshing), not a store asset.
- Worldgen is integer maths only, so every machine builds the same world from a seed.
- The world changes only through edit ops; the save is a journal of them (as in 0003).
- Netcode: Netcode for GameObjects, host-authoritative, edits sent as op messages (not yet built).
- Rendering: Built-in pipeline with our own voxel shader for now; ray tracing is a later test.
- 1 Unity unit = 1 m (0002's "1 unit = 1 voxel" was Godot-specific). Gravity stays 0.38 g.

## Why
The person prefers working through the Unity CLI. Keeping rules out of engine code makes them
testable without a licence and keeps the port of future features cheap.

Plan and audit: https://claude.ai/artifact/PxBD5NR3jihiH37U3573fA
