class_name Conditions
extends RefCounted
## The Conditions system: hunger, thirst, cold, thin air, dust, fatigue and
## gravity debt are all the same thing, a 0-100 severity that drifts with
## what is happening to you (flags) and applies effects while it sits in a
## band. Rulesets (content/survival/rulesets.json) hold every number, so
## light and hardcore are two presets of this one file. Pure logic.

## Starting severity for every condition in a ruleset.
static func start_values(ruleset: Dictionary) -> Dictionary:
	var out := {}
	var defs: Dictionary = ruleset.get("conditions", {})
	for id in defs:
		out[id] = float(defs[id].get("start", 0))
	return out


## Per-minute drift of one condition under the given flags.
static func rate(def: Dictionary, flags: Dictionary) -> float:
	var total := 0.0
	for r in def.get("rates", []):
		var on := true
		for f in r.get("when", []):
			if not flags.has(f):
				on = false
				break
		if on:
			for f in r.get("unless", []):
				if flags.has(f):
					on = false
					break
		if on:
			total += float(r.per_min)
	return total


## The bands a value sits in (usually one, or none).
static func bands_at(def: Dictionary, value: float) -> Array:
	var out: Array = []
	for b in def.get("bands", []):
		if value >= float(b.from) and value < float(b.to):
			out.append(b)
	return out


## Advances `values` by dt seconds. `buffs` is an Array of buff ids.
## Returns {"mods": Dictionary, "damage": hp lost this tick, "bands": Array of
## {"cond", "name", "good"}} for the HUD.
static func tick(values: Dictionary, ruleset: Dictionary, flags: Dictionary, buffs: Array, dt: float) -> Dictionary:
	var defs: Dictionary = ruleset.get("conditions", {})
	var mod_list: Array = []
	var buff_rates := {}
	for id in buffs:
		var b: Dictionary = GameData.buffs.get(id, {})
		for f in b.get("flags", []):
			flags[f] = true
		if b.has("mods"):
			mod_list.append(b.mods)
		for cond in b.get("rates", {}):
			buff_rates[cond] = buff_rates.get(cond, 0.0) + float(b.rates[cond])
	var damage := 0.0
	var bands: Array = []
	# Rates first, so the repay multiplier from other bands applies next tick.
	for id in defs:
		var def: Dictionary = defs[id]
		var per_min := rate(def, flags) + float(buff_rates.get(id, 0.0))
		values[id] = clampf(float(values.get(id, def.get("start", 0))) + per_min * dt / 60.0, 0.0, 100.0)
		for b in bands_at(def, values[id]):
			if b.has("mods"):
				mod_list.append(b.mods)
			damage += float(b.get("damage_per_min", 0.0)) * dt / 60.0
			bands.append({"cond": id, "name": b.name, "good": b.get("good", false)})
	return {"mods": combine_mods(mod_list), "damage": damage, "bands": bands}


## Keys ending in _mult multiply (default 1); everything else adds (default 0).
static func combine_mods(list: Array) -> Dictionary:
	var out := {}
	for mods in list:
		for k in mods:
			if String(k).ends_with("_mult"):
				out[k] = float(out.get(k, 1.0)) * float(mods[k])
			else:
				out[k] = float(out.get(k, 0.0)) + float(mods[k])
	return out


static func mod(mods: Dictionary, key: String) -> float:
	return float(mods.get(key, 1.0 if key.ends_with("_mult") else 0.0))


## Time of day 0..1 (0 = midnight, 0.5 = noon). Worlds start in the morning.
static func time_of_day(world_time: float, day_length: float) -> float:
	return fposmod(world_time / day_length + 0.3, 1.0)


static func is_night(tod: float) -> bool:
	return tod < 0.23 or tod > 0.79


static func day_number(world_time: float, day_length: float) -> int:
	return int(floor(world_time / day_length + 0.3)) + 1
