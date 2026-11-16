extends RefCounted


func test_reach_ok_and_too_far() -> String:
	if EditRules.check_reach(Vector3.ZERO, Vector3i(10, 0, 0)) != EditRules.OK:
		return "10 voxels should be in reach"
	if EditRules.check_reach(Vector3.ZERO, Vector3i(200, 0, 0)) == EditRules.OK:
		return "200 voxels should be out of reach"
	return ""


func test_place_rules() -> String:
	var placeable := PackedInt32Array([1, 2])
	if EditRules.check_place(0, 1, placeable) != EditRules.OK:
		return "placing into air should work"
	if EditRules.check_place(3, 1, placeable) != "occupied":
		return "placing into a solid should fail"
	if EditRules.check_place(0, 7, placeable) != "not_placeable":
		return "non-placeable block should fail"
	return ""


func test_blast_radius_capped() -> String:
	if EditRules.check_blast(WorldConfig.BLAST_RADIUS_MAX + 1.0) == EditRules.OK:
		return "oversized blast accepted"
	return "" if EditRules.check_dig(WorldConfig.DIG_RADIUS) == EditRules.OK else "normal dig rejected"


func test_protected_ids() -> String:
	var ids := EditRules.protected_ids([{"id": 1, "hardness": 2}, {"id": 7, "hardness": 99}])
	return "" if ids == PackedInt32Array([7]) else "expected [7], got %s" % ids


func test_project_gravity_matches_aruun() -> String:
	var g: float = ProjectSettings.get_setting("physics/3d/default_gravity")
	return "" if absf(g - WorldConfig.GRAVITY) < 0.05 else "project gravity %s != WorldConfig.GRAVITY %s" % [g, WorldConfig.GRAVITY]
