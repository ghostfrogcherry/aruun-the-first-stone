extends SceneTree
## Play-through smoke test: starts a real single-player world and drives the
## survival systems through the same requests the player's keys send.
##
##   godot --headless -s tests/integration/smoke.gd -- --single --world=smoke_test
##
## Exits 0 when every check passes. Uses its own world folder; delete
## user://worlds/smoke_test to start it fresh.

const M := 4.0

var _failed := 0
var _checks := 0
var world
var sim
var me: Node3D
var c: CharacterState
var cdb: Node


func _initialize() -> void:
	var main: Node = load("res://scenes/main/main.tscn").instantiate()
	root.add_child(main)
	_run.call_deferred(main)


func check(ok: bool, what: String) -> void:
	_checks += 1
	if not ok:
		_failed += 1
	print("  %s  %s" % ["ok  " if ok else "FAIL", what])


func wait(seconds: float) -> void:
	await create_timer(seconds).timeout


func _ground_under(pos: Vector3) -> Vector3i:
	var p := Vector3i(pos.floor())
	for i in 40:
		if cdb.collides(world.vt.get_voxel(p)):
			return p
		p.y -= 1
	return p


func _run(main: Node) -> void:
	world = main.world
	cdb = root.get_node("ContentDB")
	world.message.connect(func(t): print("    > ", t))
	sim = world.sim
	# Wait for the world, the character and solid ground under the player.
	for i in 300:
		await wait(0.1)
		me = world.players.get_node_or_null("1")
		if sim.active and sim.chars.has(1) and me and me.is_on_floor():
			break
	check(sim.active and sim.chars.has(1), "world started and the character joined")
	check(me != null and me.is_on_floor(), "player is standing on the ground")
	if _failed > 0:
		_finish()
		return
	c = sim.chars[1]
	print("ruleset: %s, start items: %s" % [sim.ruleset.name, c.inv.to_data().filter(func(s): return s != null)])

	print("inventory and crafting")
	sim.request_debug("kit")
	check(c.inv.count("fallhammer") == 1 and c.inv.count("roast_meat") == 8, "test kit arrives in the bag")
	check(sim.view_inv.count("roast_meat") == 8, "the HUD copy of the bag matches")
	sim.request_debug("give plant_fibre 3")
	var cord := c.inv.count("cord")
	sim.request_craft("cord")
	check(c.inv.count("cord") == cord + 1 and c.inv.count("plant_fibre") == 0, "crafting cord uses up fibre")
	sim.request_debug("give redroot 1")
	sim.request_craft("roast_redroot")
	check(c.inv.count("roast_redroot") == 0, "cooking is refused without a fire")

	print("conditions")
	sim.request_debug("set hunger 80")
	var slot := _slot_of("roast_meat")
	sim.request_use(slot, Vector3i.ZERO, false)
	check(float(c.cond.hunger) < 40.0 and c.buffs.has("warm_meal"), "eating lowers hunger and gives the warm meal buff")
	sim.request_debug("set cold 80")
	await wait(0.5)
	check(c.bands.any(func(b): return b.cond == "cold" and not b.good), "being cold shows as a bad condition")
	check(Conditions.mod(c.mods, "stamina_regen_mult") < 1.2, "and it changes the character's stats")
	sim.request_debug("set cold 0")

	print("structures and farming")
	var feet: Vector3 = me.global_position
	var g1 := _ground_under(feet + Vector3(3, 2, 0))
	sim.request_use(_slot_of("campfire_kit"), g1, true)
	check(sim.entities.near_structure(feet, "station:fire"), "a campfire can be set down and counts as a fire")
	sim.request_craft("roast_redroot")
	check(c.inv.count("roast_redroot") == 1, "cooking works beside the fire")
	var g2 := _ground_under(feet + Vector3(-4, 2, 2))
	world.server_ops([{"t": "set", "p": g2, "b": cdb.block_id("red_soil")}])
	var plants_before := _count("plant")
	sim.request_use(_slot_of("redroot_seed"), g2, true)
	check(_count("plant") == plants_before + 1, "a seed can be planted in red soil")
	var plant_id := _nearest("plant", Vector3(g2) + Vector3(0.5, 1, 0.5))
	sim.world_time += 3000.0
	await wait(2.5)
	check(int(sim.entities.ents.get(plant_id, {}).get("stage", 0)) == int(GameData.plants.redroot.stages) - 1,
		"the crop ripens with time")
	var roots := c.inv.count("redroot")
	me.global_position = Vector3(g2) + Vector3(1.5, 1.2, 0.5)
	await wait(0.3)
	sim.request_interact(plant_id)
	check(c.inv.count("redroot") > roots, "harvesting a ripe crop gives redroot")

	print("digging and falling pieces")
	me.global_position = feet + Vector3(0, 0.5, 0)
	await wait(0.5)
	var g3 := _ground_under(feet + Vector3(0, 2, -5))
	var rock: int = cdb.block_id("rust_rock")
	var ops: Array = []
	for y in range(1, 6):
		ops.append({"t": "set", "p": g3 + Vector3i(0, y, 0), "b": rock})
	for x in range(-1, 2):
		for z in range(-1, 2):
			ops.append({"t": "set", "p": g3 + Vector3i(x, 6, z), "b": rock})
	world.server_ops(ops)
	c.selected = _slot_of("flint_pick")
	var rocks := c.inv.count("rust_rock")
	world.request_dig(Vector3(g3) + Vector3(0.5, 2.5, 0.5), 1.2)
	check(c.inv.count("rust_rock") > rocks, "digging with a pick gives the rock that was dug")
	check(world.vt.get_voxel(g3 + Vector3i(0, 6, 0)) == 0, "the cut-off top of the pillar comes loose")
	check(not sim._landings.is_empty(), "the loose piece is falling")
	await wait(2.5)
	var landed := false
	for y in range(1, 6):
		if world.vt.get_voxel(g3 + Vector3i(1, y, 1)) == rock:
			landed = true
	check(landed and sim._landings.is_empty(), "the piece lands lower down and becomes terrain again")

	print("combat")
	sim.request_debug("time 0.5")
	c.hp = c.hp_max(sim.ruleset)
	c.selected = _slot_of("fallhammer")
	sim.request_debug("spawn redstrider")
	await wait(1.5)
	var beast := _nearest("creature", me.global_position)
	check(beast != 0, "a redstrider can be called in")
	if beast != 0:
		var e: Dictionary = sim.entities.ents[beast]
		var before := float(e.get("hp", 1.0))
		var hurt := false
		for attempt in 4:   # it runs from the player, so chase it
			if not sim.entities.ents.has(beast):
				hurt = true
				break
			var at: Vector3 = sim.entities.ents[beast].pos
			me.global_position = at + Vector3(0, 0.3, 1.0 * M)
			sim.request_attack(true, Vector3(0, 0, -1))
			await wait(0.9)
			var now: Dictionary = sim.entities.ents.get(beast, {})
			if now.is_empty() or float(now.get("hp", 0.0)) < before:
				hurt = true
				break
		check(hurt, "a heavy Fallhammer blow hurts it")
		check(c.stamina < c.stamina_max(sim.ruleset), "attacking costs stamina")
	sim.request_debug("time 0.95")
	sim.request_debug("spawn nightstalker")
	await wait(0.5)
	var stalker := _nearest_def("nightstalker")
	check(stalker != 0, "a nightstalker can be called in")
	if stalker != 0:
		var debt := float(c.cond.get("gravity_debt", 0.0))
		var e2: Dictionary = sim.entities.ents[stalker]
		var aim: Vector3 = (e2.pos + Vector3(0, 2, 0) - sim._eye(1)).normalized()
		var far: float = e2.pos.distance_to(me.global_position)
		sim.request_cast("word_push", aim)
		await wait(0.5)
		print("    stalker was %.1f m away, now %.1f m" % [far / M, sim.entities.ents[stalker].pos.distance_to(me.global_position) / M])
		check(float(c.cond.get("gravity_debt", 0.0)) > debt, "speaking a force word adds gravity debt")
		check(sim.entities.ents.has(stalker) and sim.entities.ents[stalker].pos.distance_to(me.global_position) > far,
			"the push throws the nightstalker back")
		var hp := c.hp
		for i in 80:
			await wait(0.1)
			if c.hp < hp:
				break
		check(c.hp < hp, "the nightstalker closes in and wounds the player")
		sim.set_guard(true)
		check(sim._rt[1].guard, "the player can raise a guard")
		sim.set_guard(false)

	print("falling, death and saving")
	var hp2 := c.hp
	sim.report_fall(26.0 * M)
	check(c.hp < hp2 or c.dead or c.downed > 0.0, "a long fall hurts")
	sim.request_debug("hurt 500")
	check(c.dead or c.downed > 0.0, "running out of health puts the player down")
	for i in 200:
		await wait(0.1)
		if not c.dead and c.downed <= 0.0 and c.hp > 0.0:
			break
	check(not c.dead and c.hp > 0.0, "the player wakes again")
	world.save_world()
	var dir: String = world._save_dir
	check(FileAccess.file_exists(dir + "/survival.json"), "the world's survival state is saved")
	check(FileAccess.file_exists(sim._char_path(c.guid)), "the character is saved")
	var again := CharacterState.new()
	again.from_data(sim._read_json(sim._char_path(c.guid)), sim.ruleset)
	check(again.inv.count("cord") == c.inv.count("cord") and again.words.size() == c.words.size(), "a saved character loads back the same")
	var data: Dictionary = sim.entities.to_data()
	check(not data.is_empty(), "plants, structures and drops are in the save")
	_finish()


func _slot_of(key: String) -> int:
	for i in Inventory.SIZE:
		if c.inv.key_at(i) == key:
			return i
	return -1


func _count(kind: String) -> int:
	var n := 0
	for id in sim.entities.ents:
		if sim.entities.ents[id].kind == kind:
			n += 1
	return n


func _nearest(kind: String, pos: Vector3) -> int:
	var best := 0
	var best_d := 1e9
	for id in sim.entities.ents:
		var e: Dictionary = sim.entities.ents[id]
		if e.kind == kind and e.pos.distance_to(pos) < best_d:
			best = id
			best_d = e.pos.distance_to(pos)
	return best


func _nearest_def(def: String) -> int:
	for id in sim.entities.ents:
		if sim.entities.ents[id].get("def", "") == def:
			return id
	return 0


func _finish() -> void:
	print("%d checks, %d failed" % [_checks, _failed])
	quit(1 if _failed > 0 else 0)
