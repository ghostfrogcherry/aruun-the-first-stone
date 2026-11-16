class_name Collapse
extends RefCounted
## What happens to a piece of world that has been cut loose: it falls straight
## down until it lands, then becomes terrain again. Pure logic; the server
## uses it to turn a loose piece into a "put" op, so collapses are the same
## for every player and are saved with the world.

const MAX_DROP := 96


## How many voxels the piece can fall before any cell would enter something
## solid. `is_free.call(Vector3i) -> bool` must return true for air, water and
## plants. Returns -1 if it does not land within MAX_DROP.
static func settle_drop(cells: Array, is_free: Callable, max_drop := MAX_DROP) -> int:
	var own := {}
	for c in cells:
		own[c] = true
	# Only the underside can hit anything: every other cell follows a cell below it.
	var bottoms: Array = []
	for c in cells:
		if not own.has(c + Vector3i.DOWN):
			bottoms.append(c)
	for d in range(1, max_drop + 1):
		for c in bottoms:
			if not is_free.call(c + Vector3i(0, -d, 0)):
				return d - 1
	return -1


static func landed_cells(cells: Array, drop: int) -> Array:
	var out: Array = []
	for c in cells:
		out.append(c + Vector3i(0, -drop, 0))
	return out


static func fall_time(drop: int, gravity: float) -> float:
	return sqrt(2.0 * maxf(drop, 0.0) / gravity)


## Damage to anything under a landing piece. Small pebbles sting, a pillar kills.
## It scales with landing speed, so the same fall hurts less in Aruun's low
## gravity than under Earth gravity (the default here, in units/s^2).
static func impact_damage(cell_count: int, drop: int, gravity := 39.24) -> float:
	if drop < 3:
		return 0.0
	return minf(sqrt(float(cell_count)) * sqrt(float(drop) * gravity / 39.24) * 1.6, 400.0)


## Can a carve of strength `power` remove a block of this hardness?
static func breaks(hardness: int, power: int) -> bool:
	return hardness <= power and hardness < 99
