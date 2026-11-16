class_name VoxFile
extends RefCounted
## Reads MagicaVoxel .vox files without any add-on.
##
## Every object in the file becomes one entry in `models`, already converted to Godot axes
## (x right, y up, z towards the viewer; MagicaVoxel is z-up):
##   { name: String, size: Vector3i, origin: Vector3i, voxels: PackedByteArray }
## `voxels` is dense: index = x + size.x * (y + size.y * z), value = palette index (0 = empty).
## `origin` is where the model's lowest corner sits in the scene, in voxels, so parts of a
## creature saved as separate objects come out assembled.

var palette := PackedColorArray()
var models: Array = []
var error := ""


static func load_file(path: String) -> VoxFile:
	var file := VoxFile.new()
	var bytes := FileAccess.get_file_as_bytes(path)
	if bytes.is_empty():
		file.error = "cannot read %s" % path
	else:
		file._parse(bytes)
	if file.error != "":
		push_error("VoxFile: %s (%s)" % [file.error, path])
	return file


func find(model_name: String) -> Dictionary:
	for m in models:
		if m.name == model_name:
			return m
	return {}


static func get_voxel(model: Dictionary, x: int, y: int, z: int) -> int:
	var s: Vector3i = model.size
	if x < 0 or y < 0 or z < 0 or x >= s.x or y >= s.y or z >= s.z:
		return 0
	return model.voxels[x + s.x * (y + s.y * z)]


func _parse(bytes: PackedByteArray) -> void:
	var r := StreamPeerBuffer.new()
	r.data_array = bytes
	if bytes.size() < 20 or r.get_data(4)[1].get_string_from_ascii() != "VOX ":
		error = "not a .vox file"
		return
	r.get_32() # version
	var sizes: Array[Vector3i] = []
	var raw: Array[PackedByteArray] = []
	var transforms := {}   # node id -> { name, child, t }
	var groups := {}       # node id -> [child ids]
	var shapes := {}       # node id -> [model ids]
	palette.resize(256)
	palette.fill(Color(0.5, 0.5, 0.5))
	var has_palette := false

	r.seek(8)
	r.get_data(4) # "MAIN"
	r.get_32()
	r.get_32()
	while r.get_position() + 12 <= bytes.size():
		var id: String = r.get_data(4)[1].get_string_from_ascii()
		var content := r.get_32()
		var children := r.get_32()
		var next := r.get_position() + content + children
		match id:
			"SIZE":
				sizes.append(Vector3i(r.get_32(), r.get_32(), r.get_32()))
			"XYZI":
				var count := r.get_32()
				raw.append(r.get_data(count * 4)[1])
			"RGBA":
				for i in 255:
					var c: PackedByteArray = r.get_data(4)[1]
					palette[i + 1] = Color8(c[0], c[1], c[2])
				has_palette = true
			"nTRN":
				var node := r.get_32()
				var attrs := _read_dict(r)
				var child := r.get_32()
				r.get_32() # reserved
				r.get_32() # layer
				var frames := r.get_32()
				var t := Vector3i.ZERO
				for f in frames:
					var frame := _read_dict(r)
					if f == 0 and frame.has("_t"):
						var p: PackedStringArray = frame["_t"].split(" ", false)
						if p.size() == 3:
							t = Vector3i(int(p[0]), int(p[1]), int(p[2]))
					if f == 0 and frame.get("_r", "4") != "4":
						push_warning("VoxFile: rotated objects are not supported; '%s' is read unrotated" % attrs.get("_name", "?"))
				transforms[node] = {"name": attrs.get("_name", ""), "child": child, "t": t}
			"nGRP":
				var node := r.get_32()
				_read_dict(r)
				var kids: Array[int] = []
				for i in r.get_32():
					kids.append(r.get_32())
				groups[node] = kids
			"nSHP":
				var node := r.get_32()
				_read_dict(r)
				var ids: Array[int] = []
				for i in r.get_32():
					ids.append(r.get_32())
					_read_dict(r)
				shapes[node] = ids
		r.seek(next)

	if sizes.size() != raw.size() or sizes.is_empty():
		error = "no models found"
		return
	if not has_palette:
		push_warning("VoxFile: file has no palette chunk; colours will be grey")

	if transforms.has(0):
		_walk(0, Vector3i.ZERO, "", transforms, groups, shapes, sizes, raw)
	if models.is_empty(): # old files without a scene graph: centre each model on the ground
		for i in sizes.size():
			var s := sizes[i]
			models.append(_convert("model_%d" % i, s, raw[i], Vector3i(0, 0, s.z / 2)))


func _walk(node: int, offset: Vector3i, node_name: String, transforms: Dictionary, groups: Dictionary,
		shapes: Dictionary, sizes: Array[Vector3i], raw: Array[PackedByteArray]) -> void:
	if transforms.has(node):
		var t: Dictionary = transforms[node]
		_walk(t.child, offset + t.t, t.name if t.name != "" else node_name, transforms, groups, shapes, sizes, raw)
	elif groups.has(node):
		for kid in groups[node]:
			_walk(kid, offset, node_name, transforms, groups, shapes, sizes, raw)
	elif shapes.has(node):
		for model_id in shapes[node]:
			if model_id >= 0 and model_id < sizes.size():
				var n := node_name if node_name != "" else "model_%d" % model_id
				models.append(_convert(n, sizes[model_id], raw[model_id], offset))


## vs = size in MagicaVoxel axes; t = where MagicaVoxel puts the model's centre.
func _convert(model_name: String, vs: Vector3i, cells: PackedByteArray, t: Vector3i) -> Dictionary:
	var size := Vector3i(vs.x, vs.z, vs.y)
	var voxels := PackedByteArray()
	voxels.resize(size.x * size.y * size.z)
	for i in range(0, cells.size(), 4):
		var x := cells[i]
		var y := cells[i + 2]
		var z := (vs.y - 1) - cells[i + 1]
		if x < size.x and y < size.y and z >= 0:
			voxels[x + size.x * (y + size.y * z)] = cells[i + 3]
	@warning_ignore("integer_division")
	var origin := Vector3i(t.x - vs.x / 2, t.z - vs.z / 2, -t.y - (vs.y - 1) + vs.y / 2)
	return {"name": model_name, "size": size, "origin": origin, "voxels": voxels}


func _read_dict(r: StreamPeerBuffer) -> Dictionary:
	var out := {}
	for i in r.get_32():
		var key: String = r.get_data(r.get_32())[1].get_string_from_utf8()
		out[key] = r.get_data(r.get_32())[1].get_string_from_utf8()
	return out
