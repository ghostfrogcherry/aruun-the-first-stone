class_name WorldConfig
extends RefCounted
## World-wide constants. One world unit is one voxel.

## Real-world size of one voxel. Small voxels are a core look of Aruun.
const VOXEL_SIZE_M := 0.25
## Multiply metres by this to get world units (voxels).
const UNITS_PER_METER := 1.0 / VOXEL_SIZE_M
## Aruun's surface gravity: 0.38 g (canon). 9.81 m/s^2 * 0.38 * 4 units/m = 14.9.
## project.godot's physics/3d/default_gravity must match this.
const GRAVITY_G := 0.38
const GRAVITY := 9.81 * GRAVITY_G * UNITS_PER_METER
## Earth gravity (1 g), used inside Occlusion / Confluence sites. 39.2 units/s^2.
const EARTH_GRAVITY := 9.81 * UNITS_PER_METER
## Bump when worldgen output changes, so old saves can be detected and migrated.
const GENERATION_VERSION := 1

const VIEW_DISTANCE_VOXELS := 256      # 64 m. Benchmark in the spike.
const REACH_VOXELS := 24.0             # 6 m interaction reach.
const SERVER_REACH_SLACK := 8.0        # tolerance for latency when validating reach.
const DIG_RADIUS := 1.6                # ~0.4 m round bite per swing.
const BLAST_RADIUS_MAX := 7.0          # ~1.75 m crater.
const FLOATING_SEARCH_MARGIN := 10     # voxels scanned around a carve for loose pieces.
const FLOATING_MAX_PIECE := 6000       # bigger loose pieces stay put (treated as anchored).
