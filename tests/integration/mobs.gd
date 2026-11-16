extends SceneTree
## Scripted check of the four particular creatures (docs/design/mob_behaviours.md).
##
##   godot --headless -s tests/integration/mobs.gd -- --single --world=mobs_test
##
## Exits 0 when every check passes.

const M := 4.0

var _failed := 0
var _checks := 0
var world
var sim
var ent
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


func _ground(x: float, z: float) -> Vector3:
	return Vector3(x, sim.field.height_at(int(x), int(z)) + 1.0, z)


func _state(id: int) -> String:
	return ent._srv.get(id, {}).get("state", "gone")


func _wait_state(id: int, states: Array, seconds: float) -> bool:
	for i in int(seconds * 20.0):
		if _state(id) in states:
			return true
		await wait(0.05)
	return false


func _heal() -> void:
	c.hp = c.hp_max(sim.ruleset)
	c.stamina = c.stamina_max(sim.ruleset)
	c.cond["gravity_debt"] = 0.0
	sim._rt[1].word_ready = {}
	sim._rt[1].next_attack = 0.0


func _put_player(pos: Vector3) -> void:
	me.global_position = pos
	me.velocity = Vector3.ZERO
	await wait(0.4)


func _run(main: Node) -> void:
	world = main.world
	sim = world.sim
	ent = sim.entities
	cdb = root.get_node("ContentDB")
	world.message.connect(func(t): print("    > ", t))
	sim.local_fx.connect(_on_fx)
	for i in 300:
		await wait(0.1)
		me = world.players.get_node_or_null("1")
		if sim.active and sim.chars.has(1) and me and me.is_on_floor():
			break
	check(me != null and sim.chars.has(1), "world started")
	if _failed > 0:
		_finish()
		return
	c = sim.chars[1]
	sim.request_debug("kit")
	sim.request_debug("time 0.5")
	ent._spawn_timer = 1.0e9 # no wild spawns: this test places its own creatures
	for id in ent._srv.keys():
		if ent.ents[id].kind == "creature" and ent.ents[id].def != "echo_sentinel":
			ent.remove_quietly(id)
	# An open, flat place to work, away from the town.
	var base: Vector3 = me.global_position
	var arena := base
	for i in 40:
		var a := i * 0.7
		var p := base + Vector3(cos(a), 0, sin(a)) * (50.0 + i * 4.0)
		var h: int = sim.field.height_at(int(p.x), int(p.z))
		var flat := true
		for d in [Vector2(24, 0), Vector2(-24, 0), Vector2(0, 24), Vector2(0, -24), Vector2(40, 0)]:
			if absi(sim.field.height_at(int(p.x + d.x), int(p.z + d.y)) - h) > 2:
				flat = false
		if flat and h > 34:
			arena = Vector3(p.x, h + 1.0, p.z)
			break
	await _put_player(arena + Vector3(0, 2, 0))
	await wait(3.0) # let the terrain here load
	arena = me.global_position
	print("arena at ", arena)

	print("the sentinel waits in the ruin")
	var sentinel := 0
	for id in ent.ents:
		if ent.ents[id].get("def", "") == "echo_sentinel":
			sentinel = id
	check(sentinel != 0 and world.town_sites.has("ruin_arch"), "an echo sentinel is placed at the ruin arch")
	check(_state(sentinel) in ["idle", "dormant"], "and it is dormant")

	print("reedhopper")
	var hoppers: Array = []
	for i in 3:
		hoppers.append(ent.spawn_creature("reedhopper", arena + Vector3(9.0 * M + i * 3.0, 1, i * 2.0), 9001))
	await wait(0.6)
	check(await _wait_state(hoppers[0], ["alert"], 2.0), "it freezes when it notices the player 9 m away")
	var start: Vector3 = ent._srv[hoppers[0]].pos
	check(await _wait_state(hoppers[0], ["flee"], 3.0), "then it bolts")
	check(_state(hoppers[1]) == "flee" and _state(hoppers[2]) == "flee", "and the whole group goes with it")
	var top := 0.0
	for i in 40:
		await wait(0.05)
		top = maxf(top, ent._srv[hoppers[0]].pos.y - start.y)
	var ran: float = ent._srv[hoppers[0]].pos.distance_to(start) / M
	print("    hopped %.1f m high, ran %.1f m in 2 s" % [top / M, ran])
	check(top > 1.0 * M and ran > 6.0, "it flees in high hops, faster than a player runs")
	for id in hoppers:
		ent.remove_quietly(id)

	print("glassback")
	_heal()
	var gb: int = ent.spawn_creature("glassback", arena + Vector3(0, 1, -6.0 * M))
	await wait(1.0)
	check(_state(gb) in ["idle", "wander"], "it ignores a player standing 6 m away")
	# Milk it from behind.
	var grec: Dictionary = ent._srv[gb]
	grec.state = "idle"
	grec.t = 30.0
	grec.yaw = 0.0 # facing -z
	await _put_player(grec.pos + Vector3(0, 0.5, 2.2 * M)) # behind it
	var leaves := c.inv.count("glasswort_leaf")
	sim.request_interact(gb)
	check(c.inv.count("glasswort_leaf") == leaves + 1, "a calm glassback can be milked from behind")
	sim.request_interact(gb)
	check(c.inv.count("glasswort_leaf") == leaves + 1, "but not twice in a row")
	# Shell and flank.
	c.selected = _slot_of("flint_axe")
	var hp0: float = grec.hp
	var my_hp := c.hp
	sim.request_attack(false, Vector3(0, -0.1, -1).normalized())
	await wait(0.5)
	var shell: float = hp0 - grec.hp
	check(shell > 0.0 and shell < 8.0 and c.hp < my_hp, "a blow on the shell does little and pricks the attacker (%.1f damage)" % shell)
	check(_state(gb) != "idle" and _state(gb) != "wander", "and it is provoked")
	grec.state = "recover"
	grec.t = 5.0
	grec.yaw = 0.0
	await _put_player(grec.pos + Vector3(2.0 * M, 0.5, 0)) # on its flank
	_heal()
	hp0 = grec.hp
	sim.request_attack(false, Vector3(-1, -0.1, 0).normalized())
	await wait(0.5)
	check(hp0 - grec.hp > 15.0, "a blow on the flank does full damage (%.1f)" % (hp0 - grec.hp))
	# The charge.
	_heal()
	grec.state = "face"
	grec.t = 0.2
	grec.calm_at = sim.world_time + 60.0
	await _put_player(grec.pos + Vector3(0, 0.5, -6.0 * M))
	check(await _wait_state(gb, ["windup"], 3.0), "provoked, it lowers its head for a wind-up")
	my_hp = c.hp
	var before: Vector3 = me.global_position
	check(await _wait_state(gb, ["charge"], 2.0), "then it charges")
	await _wait_state(gb, ["recover", "stun"], 4.0)
	check(c.hp <= my_hp - 20.0, "a charge that connects hurts (%.0f damage)" % (my_hp - c.hp))
	print("    player was knocked %.1f m" % (me.global_position.distance_to(before) / M))
	check(_state(gb) in ["recover", "stun"], "and it stops to pant afterwards")
	# Flip it with a pull word.
	_heal()
	grec.state = "recover"
	grec.t = 6.0
	await _put_player(grec.pos + Vector3(0, 0.5, 5.0 * M))
	var aim: Vector3 = (grec.pos + Vector3(0, 1.5, 0) - sim._eye(1)).normalized()
	sim.request_cast("word_pull", aim)
	await wait(0.2)
	check(_state(gb) == "flipped", "a pull word flips it onto its back")
	await _put_player(grec.pos + Vector3(0, 0.5, 2.0 * M))
	_heal()
	hp0 = grec.hp
	sim.request_attack(false, Vector3(0, -0.1, -1).normalized())
	await wait(0.5)
	check(hp0 - grec.hp > 22.0, "and a flipped glassback takes extra damage (%.1f)" % (hp0 - grec.hp))
	ent.remove_quietly(gb)

	print("duskmoth")
	_heal()
	sim.request_debug("time 0.0")
	await _put_player(arena + Vector3(0, 0.5, 0))
	var ground := Vector3i(arena.floor()) + Vector3i(3, 1, 0)
	while not cdb.collides(world.vt.get_voxel(ground)):
		ground.y -= 1
	sim.request_use(_slot_of("campfire_kit"), ground, true)
	await _put_player(Vector3(ground) + Vector3(2.5 * M, 1.5, 0))
	var moths: Array = []
	for i in 6:
		moths.append(ent.spawn_creature("duskmoth", Vector3(ground) + Vector3(-12.0 * M, 6.0 * M, i * 4.0), 9002))
	await wait(8.0)
	for id in moths:
		if ent._srv.has(id):
			print("    moth %s at %s, fire at %s" % [ent._srv[id].state, ent._srv[id].pos, ground])
	var near := 0
	for id in moths:
		if ent._srv.has(id) and ent._srv[id].pos.distance_to(Vector3(ground)) < 9.0 * M:
			near += 1
	check(near >= 4, "moths 12 m away gather round a campfire (%d of 6 arrived)" % near)
	check(ent._srv[moths[0]].pos.y > ground.y + 1.0 * M, "and they stay in the air")
	my_hp = c.hp
	for i in 240:
		await wait(0.1)
		c.stamina = 100.0
		if c.buffs.has("dusted"):
			break
	check(c.hp < my_hp, "they dive at a player sitting by the fire (%.0f damage in all)" % (my_hp - c.hp))
	check(c.buffs.has("dusted"), "three dives close together leave the player dusted")
	check(ent.count_state("swarm", ["dive", "dive_windup"]) <= 2, "never more than two dive at once")
	c.add_buff("calm")
	_heal()
	await wait(1.5)
	my_hp = c.hp
	await wait(7.0)
	check(c.hp >= my_hp, "a calm player is left alone")
	sim.request_debug("time 0.5")
	await wait(7.0)
	var left := 0
	for id in moths:
		if ent.ents.has(id):
			left += 1
	check(left == 0, "at dawn the swarm leaves")

	print("echo sentinel")
	_heal()
	c.buffs.clear()
	var boss: int = ent.spawn_creature("echo_sentinel", arena + Vector3(-30.0 * M, 1, 0))
	var brec: Dictionary = ent._srv[boss]
	await _put_player(brec.pos + Vector3(0, 0.5, 14.0 * M))
	await wait(0.5)
	brec.home = brec.pos
	check(_state(boss) == "dormant", "it is dormant rubble while the player keeps away")
	var full: float = brec.hp
	c.selected = _slot_of("fallhammer")
	await _put_player(brec.pos + Vector3(0, 0.5, 6.0 * M))
	check(await _wait_state(boss, ["assemble"], 2.0), "it wakes when the player comes within 8 m")
	ent.damage_creature(boss, 50.0, 0.0, Vector3.ZERO, 1)
	check(brec.hp == full, "and cannot be hurt while it assembles")
	check(await _wait_state(boss, ["throw_windup"], 6.0), "its drill opens with a throw")
	_heal()
	my_hp = c.hp
	var shots := 0
	for i in 60:
		await wait(0.05)
		shots = maxi(shots, _count("shot"))
		if c.hp < my_hp:
			break
	check(shots > 0 and c.hp < my_hp, "the thrown stone flies and hurts (%.0f damage)" % (my_hp - c.hp))
	# Send the second stone back.
	check(await _wait_state(boss, ["throw_windup"], 8.0), "then a second throw")
	_heal()
	var returned := false
	for i in 80:
		await wait(0.05)
		for id in ent.ents:
			if ent.ents[id].kind == "shot" and not ent._srv[id].returned and ent.ents[id].pos.distance_to(me.global_position) < 5.0 * M:
				sim.request_cast("word_push", (ent.ents[id].pos - sim._eye(1)).normalized())
				returned = ent._srv.has(id) and ent._srv[id].returned
		if returned:
			break
	check(returned, "a push word sends the stone back")
	await wait(1.5)
	check(brec.hp < full, "and it strikes the core (%.0f damage)" % (full - brec.hp))
	# The slam: stand in it, then jump one.
	check(await _wait_state(boss, ["slam_windup"], 10.0), "third comes the slam, after a wind-up")
	_heal()
	await _put_player(brec.pos + Vector3(0, 0.3, 2.5 * M))
	my_hp = c.hp
	print("    before slam: boss %s player %s" % [brec.pos, me.global_position])
	check(await _wait_state(boss, ["exposed"], 3.0), "after the slam its core hangs open")
	print("    after slam: boss %s player %s" % [brec.pos, me.global_position])
	check(c.hp <= my_hp - 20.0, "the shockwave hurts a player on the ground (%.0f damage)" % (my_hp - c.hp))
	# Hit the open core, then try the stone front.
	_heal()
	brec.t = 3.0
	var face: Vector3 = MobBrains._dir_of(brec.yaw)
	await _put_player(brec.pos + face * 2.0 * M + Vector3(0, 0.3, 0))
	hp0 = brec.hp
	print("    core hit: boss %s yaw %.2f state %s player %s" % [brec.pos, brec.yaw, brec.state, me.global_position])
	sim.request_attack(false, (-face + Vector3(0, 0.1, 0)).normalized())
	await wait(0.5)
	check(hp0 - brec.hp > 40.0, "a Fallhammer blow on the open core does extra damage (%.0f)" % (hp0 - brec.hp))
	brec.state = "stalk"
	brec.t = 3.0
	_heal()
	hp0 = brec.hp
	sim.request_attack(false, (-face + Vector3(0, 0.1, 0)).normalized())
	await wait(0.5)
	check(hp0 - brec.hp < 1.0, "but its stone front shrugs off weapons")
	# Pull cancels a slam.
	brec.state = "slam_windup"
	brec.t = 1.0
	_heal()
	sim.request_cast("word_pull", (brec.pos + Vector3(0, 5, 0) - sim._eye(1)).normalized())
	await wait(0.2)
	check(_state(boss) == "stagger", "a pull word on the raised fist cancels the slam and staggers it")
	# Leave: it resets.
	brec.hp = 100.0
	await _put_player(arena + Vector3(0, 1, 0))
	check(await _wait_state(boss, ["dormant"], 13.0), "when everyone leaves it settles back into rubble")
	check(brec.hp >= full, "and mends fully")
	# Kill it.
	brec.state = "exposed"
	brec.t = 5.0
	ent.damage_creature(boss, 9999.0, 0.0, Vector3.ZERO, 1)
	await wait(0.3)
	check(sim.slain.has("echo_sentinel"), "once beaten it is remembered as slain")
	var loot := false
	for id in ent.ents:
		if ent.ents[id].kind == "drop":
			for it in ent._srv[id].items:
				if it.k == "word_fragment":
					loot = true
	check(loot, "and it leaves a word fragment")
	_finish()


func _on_fx(kind: String, data: Dictionary) -> void:
	if kind == "hurt":
		print("    hurt %.0f from %s (player at %s)" % [data.amount, data.from, me.global_position])


func _slot_of(key: String) -> int:
	for i in Inventory.SIZE:
		if c.inv.key_at(i) == key:
			return i
	return -1


func _count(kind: String) -> int:
	var n := 0
	for id in ent.ents:
		if ent.ents[id].kind == kind:
			n += 1
	return n


func _finish() -> void:
	print("%d checks, %d failed" % [_checks, _failed])
	quit(1 if _failed > 0 else 0)
