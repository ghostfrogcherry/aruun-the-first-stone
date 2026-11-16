extends RefCounted

var words := {"word_rise": {"id": "word_rise", "effect": "raise_column", "range_voxels": 48,
	"params": {"height": 3, "radius": 0, "block": "rust_rock"}}}
var effects := {"raise_column": {"id": "raise_column", "max_height": 24, "max_radius": 4}}
var all_air := func(_p: Vector3i) -> bool: return true


func test_lexicon_is_valid() -> String:
	var problems := LanguageInterpreter.validate_lexicon(words, effects)
	return "" if problems.is_empty() else ", ".join(problems)


func test_lexicon_catches_bad_bounds() -> String:
	var bad := words.duplicate(true)
	bad.word_rise.params.height = 999
	return "" if not LanguageInterpreter.validate_lexicon(bad, effects).is_empty() else "height 999 accepted"


func test_unlearned_word_refused() -> String:
	var r := LanguageInterpreter.interpret("word_rise", PackedStringArray(), words, Vector3.ZERO, Vector3i(5, 0, 0), all_air)
	return "" if r.get("error") == "word_not_learned" else "expected word_not_learned"


func test_out_of_range_refused() -> String:
	var r := LanguageInterpreter.interpret("word_rise", PackedStringArray(["word_rise"]), words, Vector3.ZERO, Vector3i(500, 0, 0), all_air)
	return "" if r.get("error") == "out_of_range" else "expected out_of_range"


func test_raise_column_edits() -> String:
	var r := LanguageInterpreter.interpret("word_rise", PackedStringArray(["word_rise"]), words, Vector3.ZERO, Vector3i(5, 0, 0), all_air)
	if r.has("error"):
		return r.error
	return "" if r.edits.size() == 3 and r.edits[0][0] == Vector3i(5, 1, 0) else "unexpected edits %s" % [r.edits]
