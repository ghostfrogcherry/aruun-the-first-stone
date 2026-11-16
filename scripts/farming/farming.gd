class_name Farming
extends RefCounted
## Crop rules. A plant's growth stage is worked out from when it was planted,
## so nothing ticks per plant and a saved world grows correctly while unloaded.

## Growth stage 0..stages-1. `mult` > 1 grows faster (near a Tether).
static func stage(def: Dictionary, planted_t: float, now: float, mult := 1.0) -> int:
	var stages := int(def.get("stages", 1))
	var per := float(def.get("stage_s", 0))
	if stages <= 1 or per <= 0.0:
		return stages - 1
	return clampi(int(floor((now - planted_t) * mult / per)), 0, stages - 1)


static func is_ripe(def: Dictionary, planted_t: float, now: float, mult := 1.0) -> bool:
	return stage(def, planted_t, now, mult) >= int(def.get("stages", 1)) - 1


static func can_plant(def: Dictionary, ground_key: String) -> bool:
	return (def.get("ground", []) as Array).has(ground_key)


## Rolls a ripe plant's yield: Array of [item key, count].
static func harvest(def: Dictionary, rng: RandomNumberGenerator) -> Array:
	var out: Array = []
	for y in def.get("yields", []):
		var n := rng.randi_range(int(y.get("min", 1)), int(y.get("max", 1)))
		if n > 0:
			out.append([y.item, n])
	return out


## After a harvest: the new planted time that puts the plant back at its
## regrow stage, or -1.0 if the plant is used up.
static func regrow_time(def: Dictionary, now: float, mult := 1.0) -> float:
	var back := int(def.get("regrow_stage", -1))
	if back < 0:
		return -1.0
	return now - back * float(def.stage_s) / mult
