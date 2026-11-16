class_name GameData
extends RefCounted
## Loads gameplay content (items, recipes, rulesets, plants, creatures) once
## and hands out plain dictionaries. Content is data, not code: tune the game
## by editing the JSON under res://content, then run the content validator.

const ITEMS_PATH := "res://content/items/items.json"
const RULESETS_PATH := "res://content/survival/rulesets.json"
const PLANTS_PATH := "res://content/farming/plants.json"
const CREATURES_PATH := "res://content/creatures/creatures.json"

static var items := {}          # key -> definition (block items included)
static var unarmed := {}
static var recipes := {}        # id -> recipe
static var recipe_order: Array = []
static var structures := {}
static var block_drops := {}    # block key -> Array of drops
static var buffs := {}
static var plants := {}
static var plant_cell := 64
static var wild_regrow_s := 900.0
static var creatures := {}
static var default_ruleset := "light"
static var _rulesets_raw := {}
static var _loaded := false


static func ensure() -> void:
	if _loaded:
		return
	_loaded = true
	var data := _read(ITEMS_PATH)
	unarmed = data.get("unarmed", {})
	structures = data.get("structures", {})
	block_drops = data.get("block_drops", {})
	block_drops.erase("_comment")
	var block_stack := int(data.get("block_stack", 999))
	for b in _blocks():
		if b.get("solid", false) and int(b.get("hardness", 0)) < 99 and not b.get("fluid", false):
			items[b.key] = {"key": b.key, "name": b.name, "kind": "block", "block": b.key,
				"stack": block_stack, "color": b.get("color", "#888888")}
	for it in data.get("items", []):
		items[it.key] = it
	for r in data.get("recipes", []):
		recipes[r.id] = r
		recipe_order.append(r.id)
	var rs := _read(RULESETS_PATH)
	_rulesets_raw = rs.get("rulesets", {})
	default_ruleset = rs.get("default", "light")
	buffs = rs.get("buffs", {})
	var pl := _read(PLANTS_PATH)
	plant_cell = int(pl.get("cell_voxels", 64))
	wild_regrow_s = float(pl.get("wild_regrow_s", 900))
	for p in pl.get("plants", []):
		plants[p.key] = p
	for c in _read(CREATURES_PATH).get("creatures", []):
		creatures[c.key] = c


static func item(key: String) -> Dictionary:
	ensure()
	return items.get(key, {})


## Weapon-like stats for whatever is held: the item itself, or bare hands.
static func held_stats(key: String) -> Dictionary:
	ensure()
	var it: Dictionary = items.get(key, {})
	return it if it.has("damage") else unarmed


static func ruleset_ids() -> Array:
	ensure()
	return _rulesets_raw.keys()


## A ruleset with its "extends" chain merged in. Hardcore is light plus overrides.
static func ruleset(id: String) -> Dictionary:
	ensure()
	if not _rulesets_raw.has(id):
		id = default_ruleset
	return resolve_ruleset(_rulesets_raw, id)


static func resolve_ruleset(all: Dictionary, id: String) -> Dictionary:
	var raw: Dictionary = all[id]
	if not raw.has("extends"):
		var out: Dictionary = raw.duplicate(true)
		out["id"] = id
		return out
	var merged := merge(resolve_ruleset(all, raw["extends"]), raw)
	merged.erase("extends")
	merged["id"] = id
	return merged


## Deep merge: dictionaries merge key by key, everything else is replaced.
## A key "<name>_add" appends its array to the base array "<name>".
static func merge(base: Dictionary, over: Dictionary) -> Dictionary:
	var out: Dictionary = base.duplicate(true)
	for k in over:
		var v = over[k]
		if typeof(v) == TYPE_DICTIONARY and typeof(out.get(k)) == TYPE_DICTIONARY:
			out[k] = merge(out[k], v)
		elif typeof(v) == TYPE_ARRAY and String(k).ends_with("_add"):
			var name := String(k).trim_suffix("_add")
			var list: Array = out.get(name, []).duplicate(true)
			list.append_array(v.duplicate(true))
			out[name] = list
		else:
			out[k] = v.duplicate(true) if typeof(v) in [TYPE_DICTIONARY, TYPE_ARRAY] else v
	return out


## Rolls what digging one voxel of `block_key` gives: Array of [item_key, count].
static func roll_block_drops(block_key: String, hardness: int, rng: RandomNumberGenerator) -> Array:
	ensure()
	if not block_drops.has(block_key):
		return [[block_key, 1]] if hardness >= 1 and items.has(block_key) else []
	var out: Array = []
	for d in block_drops[block_key]:
		if rng.randf() <= float(d.get("chance", 1.0)):
			out.append([d.item, int(d.get("n", 1))])
	return out


static func _blocks() -> Array:
	var data := _read("res://content/blocks/blocks.json")
	return data.get("blocks", [])


static func _read(path: String) -> Dictionary:
	var parsed = JSON.parse_string(FileAccess.get_file_as_string(path))
	if typeof(parsed) != TYPE_DICTIONARY:
		push_error("GameData: could not parse %s" % path)
		return {}
	return parsed
