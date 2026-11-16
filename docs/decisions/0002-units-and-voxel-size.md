# 0002: Units and voxel size

Status: accepted for M0, revisit after the spike. Date: 2026-11-16.

## Decision
- One voxel = 0.25 m. Small voxels are a core part of Aruun's look and set it apart from
  1 m block games.
- **One world unit = one voxel.** The terrain is never scaled. Everything else is scaled to
  match: a 1.8 m person is 7.2 units, gravity is 39.2 units/s^2, walk speed is 18 units/s.
  Constants live in `scripts/core/world_config.gd` (`UNITS_PER_METER`).

## Why
Scaling the terrain node would scale its collision bodies, which Godot physics handles
poorly. Keeping terrain at scale 1 keeps raycasts, edits and physics in the same space.

## Consequences
- 16x more voxels per square metre than 0.5 m, 64x more than 1 m: view distance and chunk
  size must be benchmarked (tech design: compare 16^3 and 32^3 blocks).
- The player auto-steps up single voxels so 0.25 m bumps don't block walking.
