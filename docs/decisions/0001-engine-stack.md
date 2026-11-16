# 0001: Engine stack

Status: accepted, versions to pin after the M0 spike. Date: 2026-11-16.

## Decision
- Godot 4.x stable (project targets 4.4 features), Forward+ renderer.
- Zylann's Voxel Tools for terrain (blocky mesher, `VoxelTerrain`, `VoxelStreamSQLite`).
- GDScript for gameplay. C++ only when profiling proves a need. No C#: Voxel Tools'
  extension classes are awkward to call from C#.
- Godot high-level multiplayer over ENet. GodotSteam relay later.

## To pin (fill in during M0)
| Item | Version | Notes |
| --- | --- | --- |
| Godot | _tbd_ | stable only, never beta on main |
| Voxel Tools | _tbd_ | GDExtension build if available for that Godot version, else the prebuilt custom editor from the same release |

## Why
Free, open source, Linux-friendly, quick for a small team, and Voxel Tools covers streaming,
meshing, LOD, editing and saving. See the design doc and the tech design v0.1.
