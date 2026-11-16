extends VoxelGeneratorScript
## Seeded terrain + handbuilt landmark stamping (Voxel Tools blocky terrain).
## Runs on worker threads: only read the immutable data set up in setup().

const CHANNEL := VoxelBuffer.CHANNEL_TYPE

var _field: HeightField
var _ids := {}                       # layer key -> block id
var _stamps: Array = []              # [{ "aabb": AABB, "voxels": Dictionary(Vector3i -> id) }]


func setup(world_seed: int, layer_ids: Dictionary) -> void:
	_field = HeightField.new(world_seed)
	_ids = layer_ids


## Adds a handbuilt structure at a world-space voxel origin.
## Structure voxels are offsets; y=0 sits on the ground.
func add_stamp(origin: Vector3i, voxels: Array) -> void:
	var lookup := {}
	var lo := Vector3i(1 << 30, 1 << 30, 1 << 30)
	var hi := -lo
	for v in voxels:
		var p := origin + Vector3i(v[0], v[1], v[2])
		lookup[p] = v[3]
		lo = Vector3i(mini(lo.x, p.x), mini(lo.y, p.y), mini(lo.z, p.z))
		hi = Vector3i(maxi(hi.x, p.x), maxi(hi.y, p.y), maxi(hi.z, p.z))
	_stamps.append({"aabb": AABB(Vector3(lo), Vector3(hi - lo + Vector3i.ONE)), "voxels": lookup})


func _get_used_channels_mask() -> int:
	return 1 << CHANNEL


func _generate_block(out_buffer: VoxelBuffer, origin: Vector3i, lod: int) -> void:
	if lod != 0:
		return
	var size := out_buffer.get_size()
	var air: int = _ids["air"]
	for z in size.z:
		for x in size.x:
			var wx := origin.x + x
			var wz := origin.z + z
			var h := _field.height_at(wx, wz)
			if maxi(h + 1, HeightField.SEA_LEVEL) < origin.y:
				continue # whole column of this block is air (buffer starts at 0 = air)
			var surface := _field.surface_key(wx, wz, h)
			for y in size.y:
				var wy := origin.y + y
				var id: int = _ids[HeightField.layer_at(wy, h, surface)]
				if wy == h + 1 and id == air and surface == "grass":
					var plant := _field.flora_at(wx, wz)
					if plant != "":
						id = _ids[plant]
				if id != air:
					out_buffer.set_voxel(id, x, y, z, CHANNEL)
	_apply_trees(out_buffer, origin, size)
	_apply_stamps(out_buffer, origin, size)


## Trees are placed per 5 m cell, so any chunk can rebuild the parts of every
## tree that reach into it without knowing about its neighbours.
func _apply_trees(out_buffer: VoxelBuffer, origin: Vector3i, size: Vector3i) -> void:
	var reach := HeightField.TREE_REACH
	var cell := HeightField.TREE_CELL
	var air: int = _ids["air"]
	var tall: int = _ids["tall_grass"]
	var bark: int = _ids["bark"]
	for cz in range(floori(float(origin.z - reach) / cell), floori(float(origin.z + size.z + reach) / cell) + 1):
		for cx in range(floori(float(origin.x - reach) / cell), floori(float(origin.x + size.x + reach) / cell) + 1):
			var t := _field.tree_in_cell(cx, cz)
			if t.is_empty():
				continue
			var top: int = t.base + t.trunk
			var r: int = t.radius
			if t.base > origin.y + size.y or top + r < origin.y:
				continue
			var leaf: int = _ids["crimson_leaves"] if t.crimson else _ids["leaves"]
			var thick := 2 if t.trunk < 28 else 3
			var centre := Vector3(t.x + thick * 0.5, top, t.z + thick * 0.5)
			var lo := Vector3i(t.x - r, t.base, t.z - r) - origin
			var hi := Vector3i(t.x + r + thick, top + r, t.z + r + thick) - origin
			for y in range(maxi(lo.y, 0), mini(hi.y, size.y)):
				for z in range(maxi(lo.z, 0), mini(hi.z, size.z)):
					for x in range(maxi(lo.x, 0), mini(hi.x, size.x)):
						var w := origin + Vector3i(x, y, z)
						var cur := out_buffer.get_voxel(x, y, z, CHANNEL)
						if cur != air and cur != tall:
							continue
						var in_trunk: bool = w.x >= t.x and w.x < t.x + thick and w.z >= t.z and w.z < t.z + thick and w.y < top
						if in_trunk:
							out_buffer.set_voxel(bark, x, y, z, CHANNEL)
							continue
						# Canopy: a squashed sphere with a ragged edge.
						var d := Vector3(w) + Vector3(0.5, 0.5, 0.5) - centre
						d.y *= 1.4
						var edge := r * (0.8 + 0.25 * _field.hash01(w.x * 7 + w.y, w.z, 31))
						if w.y >= top - r / 2 and d.length() < edge:
							out_buffer.set_voxel(leaf, x, y, z, CHANNEL)


func _apply_stamps(out_buffer: VoxelBuffer, origin: Vector3i, size: Vector3i) -> void:
	var block_box := AABB(Vector3(origin), Vector3(size))
	for s in _stamps:
		var box: AABB = s.aabb
		if not box.intersects(block_box):
			continue
		var voxels: Dictionary = s.voxels
		for p in voxels:
			var local: Vector3i = p - origin
			if local.x >= 0 and local.y >= 0 and local.z >= 0 \
					and local.x < size.x and local.y < size.y and local.z < size.z:
				out_buffer.set_voxel(voxels[p], local.x, local.y, local.z, CHANNEL)
