class_name BlockLibraryBuilder
extends RefCounted
## Builds the Voxel Tools blocky library from content/blocks/blocks.json.
## Blocks are flat palette colours (vertex colour), so no textures are needed:
## the look comes from small voxels, the palette and lighting.

static func build(blocks: Array) -> VoxelBlockyLibrary:
	var library := VoxelBlockyLibrary.new()
	var base := StandardMaterial3D.new()
	base.vertex_color_use_as_albedo = true
	base.roughness = 0.95

	for b in blocks:
		var model: VoxelBlockyModel
		if not b.get("solid", false):
			model = VoxelBlockyModelEmpty.new()
		else:
			var cube := VoxelBlockyModelCube.new()
			cube.color = Color(b.get("color", "#ff00ff"))
			if b.get("emissive", false):
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
