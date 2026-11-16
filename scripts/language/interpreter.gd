class_name LanguageInterpreter
extends RefCounted
## Turns a spoken word into validated effect instructions. It never runs
## arbitrary code: every word maps to an effect in a fixed registry, and every
## parameter is bounds-checked against that effect's declared limits.
## Output instructions are applied by the server like any other world edit.
##
## Layers (from the tech design): lexicon (content/language), grammar
## (single words for now; combinations come later), interpreter (this file).

const REGISTRY := ["raise_column"]


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
	return problems


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
