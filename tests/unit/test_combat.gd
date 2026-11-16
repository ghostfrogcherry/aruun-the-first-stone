extends RefCounted


func _init() -> void:
	GameData.ensure()


func test_arc_and_reach() -> String:
	var o := Vector3.ZERO
	var f := Vector3(0, 0, -1)
	if not CombatRules.in_arc(o, f, Vector3(0, 0, -6), 8.0, 90.0):
		return "target straight ahead should be hit"
	if CombatRules.in_arc(o, f, Vector3(0, 0, -12), 8.0, 90.0):
		return "target out of reach was hit"
	if CombatRules.in_arc(o, f, Vector3(0, 0, 6), 8.0, 90.0):
		return "target behind was hit"
	if CombatRules.in_arc(o, f, Vector3(6, 0, -1), 8.0, 90.0):
		return "target far to the side was hit"
	if not CombatRules.in_arc(o, f, Vector3(5, 0, -2), 8.0, 90.0, 4.0):
		return "a big target to the side should be clipped"
	return "" if not CombatRules.in_arc(o, f, Vector3(0, 30, -3), 8.0, 90.0) else "target far above was hit"


func test_heavy_hits_harder_and_costs_more() -> String:
	var axe := GameData.held_stats("flint_axe")
	var light := CombatRules.attack_profile(axe, false, {})
	var heavy := CombatRules.attack_profile(axe, true, {})
	if heavy.damage <= light.damage or heavy.stamina <= light.stamina or heavy.windup <= light.windup:
		return "heavy should be stronger, dearer and slower"
	var weak := CombatRules.attack_profile(axe, false, {"damage_mult": 0.5})
	if not is_equal_approx(weak.damage, light.damage * 0.5):
		return "damage_mult ignored"
	return "" if CombatRules.attack_profile(GameData.held_stats("redroot"), false, {}).damage == float(GameData.unarmed.damage) else "holding food should punch"


func test_guard() -> String:
	var g := CombatRules.guard(20.0, 0.5, true, 100.0)
	if not is_equal_approx(g.damage, 10.0) or g.broken:
		return "guard should halve the hit"
	if CombatRules.guard(20.0, 0.5, false, 100.0).damage != 20.0:
		return "a hit from behind ignores guard"
	var b := CombatRules.guard(20.0, 0.5, true, 5.0)
	return "" if b.broken and b.damage > 10.0 and b.damage < 20.0 else "low stamina should break the guard"


func test_facing() -> String:
	if not CombatRules.is_facing(Vector3.ZERO, Vector3(0, 0, -1), Vector3(1, 0, -5)):
		return "should face a source in front"
	return "" if not CombatRules.is_facing(Vector3.ZERO, Vector3(0, 0, -1), Vector3(0, 0, 5)) else "should not face a source behind"


func test_fall_damage() -> String:
	var rules := {"safe_mps": 9.0, "damage_per_mps": 5.0}
	if CombatRules.fall_damage(8.0 * 4.0, rules) != 0.0:
		return "safe fall hurt"
	return "" if is_equal_approx(CombatRules.fall_damage(13.0 * 4.0, rules), 20.0) else "13 m/s should deal 20"


func test_knockback_pushes_away_and_up() -> String:
	var v := CombatRules.knockback(Vector3.ZERO, Vector3(4, 0, 0), 10.0, 2.0)
	return "" if v.x > 0.0 and v.y > 0.0 and is_zero_approx(v.z) else "bad knockback %s" % v
