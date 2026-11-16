class_name FloatingPieces
extends RefCounted
## Finds solid voxels that lost their connection to the rest of the world
## after a carve. Pure logic on a flat grid, so it is unit-tested.
##
## A voxel counts as anchored if it connects (6-neighbour) to any solid voxel
## on the boundary of the searched box: we cannot see past the box, so we
## assume the outside holds it up. Everything else is a loose piece.

## grid: one int per voxel, x fastest then z then y (index = x + z*sx + y*sx*sz),
##       non-zero = solid.
## Returns an Array of PackedInt32Array, one per loose piece (grid indices).
static func find(grid: PackedInt32Array, size: Vector3i, max_piece: int) -> Array:
	var sx := size.x
	var sz := size.z
	var layer := sx * sz
	var count := grid.size()
	var mark := PackedByteArray()
	mark.resize(count) # 0 unvisited, 1 anchored, 2 loose

	# Flood from boundary solids: they are anchored.
	var queue := PackedInt32Array()
	for i in count:
		if grid[i] == 0:
			continue
		var x := i % sx
		var z := (i / sx) % sz
		var y := i / layer
		if x == 0 or z == 0 or y == 0 or x == sx - 1 or z == sz - 1 or y == size.y - 1:
			mark[i] = 1
			queue.append(i)
	_flood(grid, size, mark, queue, 1)

	var pieces: Array = []
	for i in count:
		if grid[i] == 0 or mark[i] != 0:
			continue
		# Flood the whole piece even when it is too big, so its other voxels are
		# marked and never picked up as separate small pieces.
		var piece := PackedInt32Array([i])
		mark[i] = 2
		_flood(grid, size, mark, piece, 2)
		if piece.size() <= max_piece:
			pieces.append(piece)
	return pieces


## Breadth-first flood. `queue` doubles as the output list.
static func _flood(grid: PackedInt32Array, size: Vector3i, mark: PackedByteArray,
		queue: PackedInt32Array, value: int) -> void:
	var sx := size.x
	var sz := size.z
	var layer := sx * sz
	var head := 0
	while head < queue.size():
		var i := queue[head]
		head += 1
		var x := i % sx
		var z := (i / sx) % sz
		var y := i / layer
		var neighbours := [
			i - 1 if x > 0 else -1,
			i + 1 if x < sx - 1 else -1,
			i - sx if z > 0 else -1,
			i + sx if z < sz - 1 else -1,
			i - layer if y > 0 else -1,
			i + layer if y < size.y - 1 else -1,
		]
		for n in neighbours:
			if n >= 0 and grid[n] != 0 and mark[n] == 0:
				mark[n] = value
				queue.append(n)


static func index_to_pos(i: int, size: Vector3i) -> Vector3i:
	return Vector3i(i % size.x, i / (size.x * size.z), (i / size.x) % size.z)
