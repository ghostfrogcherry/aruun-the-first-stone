extends Node
## The game simulation that sits beside the world: characters (vitals,
## Conditions, bag, words), the day and night clock, combat, magick that moves
## things, crafting, farming and death. Same contract as the world: clients
## send requests, the server validates with the pure rule classes and sends
## back results. Single player is its own server, so there is one code path.
##
## Lives at World/Sim on every peer so RPC paths match. `entities` (World/Sim/
## Entities) holds creatures, plants, drops and structures.

signal inventory_changed
signal vitals_changed
signal local_fx(kind: String, data: Dictionary)

const M := WorldConfig.UNITS_PER_METER
const EntitiesScript := preload("res://scripts/entities/entities.gd")
const AvatarFx := preload("res://scripts/player/avatar_fx.gd")
const TICK := 0.1
const BLAST_POWER := 4
const WALK_SPEED := 4.5 * M

var world                              # world_authority.gd
var entities: Node3D
var field: HeightField
var ruleset := {}
var ruleset_id := ""
var world_time := 0.0
var day_length := 1200.0
var active := false

# Server only
var chars := {}                        # peer id -> CharacterState
var _rt := {}                          # peer id -> runtime record
var _swings: Array = []                # attacks waiting for their windup
var _landings: Array = []              # loose pieces in the air
var _acc := 0.0
var _vitals_timer := 0.0
var _clock_timer := 0.0
var _save_timer := 60.0
var _rng := RandomNumberGenerator.new()

# This peer's own character, as last told by the server (for HUD and input)
var view_inv := Inventory.new()
var view := {"hp": 100.0, "hp_max": 100.0, "stamina": 100.0, "stamina_max": 100.0, "cond": {}, "bands": [],
	"buffs": {}, "mods": {}, "dead": false, "downed": 0.0, "selected": 0, "tether": false}
var view_words := PackedStringArray()
var public := {}                       # peer id -> {"hp": 0..1, "held": key, "down": bool}


func _ready() -> void:
	GameData.ensure()
	_rng.randomize()
	entities = EntitiesScript.new()
	entities.name = "Entities"
	entities.sim = self
	entities.world = world
	add_child(entities)
	world.world_ready.connect(_on_world_ready)
	multiplayer.peer_disconnected.connect(_on_peer_left)


func _on_world_ready() -> void:
	field = HeightField.new(world.world_seed)
	if multiplayer.is_server():
		_load_world_state()
	active = true
	_hello.call_deferred()


func _hello() -> void:
	var me := _local_identity()
	hello.rpc_id(1, me.guid, me.name)


# =============================================================================
# Small helpers
# =============================================================================

func _sender() -> int:
	var id := multiplayer.get_remote_sender_id()
	return id if id != 0 else multiplayer.get_unique_id()


## Calls an RPC on one peer, or directly if that peer is us (the host).
func _send(id: int, method: StringName, args: Array) -> void:
	if id == multiplayer.get_unique_id():
		callv(method, args)
	else:
		callv("rpc_id", [id, method] + args)


func _tell(id: int, text: String) -> void:
	if text == "":
		return
	if id == multiplayer.get_unique_id():
		world.message.emit(text)
	else:
		world.notify.rpc_id(id, text)


func _player(id: int) -> Node3D:
	return world.players.get_node_or_null(str(id))


## Feet position of a player, or null.
func player_pos(id: int):
	var p := _player(id)
	return p.global_position if p else null


func _eye(id: int) -> Vector3:
	return _player(id).global_position + Vector3(0, 1.65 * M, 0)


func is_night() -> bool:
	return Conditions.is_night(Conditions.time_of_day(world_time, day_length))


func time_of_day() -> float:
	return Conditions.time_of_day(world_time, day_length)


func _alive(id: int) -> bool:
	var c: CharacterState = chars.get(id)
	return c != null and not c.dead and c.downed <= 0.0 and _player(id) != null


func living_positions() -> Array:
	var out: Array = []
	for id in chars:
		if _alive(id):
			out.append({"id": id, "pos": _player(id).global_position})
	return out


func nearest_player(pos: Vector3, max_dist: float) -> Dictionary:
	var best := {}
	for pl in living_positions():
		var d: float = pl.pos.distance_to(pos)
		if d <= max_dist and (best.is_empty() or d < best.dist):
			best = {"id": pl.id, "pos": pl.pos, "dist": d}
	return best


func _voxel(p: Vector3i) -> int:
	return world._voxel_at(p)


func _knows_a_word(id: int) -> bool:
	return not (world._known_words.get(id, PackedStringArray()) as PackedStringArray).is_empty()


func _stations(id: int) -> Array:
	var out: Array = []
	var pos = player_pos(id)
	if pos != null:
		for key in GameData.structures:
			var st: String = GameData.structures[key].get("station", "")
			if st != "" and entities.near_structure(pos, "station:" + st):
				out.append(st)
	return out


# =============================================================================
# Joining, saving, loading
# =============================================================================

func _local_identity() -> Dictionary:
	var path := "user://player.json"
	var data := {}
	if FileAccess.file_exists(path):
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(path))
		if typeof(parsed) == TYPE_DICTIONARY:
			data = parsed
	if not data.has("guid"):
		data = {"guid": "%08x%08x" % [randi(), randi()], "name": "Wanderer"}
		var f := FileAccess.open(path, FileAccess.WRITE)
		if f:
			f.store_string(JSON.stringify(data))
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--guid="):
			data.guid = arg.trim_prefix("--guid=").validate_filename()
		elif arg.begins_with("--name="):
			data.name = arg.trim_prefix("--name=").left(24)
	return data


@rpc("any_peer", "call_local", "reliable")
func hello(guid: String, display_name: String) -> void:
	if not multiplayer.is_server() or not active:
		return
	var id := _sender()
	if chars.has(id):
		return
	guid = guid.validate_filename().left(40)
	if guid == "":
		guid = "peer%d" % id
	for other in chars.values():
		if other.guid == guid:
			guid += "_%d" % id # two windows on one PC share an identity file
	var c := CharacterState.new()
	c.guid = guid
	c.display_name = display_name.left(24)
	var saved := _read_json(_char_path(guid))
	if saved.is_empty():
		c.setup_new(ruleset)
	else:
		c.from_data(saved, ruleset)
	chars[id] = c
	_rt[id] = {"last_pos": player_pos(id), "speed": 0.0, "combat": -99.0, "next_attack": 0.0, "guard": false,
		"iframes": 0.0, "aim": Vector3.FORWARD, "word_ready": {}, "flags": {}, "flag_timer": 0.0, "respawn": 0.0,
		"stagger": 0.0}
	# The world keeps the list of known words it checks when a word is spoken.
	var known: PackedStringArray = world._known_words.get(id, PackedStringArray())
	for w in c.words:
		if ContentDB.words.has(w) and not known.has(w):
			known.append(w)
	world._known_words[id] = known
	_send(id, &"receive_setup", [ruleset_id, world_time, known])
	entities.send_all_to(id)
	push_inventory(id)
	_tell(id, "%s rules. Tab opens your bag, F1 lists the controls." % ruleset.name)


@rpc("authority", "call_remote", "reliable")
func receive_setup(rules: String, time: float, words: PackedStringArray) -> void:
	ruleset_id = rules
	ruleset = GameData.ruleset(rules)
	day_length = float(ruleset.day_length_s)
	world_time = time
	view_words = words
	var me := _player(multiplayer.get_unique_id())
	if me:
		for w in words:
			me.learn(w)
	vitals_changed.emit()


func _on_peer_left(id: int) -> void:
	if chars.has(id):
		_save_char(id)
		chars.erase(id)
		_rt.erase(id)


func _char_path(guid: String) -> String:
	return "%s/characters/%s.json" % [world._save_dir, guid]


func _save_char(id: int) -> void:
	var c: CharacterState = chars[id]
	c.words = world._known_words.get(id, c.words)
	DirAccess.make_dir_recursive_absolute(world._save_dir + "/characters")
	_write_json(_char_path(c.guid), c.to_data())


## Called by the world when it saves (F5, quit) and by the autosave timer.
func save_all() -> void:
	if not multiplayer.is_server() or not active:
		return
	flush_landings()
	for id in chars:
		_save_char(id)
	var data: Dictionary = entities.to_data()
	data["ruleset"] = ruleset_id
	data["world_time"] = world_time
	_write_json(world._save_dir + "/survival.json", data)


func _load_world_state() -> void:
	var data := _read_json(world._save_dir + "/survival.json")
	ruleset_id = data.get("ruleset", GameData.default_ruleset)
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--ruleset="):
			ruleset_id = arg.trim_prefix("--ruleset=")
	ruleset = GameData.ruleset(ruleset_id)
	ruleset_id = ruleset.id
	day_length = float(ruleset.day_length_s)
	world_time = float(data.get("world_time", 0.0))
	entities.from_data(data)


func _read_json(path: String) -> Dictionary:
	if not FileAccess.file_exists(path):
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if typeof(parsed) == TYPE_DICTIONARY else {}


func _write_json(path: String, data: Dictionary) -> void:
	# Temp file then rename, so a crash never leaves half a save.
	var f := FileAccess.open(path + ".tmp", FileAccess.WRITE)
	if f == null:
		push_warning("Could not write %s" % path)
		return
	f.store_string(JSON.stringify(data))
	f.close()
	DirAccess.rename_absolute(path + ".tmp", path)


# =============================================================================
# Server tick
# =============================================================================

func _physics_process(delta: float) -> void:
	if not active:
		return
	world_time += delta # clients run the clock too; the server corrects it
	if not multiplayer.is_server():
		return
	entities.server_tick(delta)
	_run_swings()
	_run_landings()
	_acc += delta
	while _acc >= TICK:
		_acc -= TICK
		for id in chars.keys():
			_tick_char(id, TICK)
	_vitals_timer -= delta
	if _vitals_timer <= 0.0:
		_vitals_timer = 0.2
		var pub := {}
		for id in chars:
			_send(id, &"receive_vitals", [_vitals(id)])
			var c: CharacterState = chars[id]
			pub[id] = {"hp": clampf(c.hp / c.hp_max(ruleset), 0.0, 1.0), "held": c.held_key(),
				"down": c.dead or c.downed > 0.0, "guard": _rt[id].guard}
		receive_public.rpc(pub)
	_clock_timer -= delta
	if _clock_timer <= 0.0:
		_clock_timer = 5.0
		receive_clock.rpc(world_time)
	_save_timer -= delta
	if _save_timer <= 0.0:
		_save_timer = 60.0
		save_all()


func _tick_char(id: int, dt: float) -> void:
	var c: CharacterState = chars[id]
	var rt: Dictionary = _rt[id]
	var pos = player_pos(id)
	if pos == null:
		return
	if rt.last_pos == null:
		rt.last_pos = pos
	var step: Vector3 = pos - rt.last_pos
	step.y = 0.0
	rt.speed = lerpf(rt.speed, step.length() / dt, 0.4)
	rt.last_pos = pos
	rt.iframes = maxf(rt.iframes - dt, 0.0)
	rt.stagger = maxf(rt.stagger - dt, 0.0)
	if c.dead:
		rt.respawn -= dt
		if rt.respawn <= 0.0:
			_respawn(id)
		return
	if c.downed > 0.0:
		c.downed -= dt
		if c.downed <= 0.0:
			_die(id)
		return
	rt.flag_timer -= dt
	if rt.flag_timer <= 0.0:
		rt.flag_timer = 0.5
		rt.flags = _environment_flags(id, pos)
	var flags: Dictionary = rt.flags.duplicate()
	if rt.speed > WALK_SPEED * 1.25:
		flags["sprinting"] = true
	if world_time - float(rt.combat) < 8.0:
		flags["fighting"] = true
	elif rt.speed < 2.0 and (flags.has("near_heat") or flags.has("at_tether")):
		flags["resting"] = true
	if c.tick(ruleset, flags, dt):
		_down_or_die(id)


## What the world is doing to a player right now, as Conditions flags.
func _environment_flags(id: int, pos: Vector3) -> Dictionary:
	var flags := {}
	flags["night" if is_night() else "day"] = true
	var head := Vector3i((pos + Vector3(0, 1.9 * M, 0)).floor())
	var roof := false
	for y in range(1, 40):
		if ContentDB.collides(_voxel(head + Vector3i(0, y, 0))):
			roof = true
			break
	flags["sheltered" if roof else "exposed"] = true
	if entities.near_structure(pos, "heat"):
		flags["near_heat"] = true
	if entities.near_structure(pos, "station:tether"):
		flags["at_tether"] = true
	var alt: Dictionary = ruleset.altitude
	if pos.y > float(alt.thin_air_above):
		flags["high_altitude"] = true
	# Dust blows across the high mesas in gusts.
	if pos.y > float(alt.dusty_on_mesa_above) and field.is_mesa(int(pos.x), int(pos.z)) and sin(world_time * 0.07) > -0.2:
		flags["dusty"] = true
	return flags


func _vitals(id: int) -> Dictionary:
	var c: CharacterState = chars[id]
	return {"hp": c.hp, "hp_max": c.hp_max(ruleset), "stamina": c.stamina, "stamina_max": c.stamina_max(ruleset),
		"cond": c.cond, "bands": c.bands, "buffs": c.buffs, "mods": c.mods, "dead": c.dead, "downed": c.downed,
		"selected": c.selected, "tether": c.tether != null, "flags": _rt[id].flags.keys()}


@rpc("authority", "call_remote", "unreliable_ordered")
func receive_vitals(v: Dictionary) -> void:
	view = v
	vitals_changed.emit()


@rpc("authority", "call_local", "unreliable_ordered")
func receive_public(data: Dictionary) -> void:
	public = data
	for id in data:
		var p := _player(id)
		if p:
			_avatar(p).show_state(data[id])


@rpc("authority", "call_remote", "unreliable_ordered")
func receive_clock(t: float) -> void:
	world_time = t


func push_inventory(id: int) -> void:
	var c: CharacterState = chars[id]
	_send(id, &"receive_inventory", [c.inv.to_data(), c.selected])


@rpc("authority", "call_remote", "reliable")
func receive_inventory(slots: Array, selected: int) -> void:
	view_inv.from_data(slots)
	view["selected"] = selected
	inventory_changed.emit()


## Something everyone should see or hear: swings, hits, deaths, casts.
@rpc("authority", "call_local", "reliable")
func fx(kind: String, data: Dictionary) -> void:
	var p := _player(int(data.get("p", 0)))
	if p:
		_avatar(p).play(kind, data)
	if int(data.get("p", 0)) == multiplayer.get_unique_id():
		local_fx.emit(kind, data)


@rpc("authority", "call_remote", "reliable")
func teleport(pos: Vector3) -> void:
	var me := _player(multiplayer.get_unique_id())
	if me:
		me.global_position = pos
		me.velocity = Vector3.ZERO


func _avatar(p: Node3D) -> Node3D:
	var a: Node3D = p.get_node_or_null("AvatarFx")
	if a == null:
		a = AvatarFx.new()
		a.name = "AvatarFx"
		p.add_child(a)
	return a


# =============================================================================
# Requests: bag, crafting, using things
# =============================================================================

func _char_for_request() -> CharacterState:
	if not multiplayer.is_server():
		return null
	var id := _sender()
	return chars.get(id) if _alive(id) else null


@rpc("any_peer", "call_local", "reliable")
func request_select(slot: int) -> void:
	var c := _char_for_request()
	if c and slot >= 0 and slot < Inventory.HOTBAR:
		c.selected = slot
		_rt[_sender()].guard = false


@rpc("any_peer", "call_local", "reliable")
func request_move_slot(a: int, b: int) -> void:
	var c := _char_for_request()
	if c and c.inv.move(a, b):
		push_inventory(_sender())


@rpc("any_peer", "call_local", "reliable")
func request_drop(slot: int, all: bool) -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var taken := c.inv.take_slot(slot, 99999 if all else 1)
	if taken.is_empty():
		return
	var p := _player(id)
	entities.spawn_drop(p.global_position - p.global_basis.z * 2.5 * M + Vector3(0, 2, 0), [taken])
	push_inventory(id)


@rpc("any_peer", "call_local", "reliable")
func request_craft(recipe_id: String) -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var recipe: Dictionary = GameData.recipes.get(recipe_id, {})
	var why := Crafting.craft(recipe, c.inv, _stations(id), _knows_a_word(id))
	if why != Crafting.OK:
		_tell(id, "Can't craft: " + why)
		return
	push_inventory(id)
	_tell(id, "Made %s" % GameData.item(recipe.out).name.to_lower())


## Right click with a non-block item: eat, drink, smoke, plant or set down.
## `ground` is the voxel being looked at (used for seeds and structures).
@rpc("any_peer", "call_local", "reliable")
func request_use(slot: int, ground: Vector3i, has_ground: bool) -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var key := c.inv.key_at(slot)
	var def := GameData.item(key)
	if def.is_empty():
		return
	match def.kind:
		"food":
			c.apply_use(def.use)
			c.inv.take_slot(slot, 1)
			push_inventory(id)
			fx.rpc("use", {"p": id, "item": key})
			_tell(id, "You %s the %s" % [def.use.get("verb", "use"), def.name.to_lower()])
		"seed", "structure":
			if not has_ground:
				_tell(id, "Look at the ground where it should go")
				return
			var why := _set_down(id, c, def, ground)
			if why != "":
				_tell(id, "Can't: " + why)
				return
			c.inv.take_slot(slot, 1)
			push_inventory(id)


func _set_down(id: int, c: CharacterState, def: Dictionary, ground: Vector3i) -> String:
	var why := EditRules.check_reach(_eye(id), ground)
	if why != EditRules.OK:
		return "too far away"
	var ground_id := _voxel(ground)
	if not ContentDB.collides(ground_id):
		return "that is not solid ground"
	var above := ground + Vector3i.UP
	var above_id := _voxel(above)
	if above_id < 0 or ContentDB.collides(above_id) or ContentDB.block(above_id).get("fluid", false):
		return "there is no room above it"
	var pos := Vector3(above) + Vector3(0.5, 0, 0.5)
	if entities.plant_near(pos, entities.PLANT_SPACING):
		return "too close to something else"
	if def.kind == "seed":
		var plant: Dictionary = GameData.plants[def.plant]
		if not Farming.can_plant(plant, ContentDB.block(ground_id).get("key", "")):
			return "%s will not grow in %s" % [plant.name.to_lower(), ContentDB.block(ground_id).get("name", "that").to_lower()]
		if above_id > 0:
			world.server_ops([{"t": "clear", "cells": [above], "ids": PackedInt32Array([above_id]), "fx": "chips"}])
		var mult: float = entities.growth_mult_at(pos)
		entities.spawn_plant(def.plant, pos, world_time, mult)
		_tell(id, "Planted %s%s" % [plant.name.to_lower(), " (the Tether quickens it)" if mult > 1.0 else ""])
	else:
		if above_id > 0:
			world.server_ops([{"t": "clear", "cells": [above], "ids": PackedInt32Array([above_id]), "fx": "chips"}])
		entities.spawn_structure(def.structure, pos, c.guid)
		if GameData.structures[def.structure].get("respawn", false):
			c.tether = pos
			_tell(id, "You plant a Tether. This is home now: you will wake here.")
	return ""


@rpc("any_peer", "call_local", "reliable")
func request_interact(entity_id: int) -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var text: String = entities.interact(id, entity_id, c, _eye(id))
	push_inventory(id)
	_tell(id, text)


## E with nothing else to do: drink if there is water at hand.
@rpc("any_peer", "call_local", "reliable")
func request_drink() -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var feet := Vector3i(player_pos(id).floor())
	var water := ContentDB.block_id("water")
	for y in range(-2, 7):
		for z in range(-5, 6):
			for x in range(-5, 6):
				if _voxel(feet + Vector3i(x, y, z)) == water:
					c.cond["thirst"] = maxf(0.0, float(c.cond.get("thirst", 0.0)) - 40.0)
					_tell(id, "You drink from the water")
					return


@rpc("any_peer", "call_local", "reliable")
func request_revive(target: int) -> void:
	var c := _char_for_request()
	var other: CharacterState = chars.get(target)
	if c == null or other == null or other.downed <= 0.0:
		return
	var id := _sender()
	if player_pos(id).distance_to(player_pos(target)) > 3.0 * M:
		_tell(id, "Get closer to help them up")
		return
	other.downed = 0.0
	other.hp = other.hp_max(ruleset) * 0.3
	_rt[target].iframes = 2.0
	fx.rpc("revive", {"p": target})
	_tell(target, "%s pulls you back to your feet" % c.display_name)


# =============================================================================
# Hooks the world calls while digging, building and speaking
# =============================================================================

## How hard a block the player's held tool can dig.
func dig_power(id: int) -> int:
	var c: CharacterState = chars.get(id)
	return int(c.held_stats().get("dig_power", 1)) if c else 1


func can_edit(id: int) -> bool:
	return _alive(id)


func placeable_ids() -> PackedInt32Array:
	var out := PackedInt32Array()
	for key in GameData.items:
		if GameData.items[key].kind == "block":
			out.append(ContentDB.block_id(key))
	return out


## Takes one block item for a placement. False if the player has none.
func spend_block(id: int, block_id: int) -> bool:
	if not ruleset.build_cost:
		return true
	var c: CharacterState = chars.get(id)
	if c == null or not c.inv.remove(ContentDB.block(block_id).get("key", ""), 1):
		return false
	push_inventory(id)
	return true


## Voxels the player dug go into the bag (through the drop tables).
func on_dug(id: int, centre: Vector3, block_ids: PackedInt32Array) -> void:
	var c: CharacterState = chars.get(id)
	if c == null:
		return
	var got := {}
	for b in block_ids:
		var block := ContentDB.block(b)
		for d in GameData.roll_block_drops(block.get("key", ""), int(block.get("hardness", 0)), _rng):
			got[d[0]] = got.get(d[0], 0) + d[1]
	var spill: Array = []
	for key in got:
		var left := c.inv.add(key, got[key])
		if left > 0:
			spill.append({"k": key, "n": left})
	if not spill.is_empty():
		entities.spawn_drop(centre, spill)
	if ruleset.durability and c.inv.wear(c.selected):
		_tell(id, "Your tool breaks")
	push_inventory(id)


## Charges gravity debt and cooldown for a word. Returns "" or why not.
func pay_for_word(id: int, word_id: String) -> String:
	var c: CharacterState = chars.get(id)
	if c == null or not _alive(id):
		return "you cannot speak now"
	var w: Dictionary = ContentDB.words.get(word_id, {})
	var rt: Dictionary = _rt[id]
	if world_time < float(rt.word_ready.get(word_id, 0.0)):
		return "the word has not settled yet"
	var cost := float(w.get("debt", 0.0))
	if not c.can_owe(cost):
		return "your gravitational debt is too deep"
	c.owe(cost)
	rt.word_ready[word_id] = world_time + float(w.get("cooldown_s", 1.0))
	fx.rpc("cast", {"p": id, "word": word_id})
	return ""


func on_word_learned(id: int, word_id: String) -> void:
	var c: CharacterState = chars.get(id)
	if c and not c.words.has(word_id):
		c.words.append(word_id)


## A loose piece of terrain was cut free: work out where it lands. Adds
## "drop" to the op so every peer animates the same fall.
func schedule_landing(op: Dictionary) -> void:
	var drop := Collapse.settle_drop(op.cells, func(p: Vector3i) -> bool: return not ContentDB.collides(_voxel(p)))
	if drop <= 0:
		return
	op["drop"] = drop
	var g: float = ProjectSettings.get_setting("physics/3d/default_gravity")
	_landings.append({"at": world_time + Collapse.fall_time(drop, g), "cells": op.cells, "ids": op.ids, "drop": drop})


func _run_landings() -> void:
	while not _landings.is_empty() and float(_landings[0].at) <= world_time:
		_land(_landings.pop_front())


func flush_landings() -> void:
	while not _landings.is_empty():
		_land(_landings.pop_front())


func _land(l: Dictionary) -> void:
	# Check again where it comes to rest: the ground may have changed mid-fall.
	var drop := Collapse.settle_drop(l.cells, func(p: Vector3i) -> bool: return not ContentDB.collides(_voxel(p)),
		int(l.drop) + 8)
	if drop < 0:
		drop = int(l.drop)
	var cells: Array = []
	var ids := PackedInt32Array()
	var lo := Vector3(1e9, 1e9, 1e9)
	var hi := -lo
	for i in l.cells.size():
		var p: Vector3i = l.cells[i] + Vector3i(0, -drop, 0)
		if ContentDB.collides(_voxel(p)):
			continue
		cells.append(p)
		ids.append(l.ids[i])
		lo = lo.min(Vector3(p))
		hi = hi.max(Vector3(p) + Vector3.ONE)
	if cells.is_empty():
		return
	var damage := Collapse.impact_damage(cells.size(), drop)
	if damage > 0.0:
		var box := AABB(lo, hi - lo)
		entities.crush(box, damage)
		for id in chars.keys():
			if _alive(id) and box.grow(1.0).intersects(AABB(player_pos(id) - Vector3(1.4, 0, 1.4), Vector3(2.8, 7.2, 2.8))):
				damage_player(id, damage, Vector3(box.get_center().x, box.end.y + 4.0, box.get_center().z), "crush")
	world.server_ops([{"t": "put", "cells": cells, "ids": ids}])


# =============================================================================
# Combat
# =============================================================================

@rpc("any_peer", "call_local", "reliable")
func request_attack(heavy: bool, aim: Vector3) -> void:
	var c := _char_for_request()
	if c == null or aim.length() < 0.01:
		return
	var id := _sender()
	var rt: Dictionary = _rt[id]
	if world_time < float(rt.next_attack) or rt.stagger > 0.0:
		return
	var prof := CombatRules.attack_profile(c.held_stats(), heavy, c.mods)
	if not c.spend(prof.stamina):
		_tell(id, "You are out of breath")
		return
	rt.guard = false
	rt.next_attack = world_time + float(prof.cooldown)
	rt.combat = world_time
	_swings.append({"at": world_time + float(prof.windup), "p": id, "aim": aim.normalized(), "prof": prof, "heavy": heavy})
	fx.rpc("swing", {"p": id, "heavy": heavy})


func _run_swings() -> void:
	var keep: Array = []
	for s in _swings:
		if float(s.at) > world_time:
			keep.append(s)
		elif _alive(s.p):
			_resolve_swing(s)
	_swings = keep


func _resolve_swing(s: Dictionary) -> void:
	var id: int = s.p
	var c: CharacterState = chars[id]
	var prof: Dictionary = s.prof
	var origin := _player(id).global_position + Vector3(0, 1.2 * M, 0)
	var aim: Vector3 = s.aim
	var hit_any := false
	for eid in entities.creatures_in_arc(origin, aim, prof.reach, prof.arc):
		var target_pos: Vector3 = entities.ents[eid].pos
		var mass := float(GameData.creatures[entities.ents[eid].def].mass)
		entities.damage_creature(eid, prof.damage, prof.poise, CombatRules.knockback(origin, target_pos, prof.knockback, mass), id)
		hit_any = true
	if ruleset.pvp:
		for other in chars.keys():
			if other != id and _alive(other) and CombatRules.in_arc(origin, aim, player_pos(other) + Vector3(0, 3.6, 0), prof.reach, prof.arc, 1.4):
				damage_player(other, prof.damage, origin, "player")
				hit_any = true
	# A heavy Fallhammer blow lands with borrowed weight: it breaks the ground.
	var slam: Dictionary = c.held_stats().get("slam", {})
	if s.heavy and not slam.is_empty():
		var flat := Vector3(aim.x, 0, aim.z).normalized()
		var at := _player(id).global_position + flat * 1.6 * M + Vector3(0, -0.5, 0)
		world.server_carve(id, at, float(slam.radius), int(slam.power), false)
		c.owe(6.0)
		fx.rpc("slam", {"p": id, "at": at})
	if hit_any:
		fx.rpc("hit_confirm", {"p": id})
		if ruleset.durability and c.inv.wear(c.selected):
			_tell(id, "Your weapon breaks")
			push_inventory(id)


@rpc("any_peer", "call_local", "reliable")
func set_guard(on: bool) -> void:
	var c := _char_for_request()
	if c:
		_rt[_sender()].guard = on and c.stamina > 1.0


@rpc("any_peer", "call_local", "reliable")
func request_dodge() -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	if not c.spend(float(ruleset.vitals.dodge_cost)):
		return
	_rt[id].iframes = float(ruleset.vitals.dodge_iframes_s)
	_rt[id].guard = false
	_send(id, &"dodge_granted", [])
	fx.rpc("dodge", {"p": id})


@rpc("authority", "call_remote", "reliable")
func dodge_granted() -> void:
	local_fx.emit("dodge_go", {})


## A creature's attack lands now: who is in the way?
func creature_strike(origin: Vector3, forward: Vector3, reach: float, arc: float, damage: float) -> void:
	for id in chars.keys():
		if _alive(id) and CombatRules.in_arc(origin, forward, player_pos(id) + Vector3(0, 3.6, 0), reach, arc, 1.4):
			damage_player(id, damage * float(ruleset.creatures.damage_mult), origin, "creature")


func damage_player(id: int, amount: float, source: Vector3, kind: String) -> void:
	var c: CharacterState = chars.get(id)
	if c == null or not _alive(id) or amount <= 0.0:
		return
	var rt: Dictionary = _rt[id]
	if rt.iframes > 0.0 and kind != "fall" and kind != "condition":
		fx.rpc("evade", {"p": id})
		return
	rt.combat = world_time
	var p := _player(id)
	if rt.guard and kind in ["creature", "player"]:
		var g := CombatRules.guard(amount, float(c.held_stats().get("guard", 0.3)),
			CombatRules.is_facing(p.global_position, -p.global_basis.z, source), c.stamina)
		amount = g.damage
		if g.stamina_cost > 0.0:
			c.stamina = maxf(0.0, c.stamina - float(g.stamina_cost))
			c.since_spend = 0.0
			fx.rpc("blocked", {"p": id})
		if g.broken:
			rt.guard = false
			rt.stagger = 0.8
			fx.rpc("guard_break", {"p": id})
	var died := c.hurt(amount)
	fx.rpc("hurt", {"p": id, "amount": amount, "from": source})
	if died:
		_down_or_die(id)


@rpc("any_peer", "call_local", "reliable")
func report_fall(speed: float) -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var debt := float(c.cond.get("gravity_debt", 0.0))
	# Gravity debt is repaid in weight: you land harder while you owe.
	var dmg := CombatRules.fall_damage(clampf(speed, 0.0, 400.0) * (1.0 + debt / 200.0), ruleset.fall)
	if dmg > 0.0:
		damage_player(id, dmg, player_pos(id), "fall")


func _down_or_die(id: int) -> void:
	var c: CharacterState = chars[id]
	var others := 0
	for other in chars:
		if other != id and _alive(other):
			others += 1
	var downed_s := float(ruleset.death.downed_s)
	if downed_s > 0.0 and others > 0 and c.downed <= 0.0 and not c.dead:
		c.hp = 1.0
		c.downed = downed_s
		_rt[id].guard = false
		fx.rpc("downed", {"p": id})
		_tell(id, "You are down. A companion can pull you up (E) before you bleed out.")
		return
	_die(id)


func _die(id: int) -> void:
	var c: CharacterState = chars[id]
	var death: Dictionary = ruleset.death
	c.dead = true
	c.downed = 0.0
	c.hp = 0.0
	_rt[id].guard = false
	_rt[id].respawn = float(death.respawn_delay_s)
	if not death.keep_inventory and not c.inv.is_empty():
		entities.spawn_drop(player_pos(id) + Vector3(0, 1, 0), c.inv.clear(), true)
		push_inventory(id)
	if death.permadeath:
		c.words = PackedStringArray()
		c.tether = null
		world._known_words[id] = PackedStringArray()
	fx.rpc("die", {"p": id})
	_tell(id, "You fall. %s" % ("Your belongings lie where you fell." if not death.keep_inventory else "You will wake at your Tether."))


func _respawn(id: int) -> void:
	var c: CharacterState = chars[id]
	c.dead = false
	c.hp = c.hp_max(ruleset)
	c.stamina = float(ruleset.vitals.stamina)
	if ruleset.death.reset_conditions:
		c.cond = Conditions.start_values(ruleset)
	if ruleset.death.permadeath:
		c.setup_new(ruleset)
		push_inventory(id)
	var where: Vector3 = c.tether + Vector3(0, 1, 2.5 * M) if c.tether != null else world.spawn_point
	_rt[id].iframes = 3.0
	_rt[id].last_pos = where
	var p := _player(id)
	if p:
		p.global_position = where
	_send(id, &"teleport", [where])
	fx.rpc("respawn", {"p": id})


# =============================================================================
# Magick that moves things (Graviturge force words)
# =============================================================================

@rpc("any_peer", "call_local", "reliable")
func request_cast(word_id: String, aim: Vector3) -> void:
	var c := _char_for_request()
	if c == null:
		return
	var id := _sender()
	var known: PackedStringArray = world._known_words.get(id, PackedStringArray())
	var result := LanguageInterpreter.interpret_force(word_id, known, ContentDB.words, _eye(id), aim)
	if result.has("error"):
		_tell(id, "Can't: " + String(result.error).replace("_", " "))
		return
	var why := pay_for_word(id, word_id)
	if why != "":
		_tell(id, "Can't: " + why)
		return
	_rt[id].combat = world_time
	var moved: int = entities.apply_force(result.force, id)
	fx.rpc("force", {"p": id, "type": result.force.type, "origin": result.force.origin, "dir": result.force.dir,
		"range": result.force.range, "hit": moved})


# =============================================================================
# Debug (host only): quick ways to test rules without waiting
# =============================================================================

@rpc("any_peer", "call_local", "reliable")
func request_debug(cmd: String) -> void:
	if not multiplayer.is_server():
		return
	var id := _sender()
	if id != multiplayer.get_unique_id():
		_tell(id, "Only the host can use debug commands")
		return
	_tell(id, debug(id, cmd))


func debug(id: int, cmd: String) -> String:
	var a := cmd.split(" ", false)
	var c: CharacterState = chars.get(id)
	if a.is_empty() or c == null:
		return ""
	match a[0]:
		"time":
			var want := float(a[1]) if a.size() > 1 else 0.5
			world_time += fposmod(want - time_of_day(), 1.0) * day_length
			receive_clock.rpc(world_time)
			return "Time set"
		"give":
			if a.size() > 1 and GameData.items.has(a[1]):
				c.inv.add(a[1], int(a[2]) if a.size() > 2 else 1)
				push_inventory(id)
				return "Given"
			return "Unknown item"
		"kit":
			for key in ["flint_pick", "flint_axe", "reed_spear", "fallhammer", "campfire_kit", "tether_stone", "dust_mask"]:
				c.inv.add(key, 1)
			for key in ["roast_meat", "glasswort_leaf", "grinleaf_roll", "moss_bandage", "redroot_seed", "grinleaf_seed", "rust_rock"]:
				c.inv.add(key, 8)
			for w in ContentDB.words:
				if not world._known_words[id].has(w):
					world._known_words[id].append(w)
					world.word_learned_quiet(id, w)
			push_inventory(id)
			return "Test kit and all words given"
		"spawn":
			if a.size() > 1 and GameData.creatures.has(a[1]):
				var p := _player(id)
				var at := p.global_position - p.global_basis.z * 6.0 * M + Vector3(0, 4, 0)
				entities.spawn_creature(a[1], at)
				return "Spawned"
			return "Unknown creature"
		"ruleset":
			if a.size() > 1 and GameData.ruleset_ids().has(a[1]):
				ruleset_id = a[1]
				ruleset = GameData.ruleset(ruleset_id)
				for peer in chars:
					_send(peer, &"receive_setup", [ruleset_id, world_time, world._known_words.get(peer, PackedStringArray())])
				return "%s rules" % ruleset.name
			return "Unknown ruleset"
		"set":
			if a.size() > 2 and c.cond.has(a[1]):
				c.cond[a[1]] = clampf(float(a[2]), 0.0, 100.0)
				return "Set"
			return "Unknown condition"
		"hurt":
			damage_player(id, float(a[1]) if a.size() > 1 else 10.0, player_pos(id), "condition")
			return "Ouch"
	return "Unknown command"
