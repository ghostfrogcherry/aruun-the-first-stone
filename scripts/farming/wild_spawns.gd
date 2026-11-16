class_name WildSpawns
extends RefCounted
## Where wild plants, flint and creatures may appear. Everything comes from the
## world seed through HeightField, so every peer and every session agrees and
## the server only has to remember what was harvested.

## Is column (x, z) the kind of place `where` asks for?
## shore: just above the waterline. mesa: on the red mesas.
## meadow: dry grassland. land: anywhere dry.
static func fits(field: HeightField, where: String, x: int, z: int) -> bool:
	var h := field.height_at(x, z)
	var sea := HeightField.SEA_LEVEL
	match where:
		"shore": return h >= sea + 1 and h <= sea + 5
		"mesa": return h > sea + 1 and field.is_mesa(x, z)
		"meadow": return h > sea + 1 and not field.is_mesa(x, z)
		"land": return h > sea + 1
	return false


## Wild nodes of one cell: Array of {"id", "plant", "pos": Vector3i}.
## `id` is stable ("w:cx:cz:plant:i") so harvested nodes can be remembered.
static func nodes_in_cell(field: HeightField, cx: int, cz: int, cell: int, plants: Dictionary) -> Array:
	var out: Array = []
	var salt := 100
	for key in plants:
		salt += 40
		var wild: Dictionary = plants[key].get("wild", {})
		if wild.is_empty():
			continue
		var per := float(wild.per_cell)
		var n := int(floor(per))
		if field.hash01(cx, cz, salt) < per - n:
			n += 1
		for i in n:
			# A few tries each, so sparse places like shores still get their plants.
			for attempt in 5:
				var s := salt + 1 + i * 6 + attempt
				var x := cx * cell + int(field.hash01(cx, cz, s) * cell)
				var z := cz * cell + int(field.hash01(cz, cx, s + 17) * cell)
				if fits(field, wild.get("where", "land"), x, z):
					out.append({"id": "w:%d:%d:%s:%d" % [cx, cz, key, i], "plant": key,
						"pos": Vector3i(x, field.height_at(x, z) + 1, z)})
					break
	return out
