extends CharacterBody3D
## Player: first or third person (V toggles), digs, places, blasts, reads
## inscriptions and speaks words. All world changes are requests to the server.
## Units are voxels: 1 unit = 0.25 m, so a 1.8 m person is 7.2 units tall.

const M := WorldConfig.UNITS_PER_METER
const HEIGHT := 1.8 * M
const RADIUS := 0.35 * M
const EYE := 1.65 * M
const WALK := 4.5 * M
const SPRINT := 7.0 * M
const JUMP_HEIGHT := 1.6 * M # on Aruun. The legs are Earth-strong: see JUMP_SPEED.
## Fixed take-off speed: an Earth human's legs don't change with the world. Gives a
## 1.6 m jump in Aruun's 0.38 g but only ~0.6 m in Earth gravity inside Occlusion sites.
static var JUMP_SPEED := sqrt(2.0 * WorldConfig.GRAVITY * JUMP_HEIGHT)
const AVATAR := "human_castaway"
const AVATAR_HEIGHT_M := 1.8   # the model is authored 2 m tall
const THIRD_PERSON_DISTANCE := 3.5 * M
const MOUSE_SENS := 0.0025
const DIG_INTERVAL := 0.18
const BLAST_RADIUS := 6.0
const BLAST_COOLDOWN := 1.0
const SEND_INTERVAL := 0.05

var is_local := false
var world                   # world_authority.gd (untyped: its RPC methods are looked up at runtime)
var third_person := false
var selected_block := 0    # index into ContentDB.placeable
var known_words := PackedStringArray()
var target: VoxelRaycastResult
var actions: Node          # player_actions.gd: hotbar, combat, items (local player only)

var _pivot: Node3D
var _arm: SpringArm3D
var _camera: Camera3D
var _visual: Node3D
var _rig: VoxelRig            # null if the avatar model is missing (box figure instead)
var _last_pos := Vector3.ZERO
var _highlight: MeshInstance3D
var _dig_timer := 0.0
var _blast_timer := 0.0
var _send_timer := 0.0
var _remote_pos := Vector3.ZERO
var _remote_yaw := 0.0
var _grounded_world := false


func _ready() -> void:
	_build_visual()
	if is_local:
		var shape := CollisionShape3D.new()
		var capsule := CapsuleShape3D.new()
		capsule.height = HEIGHT
		capsule.radius = RADIUS
		shape.shape = capsule
		shape.position.y = HEIGHT / 2.0
		add_child(shape)
		_build_camera()
		_build_highlight()
		var viewer := VoxelViewer.new()
		viewer.view_distance = WorldConfig.VIEW_DISTANCE_VOXELS
		add_child(viewer)
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		actions = preload("res://scripts/player/player_actions.gd").new()
		actions.name = "Actions"
		actions.player = self
		add_child(actions)
	else:
		collision_layer = 0
		collision_mask = 0
		_remote_pos = global_position


func _build_visual() -> void:
	_visual = Node3D.new()
	add_child(_visual)
	if ModelLibrary.has(AVATAR):
		# The stranded Earth human from the art pipeline: animated voxel parts.
		_rig = ModelLibrary.instantiate(AVATAR) as VoxelRig
		var model_h := float(ModelLibrary.info(AVATAR).get("height_m", AVATAR_HEIGHT_M))
		_rig.scale = Vector3.ONE * (AVATAR_HEIGHT_M / model_h)
		_visual.add_child(_rig)
		return
	# Fallback: segmented box figure.
	var skin := Color("#c98b6b")
	var cloth := Color("#5b6f5a") if is_local else Color("#6a4a7a")
	_add_box(_visual, Vector3(1.6, 2.8, 1.0), Vector3(0, 4.4, 0), cloth)          # torso
	_add_box(_visual, Vector3(1.2, 1.2, 1.2), Vector3(0, 6.5, 0), skin)           # head
	_add_box(_visual, Vector3(0.6, 3.0, 0.6), Vector3(-0.45, 1.5, 0), Color("#4a3a2c")) # legs
	_add_box(_visual, Vector3(0.6, 3.0, 0.6), Vector3(0.45, 1.5, 0), Color("#4a3a2c"))
	_add_box(_visual, Vector3(0.5, 2.6, 0.5), Vector3(-1.05, 4.5, 0), cloth)      # arms
	_add_box(_visual, Vector3(0.5, 2.6, 0.5), Vector3(1.05, 4.5, 0), cloth)


func _add_box(parent: Node3D, size: Vector3, pos: Vector3, color: Color) -> void:
	var m := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	box.material = mat
	m.mesh = box
	m.position = pos
	parent.add_child(m)


func _build_camera() -> void:
	_pivot = Node3D.new()
	_pivot.position.y = EYE
	add_child(_pivot)
	_arm = SpringArm3D.new()
	_arm.spring_length = 0.0
	_arm.margin = 0.5
	_arm.add_excluded_object(get_rid())
	_pivot.add_child(_arm)
	_camera = Camera3D.new()
	_camera.fov = 75
	_camera.near = 0.1
	_camera.far = 2000
	_arm.add_child(_camera)
	_camera.make_current()
	_apply_camera_mode()


func _build_highlight() -> void:
	_highlight = MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3.ONE * 1.04
	var mat := StandardMaterial3D.new()
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = Color(1, 1, 1, 0.18)
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	box.material = mat
	_highlight.mesh = box
	_highlight.top_level = true
	add_child(_highlight)


func _apply_camera_mode() -> void:
	_arm.spring_length = THIRD_PERSON_DISTANCE if third_person else 0.0
	_arm.position.x = 0.6 * M if third_person else 0.0
	_visual.visible = third_person


func learn(word_id: String) -> void:
	if not known_words.has(word_id):
		known_words.append(word_id)


func set_remote_state(pos: Vector3, yaw: float, _third: bool) -> void:
	_remote_pos = pos
	_remote_yaw = yaw


# --- Input -------------------------------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	if not is_local or actions.ui_open:
		return
	if event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		rotate_y(-event.relative.x * MOUSE_SENS)
		_pivot.rotation.x = clampf(_pivot.rotation.x - event.relative.y * MOUSE_SENS, -1.5, 1.5)
	elif event is InputEventMouseButton and event.pressed and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		get_viewport().set_input_as_handled()
	elif event.is_action_pressed("ui_cancel"):
		Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
	elif event.is_action_pressed("toggle_camera"):
		third_person = not third_person
		_apply_camera_mode()
	elif actions.is_down():
		pass # dead or downed: look around only
	elif event.is_action_pressed("place"):
		actions.secondary(target) # place a block, eat, plant, set down (guarding is held)
	elif event.is_action_pressed("interact"):
		if not actions.interact(target) and target:
			world.request_learn.rpc_id(1, target.position)
	elif event.is_action_pressed("speak_word"):
		actions.speak(target)
	elif event.is_action_pressed("save_world"):
		world.save_world()


# --- Simulation --------------------------------------------------------------

func _physics_process(delta: float) -> void:
	if not is_local:
		return
	if global_position.y < -400.0:
		_grounded_world = false # fell out of the world: put us back
		global_position = world.spawn_point
		velocity = Vector3.ZERO
	if not _grounded_world:
		# Wait until the terrain's collision under us exists, or we fall through.
		var space := get_world_3d().direct_space_state
		var hit := space.intersect_ray(PhysicsRayQueryParameters3D.create(
			global_position + Vector3(0, 4, 0), global_position - Vector3(0, 200, 0)))
		if hit.is_empty():
			return
		global_position = hit.position + Vector3(0, 0.1, 0)
		velocity = Vector3.ZERO
		_grounded_world = true
		return

	# Local gravity: Aruun's 0.38 g by default; Area3D gravity overrides (such as
	# Earth gravity inside an Occlusion / Confluence site) change it here.
	var g: float = get_gravity().length()
	var chest: int = world.vt.get_voxel(Vector3i((global_position + Vector3(0, HEIGHT * 0.5, 0)).floor()))
	var swimming := chest == ContentDB.block_id("water")
	if swimming:
		# Simple swimming: slow sinking, Space to rise, heavy drag.
		velocity.y = lerpf(velocity.y, (WALK * 0.5) if Input.is_action_pressed("jump") else -WALK * 0.15, clampf(delta * 3.0, 0.0, 1.0))
	elif not is_on_floor():
		velocity.y -= g * delta
	elif Input.is_action_just_pressed("jump") and not actions.blocks_input():
		# Fixed leg strength: the same push everywhere, so jumps fall short under Earth gravity.
		velocity.y = JUMP_SPEED * sqrt(actions.jump_mult())

	var input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	if actions.blocks_input():
		input = Vector2.ZERO
	var dir := (transform.basis * Vector3(input.x, 0, input.y)).normalized()
	var speed := SPRINT if Input.is_action_pressed("sprint") and actions.can_sprint() else WALK
	speed *= actions.speed_mult() # conditions, gravity debt, guarding
	if swimming:
		speed *= 0.5
	velocity.x = dir.x * speed
	velocity.z = dir.z * speed
	var dodge: Vector3 = actions.dodge_velocity()
	if dodge != Vector3.ZERO:
		velocity.x = dodge.x
		velocity.z = dodge.z
	floor_snap_length = 1.2
	# Small voxels: let the player walk up single-voxel (0.25 m) steps.
	if is_on_floor() and dir != Vector3.ZERO and _step_ahead(dir):
		velocity.y = maxf(velocity.y, sqrt(2.0 * g * 1.3))
	var falling := -velocity.y
	var was_airborne := not is_on_floor()
	move_and_slide()
	if was_airborne and is_on_floor() and not swimming:
		actions.landed(falling)

	_send_timer -= delta
	if _send_timer <= 0.0:
		_send_timer = SEND_INTERVAL
		world.player_moved.rpc(global_position, rotation.y, third_person)


func _step_ahead(dir: Vector3) -> bool:
	var ahead := global_position + dir * (RADIUS + 0.6)
	var foot := Vector3i(ahead.floor())
	var vt: VoxelTool = world.vt
	return ContentDB.collides(vt.get_voxel(foot)) and not ContentDB.collides(vt.get_voxel(foot + Vector3i(0, 1, 0))) \
		and not ContentDB.collides(vt.get_voxel(foot + Vector3i(0, 2, 0)))


func _process(delta: float) -> void:
	_animate(delta)
	if not is_local:
		global_position = global_position.lerp(_remote_pos, clampf(delta * 12.0, 0.0, 1.0))
		rotation.y = lerp_angle(rotation.y, _remote_yaw, clampf(delta * 12.0, 0.0, 1.0))
		return
	if world.vt == null:
		return
	var origin := _camera.global_position
	var forward := -_camera.global_basis.z
	var reach := WorldConfig.REACH_VOXELS + (THIRD_PERSON_DISTANCE if third_person else 0.0)
	target = world.vt.raycast(origin, forward, reach)
	_highlight.visible = target != null
	if target:
		_highlight.global_position = Vector3(target.position) + Vector3(0.5, 0.5, 0.5)

	_dig_timer -= delta
	_blast_timer -= delta
	if target and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		var centre := Vector3(target.position) + Vector3(0.5, 0.5, 0.5)
		if Input.is_action_pressed("dig") and _dig_timer <= 0.0 and actions.can_dig():
			_dig_timer = DIG_INTERVAL
			world.request_dig.rpc_id(1, centre, WorldConfig.DIG_RADIUS)
		if Input.is_action_just_pressed("blast") and _blast_timer <= 0.0 and not actions.blocks_input():
			_blast_timer = BLAST_COOLDOWN
			world.request_blast.rpc_id(1, centre, BLAST_RADIUS)


## Picks the avatar's clip from how fast it actually moved (works for remote players too).
func _animate(delta: float) -> void:
	if _rig == null or delta <= 0.0:
		return
	var moved := global_position - _last_pos
	_last_pos = global_position
	var speed := Vector2(moved.x, moved.z).length() / delta
	var clip := "idle"
	if speed > (WALK + SPRINT) * 0.5:
		clip = "run"
	elif speed > WALK * 0.2:
		clip = "walk"
	if is_local and Input.is_action_pressed("dig") and target:
		clip = "swing"
	if clip != _rig.current_clip:
		_rig.play(clip)
	_rig.speed_scale = clampf(speed / WALK, 0.6, 1.6) if clip == "walk" else 1.0
