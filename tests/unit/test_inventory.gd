extends RefCounted


func _init() -> void:
	GameData.ensure()


func test_add_stacks_and_reports_leftover() -> String:
	var inv := Inventory.new()
	if inv.add("flint", 150) != 0:
		return "150 flint should fit"
	if inv.count("flint") != 150 or inv.slots[0].n != 99 or inv.slots[1].n != 51:
		return "expected stacks of 99 and 51"
	var left := inv.add("flint_pick", Inventory.SIZE)
	return "" if left == 2 else "30 free slots should leave 2 picks over, got %d" % left


func test_remove_is_all_or_nothing() -> String:
	var inv := Inventory.new()
	inv.add("cord", 3)
	if inv.remove("cord", 5):
		return "removed more than held"
	if inv.count("cord") != 3:
		return "failed remove changed the bag"
	return "" if inv.remove("cord", 3) and inv.is_empty() else "exact remove failed"


func test_move_swaps_and_merges() -> String:
	var inv := Inventory.new()
	inv.add("flint", 10)
	inv.add("cord", 4)
	inv.move(0, 5)
	if inv.key_at(5) != "flint" or inv.key_at(0) != "":
		return "move to empty slot failed"
	inv.move(1, 5)
	if inv.key_at(1) != "flint" or inv.key_at(5) != "cord":
		return "swap failed"
	inv.add("flint", 5) # tops up the existing stack
	return "" if inv.count("flint") == 15 and inv.slots[1].n == 15 else "stacking after move failed"


func test_tools_have_durability_and_break() -> String:
	var inv := Inventory.new()
	inv.add("flint_pick", 1)
	if not inv.slots[0].has("d"):
		return "pick has no durability"
	inv.slots[0].d = 1
	return "" if inv.wear(0) and inv.key_at(0) == "" else "worn-out pick should break"


func test_save_round_trip() -> String:
	var inv := Inventory.new()
	inv.add("redroot", 7)
	inv.add("flint_axe", 1)
	var copy := Inventory.new()
	copy.from_data(JSON.parse_string(JSON.stringify(inv.to_data())))
	return "" if copy.count("redroot") == 7 and copy.key_at(1) == "flint_axe" and copy.slots[1].d == inv.slots[1].d else "round trip changed the bag"


func test_block_items_exist_for_diggable_blocks() -> String:
	if GameData.item("rust_rock").get("kind") != "block":
		return "rust_rock should be a block item"
	if not GameData.item("inscribed_stone").is_empty():
		return "protected blocks must not be items"
	return "" if GameData.item("water").is_empty() else "water must not be an item"


func test_craft_checks_materials_station_and_word() -> String:
	var inv := Inventory.new()
	var pick: Dictionary = GameData.recipes["flint_pick"]
	if Crafting.craft(pick, inv, [], false) == Crafting.OK:
		return "crafted from nothing"
	inv.add("flint", 2)
	inv.add("reed_stalk", 2)
	inv.add("cord", 1)
	if Crafting.craft(pick, inv, [], false) != Crafting.OK:
		return "pick should craft"
	if inv.count("flint_pick") != 1 or inv.count("flint") != 0:
		return "inputs not consumed or output missing"
	inv.add("raw_meat", 1)
	var roast: Dictionary = GameData.recipes["roast_meat"]
	if Crafting.craft(roast, inv, [], false) == Crafting.OK:
		return "roasting needs a fire"
	if Crafting.craft(roast, inv, ["fire"], false) != Crafting.OK:
		return "roasting at a fire should work"
	inv.add("rust_rock", 16)
	inv.add("flint", 2)
	var tether: Dictionary = GameData.recipes["tether_stone"]
	if Crafting.craft(tether, inv, [], false) == Crafting.OK:
		return "a Tether needs a known word"
	return "" if Crafting.craft(tether, inv, [], true) == Crafting.OK else "Tether should craft once a word is known"


func test_full_bag_does_not_eat_materials() -> String:
	var inv := Inventory.new()
	inv.add("flint", 2)
	inv.add("reed_stalk", 1)
	inv.add("cord", 1)
	for i in range(3, Inventory.SIZE):
		inv.slots[i] = {"k": "flint_pick", "n": 1, "d": 5}
	# Inputs sit in three single-item slots that the craft frees, so this fits.
	if Crafting.craft(GameData.recipes["flint_axe"], inv, [], false) != Crafting.OK:
		return "craft should reuse a freed slot"
	inv.add("bark", 1) # fills a freed slot
	inv.add("hide", 1) # fills the last free slot
	# One bark becomes two timber in the same slot, so it still fits.
	return "" if Crafting.craft(GameData.recipes["timber"], inv, [], false) == Crafting.OK else "timber should fit in the freed slot"


func test_every_recipe_uses_known_items() -> String:
	for id in GameData.recipes:
		var r: Dictionary = GameData.recipes[id]
		if GameData.item(r.out).is_empty():
			return "recipe %s makes unknown item %s" % [id, r.out]
		for key in r["in"]:
			if GameData.item(key).is_empty():
				return "recipe %s needs unknown item %s" % [id, key]
	return ""
