extends RefCounted
## The player's legs are Earth-strong: the same take-off speed everywhere.

const PlayerScript := preload("res://scripts/player/player.gd")


func _jump_m(gravity: float) -> float:
	return PlayerScript.JUMP_SPEED * PlayerScript.JUMP_SPEED / (2.0 * gravity) / WorldConfig.UNITS_PER_METER


func test_jump_is_high_on_aruun() -> String:
	var h := _jump_m(WorldConfig.GRAVITY)
	return "" if absf(h - 1.6) < 0.01 else "Aruun jump %.2f m, expected 1.6" % h


func test_jump_is_low_in_earth_gravity() -> String:
	var h := _jump_m(WorldConfig.EARTH_GRAVITY)
	return "" if h > 0.5 and h < 0.7 else "Earth-gravity jump %.2f m, expected about 0.6" % h
