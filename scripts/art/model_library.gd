class_name ModelLibrary
extends RefCounted
## Loads the voxel models listed in content/models/models.json and hands them out by id.
##
##   var jar := ModelLibrary.instantiate("clay_jar")          # Node3D, feet at its origin
##   var beast: VoxelRig = ModelLibrary.instantiate("redstrider")
##   beast.play("walk")
##   var axe_mesh := ModelLibrary.mesh("flint_axe")           # just the Mesh, for held items
##   var picture := ModelLibrary.icon("flint_axe")            # 64 px inventory icon
##
## Models are authored at 1/16 m per voxel and come out in world units (1 unit = 0.25 m),
## so nothing needs scaling. Meshes are built once and shared; things face -z.

const MANIFEST_PATH := "res://content/models/models.json"
const PALETTE_PATH := "res://content/art/palette.json"
const MODELS_DIR := "res://content/models/"
const ICONS_DIR := "res://content/icons/"
const SHADER := preload("res://shaders/voxel_surface.gdshader")

static var _manifest := {}
static var _glow_colors := {}        # "rrggbb" -> true
static var _files := {}              # id -> VoxFile
static var _meshes := {}             # "id" or "id/part" -> ArrayMesh
static var _materials := {}          # "solid:0.25" -> ShaderMaterial


static func ids(category := "") -> PackedStringArray:
	var out := PackedStringArray()
	for id in _models():
		if category == "" or _models()[id].get("category", "") == category:
			out.append(id)
	return out


static func has(id: String) -> bool:
	return _models().has(id)


static func info(id: String) -> Dictionary:
	return _models().get(id, {})


## World units per model voxel (0.25 for the standard 1/16 m models).
static func voxel_units(id: String) -> float:
	return float(info(id).get("voxel_m", 0.0625)) * WorldConfig.UNITS_PER_METER


## The whole model as one mesh (all parts in their rest pose). Origin = ground centre.
static func mesh(id: String) -> ArrayMesh:
	if _meshes.has(id):
		return _meshes[id]
	var file := _file(id)
	if file == null:
		return null
	var mesher := VoxModelMesher.new()
	var glow := _glow_flags(file)
	for m in file.models:
		mesher.add(m, file.palette, glow, Vector3(m.origin))
	var built := _finish(mesher.build(voxel_units(id)), voxel_units(id))
	_meshes[id] = built
	return built


## One part of a rigged model, with its pivot at the mesh origin.
static func part_mesh(id: String, part: String, pivot: Vector3) -> ArrayMesh:
	var key := "%s/%s" % [id, part]
	if _meshes.has(key):
		return _meshes[key]
	var file := _file(id)
	if file == null or file.find(part).is_empty():
		push_error("ModelLibrary: '%s' has no part '%s'" % [id, part])
		return null
	var m := file.find(part)
	var mesher := VoxModelMesher.new()
	mesher.add(m, file.palette, _glow_flags(file), Vector3(m.origin) - pivot)
	var built := _finish(mesher.build(voxel_units(id)), voxel_units(id))
	_meshes[key] = built
	return built


## A ready-to-place node. Rigged models (creatures, people) come back as a VoxelRig.
## with_collision adds a simple box StaticBody3D, enough for props you bump into.
static func instantiate(id: String, with_collision := false) -> Node3D:
	if not has(id):
		push_error("ModelLibrary: unknown model '%s'" % id)
		return null
	var node: Node3D
	if info(id).has("rig"):
		var rig := VoxelRig.new()
		rig.setup(id)
		node = rig
	else:
		var mi := MeshInstance3D.new()
		mi.mesh = mesh(id)
		node = mi
	node.name = id
	if with_collision and mesh(id) != null:
		var box := mesh(id).get_aabb()
		var body := StaticBody3D.new()
		var shape := CollisionShape3D.new()
		var b := BoxShape3D.new()
		b.size = box.size
		shape.shape = b
		shape.position = box.get_center()
		body.add_child(shape)
		node.add_child(body)
	return node


static func icon(id: String) -> Texture2D:
	var path := ICONS_DIR + id + ".png"
	return load(path) as Texture2D if ResourceLoader.exists(path) else null


## Material used by all models; `glow` picks the emissive variant.
static func material(units: float, glow := false) -> ShaderMaterial:
	var key := "%s:%s" % ["glow" if glow else "solid", units]
	if not _materials.has(key):
		var mat := ShaderMaterial.new()
		mat.shader = SHADER
		mat.set_shader_parameter("cell_size", units)
		mat.set_shader_parameter("value_jitter", 0.0 if glow else 0.06)
		mat.set_shader_parameter("emission_energy", 2.2 if glow else 0.0)
		_materials[key] = mat
	return _materials[key]


## Builds a mesh for any VoxFile (used for structures in the gallery and by tools).
static func mesh_from_file(file: VoxFile, units: float) -> ArrayMesh:
	var mesher := VoxModelMesher.new()
	var glow := _glow_flags(file)
	for m in file.models:
		mesher.add(m, file.palette, glow, Vector3(m.origin))
	return _finish(mesher.build(units), units)


static func _finish(built: ArrayMesh, units: float) -> ArrayMesh:
	for s in built.get_surface_count():
		built.surface_set_material(s, material(units, built.surface_get_name(s) == "glow"))
	return built


static func _models() -> Dictionary:
	if _manifest.is_empty():
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
		if typeof(parsed) == TYPE_DICTIONARY:
			_manifest = parsed.get("models", {})
		else:
			push_error("ModelLibrary: could not read %s" % MANIFEST_PATH)
		var pal = JSON.parse_string(FileAccess.get_file_as_string(PALETTE_PATH))
		if typeof(pal) == TYPE_DICTIONARY:
			for c in pal.get("colors", []):
				if c.get("emissive", false):
					_glow_colors[String(c.hex).trim_prefix("#").to_lower()] = true
	return _manifest


static func _file(id: String) -> VoxFile:
	if _files.has(id):
		return _files[id]
	if not has(id):
		push_error("ModelLibrary: unknown model '%s'" % id)
		return null
	var file := VoxFile.load_file(MODELS_DIR + String(info(id).file))
	if file.error != "":
		return null
	_files[id] = file
	return file


## Glow is decided by colour, not palette position, so files re-saved by other editors still work.
static func _glow_flags(file: VoxFile) -> PackedByteArray:
	_models()
	var flags := PackedByteArray()
	flags.resize(256)
	for i in 256:
		if _glow_colors.has(file.palette[i].to_html(false)):
			flags[i] = 1
	return flags
