extends RefCounted
## The art pipeline: .vox reading, meshing, the model manifest and .vox structures.


func test_every_model_loads_and_meshes() -> String:
	var ids := ModelLibrary.ids()
	if ids.size() < 40:
		return "expected the starter set, found %d models" % ids.size()
	for id in ids:
		var mesh := ModelLibrary.mesh(id)
		if mesh == null or mesh.get_surface_count() == 0:
			return "%s did not produce a mesh" % id
		if not ModelLibrary.info(id).has("size"):
			continue # imported models do not record a size
		var box := mesh.get_aabb()
		var want := Vector3(ModelLibrary.info(id).size[0], ModelLibrary.info(id).size[1], ModelLibrary.info(id).size[2]) * ModelLibrary.voxel_units(id)
		if not box.size.is_equal_approx(want):
			return "%s: mesh is %s, manifest says %s" % [id, box.size, want]
	return ""


func test_greedy_mesher_merges_and_culls() -> String:
	# A solid 2x2x2 cube of one colour is 6 quads = 36 vertices, with nothing inside.
	var voxels := PackedByteArray()
	voxels.resize(8)
	voxels.fill(1)
	var palette := PackedColorArray()
	palette.resize(256)
	var mesher := VoxModelMesher.new()
	mesher.add({"size": Vector3i(2, 2, 2), "voxels": voxels}, palette)
	var mesh := mesher.build(0.5)
	var verts: PackedVector3Array = mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	if verts.size() != 36:
		return "expected 36 vertices, got %d" % verts.size()
	return "" if mesh.get_aabb().size.is_equal_approx(Vector3.ONE) else "scale not applied: %s" % mesh.get_aabb()


func test_rig_parts_and_clips() -> String:
	var rig := ModelLibrary.instantiate("redstrider") as VoxelRig
	if rig == null:
		return "redstrider should be a VoxelRig"
	var err := ""
	if rig.part("head") == null or rig.part("leg_fl_lower") == null:
		err = "missing parts"
	elif rig.part("leg_fl_lower").get_parent() != rig.part("leg_fl_upper"):
		err = "lower leg should hang off the upper leg"
	elif not rig.clips().has("walk"):
		err = "no walk clip"
	else:
		for id in ModelLibrary.ids("creatures"):
			var def: Dictionary = ModelLibrary.info(id).rig
			for clip in def.clips:
				for ch in def.clips[clip]:
					if not def.parts.has(ch.part):
						err = "%s clip %s moves unknown part %s" % [id, clip, ch.part]
	rig.free()
	return err


func test_glow_goes_to_its_own_surface() -> String:
	var mesh := ModelLibrary.mesh("tether_stone")
	for s in mesh.get_surface_count():
		if mesh.surface_get_name(s) == "glow":
			return ""
	return "tether stone has no glowing surface"


func test_structures_map_to_blocks() -> String:
	var blocks: Array = JSON.parse_string(FileAccess.get_file_as_string("res://content/blocks/blocks.json")).blocks
	for id in StructureLoader.ids():
		var data := StructureLoader.load_voxels(id, blocks)
		if data.voxels.is_empty():
			return "%s is empty" % id
		if data.voxels.size() != int(StructureLoader._structures()[id].voxels):
			return "%s: %d voxels mapped, manifest says %d" % [id, data.voxels.size(), StructureLoader._structures()[id].voxels]
	return ""
