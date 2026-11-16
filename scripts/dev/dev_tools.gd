extends Node
## Developer controls for the demo: fly, noclip, teleport and spawning any model from
## content/models/models.json (creatures, people, props, items, crops, flora).
## F1 turns them on and shows the key list. Everything here is local to this game:
## spawned things are not sent to other players or saved.

const KEYS := [
	["F1", "dev tools on / off"],
	["F", "fly on / off (Space up, Ctrl down, Shift fast)"],
	["N", "noclip: fly through terrain"],
	["[  ]", "choose what to spawn"],
	["G", "spawn it where you look"],
	["Backspace", "remove the last thing spawned"],
	["T", "teleport to where you look"],
	["Home", "back to the stone circle"],
]
const WANDER_RADIUS := 14.0      # voxels
const WANDER_SPEED := 1.2 * WorldConfig.UNITS_PER_METER

var player                       # player.gd (untyped: set by the player)
var enabled := false
var _choices := PackedStringArray()
var _index := 0
var _spawned: Array[Node3D] = []
var _wander := {}                # node -> {"home": Vector3, "goal": Vector3, "wait": float}
var _panel: Label


func _ready() -> void:
	for category in ["creatures", "props", "items", "crops", "flora"]:
		_choices.append_array(ModelLibrary.ids(category))
	var layer := CanvasLayer.new()
	add_child(layer)
	_panel = Label.new()
	_panel.position = Vector2(16, 16)
	_panel.add_theme_color_override("font_color", Color("#f4e9c8"))
	_panel.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.8))
	_panel.add_theme_constant_override("shadow_offset_x", 1)
	_panel.add_theme_constant_override("shadow_offset_y", 1)
	layer.add_child(_panel)
	_refresh()


func _unhandled_input(event: InputEvent) -> void:
	if not (event is InputEventKey and event.pressed and not event.echo):
		return
	var key: int = event.physical_keycode
	if key == KEY_F1:
		enabled = not enabled
		if not enabled:
			player.fly = false
			player.noclip = false
		_say("Dev tools on: see the key list, top left." if enabled else "Dev tools off.")
	elif not enabled:
		return
	elif key == KEY_F:
		player.fly = not player.fly
		player.noclip = player.noclip and player.fly
		_say("Flying." if player.fly else "Walking.")
	elif key == KEY_N:
		player.noclip = not player.noclip
		player.fly = player.fly or player.noclip
		_say("Noclip on: you pass through terrain." if player.noclip else "Noclip off.")
	elif key == KEY_BRACKETLEFT or key == KEY_BRACKETRIGHT:
		if not _choices.is_empty():
			_index = posmod(_index + (1 if key == KEY_BRACKETRIGHT else -1), _choices.size())
	elif key == KEY_G:
		_spawn(_choices[_index] if not _choices.is_empty() else "")
	elif key == KEY_BACKSPACE:
		if not _spawned.is_empty():
			var last: Node3D = _spawned.pop_back()
			_wander.erase(last)
			last.queue_free()
	elif key == KEY_T:
		var spot = _aim_point()
		if spot != null:
			player.global_position = spot + Vector3(0, 0.1, 0)
			player.velocity = Vector3.ZERO
	elif key == KEY_HOME:
		player.global_position = player.world.spawn_point
		player.velocity = Vector3.ZERO
	else:
		return
	_refresh()
	get_viewport().set_input_as_handled()


func _process(delta: float) -> void:
	for node in _wander:
		_step_wanderer(node, _wander[node], delta)


func _spawn(id: String) -> void:
	var spot = _aim_point()
	if id == "" or spot == null:
		_say("Look at the ground to spawn something.")
		return
	if GameData.creatures.has(id):
		# A creature the game knows: ask the host for a real one, with its behaviour.
		player.world.sim.request_debug.rpc_id(1, "spawn_at %s %d %d %d" % [id, int(spot.x), int(spot.y), int(spot.z)])
		_say("Spawned a living %s." % id)
		return
	var node := ModelLibrary.instantiate(id, ModelLibrary.info(id).get("category", "") == "props")
	if node == null:
		return
	player.world.add_child(node)
	node.global_position = spot
	node.rotation.y = player.rotation.y + PI # face the player
	_spawned.append(node)
	if node is VoxelRig:
		_wander[node] = {"home": spot, "goal": spot, "wait": randf_range(0.5, 2.0)}
	_say("Spawned %s." % id)


## Creatures stroll between random spots near where they were spawned.
func _step_wanderer(node: Node3D, w: Dictionary, delta: float) -> void:
	var rig := node as VoxelRig
	if w.wait > 0.0:
		w.wait -= delta
		if rig.current_clip != "idle":
			rig.play("idle")
		if w.wait <= 0.0:
			var a := randf() * TAU
			w.goal = w.home + Vector3(cos(a), 0, sin(a)) * randf_range(3.0, WANDER_RADIUS)
		return
	var to := Vector3(w.goal.x - node.global_position.x, 0, w.goal.z - node.global_position.z)
	if to.length() < 0.5:
		w.wait = randf_range(1.0, 4.0)
		return
	if rig.current_clip != "walk":
		rig.play("walk")
	node.global_position += to.normalized() * minf(WANDER_SPEED * delta, to.length())
	node.rotation.y = lerp_angle(node.rotation.y, atan2(-to.x, -to.z), clampf(delta * 6.0, 0.0, 1.0))
	var ground = _ground_under(node.global_position)
	if ground != null:
		node.global_position.y = lerpf(node.global_position.y, ground, clampf(delta * 10.0, 0.0, 1.0))


func _ground_under(p: Vector3) -> Variant:
	var hit: VoxelRaycastResult = player.world.vt.raycast(p + Vector3(0, 6, 0), Vector3.DOWN, 16.0)
	return float(hit.position.y + 1) if hit else null


## The top of the block under the crosshair, or null.
func _aim_point() -> Variant:
	var t: VoxelRaycastResult = player.target
	if t == null:
		return null
	return Vector3(t.previous_position) + Vector3(0.5, 0.0, 0.5)


func _refresh() -> void:
	_panel.visible = enabled
	if not enabled:
		return
	var lines := PackedStringArray(["DEV TOOLS"])
	for k in KEYS:
		lines.append("%-10s %s" % k)
	var id := _choices[_index] if not _choices.is_empty() else "(no models)"
	lines.append("")
	lines.append("Spawn: %s  (%s)   %d out" % [id, ModelLibrary.info(id).get("category", ""), _spawned.size()])
	lines.append("Fly: %s   Noclip: %s" % ["on" if player.fly else "off", "on" if player.noclip else "off"])
	_panel.text = "\n".join(lines)


func _say(text: String) -> void:
	player.world.message.emit(text)
