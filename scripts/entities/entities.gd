extends Node3D
## Everything in the world that is not terrain or a player: creatures, plants,
## item drops and placed structures (campfire, Tether).
##
## The server owns them. It simulates creatures on the voxel grid, grows and
## spawns plants, and tells every peer what exists with `spawn`, `despawn`,
## `moves` and `event`. Peers only draw what they are told. Wild plants and
## creature spawn points come from the world seed (WildSpawns), so the server
## only saves what players changed: crops, structures, drops, harvested nodes.

const M := WorldConfig.UNITS_PER_METER
const Visuals := preload("res://scripts/entities/entity_visuals.gd")
const SIM_STEP := 0.05                 # creature simulation rate (20 Hz)
const CELL_RADIUS := 3                 # wild cells kept alive around each player
const MAX_CREATURES := 14
const SPAWN_MIN := 34.0 * M
const SPAWN_MAX := 54.0 * M
const DESPAWN_DIST := 110.0 * M
const DROP_PICKUP := 1.7 * M
const DROP_LIFETIME_S := 600.0
const PLANT_SPACING := 2.5             # voxels between planted crops

var sim                                # scripts/sim/sim.gd
var world                              # world_authority.gd
var ents := {}                         # id -> public data (every peer)
var _nodes := {}                       # id -> visual Node3D (every peer)
var _targets := {}                     # id -> {"pos", "yaw"} to ease towards (clients)

# Server only
var _srv := {}                         # id -> private record (creature brain, plant times, ...)
var _next_id := 1
var _wild_ids := {}                    # wild node id string -> entity id
var _cells := {}                       # "cx:cz" -> true, wild cells currently spawned
var _harvested := {}                   # wild node id -> world time it regrows
var _acc := 0.0
var _slow := 0.0
var _spawn_timer := 3.0
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_rng.randomize()


# =============================================================================
# Replication (server -> everyone)
# =============================================================================

@rpc("authority", "call_local", "reliable")
func spawn(list: Array) -> void:
	for e in list:
		var id: int = e.id
		if ents.has(id):
			continue
		ents[id] = e
		var node: Node3D = Visuals.build(e)
		node.position = e.pos
		node.rotation.y = e.get("yaw", 0.0)
		add_child(node)
		_nodes[id] = node


@rpc("authority", "call_local", "reliable")
func despawn(ids: PackedInt32Array) -> void:
	for id in ids:
		ents.erase(id)
		_targets.erase(id)
		var node: Node3D = _nodes.get(id)
		if node:
			node.queue_free()
		_nodes.erase(id)


## Flat list: id, pos, yaw, anim, id, pos, yaw, anim, ...
@rpc("authority", "call_local", "unreliable_ordered")
func moves(data: Array) -> void:
	var i := 0
	while i + 3 < data.size():
		var id: int = data[i]
		if ents.has(id):
			ents[id].pos = data[i + 1]
			ents[id].yaw = data[i + 2]
			_targets[id] = {"pos": data[i + 1], "yaw": data[i + 2]}
			var node: Node3D = _nodes.get(id)
			if node and node.has_method("set_anim"):
				node.set_anim(data[i + 3])
		i += 4


@rpc("authority", "call_local", "reliable")
func event(id: int, kind: String, data: Dictionary) -> void:
	if not ents.has(id):
		return
	var e: Dictionary = ents[id]
	for k in data:
		e[k] = data[k]
	var node: Node3D = _nodes.get(id)
	if node and node.has_method("on_event"):
		node.on_event(kind, e)


func _process(delta: float) -> void:
	# Ease visuals towards the last position the server sent.
	var k := clampf(delta * 14.0, 0.0, 1.0)
	for id in _targets:
		var node: Node3D = _nodes.get(id)
		if node:
			var t: Dictionary = _targets[id]
			node.position = node.position.lerp(t.pos, k)
			node.rotation.y = lerp_angle(node.rotation.y, t.yaw, k)


## Client side: the entity under the crosshair, or 0.
func pick(origin: Vector3, dir: Vector3, max_dist: float) -> int:
	var best := 0
	var best_t := max_dist
	for id in ents:
		var e: Dictionary = ents[id]
		var r := _pick_radius(e)
		var centre: Vector3 = e.pos + Vector3(0, r, 0)
		var to := centre - origin
		var t := to.dot(dir)
		if t < 0.0 or t > best_t:
			continue
		if (origin + dir * t).distance_to(centre) <= r:
			best = id
			best_t = t
	return best


func _pick_radius(e: Dictionary) -> float:
	match e.kind:
		"creature": return float(GameData.creatures[e.def].height_m) * M * 0.5
		"plant": return maxf(1.6, float(GameData.plants[e.def].look.height_m) * M * 0.5)
		"drop": return 1.6
	return 2.4


## Short text for the interaction prompt.
func describe(id: int) -> String:
	var e: Dictionary = ents.get(id, {})
	if e.is_empty():
		return ""
	match e.kind:
		"plant":
			var def: Dictionary = GameData.plants[e.def]
			if def.get("node", false):
				return "E: gather %s" % def.name
			if int(e.get("stage", 0)) >= int(def.stages) - 1:
				return "E: harvest %s" % def.name
			return "%s (growing, stage %d of %d)" % [def.name, int(e.get("stage", 0)) + 1, int(def.stages)]
		"drop":
			return "E: pick up"
		"structure":
			return GameData.structures[e.def].name
		"creature":
			return GameData.creatures[e.def].name
	return ""


# =============================================================================
# Server: spawning and bookkeeping
# =============================================================================

func _add(e: Dictionary, private := {}) -> int:
	var id := _next_id
	_next_id += 1
	e["id"] = id
	_srv[id] = private
	spawn.rpc([e])
	return id


func _remove(id: int) -> void:
	if not ents.has(id):
		return
	var wild: String = _srv.get(id, {}).get("wild_id", "")
	if wild != "":
		_wild_ids.erase(wild)
	_srv.erase(id)
	despawn.rpc(PackedInt32Array([id]))


## Sends everything that exists to a player who just joined.
func send_all_to(peer: int) -> void:
	if peer == multiplayer.get_unique_id() or ents.is_empty():
		return
	spawn.rpc_id(peer, ents.values())


func spawn_drop(pos: Vector3, items: Array, keep := false) -> int:
	if items.is_empty():
		return 0
	return _add({"kind": "drop", "def": items[0].k, "pos": pos, "yaw": 0.0, "n": items.size()},
		{"items": items, "born": sim.world_time, "keep": keep})


func spawn_structure(def_key: String, pos: Vector3, owner_guid: String) -> int:
	return _add({"kind": "structure", "def": def_key, "pos": pos, "yaw": 0.0}, {"owner": owner_guid})


func spawn_plant(def_key: String, pos: Vector3, planted_t: float, mult: float, wild_id := "") -> int:
	var def: Dictionary = GameData.plants[def_key]
	var st := Farming.stage(def, planted_t, sim.world_time, mult)
	var id := _add({"kind": "plant", "def": def_key, "pos": pos, "yaw": _rng.randf() * TAU, "stage": st},
		{"planted": planted_t, "mult": mult, "wild_id": wild_id})
	if wild_id != "":
		_wild_ids[wild_id] = id
	return id


func spawn_creature(def_key: String, pos: Vector3, group := 0) -> int:
	var def: Dictionary = GameData.creatures[def_key]
	return _add({"kind": "creature", "def": def_key, "pos": pos, "yaw": _rng.randf() * TAU, "hp": 1.0},
		{"pos": pos, "vel": Vector3.ZERO, "yaw": 0.0, "hp": float(def.hp), "state": "idle", "t": _rng.randf_range(1.0, 3.0),
		"cd": 0.0, "poise": float(def.poise), "home": pos, "goal": pos, "grounded": false, "launched": 0.0,
		"slow": 0.0, "group": group, "threat": pos, "anim": 0, "blocked": 0.0})


func count_creatures(behavior: String, near: Vector3, dist: float) -> int:
	var n := 0
	for id in _srv:
		var e: Dictionary = ents[id]
		if e.kind == "creature" and GameData.creatures[e.def].behavior == behavior \
				and _srv[id].state != "dead" and _srv[id].pos.distance_to(near) < dist:
			n += 1
	return n


## Structures of a kind within their own radius of pos. kind: "heat" | "station:<name>" | "calm".
func near_structure(pos: Vector3, what: String) -> bool:
	for id in ents:
		var e: Dictionary = ents[id]
		if e.kind != "structure":
			continue
		var def: Dictionary = GameData.structures[e.def]
		var d: float = e.pos.distance_to(pos)
		if what == "heat" and d <= float(def.get("heat_m", 0.0)) * M:
			return true
		if what == "calm" and d <= float(def.get("calm_m", 0.0)) * M:
			return true
		if what.begins_with("station:") and def.get("station", "") == what.trim_prefix("station:") and d <= 4.0 * M:
			return true
	return false


func growth_mult_at(pos: Vector3) -> float:
	var best := 1.0
	for id in ents:
		var e: Dictionary = ents[id]
		if e.kind == "structure":
			var def: Dictionary = GameData.structures[e.def]
			if def.has("growth_mult") and e.pos.distance_to(pos) <= float(def.get("calm_m", 0.0)) * M:
				best = maxf(best, float(def.growth_mult))
	return best


func plant_near(pos: Vector3, dist: float) -> bool:
	for id in ents:
		var e: Dictionary = ents[id]
		if (e.kind == "plant" or e.kind == "structure") and e.pos.distance_to(pos) < dist:
			return true
	return false


# =============================================================================
# Server: interaction
# =============================================================================

## A player presses E on an entity. Returns a message for them ("" for none).
func interact(peer: int, id: int, c: CharacterState, from: Vector3) -> String:
	var e: Dictionary = ents.get(id, {})
	if e.is_empty():
		return ""
	if e.pos.distance_to(from) > WorldConfig.REACH_VOXELS + WorldConfig.SERVER_REACH_SLACK:
		return "too far away"
	match e.kind:
		"plant":
			return _harvest(id, c)
		"drop":
			_give_drop(id, c)
			return ""
		"structure":
			var def: Dictionary = GameData.structures[e.def]
			if def.get("respawn", false):
				c.tether = e.pos
				return "This Tether is now your home. You will wake here."
			return "The fire is warm. Stand by it to rest and cook."
	return ""


func _harvest(id: int, c: CharacterState) -> String:
	var e: Dictionary = ents[id]
	var rec: Dictionary = _srv[id]
	var def: Dictionary = GameData.plants[e.def]
	if not Farming.is_ripe(def, rec.planted, sim.world_time, rec.mult):
		return "%s is not ripe yet" % def.name
	var got := Farming.harvest(def, _rng)
	var text := PackedStringArray()
	var spill: Array = []
	for g in got:
		var left := c.inv.add(g[0], g[1])
		if left > 0:
			spill.append({"k": g[0], "n": left})
		text.append("%d %s" % [g[1], GameData.item(g[0]).name.to_lower()])
	if not spill.is_empty():
		spawn_drop(e.pos, spill)
	if rec.wild_id != "":
		_harvested[rec.wild_id] = sim.world_time + GameData.wild_regrow_s
		_remove(id)
	else:
		var back := Farming.regrow_time(def, sim.world_time, rec.mult)
		if back < 0.0:
			_remove(id)
		else:
			rec.planted = back
			event.rpc(id, "stage", {"stage": Farming.stage(def, back, sim.world_time, rec.mult)})
	return "You gather " + ", ".join(text)


func _give_drop(id: int, c: CharacterState) -> bool:
	var rec: Dictionary = _srv[id]
	var left: Array = []
	var took := false
	for it in rec.items:
		var n := c.inv.add(it.k, int(it.n), int(it.get("d", -1)))
		if n < int(it.n):
			took = true
		if n > 0:
			var rest: Dictionary = it.duplicate()
			rest.n = n
			left.append(rest)
	if left.is_empty():
		_remove(id)
	else:
		rec.items = left
	return took


# =============================================================================
# Server: combat against creatures
# =============================================================================

## Creatures inside a swing. Returns their ids.
func creatures_in_arc(origin: Vector3, forward: Vector3, reach: float, arc: float) -> Array:
	var out: Array = []
	for id in _srv:
		var e: Dictionary = ents[id]
		if e.kind != "creature" or _srv[id].state == "dead":
			continue
		var def: Dictionary = GameData.creatures[e.def]
		var centre: Vector3 = _srv[id].pos + Vector3(0, float(def.height_m) * M * 0.5, 0)
		if CombatRules.in_arc(origin, forward, centre, reach + float(def.height_m) * M * 0.25, arc, float(def.radius_m) * M):
			out.append(id)
	return out


func damage_creature(id: int, amount: float, poise: float, kick: Vector3, by_peer := 0) -> void:
	var rec: Dictionary = _srv.get(id, {})
	if rec.is_empty() or rec.get("state", "dead") == "dead":
		return
	var def: Dictionary = GameData.creatures[ents[id].def]
	rec.hp -= amount
	rec.vel += kick
	if kick.length() > 4.0 * M:
		rec.launched = 0.6
	rec.poise -= poise
	if by_peer != 0:
		var p := sim.player_pos(by_peer)
		if p != null:
			rec.threat = p
	if rec.hp <= 0.0:
		_kill(id)
		return
	if rec.poise <= 0.0:
		rec.poise = float(def.poise)
		rec.state = "stagger"
		rec.t = 0.7
	elif def.behavior == "herd":
		_alarm_group(rec.group, rec.threat)
	elif rec.state in ["idle", "wander"]:
		rec.state = "chase"
	event.rpc(id, "hit", {"hp": maxf(rec.hp, 0.0) / float(def.hp)})


func _alarm_group(group: int, threat: Vector3) -> void:
	for id in _srv:
		var r: Dictionary = _srv[id]
		if r.has("group") and r.group == group and r.state != "dead" and r.state != "stagger":
			r.state = "flee"
			r.t = _rng.randf_range(5.0, 8.0)
			r.threat = threat


func _kill(id: int) -> void:
	var rec: Dictionary = _srv[id]
	var def: Dictionary = GameData.creatures[ents[id].def]
	rec.state = "dead"
	rec.t = 2.5
	var items: Array = []
	for d in def.get("drops", []):
		var n := _rng.randi_range(int(d.min), int(d.max))
		if n > 0:
			items.append({"k": d.item, "n": n})
	spawn_drop(rec.pos + Vector3(0, 1, 0), items)
	event.rpc(id, "die", {"hp": 0.0})


## Graviturge words acting on entities. Returns how many things were moved.
func apply_force(force: Dictionary, caster_peer: int) -> int:
	var origin: Vector3 = force.origin
	var dir: Vector3 = force.dir
	var reach: float = force.range
	var moved := 0
	match force.type:
		"push":
			var cos_half := cos(deg_to_rad(float(force.cone_deg) * 0.5))
			for id in _srv:
				var rec: Dictionary = _srv[id]
				var e: Dictionary = ents[id]
				if e.kind == "creature" and rec.state != "dead":
					var to: Vector3 = rec.pos + Vector3(0, 2, 0) - origin
					var d := to.length()
					if d > reach or (d > 3.0 and to.normalized().dot(dir) < cos_half):
						continue
					var mass := float(GameData.creatures[e.def].mass)
					var power: float = float(force.impulse) * (1.0 - 0.5 * d / reach) / mass
					var kick := (dir + Vector3(0, 0.35, 0)).normalized() * power
					damage_creature(id, 0.0, 12.0, kick, caster_peer)
					rec.launched = 1.2
					moved += 1
				elif e.kind == "drop":
					var to2: Vector3 = e.pos - origin
					if to2.length() <= reach and to2.normalized().dot(dir) >= cos_half:
						_move_drop(id, e.pos + dir * 6.0 * M)
						moved += 1
		"pull":
			var id := _ray_target(origin, dir, reach)
			if id != 0:
				var rec: Dictionary = _srv[id]
				var e: Dictionary = ents[id]
				if e.kind == "creature":
					var to: Vector3 = origin - rec.pos
					var flat := Vector3(to.x, 0, to.z)
					var dist := maxf(flat.length() - 1.5 * M, 0.0)
					# Throw it towards the caster on an arc that lands just in front of them.
					var g := _gravity()
					var up: float = clampf(sqrt(g * dist) * 0.5, 2.0 * M, float(force.impulse))
					var air := 2.0 * up / g
					rec.vel = flat.normalized() * minf(dist / maxf(air, 0.2), float(force.impulse)) + Vector3(0, up, 0)
					rec.launched = air + 0.3
					damage_creature(id, 0.0, 12.0, Vector3.ZERO, caster_peer)
				else:
					_move_drop(id, origin + dir * 1.0 * M)
				moved += 1
		"weigh":
			var id := _ray_target(origin, dir, reach)
			if id != 0 and ents[id].kind == "creature":
				_srv[id].slow = float(force.duration_s)
				damage_creature(id, 0.0, 0.0, Vector3(0, -float(force.impulse), 0), caster_peer)
				event.rpc(id, "weigh", {})
				moved += 1
	return moved


func _ray_target(origin: Vector3, dir: Vector3, reach: float) -> int:
	var best := 0
	var best_t := reach
	for id in _srv:
		var e: Dictionary = ents[id]
		if e.kind == "plant" or e.kind == "structure" or _srv[id].get("state", "") == "dead":
			continue
		var r := _pick_radius(e) + 2.0 # generous: magick should feel reliable
		var centre: Vector3 = e.pos + Vector3(0, r * 0.5, 0)
		var t := (centre - origin).dot(dir)
		if t > 0.0 and t < best_t and (origin + dir * t).distance_to(centre) <= r:
			best = id
			best_t = t
	return best


func _move_drop(id: int, to: Vector3) -> void:
	ents[id].pos = to
	moves.rpc([id, to, 0.0, 0])


## Hurts creatures under a landing piece of terrain. box is in voxels.
func crush(box: AABB, amount: float) -> void:
	for id in _srv.keys():
		var e: Dictionary = ents[id]
		if e.kind == "creature" and _srv[id].state != "dead" and box.grow(1.0).has_point(_srv[id].pos + Vector3(0, 1, 0)):
			damage_creature(id, amount, 999.0, Vector3.ZERO)


# =============================================================================
# Server: simulation
# =============================================================================

func server_tick(delta: float) -> void:
	_acc += delta
	while _acc >= SIM_STEP:
		_acc -= SIM_STEP
		_step_creatures(SIM_STEP)
	_slow += delta
	if _slow >= 0.5:
		_slow = 0.0
		_slow_tick()
	_spawn_timer -= delta
	if _spawn_timer <= 0.0:
		_spawn_timer = 4.0
		_spawn_creatures()


func _gravity() -> float:
	return ProjectSettings.get_setting("physics/3d/default_gravity")


func _solid(p: Vector3i) -> bool:
	return ContentDB.collides(world.vt.get_voxel(p))


func _loaded(pos: Vector3) -> bool:
	return world.vt != null and world.vt.is_area_editable(AABB(pos - Vector3(4, 8, 4), Vector3(8, 24, 8)))


func _step_creatures(dt: float) -> void:
	var out: Array = []
	var g := _gravity()
	for id in _srv.keys():
		var e: Dictionary = ents[id]
		if e.kind != "creature":
			continue
		var rec: Dictionary = _srv[id]
		var def: Dictionary = GameData.creatures[e.def]
		if rec.state == "dead":
			rec.t -= dt
			if rec.t <= 0.0:
				_remove(id)
			continue
		if not _loaded(rec.pos):
			continue # terrain not loaded here: freeze until it is
		var want := _think(id, rec, def, dt)
		rec.slow = maxf(rec.slow - dt, 0.0)
		if rec.slow > 0.0:
			want *= 0.45
		rec.launched = maxf(rec.launched - dt, 0.0)
		if rec.launched <= 0.0 or (rec.grounded and rec.launched < 0.3):
			var k := clampf(dt * (10.0 if rec.grounded else 2.0), 0.0, 1.0)
			rec.vel.x = lerpf(rec.vel.x, want.x, k)
			rec.vel.z = lerpf(rec.vel.z, want.z, k)
		if rec.grounded and rec.blocked > 0.0 and want.length() > 0.1 and rec.launched <= 0.0:
			rec.vel.y = sqrt(2.0 * g * float(def.jump_m) * M) # hop over what is in the way
		var before: Vector3 = rec.vel
		var r := CreatureMotion.step(rec.pos, rec.vel, dt, float(def.radius_m) * M, float(def.height_m) * M,
			2, g * (2.2 if rec.slow > 0.0 else 1.0), _solid)
		rec.pos = r.pos
		rec.vel = r.vel
		rec.grounded = r.grounded
		rec.blocked = r.wall
		# Being thrown into things hurts; so does a long fall.
		var hurt := 0.0
		if r.impact / M > 8.0:
			hurt += (r.impact / M - 8.0) * 6.0
		if before.y > -1.0 and r.wall / M > 6.0 and rec.launched > 0.0:
			hurt += (r.wall / M - 6.0) * 4.0
		if rec.pos.y < -200.0:
			hurt = 9999.0
		if hurt > 0.0:
			damage_creature(id, hurt, 20.0, Vector3.ZERO)
			if rec.state == "dead":
				continue
		var flat := Vector2(rec.vel.x, rec.vel.z)
		if flat.length() > 0.5 and rec.state not in ["windup", "stagger"]:
			rec.yaw = lerp_angle(rec.yaw, atan2(-rec.vel.x, -rec.vel.z), clampf(dt * 10.0, 0.0, 1.0))
		var anim := 0
		if rec.state == "windup":
			anim = 3
		elif rec.state == "stagger":
			anim = 4
		elif flat.length() > float(def.walk_mps) * M * 1.6:
			anim = 2
		elif flat.length() > 0.5:
			anim = 1
		rec.anim = anim
		e.pos = rec.pos
		e.yaw = rec.yaw
		out.append_array([id, rec.pos, rec.yaw, anim])
	if not out.is_empty():
		moves.rpc(out)


## Decides what the creature wants: returns its desired horizontal velocity.
func _think(id: int, rec: Dictionary, def: Dictionary, dt: float) -> Vector3:
	rec.t -= dt
	rec.cd -= dt
	rec.poise = minf(rec.poise + dt * 4.0, float(def.poise))
	var hunter: bool = def.behavior == "hunter"
	var target: Dictionary = sim.nearest_player(rec.pos, float(def.sense_m) * M)
	var walk := float(def.walk_mps) * M
	var run := float(def.run_mps) * M
	var hunting: bool = hunter and (sim.is_night() or sim.ruleset.creatures.hunters_by_day)
	match rec.state:
		"stagger":
			if rec.t <= 0.0:
				rec.state = "chase" if hunter else "flee"
				rec.t = 5.0
			return Vector3.ZERO
		"idle":
			if hunting and not target.is_empty():
				rec.state = "chase"
			elif hunter and not hunting and not target.is_empty():
				rec.state = "flee" # daylight: slink away
				rec.t = 6.0
				rec.threat = target.pos
			elif not hunter and not target.is_empty() and target.dist < 4.0 * M:
				_alarm_group(rec.group, target.pos)
			elif rec.t <= 0.0:
				var a := _rng.randf() * TAU
				rec.goal = rec.home + Vector3(cos(a), 0, sin(a)) * _rng.randf_range(3.0, 9.0) * M
				rec.state = "wander"
				rec.t = _rng.randf_range(3.0, 6.0)
			return Vector3.ZERO
		"wander":
			var to: Vector3 = rec.goal - rec.pos
			to.y = 0.0
			if hunting and not target.is_empty():
				rec.state = "chase"
			elif rec.t <= 0.0 or to.length() < 2.0:
				rec.state = "idle"
				rec.t = _rng.randf_range(2.0, 5.0)
			return to.normalized() * walk
		"flee":
			if rec.t <= 0.0:
				rec.state = "idle"
				rec.t = 2.0
				rec.home = rec.pos
			var away: Vector3 = rec.pos - rec.threat
			away.y = 0.0
			return (away.normalized() if away.length() > 0.1 else Vector3.FORWARD) * run
		"chase":
			if target.is_empty() or not hunting:
				rec.state = "idle"
				rec.t = 1.0
				rec.home = rec.pos
				return Vector3.ZERO
			var to: Vector3 = target.pos - rec.pos
			to.y = 0.0
			var atk: Dictionary = def.attack
			if to.length() < float(atk.reach_m) * M * 0.75 and absf(target.pos.y - rec.pos.y) < float(atk.reach_m) * M:
				if rec.cd <= 0.0 and rec.grounded:
					rec.state = "windup"
					rec.t = float(atk.windup_s)
					rec.yaw = atan2(-to.x, -to.z)
					event.rpc(id, "windup", {})
				return Vector3.ZERO
			return to.normalized() * run
		"windup":
			if rec.t <= 0.0:
				var atk: Dictionary = def.attack
				var fwd := Vector3(-sin(rec.yaw), 0, -cos(rec.yaw))
				rec.vel += fwd * float(atk.lunge_mps) * M
				rec.launched = 0.25
				sim.creature_strike(rec.pos + Vector3(0, float(def.height_m) * M * 0.5, 0), fwd,
					float(atk.reach_m) * M, float(atk.arc_deg), float(atk.damage))
				event.rpc(id, "strike", {})
				rec.state = "recover"
				rec.t = 0.5
				rec.cd = float(atk.cooldown_s)
			return Vector3.ZERO
		"recover":
			if rec.t <= 0.0:
				rec.state = "chase"
			return Vector3.ZERO
	return Vector3.ZERO


func _slow_tick() -> void:
	var players: Array = sim.living_positions()
	# Plants: grow, and fall out if the ground under them is dug away.
	for id in _srv.keys():
		var e: Dictionary = ents[id]
		var rec: Dictionary = _srv[id]
		if e.kind == "plant":
			var def: Dictionary = GameData.plants[e.def]
			var st := Farming.stage(def, rec.planted, sim.world_time, rec.mult)
			if st != int(e.stage):
				event.rpc(id, "stage", {"stage": st})
			if _loaded(e.pos) and not _solid(Vector3i((e.pos + Vector3(0, -0.5, 0)).floor())):
				if rec.wild_id != "":
					_harvested[rec.wild_id] = sim.world_time + GameData.wild_regrow_s
				_remove(id)
		elif e.kind == "drop":
			if _loaded(e.pos):
				var p := Vector3i((e.pos + Vector3(0, -0.5, 0)).floor())
				var fell := 0
				while fell < 12 and not _solid(p):
					p.y -= 1
					fell += 1
				if fell > 0:
					_move_drop(id, e.pos - Vector3(0, fell, 0))
			for pl in players:
				if pl.pos.distance_to(e.pos) < DROP_PICKUP:
					if _give_drop(id, sim.chars[pl.id]):
						sim.push_inventory(pl.id)
					break
			if ents.has(id) and not rec.keep and sim.world_time - float(rec.born) > DROP_LIFETIME_S:
				_remove(id)
		elif e.kind == "creature" and rec.state != "dead":
			var far := true
			for pl in players:
				if pl.pos.distance_to(rec.pos) < DESPAWN_DIST:
					far = false
					break
			if far:
				_remove(id)
	_update_wild_cells(players)


func _update_wild_cells(players: Array) -> void:
	var cell := GameData.plant_cell
	var want := {}
	for pl in players:
		var cx := floori(pl.pos.x / cell)
		var cz := floori(pl.pos.z / cell)
		for dz in range(-CELL_RADIUS, CELL_RADIUS + 1):
			for dx in range(-CELL_RADIUS, CELL_RADIUS + 1):
				want["%d:%d" % [cx + dx, cz + dz]] = Vector2i(cx + dx, cz + dz)
	# Drop cells nobody is near (with a margin so they do not flicker).
	for key in _cells.keys():
		if want.has(key):
			continue
		var c: Vector2i = _cells[key]
		var near := false
		for pl in players:
			if absi(floori(pl.pos.x / cell) - c.x) <= CELL_RADIUS + 2 and absi(floori(pl.pos.z / cell) - c.y) <= CELL_RADIUS + 2:
				near = true
				break
		if not near:
			_cells.erase(key)
			var prefix := "w:%d:%d:" % [c.x, c.y]
			for wid in _wild_ids.keys():
				if wid.begins_with(prefix):
					_remove(_wild_ids[wid])
	var landmark := Vector3(world.landmark_origin)
	for key in want:
		var fresh := not _cells.has(key)
		var c: Vector2i = want[key]
		_cells[key] = c
		for node in WildSpawns.nodes_in_cell(sim.field, c.x, c.y, cell, GameData.plants):
			if _wild_ids.has(node.id):
				continue
			if _harvested.has(node.id):
				if sim.world_time < float(_harvested[node.id]):
					continue
				_harvested.erase(node.id)
			elif not fresh:
				continue # already handled when the cell was first loaded
			var pos := Vector3(node.pos) + Vector3(0.5, 0, 0.5)
			if Vector2(pos.x - landmark.x, pos.z - landmark.z).length() < 22.0:
				continue # keep the stone circle clear
			spawn_plant(node.plant, pos, -1.0e9, 1.0, node.id)


func _spawn_creatures() -> void:
	var total := 0
	for id in _srv:
		if ents[id].kind == "creature":
			total += 1
	var players: Array = sim.living_positions()
	for pl in players:
		if total >= MAX_CREATURES:
			return
		for key in GameData.creatures:
			var def: Dictionary = GameData.creatures[key]
			var hunter: bool = def.behavior == "hunter"
			var when: String = def.spawn.when
			var ok := (when == "night") == sim.is_night() or (hunter and sim.ruleset.creatures.hunters_by_day)
			if not ok:
				continue
			var have := count_creatures(def.behavior, pl.pos, 90.0 * M)
			var cap: int = int(sim.ruleset.creatures.max_hunters_per_player) if hunter else 3
			if have >= cap or _rng.randf() > 0.5:
				continue
			var spot = _spawn_spot(pl.pos, def, players)
			if spot == null:
				continue
			var group := _next_id
			var n := _rng.randi_range(int(def.group[0]), int(def.group[1]))
			for i in n:
				var off := Vector3(_rng.randf_range(-3, 3), 0, _rng.randf_range(-3, 3)) * M * float(i > 0)
				var p: Vector3 = spot + off
				p.y = sim.field.height_at(int(p.x), int(p.z)) + 1.0
				spawn_creature(key, p, group)
				total += 1


func _spawn_spot(near: Vector3, def: Dictionary, players: Array):
	for attempt in 6:
		var a := _rng.randf() * TAU
		var d := _rng.randf_range(SPAWN_MIN, SPAWN_MAX)
		var x := int(near.x + cos(a) * d)
		var z := int(near.z + sin(a) * d)
		if not WildSpawns.fits(sim.field, def.spawn.get("where", "land"), x, z):
			continue
		var pos := Vector3(x + 0.5, sim.field.height_at(x, z) + 1.0, z + 0.5)
		if not _loaded(pos) or near_structure(pos, "calm"):
			continue
		var clear := true
		for pl in players:
			if pl.pos.distance_to(pos) < SPAWN_MIN * 0.8:
				clear = false
		if clear and CreatureMotion._free(pos, float(def.radius_m) * M, float(def.height_m) * M, _solid):
			return pos
	return null


# =============================================================================
# Server: saving
# =============================================================================

func to_data() -> Dictionary:
	var list: Array = []
	for id in _srv:
		var e: Dictionary = ents[id]
		var rec: Dictionary = _srv[id]
		var p: Vector3 = e.pos
		match e.kind:
			"plant":
				if rec.wild_id == "":
					list.append({"kind": "plant", "def": e.def, "pos": [p.x, p.y, p.z], "planted": rec.planted, "mult": rec.mult})
			"structure":
				list.append({"kind": "structure", "def": e.def, "pos": [p.x, p.y, p.z], "owner": rec.owner})
			"drop":
				list.append({"kind": "drop", "pos": [p.x, p.y, p.z], "items": rec.items, "keep": rec.keep})
	return {"entities": list, "harvested": _harvested}


func from_data(d: Dictionary) -> void:
	_harvested = d.get("harvested", {})
	for e in d.get("entities", []):
		var pos := Vector3(e.pos[0], e.pos[1], e.pos[2])
		match e.kind:
			"plant":
				if GameData.plants.has(e.def):
					spawn_plant(e.def, pos, float(e.planted), float(e.get("mult", 1.0)))
			"structure":
				if GameData.structures.has(e.def):
					spawn_structure(e.def, pos, e.get("owner", ""))
			"drop":
				var items: Array = []
				for it in e.get("items", []):
					if GameData.items.has(it.get("k", "")):
						items.append(it)
				spawn_drop(pos, items, e.get("keep", false))
