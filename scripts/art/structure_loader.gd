class_name StructureLoader
extends RefCounted
## Reads buildings and trees stored as .vox (content/structures/structures.json) and turns
## them into block voxels for stamping into the terrain. Colours are matched to blocks by
## their exact colour, so structures must be painted only in block colours.

const MANIFEST_PATH := "res://content/structures/structures.json"
const DIR := "res://content/structures/"

static var _manifest := {}


static func ids() -> PackedStringArray:
	return PackedStringArray(_structures().keys())


static func has(id: String) -> bool:
	return _structures().has(id)


static func file(id: String) -> VoxFile:
	return VoxFile.load_file(DIR + String(_structures()[id].file))


## blocks: the array from blocks.json (ContentDB.blocks).
## Returns { "id": String, "voxels": Array[[x, y, z, block_id]] }; y = 0 is the floor layer.
static func load_voxels(id: String, blocks: Array) -> Dictionary:
	var out: Array = []
	if not has(id):
		push_error("StructureLoader: unknown structure '%s'" % id)
		return {"id": id, "voxels": out}
	var vox := file(id)
	var by_color := {}
	for b in blocks:
		if b.get("solid", false) and b.has("color"):
			by_color[Color(String(b.color)).to_html(false)] = int(b.id)
	var lookup := PackedInt32Array()
	lookup.resize(256)
	lookup.fill(-1)
	for i in range(1, 256):
		lookup[i] = by_color.get(vox.palette[i].to_html(false), -1)
	var missing := {}
	for m in vox.models:
		var size: Vector3i = m.size
		var origin: Vector3i = m.origin
		var cells: PackedByteArray = m.voxels
		var i := 0
		for z in size.z:
			for y in size.y:
				for x in size.x:
					var c := cells[i]
					i += 1
					if c == 0:
						continue
					if lookup[c] < 0:
						missing[vox.palette[c].to_html(false)] = true
						continue
					out.append([origin.x + x, origin.y + y, origin.z + z, lookup[c]])
	if not missing.is_empty():
		push_error("StructureLoader: '%s' uses colours that are not blocks: %s" % [id, ", ".join(missing.keys())])
	return {"id": id, "voxels": out}


static func _structures() -> Dictionary:
	if _manifest.is_empty():
		var parsed = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
		if typeof(parsed) == TYPE_DICTIONARY:
			_manifest = parsed.get("structures", {})
	return _manifest
