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
			if h < origin.y:
				continue # whole column of this block is air (buffer starts at 0 = air)
			for y in size.y:
				var wy := origin.y + y
				var key := HeightField.layer_at(wy, h)
				var id: int = _ids[key]
				if id != air:
					out_buffer.set_voxel(id, x, y, z, CHANNEL)
	_apply_stamps(out_buffer, origin, size)


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
