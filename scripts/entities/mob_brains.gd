class_name MobBrains
extends RefCounted
## How the more particular creatures think: reedhopper (skittish), glassback
## (defender), duskmoth (swarm) and echo sentinel (guardian). The design and
## the lore behind each is docs/design/mob_behaviours.md; the numbers are in
## content/creatures/creatures.json.
##
## Server only. `ent` is the Entities node. Each think function returns the
## velocity the creature wants this step (3D for fliers) and may change
## `rec.state`. Herd and hunter creatures are still handled in entities.gd.

const M := WorldConfig.UNITS_PER_METER

# Animation codes sent with every move (EntityVisual.ANIMS has the names).
const IDLE := 0
const WALK := 1
const RUN := 2
const WINDUP := 3
const STAGGER := 4
const ALERT := 5
const HOP := 6
const CHARGE := 7
const DIVE := 8
const REST := 9
const DORMANT := 10
const SLAM := 11

const BEHAVIORS := ["skittish", "defender", "swarm", "guardian"]


static func handles(def: Dictionary) -> bool:
	return BEHAVIORS.has(def.behavior)


static func think(ent, id: int, rec: Dictionary, def: Dictionary, dt: float) -> Vector3:
	rec.t -= dt
	rec.cd -= dt
	rec.poise = minf(rec.poise + dt * 4.0, float(def.poise))
	rec.anim_force = -1
	match def.behavior:
		"skittish": return _skittish(ent, id, rec, def)
		"defender": return _defender(ent, id, rec, def, dt)
		"swarm": return _swarm(ent, id, rec, def, dt)
		"guardian": return _guardian(ent, id, rec, def, dt)
	return Vector3.ZERO


static func _flat(v: Vector3) -> Vector3:
	return Vector3(v.x, 0, v.z)


static func _dir_of(yaw: float) -> Vector3:
	return Vector3(-sin(yaw), 0, -cos(yaw))


static func _yaw_to(v: Vector3) -> float:
	return atan2(-v.x, -v.z)


static func _wander(ent, rec: Dictionary, def: Dictionary) -> Vector3:
	match rec.state:
		"idle":
			if rec.t <= 0.0:
				var a: float = ent._rng.randf() * TAU
				rec.goal = rec.home + Vector3(cos(a), 0, sin(a)) * ent._rng.randf_range(2.0, 7.0) * M
				rec.state = "wander"
				rec.t = ent._rng.randf_range(3.0, 6.0)
		"wander":
			var to := _flat(rec.goal - rec.pos)
			if rec.t <= 0.0 or to.length() < 2.0:
				rec.state = "idle"
				rec.t = ent._rng.randf_range(2.0, 5.0)
			else:
				return to.normalized() * float(def.walk_mps) * M
	return Vector3.ZERO


# =============================================================================
# Reedhopper: freezes when it notices you, then the whole group bolts in hops
# =============================================================================

static func _skittish(ent, id: int, rec: Dictionary, def: Dictionary) -> Vector3:
	var brain: Dictionary = def.brain
	var target: Dictionary = ent.sim.nearest_player(rec.pos, float(def.sense_m) * M)
	match rec.state:
		"stagger":
			rec.anim_force = STAGGER
			if rec.t <= 0.0:
				_start_flee(ent, rec)
			return Vector3.ZERO
		"idle", "wander":
			if not target.is_empty():
				rec.state = "alert"
				rec.t = float(brain.alert_s)
				rec.yaw = _yaw_to(target.pos - rec.pos)
				return Vector3.ZERO
			return _wander(ent, rec, def)
		"alert":
			rec.anim_force = ALERT
			if target.is_empty():
				if rec.t <= -2.0:
					rec.state = "idle"
					rec.t = 1.0
			elif rec.t <= 0.0 or target.dist < float(brain.flee_within_m) * M or ent.sim.is_sprinting(target.id):
				rec.threat = target.pos
				ent._alarm_group(rec.group, target.pos)
				_start_flee(ent, rec)
			return Vector3.ZERO
		"flee":
			if not rec.has("hops"):
				_start_flee(ent, rec) # alarmed by a neighbour
			var away := _flat(rec.pos - rec.threat)
			if rec.t <= 0.0 or away.length() > float(brain.flee_distance_m) * M:
				rec.state = "idle"
				rec.t = 2.0
				rec.home = rec.pos
				rec.erase("hops")
				return Vector3.ZERO
			var dir := away.normalized() if away.length() > 0.1 else _dir_of(rec.yaw)
			if rec.grounded:
				if rec.pause > 0.0:
					rec.pause -= ent.SIM_STEP
					rec.anim_force = ALERT
					return Vector3.ZERO
				# Leave the ground: one hop of the burst.
				rec.vel = dir * float(def.run_mps) * M + Vector3(0, sqrt(2.0 * ent._gravity() * float(def.jump_m) * M), 0)
				rec.hops += 1
				if rec.hops >= int(brain.hops_per_burst):
					rec.hops = 0
					rec.pause = float(brain.pause_between_bursts_s)
			rec.anim_force = HOP
			return dir * float(def.run_mps) * M
	rec.state = "idle"
	return Vector3.ZERO


static func _start_flee(ent, rec: Dictionary) -> void:
	rec.state = "flee"
	rec.t = ent._rng.randf_range(6.0, 9.0)
	rec.hops = 0
	rec.pause = 0.0


# =============================================================================
# Glassback: grazes, warns, then charges in a straight line and pants after
# =============================================================================

static func _defender(ent, id: int, rec: Dictionary, def: Dictionary, dt: float) -> Vector3:
	var brain: Dictionary = def.brain
	var atk: Dictionary = def.attack
	var now: float = ent.sim.world_time
	var target: Dictionary = ent.sim.nearest_player(rec.pos, float(brain.calm_beyond_m) * M)
	match rec.state:
		"stagger":
			rec.anim_force = STAGGER
			if rec.t <= 0.0:
				rec.state = "face"
				rec.t = 0.6
			return Vector3.ZERO
		"flipped":
			rec.anim_force = STAGGER
			if rec.t <= 0.0:
				rec.state = "face"
				rec.t = 0.8
			return Vector3.ZERO
		"idle", "wander":
			# Crowding: stand too close for too long and it has had enough.
			if not target.is_empty() and target.dist < float(brain.crowd_within_m) * M:
				rec.crowd = float(rec.get("crowd", 0.0)) + dt
				if rec.crowd >= float(brain.crowd_provoke_s):
					provoke(ent, rec, def)
				elif rec.crowd >= float(brain.crowd_warn_s):
					rec.yaw = lerp_angle(rec.yaw, _yaw_to(target.pos - rec.pos), clampf(dt * 4.0, 0.0, 1.0))
					rec.anim_force = WINDUP # head down, stamping
					return Vector3.ZERO
			else:
				rec.crowd = maxf(float(rec.get("crowd", 0.0)) - dt, 0.0)
			return _wander(ent, rec, def)
		"face":
			if target.is_empty() or now > float(rec.get("calm_at", 0.0)):
				rec.state = "idle"
				rec.t = 2.0
				rec.home = rec.pos
				rec.crowd = 0.0
				return Vector3.ZERO
			rec.yaw = lerp_angle(rec.yaw, _yaw_to(target.pos - rec.pos), clampf(dt * 5.0, 0.0, 1.0))
			if rec.t <= 0.0 and rec.grounded:
				rec.state = "windup"
				rec.t = float(atk.windup_s)
				ent.event.rpc(id, "windup", {})
			return Vector3.ZERO
		"windup":
			rec.anim_force = WINDUP
			if not target.is_empty():
				rec.yaw = lerp_angle(rec.yaw, _yaw_to(target.pos - rec.pos), clampf(dt * 3.0, 0.0, 1.0))
			if rec.t <= 0.0:
				rec.state = "charge"
				rec.t = float(atk.max_distance_m) / float(atk.speed_mps)
				rec.struck = {}
				ent.event.rpc(id, "strike", {})
			return Vector3.ZERO
		"charge":
			rec.anim_force = CHARGE
			# It can barely steer, so a sidestep clears it.
			if not target.is_empty():
				var want := _yaw_to(target.pos - rec.pos)
				var turn := deg_to_rad(float(atk.turn_rate_deg_s)) * dt
				rec.yaw += clampf(wrapf(want - rec.yaw, -PI, PI), -turn, turn)
			var fwd := _dir_of(rec.yaw)
			var speed := float(atk.speed_mps) * M
			for pl in ent.sim.living_positions():
				if rec.struck.has(pl.id):
					continue
				var to: Vector3 = pl.pos - rec.pos
				if _flat(to).length() < float(def.radius_m) * M + 2.2 and absf(to.y) < 6.0 and _flat(to).dot(fwd) > -1.0:
					rec.struck[pl.id] = true
					ent.sim.damage_player(pl.id, float(atk.damage) * float(ent.sim.ruleset.creatures.damage_mult), rec.pos, "creature")
					ent.sim.shove_player(pl.id, fwd * float(atk.knockback_mps) * M, 0.35)
			# Running into something: break it if it is soft, otherwise see stars.
			if rec.blocked > speed * 0.5:
				var at: Vector3 = rec.pos + fwd * (float(def.radius_m) * M + 1.5) + Vector3(0, float(def.height_m) * M * 0.5, 0)
				var broke: String = ent.world.server_carve(0, at, 2.2, int(atk.breaks_power), false)
				if broke != "":
					rec.state = "stun"
					rec.t = float(atk.wall_stun_s)
					ent.event.rpc(id, "hit", {"hp": maxf(rec.hp, 0.0) / float(def.hp)})
					return Vector3.ZERO
			if rec.t <= 0.0:
				rec.state = "recover"
				rec.t = float(atk.recover_s)
				return Vector3.ZERO
			rec.vel.x = fwd.x * speed
			rec.vel.z = fwd.z * speed
			return fwd * speed
		"recover", "stun":
			rec.anim_force = STAGGER if rec.state == "stun" else IDLE
			if rec.t <= 0.0:
				rec.state = "face"
				rec.t = 0.4
			return Vector3.ZERO
	rec.state = "idle"
	return Vector3.ZERO


static func provoke(ent, rec: Dictionary, def: Dictionary) -> void:
	rec.calm_at = ent.sim.world_time + float(def.brain.calm_after_s)
	if rec.state in ["idle", "wander"]:
		rec.state = "face"
		rec.t = 0.5


# =============================================================================
# Duskmoth: drifts, gathers at open flame, and dives at whoever sits by it
# =============================================================================

static func _swarm(ent, id: int, rec: Dictionary, def: Dictionary, dt: float) -> Vector3:
	var brain: Dictionary = def.brain
	var atk: Dictionary = def.attack
	var walk := float(def.walk_mps) * M
	var ground := float(ent.sim.field.height_at(int(rec.pos.x), int(rec.pos.z))) + 1.0
	if not rec.has("alt"):
		rec.alt = ent._rng.randf_range(float(def.fly_height_m[0]), float(def.fly_height_m[1])) * M
		rec.phase = ent._rng.randf() * TAU
		rec.spin = 1.0 if ent._rng.randf() < 0.5 else -1.0
		rec.cd = ent._rng.randf_range(float(brain.dive_every_s[0]), float(brain.dive_every_s[1]))
		rec.hits = []
	if rec.state == "stagger":
		rec.anim_force = STAGGER
		if rec.t <= 0.0:
			rec.state = "idle"
		return Vector3(0, -walk, 0)
	if not ent.sim.is_night():
		# Dawn: the swarm lifts away and is gone.
		if rec.state != "leave":
			rec.state = "leave"
			rec.t = 5.0
		if rec.t <= 0.0:
			ent.remove_quietly(id)
		return _dir_of(rec.phase) * walk + Vector3(0, walk, 0)
	match rec.state:
		"dive_windup":
			rec.anim_force = WINDUP
			if rec.t <= 0.0:
				rec.state = "dive"
				rec.t = 2.0
			return Vector3.ZERO
		"dive":
			rec.anim_force = DIVE
			var p = ent.sim.player_pos(int(rec.prey))
			if p == null or rec.t <= 0.0:
				rec.state = "low"
				rec.t = float(atk.low_after_dive_s)
				return Vector3.ZERO
			var to: Vector3 = p + Vector3(0, 1.3 * M, 0) - rec.pos
			if to.length() < 1.3 * M:
				_moth_hit(ent, rec, def, int(rec.prey))
				rec.state = "low"
				rec.t = float(atk.low_after_dive_s)
				return Vector3.ZERO
			rec.yaw = _yaw_to(to)
			rec.vel = to.normalized() * float(atk.speed_mps) * M
			return rec.vel
		"low":
			if rec.t <= 0.0:
				rec.state = "idle"
				rec.cd = ent._rng.randf_range(float(brain.dive_every_s[0]), float(brain.dive_every_s[1]))
			return Vector3.ZERO
	# Drifting or circling a light.
	var light = ent.moth_light(rec.pos, float(brain.sense_light_m) * M, float(brain.tether_ward_m) * M)
	var centre: Vector3 = rec.home
	var radius := 6.0 * M
	if light != null:
		centre = light
		radius = lerpf(float(brain.orbit_radius_m[0]), float(brain.orbit_radius_m[1]), fposmod(rec.phase, 1.0)) * M
		if rec.cd <= 0.0 and ent.count_state("swarm", ["dive", "dive_windup"]) < int(brain.max_divers):
			var prey: Dictionary = ent.sim.nearest_player(light, float(brain.dive_within_m_of_light) * M)
			if not prey.is_empty() and not ent.sim.has_buff(prey.id, brain.ignores_buff) \
					and not ent.near_kind(prey.pos, "tether", float(brain.tether_ward_m) * M):
				rec.state = "dive_windup"
				rec.t = float(atk.windup_s)
				rec.prey = prey.id
				rec.yaw = _yaw_to(prey.pos - rec.pos)
				ent.event.rpc(id, "windup", {})
				return Vector3.ZERO
			rec.cd = 1.0
	rec.phase += dt * 0.5 * rec.spin
	var spot: Vector3 = centre + Vector3(cos(rec.phase), 0, sin(rec.phase)) * radius
	spot.y = maxf(ground, centre.y) + rec.alt + sin(rec.phase * 3.0) * 1.5
	var to_spot: Vector3 = spot - rec.pos
	if _flat(to_spot).length() > 0.5:
		rec.yaw = lerp_angle(rec.yaw, _yaw_to(to_spot), clampf(dt * 6.0, 0.0, 1.0))
	var speed := float(def.run_mps) * M if to_spot.length() > 8.0 * M else walk
	return to_spot.limit_length(1.0) * speed if to_spot.length() < 1.0 else to_spot.normalized() * speed


static func _moth_hit(ent, rec: Dictionary, def: Dictionary, peer: int) -> void:
	var atk: Dictionary = def.attack
	ent.sim.damage_player(peer, float(atk.damage) * float(ent.sim.ruleset.creatures.damage_mult), rec.pos, "creature")
	# Enough scales in the face and the world blurs for a while.
	var applies: Dictionary = atk.get("applies", {})
	if applies.is_empty():
		return
	ent.sim.note_hit(peer, "moth", float(applies.within_s), int(applies.after_hits), applies.buff)


# =============================================================================
# Echo sentinel: a recording of a guard's drill. Throw, throw, slam, forever.
# =============================================================================

static func _guardian(ent, id: int, rec: Dictionary, def: Dictionary, dt: float) -> Vector3:
	var brain: Dictionary = def.brain
	var atks: Dictionary = def.attacks
	var frac: float = rec.hp / float(rec.get("hp_max", def.hp))
	var frenzy := frac < float(brain.frenzy_below_hp)
	if rec.state == "idle" or rec.state == "wander":
		rec.state = "dormant" # how it is spawned
	if rec.state == "dormant":
		rec.anim_force = DORMANT
		var near: Dictionary = ent.sim.nearest_player(rec.pos, float(brain.wake_within_m) * M)
		if not near.is_empty():
			rec.state = "assemble"
			rec.t = float(brain.assemble_s)
			rec.step = 0
			rec.empty = 0.0
			var extra := maxi(ent.sim.living_positions().size() - 1, 0)
			rec.hp_max = float(def.hp) * (1.0 + float(def.get("hp_per_extra_player_mult", 0.0)) * extra)
			rec.hp = rec.hp_max
			ent.event.rpc(id, "wake", {"hp": 1.0})
		return Vector3.ZERO
	# Everyone gone: settle back into rubble and mend.
	var target: Dictionary = ent.sim.nearest_player(rec.home, float(brain.leash_m) * M)
	if target.is_empty():
		rec.empty = float(rec.get("empty", 0.0)) + dt
		if rec.empty >= float(brain.reset_after_empty_s):
			rec.state = "dormant"
			rec.hp = float(def.hp)
			rec.pos = rec.home
			rec.vel = Vector3.ZERO
			ent.event.rpc(id, "hit", {"hp": 1.0})
		return Vector3.ZERO
	rec.empty = 0.0
	var to := _flat(target.pos - rec.pos)
	match rec.state:
		"assemble":
			rec.anim_force = DORMANT if rec.t > float(brain.assemble_s) * 0.5 else IDLE
			if rec.t <= 0.0:
				rec.state = "stalk"
				rec.t = 1.2
			return Vector3.ZERO
		"stagger", "exposed":
			rec.anim_force = STAGGER
			if rec.t <= 0.0:
				rec.state = "stalk"
				rec.t = float(brain.between_moves_s) * 0.5
			return Vector3.ZERO
		"stalk":
			rec.yaw = lerp_angle(rec.yaw, _yaw_to(to), clampf(dt * 4.0, 0.0, 1.0))
			if rec.t <= 0.0 and rec.grounded:
				var pattern: Array = ["slam", "slam"] if frenzy else brain.pattern
				var move: String = pattern[int(rec.step) % pattern.size()]
				rec.step = int(rec.step) + 1
				if move == "slam" and frac < float(brain.pull_below_hp) and not frenzy:
					rec.state = "pull_windup"
					rec.t = float(atks.pull.windup_s)
					ent.event.rpc(id, "glow", {})
				else:
					rec.state = move + "_windup"
					rec.t = float(atks[move].windup_s)
					ent.event.rpc(id, "windup", {})
				return Vector3.ZERO
			# Hold its distance: back off if crowded, close in if the player runs.
			var keep: Array = brain.keeps_distance_m
			var leash := _flat(rec.pos - rec.home).length()
			if to.length() < float(keep[0]) * M and leash < float(brain.leash_m) * M * 0.6:
				return -to.normalized() * float(def.walk_mps) * M
			if to.length() > float(keep[1]) * M and leash < float(brain.leash_m) * M:
				return to.normalized() * float(def.walk_mps) * M
			return Vector3.ZERO
		"throw_windup":
			rec.anim_force = WINDUP
			rec.yaw = lerp_angle(rec.yaw, _yaw_to(to), clampf(dt * 6.0, 0.0, 1.0))
			if rec.t <= 0.0:
				var from: Vector3 = rec.pos + Vector3(0, float(def.height_m) * M * 0.8, 0) + _dir_of(rec.yaw) * 4.0
				var aim: Vector3 = target.pos + Vector3(0, 1.2 * M, 0) - from
				ent.spawn_shot(id, from, aim.normalized() * float(atks.throw.speed_mps) * M,
					float(atks.throw.damage) * float(ent.sim.ruleset.creatures.damage_mult))
				ent.event.rpc(id, "strike", {})
				rec.state = "stalk"
				rec.t = float(brain.between_moves_s)
			return Vector3.ZERO
		"pull_windup":
			rec.anim_force = WINDUP
			if rec.t <= 0.0:
				rec.state = "pull"
				rec.t = float(atks.pull.lasts_s)
				rec.tick = 0.0
			return Vector3.ZERO
		"pull":
			rec.anim_force = WINDUP
			rec.tick = float(rec.tick) - dt
			if rec.tick <= 0.0:
				rec.tick = 0.25
				var chest: Vector3 = rec.pos + Vector3(0, float(def.height_m) * M * 0.6, 0)
				for pl in ent.sim.living_positions():
					var d: Vector3 = chest - (pl.pos + Vector3(0, 1.2 * M, 0))
					if d.length() < float(atks.pull.range_m) * M and d.length() > 2.0 * M and ent.clear_line(chest, pl.pos + Vector3(0, 1.2 * M, 0)):
						ent.sim.shove_player(pl.id, _flat(d).normalized() * float(atks.pull.drag_mps) * M, 0.3)
			if rec.t <= 0.0:
				rec.state = "slam_windup"
				rec.t = float(atks.slam.windup_s)
				ent.event.rpc(id, "windup", {})
			return Vector3.ZERO
		"slam_windup":
			rec.anim_force = SLAM
			if rec.t <= 0.0:
				_slam(ent, id, rec, def)
				rec.state = "exposed"
				rec.t = float(atks.slam.frenzy_exposed_s if frenzy else atks.slam.exposed_s)
				if frenzy and int(rec.step) % 2 == 1:
					rec.t = 0.6 # the first of a double slam: no rest yet
			return Vector3.ZERO
	rec.state = "stalk"
	return Vector3.ZERO


static func _slam(ent, id: int, rec: Dictionary, def: Dictionary) -> void:
	var slam: Dictionary = def.attacks.slam
	ent.event.rpc(id, "slam", {"r": float(slam.radius_m) * M})
	for pl in ent.sim.living_positions():
		var to: Vector3 = pl.pos - rec.pos
		# A shockwave along the ground: anyone in the air above it is missed.
		if _flat(to).length() <= float(slam.radius_m) * M and to.y < 0.6 * M and to.y > -2.0 * M:
			ent.sim.damage_player(pl.id, float(slam.damage) * float(ent.sim.ruleset.creatures.damage_mult), rec.pos, "creature")
			ent.sim.shove_player(pl.id, _flat(to).normalized() * float(slam.knockback_mps) * M, 0.3)
	var at: Vector3 = rec.pos + _dir_of(rec.yaw) * 1.6 * M + Vector3(0, -0.5, 0)
	ent.world.server_carve(0, at, 1.6, int(slam.breaks_power), false)


# =============================================================================
# Being hit
# =============================================================================

## Damage after armour. May hurt the attacker back (a glassback's shell).
static func armour(ent, rec: Dictionary, def: Dictionary, amount: float, by_peer: int) -> float:
	if def.behavior == "guardian" and rec.state in ["dormant", "assemble", "idle", "wander"]:
		return -1.0 # cannot be hurt at all
	if amount <= 0.0 or by_peer == 0 or not def.has("armour"):
		return amount
	var a: Dictionary = def.armour
	var from = ent.sim.player_pos(by_peer)
	if from == null:
		return amount
	var side: float = _flat(from - rec.pos).normalized().dot(_dir_of(rec.yaw)) # 1 in front, -1 behind
	match def.behavior:
		"defender":
			if rec.state == "flipped":
				return amount * float(a.flipped_damage_mult)
			if absf(side) > 0.6:
				# Head-on or from behind you hit the shell; the flank is soft.
				ent.sim.damage_player(by_peer, float(a.shell_reflect_damage), rec.pos, "condition")
				return amount * float(a.shell_damage_mult)
		"guardian":
			if rec.state in ["exposed", "stagger"]:
				return amount * float(a.core_exposed_damage_mult)
			if side < -0.4:
				return amount * float(a.core_back_damage_mult)
			return amount * float(a.stone_damage_mult)
	return amount


## After a hit that did not kill. `staggered` is true if its poise broke.
static func on_hit(ent, id: int, rec: Dictionary, def: Dictionary, staggered: bool) -> void:
	match def.behavior:
		"skittish":
			ent._alarm_group(rec.group, rec.threat)
			if not staggered:
				_start_flee(ent, rec)
		"defender":
			provoke(ent, rec, def)
		"swarm":
			if not staggered:
				rec.state = "low"
				rec.t = 0.5
		"guardian":
			if staggered:
				rec.t = float(def.brain.stagger_s)


## A pull word took hold of it. Returns true if the creature reacted in its own way.
static func on_pull(ent, id: int, rec: Dictionary, def: Dictionary) -> bool:
	match def.behavior:
		"defender":
			if rec.state != "flipped":
				rec.state = "flipped"
				rec.t = float(def.brain.flip_s)
				rec.vel.y = 3.0 * M
				rec.calm_at = ent.sim.world_time + float(def.brain.calm_after_s)
				ent.event.rpc(id, "weigh", {})
			return true
		"guardian":
			if rec.state == "slam_windup":
				# The raised fist is dragged down: the slam never lands.
				rec.state = "stagger"
				rec.t = float(def.brain.stagger_s)
				ent.event.rpc(id, "weigh", {})
			return true
	return false
