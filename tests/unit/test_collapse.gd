extends RefCounted

var _solid := {}


func _free(p: Vector3i) -> bool:
	return not _solid.has(p)


func _floor(y: int) -> void:
	_solid.clear()
	for x in range(-8, 9):
		for z in range(-8, 9):
			_solid[Vector3i(x, y, z)] = true


func test_piece_lands_on_the_floor() -> String:
	_floor(0)
	var cells := [Vector3i(0, 10, 0), Vector3i(0, 11, 0), Vector3i(1, 10, 0)]
	var d := Collapse.settle_drop(cells, _free)
	if d != 9:
		return "expected a 9 voxel drop, got %d" % d
	return "" if Collapse.landed_cells(cells, d)[0] == Vector3i(0, 1, 0) else "landed in the wrong place"


func test_piece_catches_on_a_ledge() -> String:
	_floor(0)
	_solid[Vector3i(1, 4, 0)] = true
	var d := Collapse.settle_drop([Vector3i(0, 10, 0), Vector3i(1, 10, 0)], _free)
	return "" if d == 5 else "the overhanging cell should catch on the ledge, got %d" % d


func test_hollow_piece_does_not_block_itself() -> String:
	_floor(0)
	# An arch: its own cells below must not count as obstacles.
	var cells := [Vector3i(0, 5, 0), Vector3i(0, 6, 0), Vector3i(1, 6, 0), Vector3i(2, 6, 0), Vector3i(2, 5, 0)]
	return "" if Collapse.settle_drop(cells, _free) == 4 else "arch should drop 4"


func test_bottomless_fall() -> String:
	_solid.clear()
	return "" if Collapse.settle_drop([Vector3i(0, 10, 0)], _free, 32) == -1 else "nothing to land on should give -1"


func test_impact_damage_scales() -> String:
	if Collapse.impact_damage(4, 1) != 0.0:
		return "a one-voxel settle should not hurt"
	var pebble := Collapse.impact_damage(4, 8)
	var pillar := Collapse.impact_damage(400, 24)
	return "" if pebble > 0.0 and pebble < 15.0 and pillar > 100.0 else "pebble %s, pillar %s" % [pebble, pillar]


func test_hardness_against_power() -> String:
	if not Collapse.breaks(1, 1) or Collapse.breaks(2, 1):
		return "bare hands dig soil, not rock"
	return "" if Collapse.breaks(3, 3) and not Collapse.breaks(99, 100) else "protected blocks never break"


# --- Creature motion on a fake voxel world ------------------------------------

func _is_solid(p: Vector3i) -> bool:
	return _solid.has(p)


func _run(pos: Vector3, vel: Vector3, steps: int, keep_vel := false) -> Dictionary:
	var r := {"pos": pos, "vel": vel, "grounded": false, "impact": 0.0, "wall": 0.0}
	var worst := 0.0
	var wall := 0.0
	for i in steps:
		var v: Vector3 = r.vel
		if keep_vel:
			v.x = vel.x
			v.z = vel.z
		r = CreatureMotion.step(r.pos, v, 0.05, 1.6, 5.0, 2, 39.2, _is_solid)
		worst = maxf(worst, r.impact)
		wall = maxf(wall, r.wall)
	r["worst_impact"] = worst
	r["worst_wall"] = wall
	return r


func test_creature_falls_and_lands() -> String:
	_floor(0)
	var r := _run(Vector3(0.5, 20.0, 0.5), Vector3.ZERO, 60)
	if not r.grounded or not is_equal_approx(r.pos.y, 1.0):
		return "should rest on the floor at y=1, got %s" % r.pos
	return "" if r.worst_impact > 20.0 else "a 5 m fall should land hard, got %s" % r.worst_impact


func test_creature_walks_up_a_small_step() -> String:
	_floor(0)
	for z in range(-8, 9):
		for x in range(4, 9):
			_solid[Vector3i(x, 1, z)] = true # a one-voxel step
	var r := _run(Vector3(0.5, 1.0, 0.5), Vector3(8, 0, 0), 20, true)
	return "" if r.pos.x > 5.0 and is_equal_approx(r.pos.y, 2.0) else "should climb the step, got %s" % r.pos


func test_creature_is_stopped_by_a_wall() -> String:
	_floor(0)
	for z in range(-8, 9):
		for y in range(1, 9):
			_solid[Vector3i(5, y, z)] = true
	var r := _run(Vector3(0.5, 1.0, 0.5), Vector3(8, 0, 0), 30, true)
	if r.pos.x > 3.5:
		return "walked into the wall, x=%s" % r.pos.x
	return "" if r.worst_wall > 5.0 else "hitting a wall should report the lost speed"
