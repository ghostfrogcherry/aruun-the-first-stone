class_name CombatRules
extends RefCounted
## Melee rules. Pure logic: the server calls these to decide every hit.
## Distances here are world units (voxels); content stores metres.

const M := WorldConfig.UNITS_PER_METER
const HEAVY_DAMAGE := 1.9
const HEAVY_STAMINA := 1.8
const HEAVY_COOLDOWN := 1.5
const HEAVY_POISE := 2.2
const LIGHT_WINDUP_S := 0.16
const HEAVY_WINDUP_S := 0.42
const GUARD_ARC_DOT := 0.2        # blocks hits from roughly the front 160 degrees
const GUARD_STAMINA_PER_DAMAGE := 1.1


## Numbers for one swing of the held item (or bare hands).
static func attack_profile(stats: Dictionary, heavy: bool, mods: Dictionary) -> Dictionary:
	var dmg := float(stats.damage) * Conditions.mod(mods, "damage_mult")
	return {
		"damage": dmg * (HEAVY_DAMAGE if heavy else 1.0),
		"stamina": float(stats.stamina) * (HEAVY_STAMINA if heavy else 1.0),
		"cooldown": float(stats.cooldown_s) * (HEAVY_COOLDOWN if heavy else 1.0),
		"windup": HEAVY_WINDUP_S if heavy else LIGHT_WINDUP_S,
		"poise": float(stats.poise) * (HEAVY_POISE if heavy else 1.0),
		"knockback": float(stats.knockback) * (1.5 if heavy else 1.0) * M,
		"reach": float(stats.reach_m) * M,
		"arc": float(stats.arc_deg),
	}


## True if `target` (its centre) is inside the swing: within reach of `origin`
## and within the arc around `forward`, measured on the ground plane.
static func in_arc(origin: Vector3, forward: Vector3, target: Vector3, reach: float,
		arc_deg: float, target_radius := 0.0) -> bool:
	var to := target - origin
	if absf(to.y) > reach + target_radius:
		return false
	var flat := Vector2(to.x, to.z)
	var dist := flat.length()
	if dist > reach + target_radius:
		return false
	if dist <= target_radius + 0.5:
		return true # standing inside it
	var f := Vector2(forward.x, forward.z)
	if f.length() < 0.001:
		return true
	# Widen the arc by the angle the target's body covers.
	var half := deg_to_rad(arc_deg * 0.5) + atan2(target_radius, dist)
	return absf(f.angle_to(flat)) <= half


## A hit against a guarding defender. `facing` is true if the hit comes from
## the front. Returns {"damage", "stamina_cost", "broken"}.
static func guard(damage: float, guard_frac: float, facing: bool, stamina: float) -> Dictionary:
	if not facing:
		return {"damage": damage, "stamina_cost": 0.0, "broken": false}
	var cost := damage * GUARD_STAMINA_PER_DAMAGE
	if stamina < cost:
		# Guard breaks: the part you could not pay for comes through in full.
		var paid := stamina / cost
		return {"damage": damage * (1.0 - guard_frac * paid), "stamina_cost": stamina, "broken": true}
	return {"damage": damage * (1.0 - guard_frac), "stamina_cost": cost, "broken": false}


static func is_facing(defender_pos: Vector3, defender_forward: Vector3, source: Vector3) -> bool:
	var to := source - defender_pos
	to.y = 0.0
	var f := defender_forward
	f.y = 0.0
	if to.length() < 0.01 or f.length() < 0.01:
		return true
	return f.normalized().dot(to.normalized()) >= GUARD_ARC_DOT


## Damage from landing at `speed` world units per second.
static func fall_damage(speed: float, rules: Dictionary) -> float:
	var over := speed / M - float(rules.get("safe_mps", 9.0))
	return maxf(0.0, over) * float(rules.get("damage_per_mps", 5.0))


## Velocity kick away from `from`, with a little lift so the target leaves the ground.
static func knockback(from: Vector3, to: Vector3, strength: float, mass: float) -> Vector3:
	var dir := to - from
	dir.y = 0.0
	dir = dir.normalized() if dir.length() > 0.01 else Vector3.FORWARD
	return (dir * strength + Vector3.UP * strength * 0.45) / maxf(mass, 0.1)
