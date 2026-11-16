class_name VoxelRig
extends Node3D
## A voxel creature or person made of separate parts that swing around pivots.
## Built by ModelLibrary.instantiate() from the "rig" block in content/models/models.json.
##
##   rig.play("walk")            # clip names: rig.clips()
##   rig.part("arm_r")           # the pivot Node3D of a part, e.g. to attach a held item
##   rig.set_manual("arm_r")     # stop animating a part so game code can rotate it (swings)
##
## Clips are tiny and procedural: each channel swings one part around one axis with a sine
## wave, or bobs it up and down. Good enough for walk cycles until real animation exists.

var model_id := ""
var speed_scale := 1.0
var current_clip := ""

var _parts := {}        # name -> Node3D pivot
var _rest := {}         # name -> rest position
var _manual := {}       # name -> true
var _clips := {}
var _units := 0.25
var _time := 0.0


func setup(id: String) -> void:
	model_id = id
	_units = ModelLibrary.voxel_units(id)
	var rig: Dictionary = ModelLibrary.info(id).get("rig", {})
	_clips = rig.get("clips", {})
	var defs: Dictionary = rig.get("parts", {})
	var pending := defs.keys()
	var guard := 0
	while not pending.is_empty() and guard < 64: # parents first
		guard += 1
		for part_name in pending.duplicate():
			var def: Dictionary = defs[part_name]
			var parent_name: String = def.get("parent", "")
			if parent_name != "" and not _parts.has(parent_name):
				continue
			var pivot := _vec(def.pivot)
			var node := Node3D.new()
			node.name = part_name
			var parent_pivot := _vec(defs[parent_name].pivot) if parent_name != "" else Vector3.ZERO
			node.position = (pivot - parent_pivot) * _units
			var mi := MeshInstance3D.new()
			mi.name = "Mesh"
			mi.mesh = ModelLibrary.part_mesh(id, part_name, pivot)
			node.add_child(mi)
			(_parts[parent_name] if parent_name != "" else self).add_child(node)
			_parts[part_name] = node
			_rest[part_name] = node.position
			pending.erase(part_name)
	if _clips.has("idle"):
		play("idle")


func clips() -> PackedStringArray:
	return PackedStringArray(_clips.keys())


func play(clip: String) -> void:
	if _clips.has(clip):
		current_clip = clip
	else:
		push_warning("VoxelRig: '%s' has no clip '%s'" % [model_id, clip])


func part(part_name: String) -> Node3D:
	return _parts.get(part_name)


func set_manual(part_name: String, manual := true) -> void:
	if manual:
		_manual[part_name] = true
	else:
		_manual.erase(part_name)


func _process(delta: float) -> void:
	_time += delta * speed_scale
	var rot := {}
	var lift := {}
	for ch in _clips.get(current_clip, []):
		var wave := sin(TAU * (float(ch.freq) * _time + float(ch.get("phase", 0.0))))
		if ch.has("bob"):
			lift[ch.part] = lift.get(ch.part, 0.0) + float(ch.bob) * wave * _units
		else:
			var e: Vector3 = rot.get(ch.part, Vector3.ZERO)
			e["xyz".find(ch.axis)] += deg_to_rad(float(ch.get("offset", 0.0)) + float(ch.amp) * wave)
			rot[ch.part] = e
	var blend := clampf(delta * 8.0, 0.0, 1.0) # ease between clips instead of snapping
	for part_name in _parts:
		if _manual.has(part_name):
			continue
		var node: Node3D = _parts[part_name]
		var target: Vector3 = rot.get(part_name, Vector3.ZERO)
		node.rotation = node.rotation.lerp(target, blend)
		node.position = _rest[part_name] + Vector3(0.0, lift.get(part_name, 0.0), 0.0)


static func _vec(a: Array) -> Vector3:
	return Vector3(float(a[0]), float(a[1]), float(a[2]))
