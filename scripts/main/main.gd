extends Node3D
## Entry point: atmosphere, HUD, start menu, and the world node.
## The scene is built in code so it stays readable in diffs and easy to change.

const WorldScript := preload("res://scripts/multiplayer/world_authority.gd")

var world: Node3D
var _hud: Label
var _message: Label
var _message_timer := 0.0
var _menu: Control


func _ready() -> void:
	get_tree().auto_accept_quit = false
	_build_atmosphere()
	world = WorldScript.new()
	world.name = "World" # RPC path must match on every peer: /root/Main/World
	add_child(world)
	world.message.connect(_show_message)
	_build_hud()
	Net.session_started.connect(_on_session_started)
	Net.session_failed.connect(_on_session_failed)

	var args := Net.parse_args()
	match args.mode:
		"single": Net.start_single_player()
		"host": Net.host(args.port)
		"join": Net.join(args.address, args.port)
		_: _build_menu()


func _on_session_started(is_server: bool) -> void:
	if _menu:
		_menu.visible = false
	if is_server:
		world.start_as_server()
		_show_message("World ready. Find the old stone circle; its glowing inscription teaches a word.")
	else:
		_show_message("Connected. Loading the host's world...")


func _on_session_failed(reason: String) -> void:
	_show_message(reason)
	if _menu:
		_menu.visible = true


func _process(delta: float) -> void:
	_message_timer -= delta
	_message.modulate.a = clampf(_message_timer, 0.0, 1.0)
	var me = world.players.get_node_or_null(str(multiplayer.get_unique_id())) if multiplayer.has_multiplayer_peer() else null
	if me == null:
		_hud.text = ""
		return
	_hud.text = "View [V]: %s    Controls: F1    %d players" % [
		"third person" if me.third_person else "first person", world.players.get_child_count()]


func _show_message(text: String) -> void:
	_message.text = text
	_message_timer = 6.0


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		world.save_world()
		# Chunk saving is asynchronous; give it a moment. The edit journal is
		# already flushed, so nothing is lost even if this is cut short.
		await get_tree().create_timer(0.5).timeout
		get_tree().quit()


# --- Look --------------------------------------------------------------------

func _build_atmosphere() -> void:
	# A living world: clear, breathable sky with a warm horizon, not a dust bowl.
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color("#4f86c6")
	sky_mat.sky_horizon_color = Color("#d9c6b0")
	sky_mat.ground_horizon_color = Color("#9a8a72")
	sky_mat.ground_bottom_color = Color("#3b3a2c")
	sky_mat.sun_angle_max = 20.0
	var sky := Sky.new()
	sky.sky_material = sky_mat

	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.ssao_enabled = true          # small voxels read much better with AO
	env.ssao_radius = 2.0
	env.glow_enabled = true          # makes inscriptions glow
	env.glow_bloom = 0.1
	env.fog_enabled = true
	env.fog_light_color = Color("#bcd0dc")
	env.fog_density = 0.0015
	env.fog_aerial_perspective = 0.4
	var we := WorldEnvironment.new()
	we.environment = env
	we.add_to_group("world_env") # the sim's day and night clock drives these
	add_child(we)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-38, 35, 0)
	sun.light_color = Color("#fff1dc")
	sun.light_energy = 1.4
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 600.0
	sun.add_to_group("sun")
	add_child(sun)


# --- UI ----------------------------------------------------------------------

func _build_hud() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	var cross := Label.new()
	cross.text = "+"
	cross.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	cross.add_theme_font_size_override("font_size", 22)
	layer.add_child(cross)
	_hud = Label.new()
	_hud.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	_hud.position = Vector2(16, -64)
	_hud.grow_vertical = Control.GROW_DIRECTION_BEGIN
	_hud.add_theme_color_override("font_shadow_color", Color.BLACK)
	layer.add_child(_hud)
	_message = Label.new()
	_message.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	_message.position.y = 40
	_message.grow_horizontal = Control.GROW_DIRECTION_BOTH
	_message.add_theme_font_size_override("font_size", 20)
	_message.add_theme_color_override("font_shadow_color", Color.BLACK)
	layer.add_child(_message)


func _build_menu() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	var layer := CanvasLayer.new()
	add_child(layer)
	var box := VBoxContainer.new()
	box.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	box.custom_minimum_size = Vector2(320, 0)
	box.add_theme_constant_override("separation", 10)
	layer.add_child(box)
	_menu = box

	var title := Label.new()
	title.text = "ARUUN\nThe First Stone"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.add_theme_font_size_override("font_size", 28)
	box.add_child(title)
	var single := Button.new()
	single.text = "Play alone"
	single.pressed.connect(Net.start_single_player)
	box.add_child(single)
	var host := Button.new()
	host.text = "Host co-op (port %d)" % Net.DEFAULT_PORT
	host.pressed.connect(func(): Net.host())
	box.add_child(host)
	var address := LineEdit.new()
	address.placeholder_text = "Host address, e.g. 192.168.1.20"
	box.add_child(address)
	var join := Button.new()
	join.text = "Join co-op"
	join.pressed.connect(func():
		var a := address.text.strip_edges()
		Net.join(a if a != "" else "127.0.0.1"))
	box.add_child(join)
