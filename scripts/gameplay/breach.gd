extends Node3D
## A closing Occlusion breach: where the player came through on arrival.
## A bright, shrinking sphere of light. Inside it Earth's gravity still holds
## (an Area3D gravity override), so the first steps feel heavy until it closes.
## Cosmetic and local: every peer plays its own when it arrives.

const RADIUS := 10.0          # voxels (2.5 m)
const HOLD := 2.5             # seconds fully open
const CLOSE := 5.0            # seconds to shrink away

var _light: OmniLight3D
var _shell: MeshInstance3D
var _area: Area3D
var _shape: SphereShape3D
var _mat: StandardMaterial3D
var _t := 0.0


func _ready() -> void:
	_mat = StandardMaterial3D.new()
	_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	_mat.albedo_color = Color(0.75, 0.95, 1.0, 0.35)
	var sphere := SphereMesh.new()
	sphere.radius = RADIUS
	sphere.height = RADIUS * 2.0
	_shell = MeshInstance3D.new()
	_shell.mesh = sphere
	_shell.material_override = _mat
	_shell.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(_shell)

	_light = OmniLight3D.new()
	_light.light_color = Color(0.7, 0.95, 1.0)
	_light.light_energy = 6.0
	_light.omni_range = RADIUS * 4.0
	add_child(_light)

	_area = Area3D.new()
	_area.gravity_space_override = Area3D.SPACE_OVERRIDE_REPLACE
	_area.gravity = WorldConfig.EARTH_GRAVITY
	_shape = SphereShape3D.new()
	_shape.radius = RADIUS
	var cs := CollisionShape3D.new()
	cs.shape = _shape
	_area.add_child(cs)
	add_child(_area)


func _process(delta: float) -> void:
	_t += delta
	var k := 1.0 - clampf((_t - HOLD) / CLOSE, 0.0, 1.0) # 1 open -> 0 closed
	var flicker := 1.0 + 0.08 * sin(_t * 23.0) * k
	_shell.scale = Vector3.ONE * maxf(k * flicker, 0.001)
	_shape.radius = maxf(RADIUS * k, 0.01)
	_mat.albedo_color.a = 0.35 * k
	_light.light_energy = 6.0 * k * flicker
	if k <= 0.0:
		queue_free()
