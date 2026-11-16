class_name EntityVisual
extends Node3D
## Draws one entity. Placeholder art built from boxes in code, animated by
## moving the pieces (the segmented-limb style). When the art pipeline's
## ModelLibrary has a model with the same key, that model is used instead.

const M := WorldConfig.UNITS_PER_METER

static var _mats := {}
static var _box: BoxMesh

var _kind := ""
var _def := {}
var _rig: Node3D                 # everything that tilts, flashes and falls over
var _legs: Array = []            # leg pivots, swung while moving
var _anim := 0                   # 0 idle, 1 walk, 2 run, 3 windup, 4 stagger
var _time := 0.0
var _pose := 0.0                 # windup lean, eased
var _flash := 0.0
var _dead := false
var _bar: MeshInstance3D
var _spin := false
var _model: VoxelRig             # the art pipeline's rigged model, if there is one


static func build(e: Dictionary) -> EntityVisual:
	var v := EntityVisual.new()
	v._setup(e)
	return v


func _setup(e: Dictionary) -> void:
	_kind = e.kind
	_rig = Node3D.new()
	add_child(_rig)
	match _kind:
		"creature":
			_def = GameData.creatures[e.def]
			if not _try_model(e.def):
				_build_creature()
			_bar = _piece(self, Vector3(3.0, 0.25, 0.25), Vector3(0, float(_def.height_m) * M + 1.5, 0), "#c8413a", true)
			_bar.visible = false
		"plant":
			_def = GameData.plants[e.def]
			_build_plant(int(e.get("stage", 0)))
		"drop":
			_spin = true
			var it := GameData.item(e.def)
			var art := "seeds" if it.get("kind", "") == "seed" else str(e.def)
			if ModelLibrary.has(art):
				var held := MeshInstance3D.new()
				held.mesh = ModelLibrary.mesh(art)
				held.rotation_degrees = Vector3(0, 0, 70) # lying on its side
				held.position = Vector3(0, 1.0, 0)
				_rig.add_child(held)
			else:
				_piece(_rig, Vector3.ONE * 0.9, Vector3(0, 0.9, 0), it.get("color", "#cccccc"))
			if int(e.get("n", 1)) > 1:
				_piece(_rig, Vector3.ONE * 0.6, Vector3(0.5, 0.5, 0.4), "#d8c8a8")
		"structure":
			_def = GameData.structures[e.def]
			if not _try_model("tether_stone" if e.def == "tether" else e.def):
				_build_structure(e.def)
			var light := OmniLight3D.new()
			light.light_color = Color(_def.get("light", "#ffffff"))
			light.omni_range = 9.0 * M
			light.light_energy = 2.0
			light.position.y = 3.0
			add_child(light)


## Uses the art pipeline's model if there is one for this key.
func _try_model(key: String) -> bool:
	if not ModelLibrary.has(key):
		return false
	var node := ModelLibrary.instantiate(key)
	if node == null:
		return false
	_rig.add_child(node)
	if node is VoxelRig:
		_model = node
	return true


func _mat(color: String, glow := false) -> StandardMaterial3D:
	var key := color + ("g" if glow else "")
	if not _mats.has(key):
		var m := StandardMaterial3D.new()
		m.albedo_color = Color(color)
		m.roughness = 0.9
		if glow:
			m.emission_enabled = true
			m.emission = Color(color)
			m.emission_energy_multiplier = 1.8
		_mats[key] = m
	return _mats[key]


func _piece(parent: Node3D, size: Vector3, pos: Vector3, color: String, glow := false) -> MeshInstance3D:
	if _box == null:
		_box = BoxMesh.new()
	var m := MeshInstance3D.new()
	m.mesh = _box
	m.scale = size
	m.position = pos
	m.material_override = _mat(color, glow)
	parent.add_child(m)
	return m


func _leg(pos: Vector3, length: float, thick: float, color: String) -> void:
	var pivot := Node3D.new()
	pivot.position = pos
	_rig.add_child(pivot)
	_piece(pivot, Vector3(thick, length, thick), Vector3(0, -length * 0.5, 0), color)
	_legs.append(pivot)


func _build_creature() -> void:
	var look: Dictionary = _def.look
	var h := float(_def.height_m) * M
	var r := float(_def.radius_m) * M
	var c: String = look.color
	var a: String = look.accent
	if look.shape == "strider":
		# Tall grazer: small body high on four long legs, long neck. Forward is -Z.
		var leg := h * 0.6
		_piece(_rig, Vector3(r * 1.3, h * 0.2, r * 2.6), Vector3(0, leg + h * 0.1, 0), c)
		_piece(_rig, Vector3(r * 0.5, h * 0.34, r * 0.5), Vector3(0, leg + h * 0.32, -r * 1.3), c)   # neck
		_piece(_rig, Vector3(r * 0.7, h * 0.09, r * 1.1), Vector3(0, leg + h * 0.5, -r * 1.6), a)    # head
		_piece(_rig, Vector3(r * 0.3, h * 0.05, r * 1.2), Vector3(0, leg + h * 0.16, r * 1.7), a)    # tail
		for sx in [-1.0, 1.0]:
			for sz in [-1.0, 1.0]:
				_leg(Vector3(sx * r * 0.5, leg, sz * r * 1.0), leg, r * 0.28, a)
	else:
		# Low hunter: long striped body, four legs and two small grasping limbs.
		var leg := h * 0.5
		_piece(_rig, Vector3(r * 1.2, h * 0.38, r * 3.4), Vector3(0, leg + h * 0.2, 0), c)
		for i in 3:
			_piece(_rig, Vector3(r * 1.25, h * 0.4, r * 0.3), Vector3(0, leg + h * 0.2, -r * 0.9 + i * r * 0.9), a)
		_piece(_rig, Vector3(r * 0.9, h * 0.3, r * 1.2), Vector3(0, leg + h * 0.36, -r * 2.1), c)    # head
		_piece(_rig, Vector3(r * 0.95, h * 0.07, r * 0.2), Vector3(0, leg + h * 0.42, -r * 2.6), "#f2e36b", true) # eyes
		_piece(_rig, Vector3(r * 0.3, h * 0.12, r * 1.6), Vector3(0, leg + h * 0.3, r * 2.3), c)     # tail
		for sx in [-1.0, 1.0]:
			for sz in [-1.0, 1.0]:
				_leg(Vector3(sx * r * 0.55, leg, sz * r * 1.3), leg, r * 0.3, c)
			_piece(_rig, Vector3(r * 0.18, h * 0.22, r * 0.18), Vector3(sx * r * 0.5, leg, -r * 1.7), a)


func _build_plant(stage: int) -> void:
	for ch in _rig.get_children():
		ch.queue_free()
	var art := "%s_s%d" % [_def.key, stage + 1]
	if _try_model(art) or (int(_def.stages) <= 1 and _try_model(_def.key)):
		return
	var look: Dictionary = _def.look
	var stages := maxi(int(_def.stages), 1)
	var grown := float(stage + 1) / stages
	var ripe := stage >= stages - 1
	var h := float(look.height_m) * M * grown
	var c: String = look.color
	var a: String = look.accent
	match look.shape:
		"reed":
			for i in 5:
				var off := Vector3(cos(i * 1.3) * 0.7, 0, sin(i * 1.3) * 0.7)
				var hh := h * (0.7 + 0.3 * fmod(i * 0.37, 1.0))
				_piece(_rig, Vector3(0.3, hh, 0.3), off + Vector3(0, hh * 0.5, 0), c)
				if ripe:
					_piece(_rig, Vector3(0.5, 0.9, 0.5), off + Vector3(0, hh + 0.3, 0), a)
		"bulb":
			_piece(_rig, Vector3(h * 0.8, h, h * 0.8), Vector3(0, h * 0.5, 0), c)
			_piece(_rig, Vector3(h * 0.4, h * 0.5, h * 0.4), Vector3(h * 0.5, h * 0.4, 0.2), c)
			if ripe:
				_piece(_rig, Vector3(h * 0.5, h * 0.3, h * 0.5), Vector3(0, h * 1.05, 0), a)
		"rock":
			_piece(_rig, Vector3(1.6, 1.0, 1.3), Vector3(0, 0.5, 0), c)
			_piece(_rig, Vector3(0.8, 0.6, 0.9), Vector3(0.7, 0.3, 0.5), a)
		_:
			_piece(_rig, Vector3(0.3, h * 0.6, 0.3), Vector3(0, h * 0.3, 0), "#4a3a2c")
			_piece(_rig, Vector3(h * 0.9, h * 0.55, h * 0.9), Vector3(0, h * 0.7, 0), c)
			if ripe:
				for i in 4:
					_piece(_rig, Vector3(0.5, 0.5, 0.5), Vector3(cos(i * 1.6) * h * 0.4, h * (0.6 + 0.12 * i), sin(i * 1.6) * h * 0.4), a)


func _build_structure(key: String) -> void:
	if key == "campfire":
		for i in 6:
			var ang := i * TAU / 6.0
			_piece(_rig, Vector3(1.0, 0.7, 1.0), Vector3(cos(ang) * 1.6, 0.35, sin(ang) * 1.6), "#6a625c")
		_piece(_rig, Vector3(2.0, 0.4, 0.5), Vector3(0, 0.4, 0), "#4a3a2c")
		_piece(_rig, Vector3(0.5, 0.4, 2.0), Vector3(0, 0.6, 0), "#4a3a2c")
		_piece(_rig, Vector3(0.9, 1.6, 0.9), Vector3(0, 1.4, 0), "#ff8a30", true)
		_piece(_rig, Vector3(0.5, 1.0, 0.5), Vector3(0.2, 2.3, 0.1), "#ffd060", true)
	else:
		_piece(_rig, Vector3(2.2, 0.8, 2.2), Vector3(0, 0.4, 0), "#8c867c")
		_piece(_rig, Vector3(1.4, 5.2, 1.0), Vector3(0, 3.4, 0), "#8c867c")
		_piece(_rig, Vector3(0.9, 2.4, 1.1), Vector3(0, 3.8, 0), "#3fb8a8", true)


func set_anim(a: int) -> void:
	if a != _anim and _model:
		var clip: String = ["idle", "walk", "run", "idle", "idle"][clampi(a, 0, 4)]
		_model.speed_scale = 1.0
		if not _model.clips().has(clip):
			_model.speed_scale = 1.8 if clip == "run" else 1.0   # no run clip: a fast walk
			clip = "walk" if clip == "run" else "idle"
		if _model.clips().has(clip):
			_model.play(clip)
	_anim = a


func on_event(kind: String, e: Dictionary) -> void:
	match kind:
		"stage":
			_build_plant(int(e.stage))
		"hit":
			_flash = 0.18
			if _bar:
				_bar.visible = true
				_bar.scale.x = 3.0 * clampf(float(e.hp), 0.02, 1.0)
		"strike":
			_pose = -0.5
		"weigh":
			_flash = 0.4
		"die":
			_dead = true
			if _bar:
				_bar.visible = false


func _process(delta: float) -> void:
	_time += delta
	if _spin:
		_rig.rotation.y += delta * 1.5
		_rig.position.y = 0.3 + sin(_time * 2.0) * 0.2
		return
	if _kind != "creature":
		return
	if _dead:
		_rig.rotation.z = lerpf(_rig.rotation.z, PI * 0.5, clampf(delta * 6.0, 0.0, 1.0))
		_rig.position.y = lerpf(_rig.position.y, float(_def.radius_m) * M, clampf(delta * 6.0, 0.0, 1.0))
		return
	# Lean back while winding up an attack so players can read it, snap forward on the strike.
	_pose = lerpf(_pose, 0.45 if _anim == 3 else 0.0, clampf(delta * 9.0, 0.0, 1.0))
	_rig.rotation.x = _pose + (sin(_time * 30.0) * 0.08 if _anim == 4 else 0.0)
	var speed := 0.0
	if _anim == 1:
		speed = 7.0
	elif _anim == 2:
		speed = 15.0
	for i in _legs.size():
		var swing := sin(_time * speed + (PI if i % 3 == 0 else 0.0)) * 0.55 if speed > 0.0 else 0.0
		_legs[i].rotation.x = lerpf(_legs[i].rotation.x, swing, clampf(delta * 12.0, 0.0, 1.0))
	_flash = maxf(_flash - delta, 0.0)
	var s := 1.0 + _flash * 1.2
	_rig.scale = Vector3(s, 2.0 - s, s)
