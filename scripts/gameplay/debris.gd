class_name Debris
extends RefCounted
## Physics debris for destruction. Purely visual for now: every peer spawns
## its own copy from the same server op, so it is not synced and never turns
## back into terrain. (Later: settle large pieces back into voxels on the server.)

const CHIP_SIZE := 0.45
const MAX_CHIPS := 48
const CHIP_LIFETIME := 2.5
const PIECE_LIFETIME := 30.0

static var _chip_mesh: BoxMesh
static var _chip_shape: BoxShape3D
static var _material: StandardMaterial3D


static func _setup() -> void:
	if _material:
		return
	_material = StandardMaterial3D.new()
	_material.vertex_color_use_as_albedo = true
	_material.vertex_color_is_srgb = true
	_material.roughness = 0.95
	_chip_mesh = BoxMesh.new()
	_chip_mesh.size = Vector3.ONE * CHIP_SIZE
	_chip_shape = BoxShape3D.new()
	_chip_shape.size = Vector3.ONE * CHIP_SIZE


static func _color(id: int) -> Color:
	return Color(ContentDB.block(id).get("color", "#888888"))


## Small tumbling bits from a dig or blast.
static func spawn_chips(parent: Node3D, cells: Array, ids: PackedInt32Array) -> void:
	_setup()
	var step := maxi(1, cells.size() / MAX_CHIPS)
	for i in range(0, cells.size(), step):
		var body := RigidBody3D.new()
		var mesh := MeshInstance3D.new()
		mesh.mesh = _chip_mesh
		var mat := _material.duplicate() as StandardMaterial3D
		mat.albedo_color = _color(ids[i])
		mesh.material_override = mat
		var shape := CollisionShape3D.new()
		shape.shape = _chip_shape
		body.add_child(mesh)
		body.add_child(shape)
		body.mass = 0.05
		parent.add_child(body)
		body.global_position = Vector3(cells[i]) + Vector3(0.5, 0.5, 0.5)
		body.apply_central_impulse(Vector3(randf_range(-1, 1), randf_range(0.5, 1.5), randf_range(-1, 1)) * 0.25)
		_expire(body, CHIP_LIFETIME * randf_range(0.7, 1.3))


## Builds one mesh shaped like the cells, centred on their middle.
## Returns {"mesh": ArrayMesh, "centre": Vector3, "hull": PackedVector3Array}.
static func _piece_mesh(cells: Array, ids: PackedInt32Array) -> Dictionary:
	var lookup := {}
	var centre := Vector3.ZERO
	for i in cells.size():
		lookup[cells[i]] = ids[i]
		centre += Vector3(cells[i]) + Vector3(0.5, 0.5, 0.5)
	centre /= cells.size()
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var hull := {}
	for p in lookup:
		var base := Vector3(p) - centre
		st.set_color(_color(lookup[p]))
		for face in _FACES:
			if lookup.has(p + face.n):
				continue # hidden face
			st.set_normal(Vector3(face.n))
			var q: Array = face.q
			for k in [0, 2, 1, 0, 3, 2]: # Godot front faces are clockwise
				st.add_vertex(base + q[k])
			for corner in q:
				hull[base + corner] = true
	return {"mesh": st.commit(), "centre": centre, "hull": PackedVector3Array(hull.keys())}


## A loose chunk of world that broke off and has nowhere to land: one rigid
## body shaped like the cells, purely for show.
static func spawn_piece(parent: Node3D, cells: Array, ids: PackedInt32Array) -> void:
	_setup()
	if cells.is_empty():
		return
	var built := _piece_mesh(cells, ids)
	var body := RigidBody3D.new()
	var mesh := MeshInstance3D.new()
	mesh.mesh = built.mesh
	mesh.material_override = _material
	var shape := CollisionShape3D.new()
	var convex := ConvexPolygonShape3D.new()
	convex.points = built.hull
	shape.shape = convex
	body.add_child(mesh)
	body.add_child(shape)
	body.mass = maxf(1.0, cells.size() * 0.4)
	parent.add_child(body)
	body.global_position = built.centre
	body.apply_torque_impulse(Vector3(randf_range(-1, 1), 0, randf_range(-1, 1)) * body.mass * 0.2)
	_expire(body, PIECE_LIFETIME)


## A loose chunk that the server says will fall `drop` voxels and then become
## terrain again. Every peer plays the same fall; the server's "put" op
## arrives as it lands, so the mesh is removed just after.
static func spawn_falling(parent: Node3D, cells: Array, ids: PackedInt32Array, drop: int) -> void:
	_setup()
	if cells.is_empty():
		return
	var built := _piece_mesh(cells, ids)
	var mesh := MeshInstance3D.new()
	mesh.mesh = built.mesh
	mesh.material_override = _material
	parent.add_child(mesh)
	mesh.global_position = built.centre
	var g: float = ProjectSettings.get_setting("physics/3d/default_gravity")
	var t := Collapse.fall_time(drop, g)
	var tween := mesh.create_tween()
	tween.tween_property(mesh, "global_position:y", built.centre.y - drop, t) \
		.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tween.tween_interval(0.12)
	tween.tween_callback(mesh.queue_free)
	# Dust where it hits.
	var low: Array = []
	var low_ids := PackedInt32Array()
	var step := maxi(1, cells.size() / 12)
	for i in range(0, cells.size(), step):
		low.append(cells[i] + Vector3i(0, -drop, 0))
		low_ids.append(ids[i])
	parent.get_tree().create_timer(t).timeout.connect(func():
		if is_instance_valid(parent):
			spawn_chips(parent, low, low_ids))


static func _expire(body: Node, seconds: float) -> void:
	body.get_tree().create_timer(seconds).timeout.connect(func():
		if is_instance_valid(body):
			body.queue_free())


# Cube faces: neighbour direction + 4 corners, counter-clockwise seen from outside
# (emitted reversed above, because Godot treats clockwise as front).
static var _FACES := [
	{"n": Vector3i(1, 0, 0),  "q": [Vector3(1, 0, 0), Vector3(1, 1, 0), Vector3(1, 1, 1), Vector3(1, 0, 1)]},
	{"n": Vector3i(-1, 0, 0), "q": [Vector3(0, 0, 1), Vector3(0, 1, 1), Vector3(0, 1, 0), Vector3(0, 0, 0)]},
	{"n": Vector3i(0, 1, 0),  "q": [Vector3(0, 1, 0), Vector3(0, 1, 1), Vector3(1, 1, 1), Vector3(1, 1, 0)]},
	{"n": Vector3i(0, -1, 0), "q": [Vector3(0, 0, 1), Vector3(0, 0, 0), Vector3(1, 0, 0), Vector3(1, 0, 1)]},
	{"n": Vector3i(0, 0, 1),  "q": [Vector3(1, 0, 1), Vector3(1, 1, 1), Vector3(0, 1, 1), Vector3(0, 0, 1)]},
	{"n": Vector3i(0, 0, -1), "q": [Vector3(0, 0, 0), Vector3(0, 1, 0), Vector3(1, 1, 0), Vector3(1, 0, 0)]},
]
