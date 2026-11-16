extends Node
## Loads authored content (blocks, language, structures) from res://content.
## Content is data, not code: add a block or word by editing JSON, then run
## tools/content_validator/validate.py.

const BLOCKS_PATH := "res://content/blocks/blocks.json"
const LEXICON_PATH := "res://content/language/lexicon.json"
const STRUCTURES := {
	"first_stone_circle": "res://content/structures/first_stone.json",
}

var blocks: Array = []            # index == block id
var block_ids: Dictionary = {}    # key -> id
var placeable: PackedInt32Array = []
var words: Dictionary = {}        # word id -> definition
var effects: Dictionary = {}      # effect id -> definition


func _ready() -> void:
	_load_blocks()
	_load_lexicon()


func block_id(key: String) -> int:
	return block_ids.get(key, -1)


func block(id: int) -> Dictionary:
	if id < 0 or id >= blocks.size():
		return {}
	return blocks[id]


func is_solid(id: int) -> bool:
	return block(id).get("solid", false)


## True if players and debris bump into it (grass, flowers and water don't).
func collides(id: int) -> bool:
	var b := block(id)
	return b.get("solid", false) and b.get("collision", true)


## Returns { "id": String, "voxels": Array[[x, y, z, block_id]] }.
## Ids not listed in STRUCTURES are .vox buildings and trees from the art pipeline
## (content/structures/structures.json), which come back in the same shape.
func load_structure(structure_id: String) -> Dictionary:
	if not STRUCTURES.has(structure_id):
		return StructureLoader.load_voxels(structure_id, blocks)
	var data := _read_json(STRUCTURES[structure_id])
	var out: Array = []
	for v in data.get("voxels", []):
		out.append([int(v[0]), int(v[1]), int(v[2]), block_id(v[3])])
	return {"id": structure_id, "voxels": out}


func _load_blocks() -> void:
	var data := _read_json(BLOCKS_PATH)
	var list: Array = data.get("blocks", [])
	blocks.resize(list.size())
	for b in list:
		blocks[int(b.id)] = b
		block_ids[b.key] = int(b.id)
	for key in data.get("placeable", []):
		placeable.append(block_id(key))


func _load_lexicon() -> void:
	var data := _read_json(LEXICON_PATH)
	for w in data.get("words", []):
		words[w.id] = w
	for e in data.get("effects", []):
		effects[e.id] = e


func _read_json(path: String) -> Dictionary:
	var text := FileAccess.get_file_as_string(path)
	var parsed = JSON.parse_string(text)
	if typeof(parsed) != TYPE_DICTIONARY:
		push_error("ContentDB: could not parse %s" % path)
		return {}
	return parsed
