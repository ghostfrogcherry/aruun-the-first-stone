class_name EditRules
extends RefCounted
## Server-side validation for world edits. Clients send requests; only edits
## that pass these checks are applied and broadcast. Pure logic, unit-tested.

const OK := ""

static func check_reach(player_pos: Vector3, target: Vector3i) -> String:
	var limit := WorldConfig.REACH_VOXELS + WorldConfig.SERVER_REACH_SLACK
	if player_pos.distance_to(Vector3(target) + Vector3(0.5, 0.5, 0.5)) > limit:
		return "out_of_reach"
	return OK


static func check_place(current_id: int, new_id: int, placeable: PackedInt32Array) -> String:
	if current_id != 0:
		return "occupied"
	if not placeable.has(new_id):
		return "not_placeable"
	return OK


static func check_dig(radius: float) -> String:
	if radius <= 0.0 or radius > WorldConfig.DIG_RADIUS + 0.01:
		return "bad_radius"
	return OK


static func check_blast(radius: float) -> String:
	if radius <= 0.0 or radius > WorldConfig.BLAST_RADIUS_MAX + 0.01:
		return "bad_radius"
	return OK


## Blocks that a carve must never remove (e.g. inscriptions). Returns ids.
static func protected_ids(blocks: Array) -> PackedInt32Array:
	var out := PackedInt32Array()
	for b in blocks:
		if int(b.get("hardness", 0)) >= 99:
			out.append(int(b.id))
	return out
