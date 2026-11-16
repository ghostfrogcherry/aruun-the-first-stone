extends SceneTree
## Two-player check. Run one copy as host and one as the joining player:
##
##   godot --headless -s tests/integration/coop.gd -- --host --world=coop_test
##   godot --headless -s tests/integration/coop.gd -- --join=127.0.0.1 --guid=friend
##
## The host waits for the friend, hands them a kit and calls in a redstrider.
## The friend checks that what the server did reached them and that their own
## requests (crafting, attacking, speaking a word) are carried out by the host.
## Each side exits 0 when its checks pass.

const M := 4.0

var _failed := 0
var _checks := 0
var world
var sim
var host := false


func _initialize() -> void:
	host = OS.get_cmdline_user_args().has("--host")
	var main: Node = load("res://scenes/main/main.tscn").instantiate()
	root.add_child(main)
	_start.call_deferred(main)


func _start(main: Node) -> void:
	world = main.world
	sim = world.sim
	world.message.connect(func(t): print("    > ", t))
	if host:
		_host()
	else:
		_friend()


func check(ok: bool, what: String) -> void:
	_checks += 1
	if not ok:
		_failed += 1
	print("  %s  %s: %s" % ["ok  " if ok else "FAIL", "host" if host else "friend", what])


func wait(seconds: float) -> void:
	await create_timer(seconds).timeout


func _finish() -> void:
	print("%s: %d checks, %d failed" % ["host" if host else "friend", _checks, _failed])
	quit(1 if _failed > 0 else 0)


func _host() -> void:
	var friend := 0
	for i in 600:
		await wait(0.1)
		for id in sim.chars:
			if id != 1:
				friend = id
		if friend != 0 and sim.player_pos(friend) != null:
			break
	check(friend != 0, "the friend's character joined")
	if friend == 0:
		_finish()
		return
	await wait(3.0)
	var c: CharacterState = sim.chars[friend]
	sim.debug(friend, "kit")
	sim.debug(friend, "give plant_fibre 3")
	sim.debug(friend, "time 0.5")
	var pos: Vector3 = sim.player_pos(friend)
	var beast: int = sim.entities.spawn_creature("redstrider", pos + Vector3(0, 1, -1.2 * M))
	var hp := float(sim.entities.ents[beast].get("hp", 1.0))
	var debt := float(c.cond.get("gravity_debt", 0.0))
	for i in 150:
		await wait(0.1)
		if c.inv.count("cord") > 0 and float(c.cond.get("gravity_debt", 0.0)) > debt:
			break
	check(c.inv.count("cord") == 1, "the friend's crafting request was carried out")
	check(float(c.cond.get("gravity_debt", 0.0)) > debt, "the friend's word was spoken and charged")
	var now: Dictionary = sim.entities.ents.get(beast, {})
	check(now.is_empty() or float(now.get("hp", 0.0)) < hp or sim.entities._srv[beast].state != "idle",
		"the friend's attack or word reached the redstrider")
	world.save_world()
	check(FileAccess.file_exists(sim._char_path(c.guid)), "the friend's character is saved on the host")
	await wait(4.0)
	_finish()


func _friend() -> void:
	var me: Node3D
	for i in 600:
		await wait(0.1)
		if root.multiplayer.has_multiplayer_peer() and root.multiplayer.get_unique_id() != 1:
			me = world.players.get_node_or_null(str(root.multiplayer.get_unique_id()))
		if me and sim.active and sim.view_inv.count("fallhammer") > 0:
			break
	check(me != null and sim.active, "joined the host's world")
	check(sim.view_inv.count("fallhammer") == 1, "the bag the host filled arrived")
	check(sim.ruleset_id != "" and float(sim.view.hp_max) > 0.0, "ruleset and vitals arrived")
	await wait(1.0)
	var beasts := 0
	for id in sim.entities.ents:
		if sim.entities.ents[id].kind == "creature":
			beasts += 1
	check(beasts > 0, "the creature the host called in is visible here")
	check(me.known_words.has("word_push"), "the words the host granted are known here")
	sim.request_craft.rpc_id(1, "cord")
	for i in Inventory.SIZE:
		if sim.view_inv.key_at(i) == "fallhammer":
			sim.request_select.rpc_id(1, i)
	await wait(0.3)
	sim.request_attack.rpc_id(1, false, Vector3(0, 0, -1))
	sim.request_cast.rpc_id(1, "word_push", Vector3(0, 0, -1))
	for i in 50:
		await wait(0.1)
		if sim.view_inv.count("cord") > 0:
			break
	check(sim.view_inv.count("cord") == 1, "the crafted cord shows in the bag")
	await wait(1.0)
	check(float(sim.view.get("cond", {}).get("gravity_debt", 0.0)) > 0.0, "gravity debt shows on the HUD data")
	_finish()
