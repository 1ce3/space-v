extends Node3D
## Dynamically drives the custom atmospheric sky shader and environment fog/lighting
## based on the rocket's live altitude (0 km -> 50+ km).

@export var follow_target: Node3D
@export var directional_light: DirectionalLight3D
@export var visual_planet_radius: float = 6000000.0
@export var space_altitude: float = 50000.0

@export_group("Horizon & Aerial Perspective")
@export var low_horizon_color: Color = Color(0.76, 0.88, 0.98, 1.0)
@export var limb_glow_color: Color = Color(0.32, 0.68, 1.0, 1.0)
@export var surface_fog_begin: float = 80.0
@export var surface_fog_end: float = 420.0

var _sky_mat: ShaderMaterial

func set_target_player(target: Node3D):
	follow_target = target

func _ready():
	var we := _find_world_env(get_parent())
	if we and we.environment and we.environment.sky and we.environment.sky.sky_material is ShaderMaterial:
		_sky_mat = we.environment.sky.sky_material

func _process(_delta: float):
	if follow_target == null:
		return
	var alt: float = maxf(0.0, follow_target.global_position.y - global_position.y)
	if _sky_mat != null:
		_sky_mat.set_shader_parameter("rocket_altitude", alt)
		_sky_mat.set_shader_parameter("visual_planet_radius", visual_planet_radius)
		_sky_mat.set_shader_parameter("space_altitude", space_altitude)

func _find_world_env(node: Node) -> WorldEnvironment:
	if node == null:
		return null
	if node is WorldEnvironment:
		return node
	for child in node.get_children():
		var found := _find_world_env(child)
		if found != null:
			return found
	return null
