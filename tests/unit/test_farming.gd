extends RefCounted


func _init() -> void:
	GameData.ensure()


func test_growth_stages() -> String:
	var def: Dictionary = GameData.plants["grinleaf"]
	var per: float = def.stage_s
	if Farming.stage(def, 100.0, 100.0) != 0:
		return "fresh seed should be stage 0"
	if Farming.stage(def, 100.0, 100.0 + per * 2.5) != 2:
		return "expected stage 2"
	if not Farming.is_ripe(def, 100.0, 100.0 + per * 50.0):
		return "old plant should be ripe"
	return "" if Farming.stage(def, 100.0, 100.0 + per * 2.0 / 1.5 + 1.0, 1.5) == 2 else "Tether bonus should speed growth"


func test_ground_rules() -> String:
	var def: Dictionary = GameData.plants["redroot"]
	if not Farming.can_plant(def, "grass") or not Farming.can_plant(def, "red_soil"):
		return "redroot should grow in soil and grass"
	return "" if not Farming.can_plant(def, "rust_rock") else "redroot should not grow on rock"


func test_harvest_and_regrow() -> String:
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	var grin: Dictionary = GameData.plants["grinleaf"]
	var got := Farming.harvest(grin, rng)
	if got.is_empty() or got[0][0] != "grinleaf_bud" or got[0][1] < 2:
		return "grinleaf should give buds"
	var t := Farming.regrow_time(grin, 5000.0)
	if Farming.stage(grin, t, 5000.0) != int(grin.regrow_stage):
		return "grinleaf should regrow from its regrow stage"
	return "" if Farming.regrow_time(GameData.plants["redroot"], 5000.0) < 0.0 else "redroot is pulled up when harvested"


func test_every_seed_and_yield_is_an_item() -> String:
	for key in GameData.plants:
		for y in GameData.plants[key].yields:
			if GameData.item(y.item).is_empty():
				return "%s yields unknown item %s" % [key, y.item]
	for key in GameData.items:
		var it: Dictionary = GameData.items[key]
		if it.get("kind") == "seed" and not GameData.plants.has(it.get("plant", "")):
			return "seed %s grows unknown plant" % key
		if it.get("kind") == "structure" and not GameData.structures.has(it.get("structure", "")):
			return "%s places unknown structure" % key
	return ""


func test_wild_nodes_are_deterministic_and_dry() -> String:
	var field := HeightField.new(1234)
	var found := 0
	for cx in range(-6, 6):
		for cz in range(-6, 6):
			var a := WildSpawns.nodes_in_cell(field, cx, cz, GameData.plant_cell, GameData.plants)
			var b := WildSpawns.nodes_in_cell(HeightField.new(1234), cx, cz, GameData.plant_cell, GameData.plants)
			if a.size() != b.size():
				return "same seed gave different nodes"
			for i in a.size():
				if a[i].id != b[i].id or a[i].pos != b[i].pos:
					return "same seed gave different nodes"
				if a[i].pos.y <= HeightField.SEA_LEVEL + 1:
					return "%s spawned under water" % a[i].id
				found += 1
	return "" if found > 50 else "only %d wild nodes in 144 cells" % found
