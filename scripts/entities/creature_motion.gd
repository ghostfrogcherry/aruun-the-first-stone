class_name CreatureMotion
extends RefCounted
## Moves a creature through voxel terrain without physics bodies: gravity,
## walking up small steps, being stopped by walls, landing. It reads voxels
## through a callback, so it runs on the server for creatures far from the
## host's camera, and in unit tests on a fake world.

const SKIN := 0.01


## pos is the feet centre. `solid.call(Vector3i) -> bool`.
## Returns {"pos", "vel", "grounded", "impact": landing speed this step,
## "wall": horizontal speed lost against walls this step}.
static func step(pos: Vector3, vel: Vector3, dt: float, radius: float, height: float,
		step_up: int, gravity: float, solid: Callable) -> Dictionary:
	if not _free(pos, radius, height, solid):
		# Buried (spawned inside something, or terrain landed on it): rise until free.
		pos.y = floorf(pos.y) + 1.0
		return {"pos": pos, "vel": Vector3.ZERO, "grounded": false, "impact": 0.0, "wall": 0.0}
	var grounded := _solid_under(pos, radius, solid)
	var wall := 0.0
	vel.y -= gravity * dt
	# Horizontal, one axis at a time so creatures slide along walls.
	for axis in [0, 2]:
		var move := vel[axis] * dt
		if absf(move) < 0.0001:
			continue
		var next := pos
		next[axis] += move
		if _free(next, radius, height, solid):
			pos = next
			continue
		var stepped := false
		if grounded:
			for k in range(1, step_up + 1):
				var up := next + Vector3(0, k, 0)
				up.y = floorf(up.y + SKIN)
				if _free(up, radius, height, solid):
					pos = up
					stepped = true
					break
		if not stepped:
			wall += absf(vel[axis])
			vel[axis] = 0.0
	# Vertical, a voxel at a time so fast falls never tunnel.
	var impact := 0.0
	var remaining := vel.y * dt
	grounded = false
	while absf(remaining) > 0.0001:
		var part := clampf(remaining, -0.9, 0.9)
		remaining -= part
		var next := pos + Vector3(0, part, 0)
		if _free(next, radius, height, solid):
			pos = next
			continue
		if part < 0.0:
			pos.y = floorf(pos.y + SKIN) # rest on top of the voxel below
			impact = -vel.y
			grounded = true
		vel.y = 0.0
		break
	if not grounded and vel.y <= 0.0:
		grounded = _solid_under(pos, radius, solid) and pos.y - floorf(pos.y) < 0.05
	return {"pos": pos, "vel": vel, "grounded": grounded, "impact": impact, "wall": wall}


static func _samples(radius: float) -> Array:
	return [Vector2.ZERO, Vector2(radius, 0), Vector2(-radius, 0), Vector2(0, radius), Vector2(0, -radius)]


static func _free(pos: Vector3, radius: float, height: float, solid: Callable) -> bool:
	var y0 := int(floor(pos.y + SKIN))
	var y1 := int(floor(pos.y + height - SKIN))
	for s in _samples(radius):
		var x := int(floor(pos.x + s.x))
		var z := int(floor(pos.z + s.y))
		var y := y0
		while true:
			if solid.call(Vector3i(x, y, z)):
				return false
			if y >= y1:
				break
			y = mini(y + 2, y1)
	return true


static func _solid_under(pos: Vector3, radius: float, solid: Callable) -> bool:
	var y := int(floor(pos.y - 0.05))
	for s in _samples(radius):
		if solid.call(Vector3i(int(floor(pos.x + s.x)), y, int(floor(pos.z + s.y)))):
			return true
	return false
