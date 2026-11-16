extends Node
## What the local player does with their hands: the hotbar, attacking,
## guarding, dodging, using items, interacting and choosing words. It turns
## input into requests to the sim and tells player.gd how fast it may move.
## player.gd keeps movement, the camera and digging.

const M := WorldConfig.UNITS_PER_METER
const Hud := preload("res://scripts/ui/survival_hud.gd")
const HEAVY_HOLD_S := 0.38
const DODGE_SPEED := 9.5 * M
const DODGE_TIME := 0.24
const LOCK_RANGE := 16.0 * M

var player: CharacterBody3D
var world
var sim
var hud: CanvasLayer
var ui_open := false
var selected := 0
var word_index := 0
var prompt := ""
var hover_entity := 0
var lock_target := 0

var _hold := -1.0             # seconds the attack button has been held, -1 if not
var _guarding := false
var _dodge_left := 0.0
var _dodge_dir := Vector3.ZERO
var _attack_ready := 0.0
var _time := 0.0
var _push := Vector3.ZERO     # something is shoving the player (a charge, a shockwave, a pull)
var _push_left := 0.0


func _ready() -> void:
	world = player.world
	sim = world.sim
	_add_key("dodge", KEY_CTRL)
	_add_key("inventory", KEY_TAB)
	_add_key("next_word", KEY_R)
	_add_key("help", KEY_F1)
	_add_key("debug_kit", KEY_F9)
	_add_key("debug_time", KEY_F10)
	_add_key("debug_spawn", KEY_F11)
	if not InputMap.has_action("lock_on"):
		InputMap.add_action("lock_on")
		var mb := InputEventMouseButton.new()
		mb.button_index = MOUSE_BUTTON_MIDDLE
		InputMap.action_add_event("lock_on", mb)
	hud = Hud.new()
	hud.actions = self
	hud.sim = sim
	add_child(hud)
	sim.local_fx.connect(_on_fx)
	sim.inventory_changed.connect(func(): selected = clampi(selected, 0, Inventory.HOTBAR - 1))


func _add_key(action: String, key: Key) -> void:
	if InputMap.has_action(action):
		return
	InputMap.add_action(action)
	var ev := InputEventKey.new()
	ev.physical_keycode = key
	InputMap.action_add_event(action, ev)


# --- State player.gd asks about ----------------------------------------------

func held_key() -> String:
	return sim.view_inv.key_at(selected)


func held() -> Dictionary:
	return GameData.item(held_key())


func is_down() -> bool:
	return sim.view.get("dead", false) or float(sim.view.get("downed", 0.0)) > 0.0


## True while the bag is open or the character cannot act: player.gd skips its input.
func blocks_input() -> bool:
	return ui_open or is_down()


## Left click digs unless a weapon is held or a creature is in the way.
func can_dig() -> bool:
	if blocks_input() or held().get("kind", "") == "weapon":
		return false
	return not _is_creature(hover_entity)


func can_sprint() -> bool:
	return float(sim.view.get("stamina", 100.0)) > 4.0 and not _guarding


func speed_mult() -> float:
	if is_down():
		return 0.0
	var m := Conditions.mod(sim.view.get("mods", {}), "move_speed_mult")
	return m * (0.45 if _guarding else 1.0)


func jump_mult() -> float:
	return Conditions.mod(sim.view.get("mods", {}), "jump_mult")


## Velocity of a dodge in progress, or zero.
func dodge_velocity() -> Vector3:
	return _dodge_dir * DODGE_SPEED if _dodge_left > 0.0 else Vector3.ZERO


## Velocity from being shoved, or zero. Added to the player's own movement.
func push_velocity() -> Vector3:
	return _push if _push_left > 0.0 else Vector3.ZERO


func landed(speed: float) -> void:
	if speed > 6.0 * M:
		sim.report_fall.rpc_id(1, speed)


func current_word() -> String:
	var words: PackedStringArray = player.known_words
	if words.is_empty():
		return ""
	word_index = posmod(word_index, words.size())
	return words[word_index]


# --- Input -------------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("inventory"):
		_set_ui(not ui_open)
		get_viewport().set_input_as_handled()
		return
	if event.is_action_pressed("help"):
		hud.toggle_help()
		return
	if ui_open:
		if event.is_action_pressed("ui_cancel"):
			_set_ui(false)
			get_viewport().set_input_as_handled()
		return
	if is_down():
		return
	if event is InputEventKey and event.pressed and not event.echo:
		var n := int(event.keycode) - KEY_1
		if n >= 0 and n < Inventory.HOTBAR:
			select(n)
			return
	if event is InputEventMouseButton and event.pressed and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		if event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			select(posmod(selected + 1, Inventory.HOTBAR))
		elif event.button_index == MOUSE_BUTTON_WHEEL_UP:
			select(posmod(selected - 1, Inventory.HOTBAR))
	if event.is_action_pressed("next_word"):
		word_index += 1
		var w := current_word()
		if w != "":
			world.message.emit("Word: %s, \"%s\"" % [ContentDB.words[w].display, ContentDB.words[w].meaning])
	elif event.is_action_pressed("dodge"):
		sim.request_dodge.rpc_id(1)
	elif event.is_action_pressed("lock_on"):
		lock_target = 0 if lock_target != 0 else _nearest_creature_ahead()
	elif event.is_action_pressed("debug_kit"):
		sim.request_debug.rpc_id(1, "kit")
	elif event.is_action_pressed("debug_time"):
		sim.request_debug.rpc_id(1, "time %s" % (0.5 if sim.is_night() else 0.92))
	elif event.is_action_pressed("debug_spawn"):
		sim.request_debug.rpc_id(1, "spawn nightstalker")


func select(slot: int) -> void:
	selected = slot
	sim.request_select.rpc_id(1, slot)
	hud.refresh()


func _set_ui(open: bool) -> void:
	ui_open = open
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE if open else Input.MOUSE_MODE_CAPTURED
	hud.show_bag(open)
	if open:
		_hold = -1.0


## Right click was pressed. `target` is the voxel raycast result or null.
func secondary(target) -> void:
	var def := held()
	match def.get("kind", ""):
		"block":
			if target:
				world.request_place.rpc_id(1, target.previous_position, ContentDB.block_id(def.block))
		"food":
			sim.request_use.rpc_id(1, selected, Vector3i.ZERO, false)
		"seed", "structure":
			sim.request_use.rpc_id(1, selected, target.position if target else Vector3i.ZERO, target != null)


## E was pressed. Returns true if it was used up here (otherwise player.gd
## tries to read an inscription).
func interact(target) -> bool:
	if hover_entity != 0 and (not _is_creature(hover_entity) or prompt.contains("E:")):
		sim.request_interact.rpc_id(1, hover_entity)
		return true
	var friend := _downed_friend()
	if friend != 0:
		sim.request_revive.rpc_id(1, friend)
		return true
	if target and ContentDB.block(world.vt.get_voxel(target.position)).has("inscription"):
		return false
	sim.request_drink.rpc_id(1)
	return true


## Q was pressed: speak the chosen word at whatever is ahead.
func speak(target) -> void:
	var w := current_word()
	if w == "":
		world.message.emit("You don't know any words yet. Find an inscription and press E.")
		return
	if LanguageInterpreter.is_force_word(w, ContentDB.words):
		sim.request_cast.rpc_id(1, w, _aim())
	elif target:
		world.request_speak.rpc_id(1, w, target.position)
	else:
		world.message.emit("%s needs a surface to act on" % ContentDB.words[w].display)


func _aim() -> Vector3:
	return -player._camera.global_basis.z


func _is_creature(id: int) -> bool:
	return id != 0 and sim.entities.ents.get(id, {}).get("kind", "") == "creature"


func _downed_friend() -> int:
	for id in sim.public:
		if id != multiplayer.get_unique_id() and sim.public[id].get("down", false):
			var p = sim.player_pos(id)
			if p != null and p.distance_to(player.global_position) < 3.0 * M:
				return id
	return 0


func _nearest_creature_ahead() -> int:
	var best := 0
	var best_d := LOCK_RANGE
	var fwd := -player.global_basis.z
	for id in sim.entities.ents:
		var e: Dictionary = sim.entities.ents[id]
		if e.kind != "creature":
			continue
		var to: Vector3 = e.pos - player.global_position
		if to.length() < best_d and to.normalized().dot(fwd) > 0.3:
			best = id
			best_d = to.length()
	return best


func _on_fx(kind: String, _data: Dictionary) -> void:
	match kind:
		"dodge_go":
			var input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
			var dir := player.transform.basis * Vector3(input.x, 0, input.y)
			_dodge_dir = dir.normalized() if dir.length() > 0.1 else player.global_basis.z # back-step
			_dodge_left = DODGE_TIME
		"shove":
			_push = _data.v
			_push_left = float(_data.t)
		"hurt", "guard_break", "blocked", "evade", "hit_confirm", "die", "respawn", "downed":
			hud.flash(kind)


# --- Per frame ---------------------------------------------------------------

func _process(delta: float) -> void:
	_time += delta
	_dodge_left = maxf(_dodge_left - delta, 0.0)
	_push_left = maxf(_push_left - delta, 0.0)
	if not sim.active or world.vt == null:
		return
	# What is under the crosshair?
	var cam: Camera3D = player._camera
	var reach: float = WorldConfig.REACH_VOXELS + (player.THIRD_PERSON_DISTANCE if player.third_person else 0.0)
	var limit: float = reach
	if player.target:
		limit = minf(reach, cam.global_position.distance_to(Vector3(player.target.position) + Vector3(0.5, 0.5, 0.5)) + 1.0)
	hover_entity = sim.entities.pick(cam.global_position, -cam.global_basis.z, limit)
	prompt = sim.entities.describe(hover_entity)
	if prompt == "" and _downed_friend() != 0:
		prompt = "E: help them up"
	if prompt == "" and player.target and ContentDB.block(world.vt.get_voxel(player.target.position)).has("inscription"):
		prompt = "E: read the inscription"

	if lock_target != 0:
		var e: Dictionary = sim.entities.ents.get(lock_target, {})
		if e.is_empty() or e.pos.distance_to(player.global_position) > LOCK_RANGE * 1.3 or float(e.get("hp", 1.0)) <= 0.0:
			lock_target = 0
		elif player.third_person and not blocks_input():
			var to: Vector3 = e.pos - player.global_position
			player.rotation.y = lerp_angle(player.rotation.y, atan2(-to.x, -to.z), clampf(delta * 8.0, 0.0, 1.0))

	if blocks_input() or Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		_hold = -1.0
		_set_guard(false)
		return
	# Guard: hold right click with a weapon, a tool or bare hands.
	var kind: String = held().get("kind", "")
	_set_guard(Input.is_action_pressed("place") and kind in ["weapon", "tool", ""])
	# Attack: tap for a light blow, hold for a heavy one.
	var fighting := not can_dig() or player.target == null
	if Input.is_action_just_pressed("dig") and fighting:
		_hold = 0.0
	if _hold >= 0.0:
		_hold += delta
		if _hold >= HEAVY_HOLD_S:
			_attack(true)
		elif not Input.is_action_pressed("dig"):
			_attack(false)


func _attack(heavy: bool) -> void:
	_hold = -1.0
	if _time < _attack_ready:
		return
	_attack_ready = _time + 0.2
	sim.request_attack.rpc_id(1, heavy, _aim())


func _set_guard(on: bool) -> void:
	if on != _guarding:
		_guarding = on
		sim.set_guard.rpc_id(1, on)
