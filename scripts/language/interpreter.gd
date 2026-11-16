class_name LanguageInterpreter
extends RefCounted
## Turns a spoken word into validated effect instructions. It never runs
## arbitrary code: every word maps to an effect in a fixed registry, and every
## parameter is bounds-checked against that effect's declared limits.
## Output instructions are applied by the server like any other world edit.
##
## Layers (from the tech design): lexicon (content/language), grammar
## (single words for now; combinations come later), interpreter (this file).

const REGISTRY := ["raise_column", "force_push", "force_pull", "force_weigh"]
const FORCE_EFFECTS := ["force_push", "force_pull", "force_weigh"]


## Checks a lexicon for malformed definitions. Returns a list of problems.
static func validate_lexicon(words: Dictionary, effects: Dictionary) -> PackedStringArray:
	var problems := PackedStringArray()
	for id in effects:
		if not REGISTRY.has(id):
			problems.append("effect '%s' has no handler" % id)
	for id in words:
		var w: Dictionary = words[id]
		var effect_id: String = w.get("effect", "")
		if not effects.has(effect_id):
			problems.append("word '%s' uses unknown effect '%s'" % [id, effect_id])
			continue
		var e: Dictionary = effects[effect_id]
		var p: Dictionary = w.get("params", {})
		if effect_id == "raise_column":
			if int(p.get("height", 0)) < 1 or int(p.get("height", 0)) > int(e.max_height):
				problems.append("word '%s': height out of bounds" % id)
			if int(p.get("radius", -1)) < 0 or int(p.get("radius", -1)) > int(e.max_radius):
				problems.append("word '%s': radius out of bounds" % id)
		elif FORCE_EFFECTS.has(effect_id):
			if float(p.get("range_m", 0)) <= 0.0 or float(p.get("range_m", 0)) > float(e.max_range_m):
				problems.append("word '%s': range out of bounds" % id)
			if float(p.get("impulse_mps", 0)) <= 0.0 or float(p.get("impulse_mps", 0)) > float(e.max_impulse_mps):
				problems.append("word '%s': impulse out of bounds" % id)
			if e.has("max_duration_s") and float(p.get("duration_s", 0)) > float(e.max_duration_s):
				problems.append("word '%s': duration out of bounds" % id)
	return problems


static func is_force_word(word_id: String, words: Dictionary) -> bool:
	return FORCE_EFFECTS.has(words.get(word_id, {}).get("effect", ""))


## Graviturge words that move creatures and loose things instead of terrain.
## Returns { "error": String } or { "force": {"type", "origin", "dir", "range",
## "cone_deg", "impulse", "duration_s"} } in world units. The server applies it.
static func interpret_force(word_id: String, known: PackedStringArray, words: Dictionary,
		origin: Vector3, aim: Vector3) -> Dictionary:
	if not words.has(word_id):
		return {"error": "unknown_word"}
	if not known.has(word_id):
		return {"error": "word_not_learned"}
	var w: Dictionary = words[word_id]
	if not FORCE_EFFECTS.has(w.effect):
		return {"error": "no_handler"}
	if aim.length() < 0.01:
		return {"error": "no_direction"}
	var p: Dictionary = w.params
	var units := WorldConfig.UNITS_PER_METER
	return {"force": {
		"type": String(w.effect).trim_prefix("force_"), "origin": origin, "dir": aim.normalized(),
		"range": float(p.range_m) * units, "cone_deg": float(p.get("cone_deg", 30.0)),
		"impulse": float(p.impulse_mps) * units, "duration_s": float(p.get("duration_s", 0.0)),
	}}


## Returns { "error": String } or { "edits": Array[[Vector3i, block_key]] }.
## `is_air` is a Callable(Vector3i) -> bool used to avoid overwriting solids.
static func interpret(word_id: String, known: PackedStringArray, words: Dictionary,
		caster_pos: Vector3, target: Vector3i, is_air: Callable) -> Dictionary:
	if not words.has(word_id):
		return {"error": "unknown_word"}
	if not known.has(word_id):
		return {"error": "word_not_learned"}
	var w: Dictionary = words[word_id]
	if caster_pos.distance_to(Vector3(target)) > float(w.get("range_voxels", 0)) + WorldConfig.SERVER_REACH_SLACK:
		return {"error": "out_of_range"}
	match w.effect:
		"raise_column":
			return {"edits": _raise_column(target, w.params, is_air)}
	return {"error": "no_handler"}


static func _raise_column(target: Vector3i, p: Dictionary, is_air: Callable) -> Array:
	var edits: Array = []
	var r := int(p.radius)
	for dz in range(-r, r + 1):
		for dx in range(-r, r + 1):
			if dx * dx + dz * dz > r * r:
				continue
			for dy in range(1, int(p.height) + 1):
				var pos := target + Vector3i(dx, dy, dz)
				if is_air.call(pos):
					edits.append([pos, p.block])
	return edits
