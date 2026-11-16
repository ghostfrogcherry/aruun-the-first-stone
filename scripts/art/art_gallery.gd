extends Node3D
## Walk-around gallery of every model and structure in the art set.
## Open scenes/art/gallery.tscn and press F6, or run:
##   godot res://scenes/art/gallery.tscn
## It does not need Voxel Tools, so it also works as a quick check that art loads.
##
## Fly: WASD + Q/E, hold right mouse to look, Shift to go faster.
## C: next animation clip for all creatures. Esc: back to the main menu.

const ROWS := ["items", "crops", "flora", "props", "creatures"]
const ROW_GAP := 34.0

var _cam: Camera3D
var _yaw := 0.0
var _pitch := -0.35
var _rigs: Array[VoxelRig] = []
var _clip_index := 0
var _hint: Label


func _ready() -> void:
	_build_world()
	var z := 0.0
	for category in ROWS:
		z -= _add_row(category, z) + 22.0
	_add_structures(z - 70.0)
	_cam = Camera3D.new()
	_cam.far = 2000.0
	add_child(_cam)
	_cam.position = Vector3(0, 26, 48)
	_apply_look()
	_cam.make_current()
	_build_hint()
	for arg in OS.get_cmdline_user_args(): # --shot=file.png [--cam=x,y,z,yaw,pitch] for automated checks
		if arg.begins_with("--cam="):
			var p := arg.trim_prefix("--cam=").split_floats(",")
			_cam.position = Vector3(p[0], p[1], p[2])
			_yaw = p[3]
			_pitch = p[4]
			_apply_look()
		elif arg.begins_with("--clip="):
			for rig in _rigs:
				if rig.clips().has(arg.trim_prefix("--clip=")):
					rig.play(arg.trim_prefix("--clip="))
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--shot="):
			_hint.visible = false
			await get_tree().create_timer(1.3).timeout
			await RenderingServer.frame_post_draw
			get_viewport().get_texture().get_image().save_png(arg.trim_prefix("--shot="))
			get_tree().quit()


func _add_row(category: String, z: float) -> float:
	var ids := ModelLibrary.ids(category)
	var nodes: Array[Node3D] = []
	var widths: Array[float] = []
	var total := 0.0
	var depth := 0.0
	for id in ids:
		var node := ModelLibrary.instantiate(id)
		if node == null:
			continue
		var box := ModelLibrary.mesh(id).get_aabb()
		nodes.append(node)
		widths.append(maxf(box.size.x, 6.0) + 7.0)
		total += widths[-1]
		depth = maxf(depth, box.size.z)
		if node is VoxelRig:
			_rigs.append(node)
	var x := -total * 0.5
	for i in nodes.size():
		var node := nodes[i]
		var box := ModelLibrary.mesh(ids[i]).get_aabb()
		add_child(node)
		node.position = Vector3(x + widths[i] * 0.5, 0.0 if box.position.y >= -0.01 else -box.position.y, z - depth * 0.5)
		node.rotation.y = PI # face the camera
		_label(String(ids[i]), node.position + Vector3(0, box.end.y + 2.0 + (0.0 if box.position.y >= 0 else -box.position.y), 0), 0.035)
		x += widths[i]
	_label(category.to_upper(), Vector3(-total * 0.5 - 14.0, 5.0, z - depth * 0.5), 0.09)
	return maxf(depth, 8.0)


func _add_structures(z: float) -> void:
	var x := -170.0
	for id in StructureLoader.ids():
		var file := StructureLoader.file(id)
		if file.error != "":
			continue
		var mi := MeshInstance3D.new()
		mi.mesh = ModelLibrary.mesh_from_file(file, 1.0) # terrain voxels: 1 unit each
		var box := mi.mesh.get_aabb()
		add_child(mi)
		mi.position = Vector3(x + box.size.x * 0.5, 0, z)
		mi.rotation.y = PI
		_label(String(id), mi.position + Vector3(0, box.end.y + 4.0, 0), 0.08)
		x += box.size.x + 22.0
	_label("STRUCTURES (stamped into terrain)", Vector3(-150.0, 6.0, z + 36.0), 0.09)


func _label(text: String, at: Vector3, pixel_size: float) -> void:
	var label := Label3D.new()
	label.text = text.replace("_", " ")
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.pixel_size = pixel_size
	label.font_size = 48
	label.outline_size = 12
	label.position = at
	add_child(label)


func _build_world() -> void:
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color("#4f86c6")
	sky_mat.sky_horizon_color = Color("#d9c6b0")
	sky_mat.ground_horizon_color = Color("#9a8a72")
	sky_mat.ground_bottom_color = Color("#3b3a2c")
	var sky := Sky.new()
	sky.sky_material = sky_mat
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.ssao_enabled = true
	env.glow_enabled = true
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, 28, 0)
	sun.light_color = Color("#fff1dc")
	sun.light_energy = 1.4
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 400.0
	add_child(sun)
	var ground := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(1200, 1200)
	ground.mesh = plane
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color("#4f6b3a")
	mat.roughness = 1.0
	ground.material_override = mat
	add_child(ground)


func _build_hint() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	_hint = Label.new()
	_hint.position = Vector2(16, 12)
	_hint.add_theme_color_override("font_shadow_color", Color.BLACK)
	_hint.text = "Aruun art gallery    Fly: WASD + Q/E, hold right mouse to look, Shift = fast    C: next animation    Esc: back"
	layer.add_child(_hint)


func _apply_look() -> void:
	_cam.rotation = Vector3(_pitch, _yaw, 0)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.is_mouse_button_pressed(MOUSE_BUTTON_RIGHT):
		_yaw -= event.relative.x * 0.004
		_pitch = clampf(_pitch - event.relative.y * 0.004, -1.5, 1.5)
		_apply_look()
	elif event is InputEventKey and event.pressed and not event.echo:
		if event.physical_keycode == KEY_C:
			_clip_index += 1
			for rig in _rigs:
				var names := rig.clips()
				if not names.is_empty():
					rig.play(names[_clip_index % names.size()])
		elif event.physical_keycode == KEY_ESCAPE:
			get_tree().change_scene_to_file("res://scenes/main/main.tscn")


func _process(delta: float) -> void:
	var move := Vector3.ZERO
	if Input.is_physical_key_pressed(KEY_W): move.z -= 1
	if Input.is_physical_key_pressed(KEY_S): move.z += 1
	if Input.is_physical_key_pressed(KEY_A): move.x -= 1
	if Input.is_physical_key_pressed(KEY_D): move.x += 1
	if Input.is_physical_key_pressed(KEY_E): move.y += 1
	if Input.is_physical_key_pressed(KEY_Q): move.y -= 1
	var speed := 90.0 if Input.is_physical_key_pressed(KEY_SHIFT) else 30.0
	_cam.position += _cam.global_transform.basis * move.normalized() * speed * delta
