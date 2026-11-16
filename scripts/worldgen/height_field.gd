class_name HeightField
extends RefCounted
## Deterministic terrain shape from the world seed. Used by the threaded
## generator and by gameplay (spawn height, landmark placement), so it must
## stay pure: no scene access, no randomness outside the seed.

var _hills := FastNoiseLite.new()
var _detail := FastNoiseLite.new()
var _mesas := FastNoiseLite.new()


func _init(world_seed: int) -> void:
	_hills.seed = world_seed
	_hills.frequency = 0.0025
	_hills.fractal_octaves = 4
	_detail.seed = world_seed + 1
	_detail.frequency = 0.03
	_mesas.seed = world_seed + 2
	_mesas.frequency = 0.004
	_mesas.fractal_octaves = 2


## Ground height in voxels at column (x, z).
func height_at(x: int, z: int) -> int:
	var h := 32.0 + _hills.get_noise_2d(x, z) * 72.0 + _detail.get_noise_2d(x, z) * 3.0
	# Terraced red mesas: flat tops and steep sides where the mask is high.
	var m := _mesas.get_noise_2d(x, z)
	if m > 0.25:
		var lift := clampf((m - 0.25) * 6.0, 0.0, 1.0) * 56.0
		h += floorf(lift / 8.0) * 8.0
	return int(floorf(h))


## Block key for a voxel at world height y in a column with ground height h.
static func layer_at(y: int, h: int) -> String:
	if y > h:
		return "air"
	if y == h:
		return "moss" if h < 20 else ("dust_sand" if h > 90 else "red_soil")
	if y > h - 6:
		return "red_soil"
	if y > h - 40:
		return "rust_rock"
	return "deep_stone"
