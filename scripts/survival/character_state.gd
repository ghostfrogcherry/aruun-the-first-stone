class_name CharacterState
extends RefCounted
## Everything the server knows about one character: vitals, conditions, buffs,
## bag, known words and home Tether. Pure logic so it can be unit-tested; the
## Sim node feeds it flags and time. Saved per character, separate from the world.

var guid := ""
var display_name := "Wanderer"
var hp := 100.0
var stamina := 100.0
var cond := {}                    # condition id -> severity 0..100
var buffs := {}                   # buff id -> seconds left
var inv := Inventory.new()
var selected := 0                 # hotbar slot in hand
var words := PackedStringArray()
var tether = null                 # Vector3 of the home Tether, or null
var dead := false
var downed := 0.0                 # seconds left before a downed character dies
var mods := {}                    # combined modifiers from the last tick
var bands: Array = []             # active band names for the HUD
var since_spend := 99.0
var since_hurt := 99.0


func setup_new(ruleset: Dictionary) -> void:
	cond = Conditions.start_values(ruleset)
	hp = float(ruleset.vitals.hp)
	stamina = float(ruleset.vitals.stamina)
	buffs = {}
	inv = Inventory.new()
	var kit: Dictionary = ruleset.get("starting_items", {})
	for key in kit:
		inv.add(key, int(kit[key]))
	for w in ruleset.get("starting_words", []):
		if not words.has(w):
			words.append(w)


func hp_max(ruleset: Dictionary) -> float:
	return float(ruleset.vitals.hp) * Conditions.mod(mods, "hp_max_mult")


func stamina_max(ruleset: Dictionary) -> float:
	return float(ruleset.vitals.stamina) * Conditions.mod(mods, "stamina_max_mult")


func held_key() -> String:
	return inv.key_at(selected)


func held_stats() -> Dictionary:
	return GameData.held_stats(held_key())


## Flags granted by carried gear (e.g. a dust mask gives "masked").
func gear_flags(flags: Dictionary) -> void:
	for s in inv.slots:
		if s != null:
			var f: String = GameData.item(s.k).get("grants_flag", "")
			if f != "":
				flags[f] = true


## Advances the character by dt seconds. Returns true if conditions killed it.
func tick(ruleset: Dictionary, flags: Dictionary, dt: float) -> bool:
	if dead:
		return false
	since_spend += dt
	since_hurt += dt
	for id in buffs.keys():
		buffs[id] -= dt
		if buffs[id] <= 0.0:
			buffs.erase(id)
	gear_flags(flags)
	var result := Conditions.tick(cond, ruleset, flags, buffs.keys(), dt)
	mods = result.mods
	bands = result.bands
	var v: Dictionary = ruleset.vitals
	if flags.has("sprinting"):
		stamina = maxf(0.0, stamina - float(v.sprint_cost) * dt)
		since_spend = 0.0
	elif since_spend >= float(v.stamina_regen_delay_s):
		stamina += float(v.stamina_regen) * Conditions.mod(mods, "stamina_regen_mult") * dt
	stamina = minf(stamina, stamina_max(ruleset))
	if downed > 0.0:
		return false
	var regen := Conditions.mod(mods, "hp_regen")
	if since_hurt >= float(v.hp_regen_delay_s):
		regen += float(v.hp_regen)
	hp = minf(hp + regen * dt, hp_max(ruleset))
	if result.damage > 0.0:
		hp -= result.damage
	return hp <= 0.0


## Spends stamina for an action. Like a soulslike: any stamina left is enough,
## and the action can take you to zero.
func spend(amount: float) -> bool:
	if stamina <= 0.5:
		return false
	stamina = maxf(0.0, stamina - amount)
	since_spend = 0.0
	return true


## Applies damage. Returns true if hp reached zero.
func hurt(amount: float) -> bool:
	if dead or amount <= 0.0:
		return false
	hp -= amount * Conditions.mod(mods, "damage_taken_mult")
	since_hurt = 0.0
	return hp <= 0.0


## Eats, drinks, smokes or applies an item's "use" block.
func apply_use(use: Dictionary) -> void:
	for id in use.get("conditions", {}):
		if cond.has(id):
			cond[id] = clampf(float(cond[id]) + float(use.conditions[id]), 0.0, 100.0)
	if use.has("buff"):
		add_buff(use.buff)
	if use.has("heal"):
		hp += float(use.heal)


func add_buff(id: String) -> void:
	var b: Dictionary = GameData.buffs.get(id, {})
	if not b.is_empty():
		buffs[id] = float(b.duration_s)


## Gravity debt: can this character take on `cost` more?
func can_owe(cost: float) -> bool:
	return float(cond.get("gravity_debt", 0.0)) + cost <= 100.0


func owe(cost: float) -> void:
	if cond.has("gravity_debt"):
		cond["gravity_debt"] = clampf(float(cond["gravity_debt"]) + cost, 0.0, 100.0)


func to_data() -> Dictionary:
	return {
		"guid": guid, "name": display_name, "hp": hp, "stamina": stamina, "cond": cond.duplicate(),
		"buffs": buffs.duplicate(), "inv": inv.to_data(), "selected": selected,
		"words": Array(words), "tether": [tether.x, tether.y, tether.z] if tether != null else null,
	}


func from_data(d: Dictionary, ruleset: Dictionary) -> void:
	guid = d.get("guid", guid)
	display_name = d.get("name", display_name)
	hp = float(d.get("hp", ruleset.vitals.hp))
	stamina = float(d.get("stamina", ruleset.vitals.stamina))
	cond = Conditions.start_values(ruleset)
	var saved: Dictionary = d.get("cond", {})
	for id in cond:
		if saved.has(id):
			cond[id] = float(saved[id])
	buffs = {}
	var saved_buffs: Dictionary = d.get("buffs", {})
	for id in saved_buffs:
		if GameData.buffs.has(id):
			buffs[id] = float(saved_buffs[id])
	inv.from_data(d.get("inv", []))
	selected = clampi(int(d.get("selected", 0)), 0, Inventory.HOTBAR - 1)
	words = PackedStringArray(d.get("words", []))
	var t = d.get("tether")
	tether = Vector3(t[0], t[1], t[2]) if typeof(t) == TYPE_ARRAY and t.size() == 3 else null
	if hp <= 0.0:
		hp = float(ruleset.vitals.hp) * 0.5
