# 0001: Engine stack

Status: accepted, versions pinned 2026-11-16 after the first run. Date: 2026-11-16.

## Decision
- Godot 4.x stable (project targets 4.4 features), Forward+ renderer.
- Zylann's Voxel Tools for terrain (blocky mesher, `VoxelTerrain`, `VoxelStreamSQLite`).
- GDScript for gameplay. C++ only when profiling proves a need. No C#: Voxel Tools'
  extension classes are awkward to call from C#.
- Godot high-level multiplayer over ENet. GodotSteam relay later.

## To pin (fill in during M0)
| Item | Version | Notes |
| --- | --- | --- |
| Godot | 4.7.2 stable | first tested on Arch Linux; stable only, never beta on main |
| Voxel Tools | 1.7 GDExtension (release `v1.7x`, `GodotVoxelExtension.zip`) | unzip into `addons/zylann.voxel/`; works with stock Godot |

## Why
Free, open source, Linux-friendly, quick for a small team, and Voxel Tools covers streaming,
meshing, LOD, editing and saving. See the design doc and the tech design v0.1.

## Known issue
The very first `godot --headless --import` on a fresh clone can crash in the editor ("Loading docks", cowdata index error). Running it again succeeds. This is an engine quirk, not game code.
