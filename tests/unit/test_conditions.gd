extends RefCounted

var light: Dictionary
var hardcore: Dictionary


func _init() -> void:
	GameData.ensure()
	light = GameData.ruleset("light")
	hardcore = GameData.ruleset("hardcore")


func _flags(list: Array) -> Dictionary:
	var out := {}
	for f in list:
		out[f] = true
	return out


func test_hardcore_extends_light() -> String:
	if hardcore.conditions.hunger.rates.size() != light.conditions.hunger.rates.size():
		return "hardcore should inherit hunger rates"
	if hardcore.conditions.hunger.bands.size() != light.conditions.hunger.bands.size() + 2:
		return "hardcore should add two hunger bands"
	if light.death.keep_inventory != true or hardcore.death.keep_inventory != false:
		return "death rules not overridden"
	return "" if hardcore.vitals.stamina == light.vitals.stamina else "untouched vitals should be inherited"


func test_hunger_rises_and_sprinting_adds() -> String:
	var a := Conditions.start_values(light)
	var b := a.duplicate()
	Conditions.tick(a, light, _flags(["day"]), [], 60.0)
	Conditions.tick(b, light, _flags(["day", "sprinting"]), [], 60.0)
	if a.hunger <= light.conditions.hunger.start:
		return "hunger did not rise"
	return "" if b.hunger > a.hunger else "sprinting should make you hungrier"


func test_light_mode_never_hurts_only_removes_buffs() -> String:
	var v := Conditions.start_values(light)
	for id in v:
		v[id] = 100.0
	var r := Conditions.tick(v, light, _flags(["night", "exposed", "high_altitude", "dusty"]), [], 60.0)
	if r.damage > 0.0:
		return "light mode dealt %s damage" % r.damage
	for b in r.bands:
		if b.good:
			return "still well fed while starving"
	return ""


func test_hardcore_starvation_hurts() -> String:
	var v := Conditions.start_values(hardcore)
	v.hunger = 95.0
	var r := Conditions.tick(v, hardcore, _flags(["day"]), [], 60.0)
	return "" if r.damage > 5.0 else "starving in hardcore should hurt, got %s" % r.damage


func test_well_fed_gives_buffs() -> String:
	var v := Conditions.start_values(light)
	v.hunger = 5.0
	var r := Conditions.tick(v, light, _flags(["day"]), [], 1.0)
	return "" if Conditions.mod(r.mods, "hp_regen") > 0.0 and Conditions.mod(r.mods, "stamina_regen_mult") > 1.0 else "no well-fed bonus"


func test_cold_at_night_and_fire_warms() -> String:
	var v := Conditions.start_values(light)
	Conditions.tick(v, light, _flags(["night", "exposed"]), [], 300.0)
	if v.cold < 50.0:
		return "five exposed night minutes should chill, got %s" % v.cold
	var r := Conditions.tick(v, light, _flags(["night", "exposed"]), [], 1.0)
	if Conditions.mod(r.mods, "stamina_regen_mult") >= 1.0:
		return "chilled should slow stamina regen"
	Conditions.tick(v, light, _flags(["night", "exposed", "near_heat"]), [], 120.0)
	return "" if v.cold < 5.0 else "a fire should warm you up, got %s" % v.cold


func test_shelter_stops_the_cold() -> String:
	var v := Conditions.start_values(light)
	Conditions.tick(v, light, _flags(["night", "sheltered"]), [], 600.0)
	return "" if v.cold == 0.0 else "sheltered night should not chill"


func test_mask_blocks_dust() -> String:
	var bare := Conditions.start_values(light)
	var masked := bare.duplicate()
	Conditions.tick(bare, light, _flags(["dusty", "exposed"]), [], 60.0)
	Conditions.tick(masked, light, _flags(["dusty", "exposed", "masked"]), [], 60.0)
	return "" if bare.dust > 30.0 and masked.dust == 0.0 else "mask should stop dust (%s vs %s)" % [bare.dust, masked.dust]


func test_gravity_debt_is_repaid_and_weighs_you_down() -> String:
	var v := Conditions.start_values(light)
	v.gravity_debt = 80.0
	var r := Conditions.tick(v, light, _flags(["day"]), [], 1.0)
	if Conditions.mod(r.mods, "move_speed_mult") >= 0.8:
		return "crushing debt should slow you"
	Conditions.tick(v, light, _flags(["day"]), [], 300.0)
	return "" if v.gravity_debt == 0.0 else "debt should be repaid over time"


func test_calm_buff_eases_fatigue_and_expires() -> String:
	var c := CharacterState.new()
	c.setup_new(light)
	c.cond.fatigue = 60.0
	c.add_buff("calm")
	c.tick(light, _flags(["day"]), 60.0)
	if c.cond.fatigue >= 60.0:
		return "calm should ease fatigue"
	for i in 10:
		c.tick(light, _flags(["day"]), 30.0)
	return "" if not c.buffs.has("calm") else "calm should wear off"


func test_time_of_day() -> String:
	if Conditions.is_night(Conditions.time_of_day(0.0, 1200.0)):
		return "a new world should start in daylight"
	if not Conditions.is_night(Conditions.time_of_day(1200.0 * 0.6, 1200.0)):
		return "0.6 days in should be night"
	return "" if Conditions.day_number(1200.0 * 0.8, 1200.0) == 2 else "day counter wrong"


func test_character_stamina_and_death() -> String:
	var c := CharacterState.new()
	c.setup_new(hardcore)
	if not c.spend(30.0) or c.stamina != 70.0:
		return "spend failed"
	c.tick(hardcore, _flags(["day"]), 0.2)
	if c.stamina != 70.0:
		return "stamina should wait before regenerating"
	c.tick(hardcore, _flags(["day"]), 2.0)
	if c.stamina <= 70.0:
		return "stamina should regenerate"
	c.stamina = 0.0
	if c.spend(5.0):
		return "no stamina, no action"
	c.cond.thirst = 100.0
	c.cond.hunger = 60.0 # not well fed, so nothing heals the damage
	c.hp = 1.0
	return "" if c.tick(hardcore, _flags(["day"]), 60.0) else "dehydration should kill in hardcore"


func test_character_save_round_trip() -> String:
	var c := CharacterState.new()
	c.guid = "abc"
	c.setup_new(light)
	c.cond.hunger = 61.0
	c.tether = Vector3(1, 2, 3)
	c.words.append("word_rise")
	c.add_buff("calm")
	var d := CharacterState.new()
	d.from_data(JSON.parse_string(JSON.stringify(c.to_data())), light)
	if d.cond.hunger != 61.0 or d.tether != Vector3(1, 2, 3) or not d.words.has("word_rise"):
		return "state lost in save"
	return "" if d.buffs.has("calm") and d.inv.count("redroot") == c.inv.count("redroot") else "buffs or bag lost in save"


func test_eating_and_gear_flags() -> String:
	var c := CharacterState.new()
	c.setup_new(light)
	c.cond.hunger = 80.0
	c.apply_use(GameData.item("roast_meat").use)
	if c.cond.hunger != 30.0 or not c.buffs.has("warm_meal"):
		return "roast meat should feed and warm"
	c.inv.add("dust_mask", 1)
	var flags := {}
	c.gear_flags(flags)
	return "" if flags.has("masked") else "carrying a mask should set masked"
