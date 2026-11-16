extends RefCounted
## Each test returns "" on success or a failure message.

func _grid(size: Vector3i) -> PackedInt32Array:
	var g := PackedInt32Array()
	g.resize(size.x * size.y * size.z)
	return g


func _i(p: Vector3i, size: Vector3i) -> int:
	return p.x + p.z * size.x + p.y * size.x * size.z


func test_supported_column_is_not_loose() -> String:
	var size := Vector3i(5, 5, 5)
	var g := _grid(size)
	for y in 5:
		g[_i(Vector3i(2, y, 2), size)] = 1 # touches floor and ceiling of box
	return "" if FloatingPieces.find(g, size, 1000).is_empty() else "column reported loose"


func test_hanging_block_is_loose() -> String:
	var size := Vector3i(7, 7, 7)
	var g := _grid(size)
	g[_i(Vector3i(3, 3, 3), size)] = 2
	g[_i(Vector3i(3, 4, 3), size)] = 2
	var pieces := FloatingPieces.find(g, size, 1000)
	if pieces.size() != 1:
		return "expected 1 piece, got %d" % pieces.size()
	return "" if pieces[0].size() == 2 else "expected 2 voxels"


func test_two_separate_pieces() -> String:
	var size := Vector3i(9, 9, 9)
	var g := _grid(size)
	g[_i(Vector3i(2, 4, 2), size)] = 1
	g[_i(Vector3i(6, 4, 6), size)] = 1
	var n := FloatingPieces.find(g, size, 1000).size()
	return "" if n == 2 else "expected 2 pieces, got %d" % n


func test_oversized_piece_is_kept() -> String:
	var size := Vector3i(9, 9, 9)
	var g := _grid(size)
	for x in range(2, 7):
		for z in range(2, 7):
			g[_i(Vector3i(x, 4, z), size)] = 1 # 25-voxel slab, limit 10
	return "" if FloatingPieces.find(g, size, 10).is_empty() else "big piece should stay put"


func test_index_round_trip() -> String:
	var size := Vector3i(4, 6, 5)
	var p := Vector3i(3, 5, 2)
	return "" if FloatingPieces.index_to_pos(_i(p, size), size) == p else "index_to_pos mismatch"
