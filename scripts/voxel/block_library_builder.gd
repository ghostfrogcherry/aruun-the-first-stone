class_name BlockLibraryBuilder
extends RefCounted
## Builds the Voxel Tools blocky library from content/blocks/blocks.json.
## Blocks are flat palette colours (vertex colour), so no textures are needed:
## the look comes from small voxels, the palette and lighting.

static func build(blocks: Array) -> VoxelBlockyLibrary:
	var library := VoxelBlockyLibrary.new()
	var base := StandardMaterial3D.new()
	base.vertex_color_use_as_albedo = true
	base.vertex_color_is_srgb = true # block colours are sRGB hex; without this they wash out
	base.roughness = 0.95

	for b in blocks:
		var model: VoxelBlockyModel
		if not b.get("solid", false):
			model = VoxelBlockyModelEmpty.new()
		elif b.get("shape", "") == "cross":
			model = _cross_model(b)
		else:
			var cube := VoxelBlockyModelCube.new()
			cube.color = Color(b.get("color", "#ff00ff"))
			if not b.get("collision", true):
				_no_collision(cube) # walk-through: water
			if b.get("transparent", false):
				var clear := base.duplicate() as StandardMaterial3D
				clear.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
				clear.albedo_color = Color(1, 1, 1, float(b.get("alpha", 0.6)))
				clear.roughness = 0.1
				cube.set_material_override(0, clear)
				cube.transparency_index = 1 # faces between two water voxels are culled
				cube.culls_neighbors = false # ground under water still renders
			elif b.get("emissive", false):
				var glow := base.duplicate() as StandardMaterial3D
				glow.emission_enabled = true
				glow.emission = Color(b.color)
				glow.emission_energy_multiplier = 1.6
				cube.set_material_override(0, glow)
			else:
				cube.set_material_override(0, base)
			model = cube
		var index := library.add_model(model)
		assert(index == int(b.id), "Block ids must be contiguous from 0")
	library.bake()
	return library


## Two crossed, double-sided quads: grass tufts and flowers that you walk through.
static func _cross_model(b: Dictionary) -> VoxelBlockyModelMesh:
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var uvs := PackedVector2Array()
	var quads := [
		[Vector3(0.1, 0, 0.1), Vector3(0.9, 0, 0.9), Vector3(0.9, 1, 0.9), Vector3(0.1, 1, 0.1)],
		[Vector3(0.9, 0, 0.1), Vector3(0.1, 0, 0.9), Vector3(0.1, 1, 0.9), Vector3(0.9, 1, 0.1)],
	]
	var indices := PackedInt32Array()
	for q in quads:
		var n: Vector3 = (q[1] - q[0]).cross(q[3] - q[0]).normalized()
		var first := verts.size()
		for corner in q:
			verts.append(corner)
			normals.append(n)
			uvs.append(Vector2(corner.x, 1.0 - corner.y))
		for k in [0, 2, 1, 0, 3, 2]:
			indices.append(first + k)
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices # Voxel Tools needs indexed meshes
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	var mat := StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.vertex_color_is_srgb = true
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mat.roughness = 1.0
	var model := VoxelBlockyModelMesh.new()
	model.mesh = mesh
	model.color = Color(b.get("color", "#ff00ff"))
	_no_collision(model)
	model.culls_neighbors = false
	model.set_material_override(0, mat)
	return model


## Walk-through block: no collision boxes, and raycasts (targeting) skip it.
static func _no_collision(model: VoxelBlockyModel) -> void:
	model.collision_aabbs = []
	model.collision_mask = 0
