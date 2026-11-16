class_name Crafting
extends RefCounted
## Recipe rules. Pure logic: the server calls these with its copy of the
## inventory; clients call `check` only to grey out buttons.

const OK := ""


## stations: what the crafter stands near, e.g. ["fire", "tether"].
static func check(recipe: Dictionary, inv: Inventory, stations: Array, knows_a_word: bool) -> String:
	if recipe.is_empty():
		return "unknown recipe"
	var station: String = recipe.get("station", "")
	if station != "" and not stations.has(station):
		return "needs a %s nearby" % GameData.structures.get(_station_structure(station), {}).get("name", station).to_lower()
	if recipe.get("needs_word", false) and not knows_a_word:
		return "you must know a word of Avarra to inscribe it"
	if not inv.has_all(recipe["in"]):
		return "missing materials"
	return OK


## Removes the inputs and adds the output. Returns OK or the reason it failed.
static func craft(recipe: Dictionary, inv: Inventory, stations: Array, knows_a_word: bool) -> String:
	var why := check(recipe, inv, stations, knows_a_word)
	if why != OK:
		return why
	# Try on a copy first so a full bag never eats the materials.
	var trial := Inventory.new()
	trial.from_data(inv.to_data())
	for key in recipe["in"]:
		trial.remove(key, int(recipe["in"][key]))
	if trial.add(recipe.out, int(recipe.get("n", 1))) > 0:
		return "your bag is full"
	inv.slots = trial.slots
	return OK


static func _station_structure(station: String) -> String:
	for key in GameData.structures:
		if GameData.structures[key].get("station", "") == station:
			return key
	return ""
