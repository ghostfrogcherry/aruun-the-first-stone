extends CharacterBody3D
## Player: first or third person (V toggles), digs, places, blasts, reads
## inscriptions and speaks words. All world changes are requests to the server.
## Units are voxels: 1 unit = 0.25 m, so a 1.8 m person is 7.2 units tall.

const M := WorldConfig.UNITS_PER_METER
const DevTools := preload("res://scripts/dev/dev_tools.gd")
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
## Highest ledge the player walks up without jumping: 2 voxels (0.5 m). Terrain made of
## 0.25 m voxels is full of one- and two-voxel steps, so this has to feel automatic.
const STEP_MAX := 2.0
const FLY_SPEED := 10.0 * M

var is_local := false
var world                   # world_authority.gd (untyped: its RPC methods are looked up at runtime)
var third_person := false
var selected_block := 0    # index into ContentDB.placeable
var known_words := PackedStringArray()
var target: VoxelRaycastResult

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
var _eye_drop := 0.0          # camera lag after a step up, eased back to 0

## Dev tools (scripts/dev/dev_tools.gd) switch these.
var fly := false
var noclip := false           # flying straight through terrain


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
		safe_margin = 0.05
		_build_camera()
		_build_highlight()
		var viewer := VoxelViewer.new()
		viewer.view_distance = WorldConfig.VIEW_DISTANCE_VOXELS
		add_child(viewer)
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		var dev := DevTools.new()
		dev.player = self
		add_child(dev)
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
	if not is_local:
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
	elif event.is_action_pressed("place") and target:
		world.request_place.rpc_id(1, target.previous_position, ContentDB.placeable[selected_block])
	elif event.is_action_pressed("interact") and target:
		world.request_learn.rpc_id(1, target.position)
	elif event.is_action_pressed("speak_word") and target:
		if known_words.is_empty():
			world.message.emit("You don't know any words yet. Find an inscription and press E.")
		else:
			world.request_speak.rpc_id(1, known_words[0], target.position)
	elif event.is_action_pressed("save_world"):
		world.save_world()
	elif event is InputEventKey and event.pressed and not event.echo:
		var n := int(event.keycode) - KEY_1
		if n >= 0 and n < ContentDB.placeable.size():
			selected_block = n


# --- Simulation --------------------------------------------------------------

func _physics_process(delta: float) -> void:
	if not is_local:
		return
	if global_position.y < -400.0:
		_grounded_world = false # fell out of the world: put us back
		global_position = world.spawn_point
		velocity = Vector3.ZERO
	if fly:
		_fly(delta)
		_send_state(delta)
		return
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
	elif Input.is_action_just_pressed("jump"):
		velocity.y = JUMP_SPEED

	var input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var dir := (transform.basis * Vector3(input.x, 0, input.y)).normalized()
	var speed := SPRINT if Input.is_action_pressed("sprint") else WALK
	if swimming:
		speed *= 0.5
	velocity.x = dir.x * speed
	velocity.z = dir.z * speed
	floor_snap_length = STEP_MAX + 0.2
	if dir != Vector3.ZERO and (is_on_floor() or swimming):
		_step_up(dir * maxf(speed * delta, RADIUS * 0.5))
	move_and_slide()
	_send_state(delta)


## Walks up small ledges: if the way ahead is blocked but clear STEP_MAX higher, lift
## the body onto the ledge (the camera eases up after it, so it doesn't jolt).
## Uses the real collision shape, so it works on diagonals and corners too.
func _step_up(motion: Vector3) -> void:
	var here := global_transform
	if not test_move(here, motion):
		return # nothing in the way
	var lift := Vector3(0, STEP_MAX, 0)
	if test_move(here, lift):
		return # no headroom
	var raised := here.translated(lift)
	if test_move(raised, motion):
		return # a wall, not a step
	var hit := KinematicCollision3D.new()
	if not test_move(raised.translated(motion), -lift, hit):
		return
	var rise := STEP_MAX - hit.get_travel().length()
	if rise <= 0.05 or hit.get_normal().y < 0.7:
		return
	global_position.y += rise + 0.02
	velocity.y = maxf(velocity.y, 0.0)
	_eye_drop = minf(_eye_drop + rise, STEP_MAX)


func _fly(delta: float) -> void:
	var input := Input.get_vector("move_left", "move_right", "move_forward", "move_back")
	var look := _camera.global_basis if _camera else global_basis
	var dir := look * Vector3(input.x, 0, input.y)
	if Input.is_action_pressed("jump"):
		dir.y += 1.0
	if Input.is_physical_key_pressed(KEY_CTRL):
		dir.y -= 1.0
	var speed := FLY_SPEED * (4.0 if Input.is_action_pressed("sprint") else 1.0)
	velocity = dir.normalized() * speed if dir != Vector3.ZERO else Vector3.ZERO
	if noclip:
		global_position += velocity * delta
	else:
		move_and_slide()
	_grounded_world = true # landing from a flight needs no terrain wait


func _send_state(delta: float) -> void:
	_send_timer -= delta
	if _send_timer <= 0.0:
		_send_timer = SEND_INTERVAL
		world.player_moved.rpc(global_position, rotation.y, third_person)


func _process(delta: float) -> void:
	_animate(delta)
	if not is_local:
		global_position = global_position.lerp(_remote_pos, clampf(delta * 12.0, 0.0, 1.0))
		rotation.y = lerp_angle(rotation.y, _remote_yaw, clampf(delta * 12.0, 0.0, 1.0))
		return
	if world.vt == null:
		return
	_eye_drop = move_toward(_eye_drop, 0.0, delta * maxf(8.0, _eye_drop * 10.0))
	_pivot.position.y = EYE - _eye_drop
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
		if Input.is_action_pressed("dig") and _dig_timer <= 0.0:
			_dig_timer = DIG_INTERVAL
			world.request_dig.rpc_id(1, centre, WorldConfig.DIG_RADIUS)
		if Input.is_action_just_pressed("blast") and _blast_timer <= 0.0:
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
