class_name HeightField
extends RefCounted
## Deterministic terrain shape and biome from the world seed. Used by the
## threaded generator and by gameplay (spawn height, landmark placement), so it
## must stay pure: no scene access, no randomness outside the seed.
##
## Aruun is a living world: meadows and forests over red soil, lakes in the
## lowlands, and red mesas where the rock breaks through.

const SEA_LEVEL := 14
const TREE_CELL := 20          # one possible tree per 5 m x 5 m cell
const TREE_REACH := 12         # max canopy radius, for chunk overlap tests

var world_seed := 0
var _hills := FastNoiseLite.new()
var _detail := FastNoiseLite.new()
var _mesas := FastNoiseLite.new()
var _lush := FastNoiseLite.new()


func _init(seed_value: int) -> void:
	world_seed = seed_value
	_hills.seed = seed_value
	_hills.frequency = 0.0025
	_hills.fractal_octaves = 4
	_detail.seed = seed_value + 1
	_detail.frequency = 0.03
	_mesas.seed = seed_value + 2
	_mesas.frequency = 0.004
	_mesas.fractal_octaves = 2
	_lush.seed = seed_value + 3
	_lush.frequency = 0.006


## Ground height in voxels at column (x, z).
func height_at(x: int, z: int) -> int:
	var h := 28.0 + _hills.get_noise_2d(x, z) * 64.0 + _detail.get_noise_2d(x, z) * 3.0
	# Terraced red mesas: flat tops and steep sides where the mask is high.
	var m := _mesas.get_noise_2d(x, z)
	if m > 0.25:
		var lift := clampf((m - 0.25) * 6.0, 0.0, 1.0) * 56.0
		h += floorf(lift / 8.0) * 8.0
	return int(floorf(h))


func is_mesa(x: int, z: int) -> bool:
	return _mesas.get_noise_2d(x, z) > 0.32


## 0..1: how overgrown the land is here (forest density, flowers).
func lushness(x: int, z: int) -> float:
	return clampf(_lush.get_noise_2d(x, z) * 0.8 + 0.55, 0.0, 1.0)


## Block key for the top voxel of a column.
func surface_key(x: int, z: int, h: int) -> String:
	if h <= SEA_LEVEL + 1:
		return "dust_sand" if h >= SEA_LEVEL - 2 else "red_soil" # beaches, red lake beds
	if is_mesa(x, z):
		return "red_soil"
	return "grass"


## Block key for a voxel at height y in a column with ground height h.
static func layer_at(y: int, h: int, surface: String) -> String:
	if y > h:
		return "water" if y <= SEA_LEVEL else "air"
	if y == h:
		return surface
	if y > h - 6:
		return "red_soil"
	if y > h - 40:
		return "rust_rock"
	return "deep_stone"


## Small plant on top of a grass column, or "" for none.
func flora_at(x: int, z: int) -> String:
	var r := hash01(x, z, 11)
	var lush := lushness(x, z)
	if r < 0.004 * lush:
		return "grinleaf"
	if r < 0.02 * lush:
		return "gold_flower"
	if r < 0.035 * lush:
		return "white_flower"
	if r < 0.35 * lush:
		return "tall_grass"
	return ""


## Tree in a cell, or {} if none. Keys: x, z, base, trunk, radius, crimson.
func tree_in_cell(cx: int, cz: int) -> Dictionary:
	var ox := cx * TREE_CELL
	var oz := cz * TREE_CELL
	if hash01(cx, cz, 21) > lushness(ox, oz) * 0.85:
		return {}
	var x := ox + 3 + int(hash01(cx, cz, 22) * (TREE_CELL - 6))
	var z := oz + 3 + int(hash01(cx, cz, 23) * (TREE_CELL - 6))
	var h := height_at(x, z)
	if h <= SEA_LEVEL + 1 or is_mesa(x, z):
		return {}
	return {
		"x": x, "z": z, "base": h + 1,
		"trunk": 18 + int(hash01(cx, cz, 24) * 18),
		"radius": 6 + int(hash01(cx, cz, 25) * 6),
		"crimson": hash01(cx, cz, 26) < 0.3,
	}


## Deterministic 0..1 value for integer coordinates.
func hash01(a: int, b: int, salt: int) -> float:
	var h := (a * 73856093) ^ (b * 19349663) ^ (salt * 83492791) ^ world_seed
	h = (h ^ (h >> 13)) * 1274126177
	h = h ^ (h >> 16)
	return float(h & 0xFFFFFF) / float(0x1000000)
