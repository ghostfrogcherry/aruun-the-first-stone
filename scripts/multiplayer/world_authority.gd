extends Node3D
## The world belongs to the server; characters belong to players.
##
## Every change to the world is a request from a client (or the local player,
## which talks to its own offline server). The server validates it, turns it
## into explicit ops, applies them, appends them to the journal and broadcasts
## them. Clients only ever apply ops the server sent.
##
## Op shapes (all positions are voxel coordinates, Vector3i):
##   {"t": "set",   "p": Vector3i, "b": int}
##   {"t": "fill",  "cells": Array[Vector3i], "b": int}
##   {"t": "clear", "cells": Array[Vector3i], "ids": PackedInt32Array, "fx": "chips"|"fall"}

signal world_ready
signal message(text: String)

const CHANNEL := VoxelBuffer.CHANNEL_TYPE
const Generator := preload("res://scripts/worldgen/aruun_generator.gd")
const PlayerScript := preload("res://scripts/player/player.gd")

var world_seed := 0
var terrain: VoxelTerrain
var vt: VoxelTool
var players: Node3D
var debris: Node3D
var spawn_point := Vector3.ZERO
var landmark_origin := Vector3i.ZERO

var _journal: Array = []          # every accepted op, in order (server + clients)
var _pending: Array = []          # ops waiting for their area to load
var _known_words := {}            # peer id -> PackedStringArray (server only)
var _protected := PackedInt32Array()
var _water := -1
var _save_dir := ""
var _journal_file: FileAccess


func _ready() -> void:
	players = Node3D.new()
	players.name = "Players"
	add_child(players)
	debris = Node3D.new()
	debris.name = "Debris"
	add_child(debris)
	multiplayer.peer_connected.connect(_on_peer_connected)
	multiplayer.peer_disconnected.connect(_on_peer_disconnected)


# --- Session start -----------------------------------------------------------

## Server / single player: load or create the world, then spawn ourselves.
func start_as_server() -> void:
	_save_dir = "user://worlds/%s" % Net.world_name
	DirAccess.make_dir_recursive_absolute(_save_dir)
	var meta := _load_meta()
	if meta.is_empty():
		meta = {"seed": randi(), "generation_version": WorldConfig.GENERATION_VERSION}
		_save_meta(meta)
	elif int(meta.generation_version) != WorldConfig.GENERATION_VERSION:
		message.emit("World was made with worldgen v%s; this build is v%d. Terrain may not match." % [meta.generation_version, WorldConfig.GENERATION_VERSION])
	_build(int(meta.seed), true)
	for op in _read_journal():
		_journal.append(op)
		_pending.append(op) # replay: the journal is the source of truth for edits
	_journal_file = FileAccess.open(_save_dir + "/journal.txt", FileAccess.READ_WRITE)
	if _journal_file == null:
		_journal_file = FileAccess.open(_save_dir + "/journal.txt", FileAccess.WRITE)
	_journal_file.seek_end()
	_spawn_local_player()
	for id in multiplayer.get_peers():
		_on_peer_connected(id)


@rpc("authority", "call_remote", "reliable")
func receive_world_info(seed_value: int, generation_version: int, journal: Array) -> void:
	if generation_version != WorldConfig.GENERATION_VERSION:
		message.emit("Host runs worldgen v%d, you run v%d. Update your build." % [generation_version, WorldConfig.GENERATION_VERSION])
	_build(seed_value, false)
	for op in journal:
		_journal.append(op)
		_pending.append(op)
	_spawn_local_player()


func _build(seed_value: int, with_stream: bool) -> void:
	world_seed = seed_value
	var layer_ids := ContentDB.block_ids.duplicate()

	var gen := Generator.new()
	gen.setup(world_seed, layer_ids)
	var field := HeightField.new(world_seed)
	landmark_origin = _pick_landmark_site(field)
	gen.add_stamp(landmark_origin, _landmark_voxels())
	spawn_point = Vector3(landmark_origin) + Vector3(24, 0, 6)
	spawn_point.y = field.height_at(int(spawn_point.x), int(spawn_point.z)) + 10

	var mesher := VoxelMesherBlocky.new()
	mesher.library = BlockLibraryBuilder.build(ContentDB.blocks)

	terrain = VoxelTerrain.new()
	terrain.name = "Terrain"
	terrain.mesher = mesher
	terrain.generator = gen
	terrain.max_view_distance = WorldConfig.VIEW_DISTANCE_VOXELS
	if with_stream:
		# Host-side cache of meshed-ready chunks. The journal stays the source of truth.
		var stream := VoxelStreamSQLite.new()
		stream.database_path = ProjectSettings.globalize_path(_save_dir + "/chunks.sqlite")
		terrain.stream = stream
	add_child(terrain)
	vt = terrain.get_voxel_tool()
	vt.channel = CHANNEL
	_protected = EditRules.protected_ids(ContentDB.blocks)
	_water = ContentDB.block_id("water")
	world_ready.emit()


## Seeded choice of a fairly flat spot 40-90 voxels from the origin.
func _pick_landmark_site(field: HeightField) -> Vector3i:
	var rng := RandomNumberGenerator.new()
	rng.seed = world_seed ^ 0x51_0E
	var best := Vector3i.ZERO
	var best_slope := INF
	for i in 24:
		var a := rng.randf() * TAU
		var d := rng.randf_range(40.0, 90.0)
		var cx := int(cos(a) * d)
		var cz := int(sin(a) * d)
		var lo := 1 << 30
		var hi := -lo
		for k in 12:
			var b := k * TAU / 12.0
			var h := field.height_at(cx + int(cos(b) * 16.0), cz + int(sin(b) * 16.0))
			lo = mini(lo, h)
			hi = maxi(hi, h)
		if lo <= HeightField.SEA_LEVEL + 2:
			continue # keep the ruin on dry land
		if hi - lo < best_slope:
			best_slope = hi - lo
			best = Vector3i(cx, lo + 1, cz)
	return best


## The landmark plus a cleared floor and a foundation, so it sits in any terrain.
func _landmark_voxels() -> Array:
	var out: Array = []
	var air := 0
	var rock := ContentDB.block_id("rust_rock")
	for z in range(-17, 18):
		for x in range(-17, 18):
			if x * x + z * z > 17 * 17:
				continue
			for y in range(-10, 0):
				out.append([x, y, z, rock])
			for y in range(0, 30):
				out.append([x, y, z, air])
	out.append_array(ContentDB.load_structure("first_stone_circle").voxels)
	return out


# --- Players -----------------------------------------------------------------

func _spawn_local_player() -> void:
	var id := multiplayer.get_unique_id()
	var p := _make_player(id, true)
	p.global_position = spawn_point
	_known_words[id] = PackedStringArray()


func _make_player(id: int, local: bool) -> CharacterBody3D:
	var existing = players.get_node_or_null(str(id))
	if existing:
		return existing
	var p := PlayerScript.new()
	p.name = str(id)
	p.is_local = local
	p.world = self
	players.add_child(p)
	if not local and multiplayer.is_server():
		# Keep terrain loaded around remote players so their edits can be validated.
		var viewer := VoxelViewer.new()
		viewer.view_distance = 64
		viewer.requires_visuals = false
		p.add_child(viewer)
	return p


func _on_peer_connected(id: int) -> void:
	if terrain == null and multiplayer.is_server():
		return # world not built yet; start_as_server() calls back for existing peers
	_make_player(id, false).global_position = spawn_point
	if multiplayer.is_server():
		_known_words[id] = PackedStringArray()
		receive_world_info.rpc_id(id, world_seed, WorldConfig.GENERATION_VERSION, _journal)
		message.emit("Player %d joined" % id)


func _on_peer_disconnected(id: int) -> void:
	var p := players.get_node_or_null(str(id))
	if p:
		p.queue_free()
	_known_words.erase(id)


@rpc("any_peer", "call_remote", "unreliable_ordered")
func player_moved(pos: Vector3, yaw: float, third_person: bool) -> void:
	var p = players.get_node_or_null(str(multiplayer.get_remote_sender_id()))
	if p == null:
		p = _make_player(multiplayer.get_remote_sender_id(), false)
	p.set_remote_state(pos, yaw, third_person)


# --- Requests (client -> server) ---------------------------------------------

func _sender() -> int:
	var id := multiplayer.get_remote_sender_id()
	return id if id != 0 else multiplayer.get_unique_id()


func _sender_pos(id: int) -> Variant:
	var p = players.get_node_or_null(str(id))
	return p.global_position + Vector3(0, 6.6, 0) if p else null


func _deny(id: int, reason: String) -> void:
	if id == multiplayer.get_unique_id():
		message.emit("Can't: " + reason)
	else:
		notify.rpc_id(id, "Can't: " + reason)


@rpc("authority", "call_remote", "reliable")
func notify(text: String) -> void:
	message.emit(text)


@rpc("any_peer", "call_local", "reliable")
func request_place(pos: Vector3i, block_id: int) -> void:
	if not multiplayer.is_server():
		return
	var id := _sender()
	var from = _sender_pos(id)
	if from == null:
		return
	var why := EditRules.check_reach(from, pos)
	if why == EditRules.OK:
		var current := _voxel_at(pos)
		if current > 0 and not ContentDB.collides(current):
			current = 0 # grass, flowers and water can be built over
		why = EditRules.check_place(current, block_id, ContentDB.placeable)
	if why == EditRules.OK and _overlaps_player(pos):
		why = "a player is standing there"
	if why != EditRules.OK:
		_deny(id, why)
		return
	_commit([{"t": "set", "p": pos, "b": block_id}])


@rpc("any_peer", "call_local", "reliable")
func request_dig(center: Vector3, radius: float) -> void:
	_carve_request(center, radius, EditRules.check_dig(radius), "chips")


@rpc("any_peer", "call_local", "reliable")
func request_blast(center: Vector3, radius: float) -> void:
	_carve_request(center, radius, EditRules.check_blast(radius), "chips")


@rpc("any_peer", "call_local", "reliable")
func request_learn(pos: Vector3i) -> void:
	if not multiplayer.is_server():
		return
	var id := _sender()
	var from = _sender_pos(id)
	if from == null:
		return
	var why := EditRules.check_reach(from, pos)
	var word: String = ContentDB.block(_voxel_at(pos)).get("inscription", "")
	if why == EditRules.OK and word == "":
		why = "nothing is written there"
	if why != EditRules.OK:
		_deny(id, why)
		return
	var known: PackedStringArray = _known_words.get(id, PackedStringArray())
	if not known.has(word):
		known.append(word)
		_known_words[id] = known
	var w: Dictionary = ContentDB.words[word]
	var text := "You learned the word %s: \"%s\". Press Q to speak it." % [w.display, w.meaning]
	if id == multiplayer.get_unique_id():
		word_learned(word, text)
	else:
		word_learned.rpc_id(id, word, text)


@rpc("authority", "call_remote", "reliable")
func word_learned(word_id: String, text: String) -> void:
	var me = players.get_node_or_null(str(multiplayer.get_unique_id()))
	if me:
		me.learn(word_id)
	message.emit(text)


@rpc("any_peer", "call_local", "reliable")
func request_speak(word_id: String, target: Vector3i) -> void:
	if not multiplayer.is_server():
		return
	var id := _sender()
	var from = _sender_pos(id)
	if from == null:
		return
	var result := LanguageInterpreter.interpret(word_id, _known_words.get(id, PackedStringArray()),
		ContentDB.words, from, target, func(p: Vector3i) -> bool: return _voxel_at(p) == 0)
	if result.has("error"):
		_deny(id, result.error)
		return
	var cells: Array = []
	var block := -1
	for e in result.edits:
		cells.append(e[0])
		block = ContentDB.block_id(e[1])
	if not cells.is_empty():
		_commit([{"t": "fill", "cells": cells, "b": block}])


func _carve_request(center: Vector3, radius: float, why: String, fx: String) -> void:
	if not multiplayer.is_server():
		return
	var id := _sender()
	var from = _sender_pos(id)
	if from == null:
		return
	var c := Vector3i(center.floor())
	if why == EditRules.OK:
		why = EditRules.check_reach(from, c)
	if why == EditRules.OK and not _editable(c, int(ceil(radius)) + WorldConfig.FLOATING_SEARCH_MARGIN):
		why = "that area is still loading"
	if why != EditRules.OK:
		_deny(id, why)
		return
	var ops: Array = []
	var carve := _sphere_cells(center, radius)
	if carve.cells.is_empty():
		return
	carve["t"] = "clear"
	carve["fx"] = fx
	ops.append(carve)
	_apply_op(carve, false) # apply now so the loose-piece scan sees the hole
	for piece in _loose_pieces_around(c, int(ceil(radius))):
		ops.append(piece)
		_apply_op(piece, false)
	_commit(ops, true)


# --- Applying ops ------------------------------------------------------------

## Server: journal + broadcast. `already_applied` skips local application.
func _commit(ops: Array, already_applied := false) -> void:
	for op in ops:
		if not already_applied:
			_apply_op(op, true)
		else:
			_play_fx(op)
		_journal.append(op)
		if _journal_file:
			_journal_file.store_line(var_to_str(op).replace("\n", " "))
	if _journal_file:
		_journal_file.flush()
	apply_ops.rpc(ops)


@rpc("authority", "call_remote", "reliable")
func apply_ops(ops: Array) -> void:
	for op in ops:
		_journal.append(op)
		# Keep order: once anything is waiting, everything after it waits too.
		if not _pending.is_empty() or not _apply_op(op, true):
			_pending.append(op)


## Returns false if the area is not loaded yet (the op is then retried).
func _apply_op(op: Dictionary, with_fx: bool) -> bool:
	if vt == null:
		return false
	match op.t:
		"set":
			if not _editable(op.p, 0):
				return false
			vt.set_voxel(op.p, op.b)
		"fill":
			for p in op.cells:
				if not _editable(p, 0):
					return false
			for p in op.cells:
				vt.set_voxel(p, op.b)
		"clear":
			for p in op.cells:
				if not _editable(p, 0):
					return false
			for p in op.cells:
				vt.set_voxel(p, 0)
	if with_fx:
		_play_fx(op)
	return true


func _play_fx(op: Dictionary) -> void:
	if op.t != "clear":
		return
	if op.fx == "fall":
		Debris.spawn_piece(debris, op.cells, op.ids)
	else:
		Debris.spawn_chips(debris, op.cells, op.ids)


func _process(_delta: float) -> void:
	if _pending.is_empty() or vt == null:
		return
	# Apply in order; stop at the first op whose area is not loaded yet.
	var done := 0
	for op in _pending:
		if not _apply_op(op, false):
			break
		done += 1
	if done > 0:
		_pending = _pending.slice(done)


# --- Voxel helpers -----------------------------------------------------------

func _voxel_at(p: Vector3i) -> int:
	return vt.get_voxel(p) if vt and _editable(p, 0) else -1


func _editable(p: Vector3i, margin: int) -> bool:
	var m := Vector3i(margin, margin, margin)
	return vt.is_area_editable(AABB(Vector3(p - m), Vector3(m * 2 + Vector3i.ONE)))


func _overlaps_player(p: Vector3i) -> bool:
	var box := AABB(Vector3(p), Vector3.ONE)
	for pl: Node3D in players.get_children():
		var foot: Vector3 = pl.global_position
		if box.intersects(AABB(foot - Vector3(1.4, 0, 1.4), Vector3(2.8, 7.2, 2.8))):
			return true
	return false


func _sphere_cells(center: Vector3, radius: float) -> Dictionary:
	var cells: Array = []
	var ids := PackedInt32Array()
	var r := int(ceil(radius))
	var c := Vector3i(center.floor())
	for y in range(-r, r + 1):
		for z in range(-r, r + 1):
			for x in range(-r, r + 1):
				var p := c + Vector3i(x, y, z)
				if (Vector3(p) + Vector3(0.5, 0.5, 0.5)).distance_to(center) > radius:
					continue
				var id := _voxel_at(p)
				if id > 0 and id != _water and not _protected.has(id):
					cells.append(p)
					ids.append(id)
	return {"cells": cells, "ids": ids}


## Scans a box around a carve and turns every unsupported piece into a falling op.
func _loose_pieces_around(c: Vector3i, r: int) -> Array:
	var half := r + WorldConfig.FLOATING_SEARCH_MARGIN
	var lo := c - Vector3i(half, half, half)
	var size := Vector3i.ONE * (half * 2 + 1)
	var grid := PackedInt32Array()
	grid.resize(size.x * size.y * size.z)
	var i := 0
	for y in size.y:
		for z in size.z:
			for x in size.x:
				var v := vt.get_voxel(lo + Vector3i(x, y, z))
				grid[i] = 0 if v == _water else v # water never holds anything up or falls
				i += 1
	var out: Array = []
	for piece in FloatingPieces.find(grid, size, WorldConfig.FLOATING_MAX_PIECE):
		var cells: Array = []
		var ids := PackedInt32Array()
		var protected_piece := false
		for idx in piece:
			if _protected.has(grid[idx]):
				protected_piece = true
				break
			cells.append(lo + FloatingPieces.index_to_pos(idx, size))
			ids.append(grid[idx])
		if not protected_piece:
			out.append({"t": "clear", "cells": cells, "ids": ids, "fx": "fall"})
	return out


# --- Saving ------------------------------------------------------------------

func save_world() -> void:
	if not multiplayer.is_server() or terrain == null:
		return
	terrain.save_modified_blocks()
	if _journal_file:
		_journal_file.flush()
	message.emit("World saved")


func _load_meta() -> Dictionary:
	var path := _save_dir + "/world.json"
	if not FileAccess.file_exists(path):
		return {}
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if typeof(parsed) == TYPE_DICTIONARY else {}


func _save_meta(meta: Dictionary) -> void:
	# Write to a temp file then rename, so a crash never leaves a half-written file.
	var tmp := _save_dir + "/world.json.tmp"
	var f := FileAccess.open(tmp, FileAccess.WRITE)
	f.store_string(JSON.stringify(meta, "\t"))
	f.close()
	DirAccess.rename_absolute(tmp, _save_dir + "/world.json")


func _read_journal() -> Array:
	var path := _save_dir + "/journal.txt"
	var out: Array = []
	if not FileAccess.file_exists(path):
		return out
	var f := FileAccess.open(path, FileAccess.READ)
	while not f.eof_reached():
		var line := f.get_line().strip_edges()
		if line == "":
			continue
		var op = str_to_var(line)
		if typeof(op) == TYPE_DICTIONARY:
			out.append(op)
		else:
			push_warning("Skipping damaged journal line") # partial write at crash
	return out
