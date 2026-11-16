extends SceneTree
## Minimal test runner (no plugins needed).
## Run:  godot --headless --import && godot --headless -s tests/run_tests.gd

var _failures := 0
var _count := 0


func _initialize() -> void:
	for path in ["res://tests/unit/test_floating_pieces.gd", "res://tests/unit/test_edit_rules.gd",
			"res://tests/unit/test_language.gd", "res://tests/unit/test_art_pipeline.gd",
			"res://tests/unit/test_player_strength.gd"]:
		var suite = load(path).new()
		for m in suite.get_method_list():
			if String(m.name).begins_with("test_"):
				_count += 1
				var err: String = suite.call(m.name)
				if err != "":
					_failures += 1
					printerr("FAIL %s::%s  %s" % [path.get_file(), m.name, err])
	print("%d tests, %d failed" % [_count, _failures])
	quit(1 if _failures > 0 else 0)
