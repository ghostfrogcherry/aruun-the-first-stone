# 0002: Units and voxel size

Status: accepted for M0, revisit after the spike. Date: 2026-11-16.

## Decision
- One voxel = 0.25 m. Small voxels are a core part of Aruun's look and set it apart from
  1 m block games.
- **One world unit = one voxel.** The terrain is never scaled. Everything else is scaled to
  match: a 1.8 m person is 7.2 units, walk speed is 18 units/s.
- Gravity is Aruun's canon 0.38 g: 3.73 m/s^2 = 14.9 units/s^2 (decided by Bruh 2026-11-16).
  Jumps reach about 1.6 m and falls are slow; debris drifts down.
- Inside an Occlusion / Confluence site, Earth gravity (1 g = 39.2 units/s^2) applies. Build those
  sites with an Area3D using gravity_space_override = REPLACE; the player reads get_gravity(), so
  it already follows local gravity.
  Constants live in `scripts/core/world_config.gd` (`UNITS_PER_METER`).

## Why
Scaling the terrain node would scale its collision bodies, which Godot physics handles
poorly. Keeping terrain at scale 1 keeps raycasts, edits and physics in the same space.

## Consequences
- 16x more voxels per square metre than 0.5 m, 64x more than 1 m: view distance and chunk
  size must be benchmarked (tech design: compare 16^3 and 32^3 blocks).
- The player auto-steps up single voxels so 0.25 m bumps don't block walking.

## Addendum (2026-11-16): Earth-strong player

The player is an Earth human, so their strength does not change with gravity. Jumping uses a
fixed take-off speed (`JUMP_SPEED` in player.gd, set for a 1.6 m jump at 0.38 g). Inside an
Occlusion site's Earth gravity the same legs only reach about 0.6 m, which is part of why those
sites are harder than the rest of Aruun. Later strength-based systems (carry weight, fall
damage, throwing) should follow the same rule: fixed human strength, local gravity.
