extends Node3D
## What other players see you do: the item in your hand, swings, guarding,
## being hit, falling and standing up, and the shimmer of a spoken word.
## Added to every player node by the sim. Placeholder boxes until the art
## pipeline's rigged character replaces them.

const M := WorldConfig.UNITS_PER_METER

var _player: Node3D
var _hand: Node3D              # shoulder pivot, third person
var _item: MeshInstance3D
var _view: Node3D              # first-person copy, under the camera
var _view_item: MeshInstance3D
var _held := "?"
var _guard := false
var _down := false
var _swing := 0.0              # seconds left of the current swing
var _swing_len := 0.3
var _flash := 0.0


func _ready() -> void:
	_player = get_parent()
	_hand = Node3D.new()
	_hand.position = Vector3(1.05, 5.6, 0)
	add_child(_hand)
	_item = _make_item(_hand, Vector3(0, -2.2, -0.9))
	if _player.is_local:
		_view = Node3D.new()
		_view.position = Vector3(1.5, -1.5, -2.4)
		_player._camera.add_child(_view)
		_view_item = _make_item(_view, Vector3.ZERO)
	_show_held("")


func _make_item(parent: Node3D, pos: Vector3) -> MeshInstance3D:
	var m := MeshInstance3D.new()
	m.mesh = BoxMesh.new()
	m.position = pos
	m.material_override = StandardMaterial3D.new()
	parent.add_child(m)
	return m


func show_state(s: Dictionary) -> void:
	_show_held(s.get("held", ""))
	_guard = s.get("guard", false)
	_down = s.get("down", false)


func _show_held(key: String) -> void:
	if key == _held:
		return
	_held = key
	var def := GameData.item(key)
	var size := Vector3(0.5, 0.5, 0.5)
	match def.get("kind", ""):
		"weapon", "tool": size = Vector3(0.35, 0.35, float(def.get("reach_m", 1.5)) * M * 0.55)
		"block": size = Vector3(0.9, 0.9, 0.9)
		"": size = Vector3(0.45, 0.45, 0.7) # bare fist
	for m in [_item, _view_item]:
		if m:
			m.scale = size
			(m.material_override as StandardMaterial3D).albedo_color = Color(def.get("color", "#c98b6b"))


func play(kind: String, data: Dictionary) -> void:
	match kind:
		"swing":
			_swing_len = 0.55 if data.get("heavy", false) else 0.28
			_swing = _swing_len
		"hurt", "guard_break":
			_flash = 0.2
		"force":
			_burst(data.origin + data.dir * 2.0 * M, data.dir, float(data.range), data.type)
		"cast":
			_flash = 0.1
		"slam":
			_burst(data.at, Vector3.UP, 3.0 * M, "push")


## A quick translucent pulse so everyone can see where a word went.
func _burst(at: Vector3, dir: Vector3, reach: float, type: String) -> void:
	var m := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	m.mesh = sphere
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(0.55, 0.4, 0.95, 0.45) if type != "pull" else Color(0.25, 0.8, 0.75, 0.45)
	m.material_override = mat
	m.top_level = true
	add_child(m)
	var from := at
	var to := at + dir * reach * 0.8
	if type == "pull":
		from = to
		to = at
	m.global_position = from
	m.scale = Vector3.ONE * (0.6 if type != "pull" else 3.0)
	var tween := m.create_tween().set_parallel(true)
	tween.tween_property(m, "global_position", to, 0.35)
	tween.tween_property(m, "scale", Vector3.ONE * (4.0 if type != "pull" else 0.6), 0.35)
	tween.tween_property(mat, "albedo_color:a", 0.0, 0.35)
	tween.chain().tween_callback(m.queue_free)


func _process(delta: float) -> void:
	var third: bool = _player._visual.visible
	_hand.visible = third
	if _view:
		_view.visible = not third and not _down
	# Swing: raise, chop down past level, return.
	var x := 0.0
	var z := 0.0
	if _swing > 0.0:
		_swing -= delta
		var t := 1.0 - maxf(_swing, 0.0) / _swing_len
		x = lerpf(1.9, -0.9, smoothstep(0.15, 0.7, t)) if t < 0.7 else lerpf(-0.9, 0.0, (t - 0.7) / 0.3)
	elif _guard:
		x = 1.1
		z = 0.7
	var k := clampf(delta * 22.0, 0.0, 1.0)
	_hand.rotation.x = lerpf(_hand.rotation.x, x, k)
	_hand.rotation.z = lerpf(_hand.rotation.z, z, k)
	if _view:
		_view.rotation.x = lerpf(_view.rotation.x, x * 0.6, k)
		_view.rotation.z = lerpf(_view.rotation.z, z, k)
	# Hit flash and lying down.
	_flash = maxf(_flash - delta, 0.0)
	var vis: Node3D = _player._visual
	vis.scale = Vector3.ONE * (1.0 + _flash * 0.8)
	vis.rotation.x = lerpf(vis.rotation.x, -PI * 0.5 if _down else 0.0, clampf(delta * 8.0, 0.0, 1.0))
	vis.position.y = lerpf(vis.position.y, 0.6 if _down else 0.0, clampf(delta * 8.0, 0.0, 1.0))
