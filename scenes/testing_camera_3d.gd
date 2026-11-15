extends Camera3D

@export var fly_speed: float = 50.0 
@export var world_environment: WorldEnvironment 
@export var sun_light: DirectionalLight3D 
@export var terrain_node: Node3D 

@export_group("1. Troposphere (clouds and haze)")
@export var fog_start_height: float = 83.0 
@export var fog_thick_height: float = 120.0  
@export var normal_density: float = 0.0100 
@export var thick_density: float = 0.0800  

@export_group("2. Exosphere (space, haze fades)")
@export var space_start_height: float = 130.0 
# Độ cao mà sương mù sẽ biến mất hoàn toàn
@export var space_dark_height: float = 200.0  

@export_group("3. Terrain Management")
@export var terrain_hide_height: float = 125.0 

@export_group("4. Horizon Rotation")
@export var target_z_rotation_degrees: float = 90.0 
@export var rotation_start_height: float = 150.0 
@export var rotation_distance: float = 100.0 

# --- NHÓM MỚI CHO VIỆC LÀM TỐI ---
@export_group("5. Deep Space (darken the sky)")
# Độ cao bắt đầu làm tối (Nên đặt bằng hoặc gần bằng space_dark_height để nối tiếp sau khi sương tan)
@export var darken_start_height: float = 200.0 
# Cần bay thêm bao nhiêu mét nữa thì trời đen kịt 100%?
@export var darken_distance: float = 150.0 

@export var space_top_color: Color = Color(0.0, 0.0, 0.0, 1.0) 
@export var space_horizon_color: Color = Color(0.0, 0.0, 0.0, 1.0) 
@export var space_sky_energy: float = 0.0 
@export var space_sun_energy: float = 0.0 

var initial_sky_rotation: Vector3
var initial_top_color: Color
var initial_horizon_color: Color
var initial_ground_bottom_color: Color
var initial_ground_horizon_color: Color
var initial_sky_energy: float = 1.0
var sky_mat: ProceduralSkyMaterial

func _ready() -> void:
	if world_environment and world_environment.environment:
		var env = world_environment.environment
		initial_sky_rotation = env.sky_rotation 
		initial_sky_energy = env.background_energy_multiplier
		
		if env.sky and env.sky.sky_material is ProceduralSkyMaterial:
			sky_mat = env.sky.sky_material
			initial_top_color = sky_mat.sky_top_color
			initial_horizon_color = sky_mat.sky_horizon_color
			initial_ground_bottom_color = sky_mat.ground_bottom_color
			initial_ground_horizon_color = sky_mat.ground_horizon_color

func _process(delta: float) -> void:
	if Input.is_key_pressed(KEY_SPACE): position += Vector3.UP * fly_speed * delta
	if Input.is_key_pressed(KEY_SHIFT): position -= Vector3.UP * fly_speed * delta

	if terrain_node: terrain_node.visible = position.y < terrain_hide_height

	if world_environment and world_environment.environment:
		var env = world_environment.environment
		var current_height = position.y
		
		# --- A. Mây mù (Volumetric Fog) ---
		if current_height < space_start_height:
			var fog_weight = clampf((current_height - fog_start_height) / (fog_thick_height - fog_start_height), 0.0, 1.0)
			env.volumetric_fog_density = lerp(normal_density, thick_density, fog_weight)
		else:
			var fade_weight = clampf((current_height - space_start_height) / (space_dark_height - space_start_height), 0.0, 1.0)
			env.volumetric_fog_density = lerp(thick_density, 0.0, fade_weight)
			
		# --- B. Xoay Chân Trời ---
		var current_rot_weight = 0.0
		if current_height >= rotation_start_height:
			var raw_rot_weight = clampf((current_height - rotation_start_height) / rotation_distance, 0.0, 1.0)
			current_rot_weight = 1.0 - pow(1.0 - raw_rot_weight, 3.0) # Ease-Out
		
		env.sky_rotation.z = initial_sky_rotation.z + (deg_to_rad(target_z_rotation_degrees) * current_rot_weight)
			
		# --- C. Làm tối Bầu Trời ---
		var darken_weight = 0.0
		if current_height >= darken_start_height:
			# Tính % hành trình làm tối từ 0.0 đến 1.0
			darken_weight = clampf((current_height - darken_start_height) / darken_distance, 0.0, 1.0)
			
		if sky_mat:
			sky_mat.sky_top_color = initial_top_color.lerp(space_top_color, darken_weight)
			sky_mat.sky_horizon_color = initial_horizon_color.lerp(space_horizon_color, darken_weight)
			sky_mat.ground_bottom_color = initial_ground_bottom_color.lerp(space_top_color, darken_weight)
			sky_mat.ground_horizon_color = initial_ground_horizon_color.lerp(space_horizon_color, darken_weight)
		
		env.background_energy_multiplier = lerp(initial_sky_energy, space_sky_energy, darken_weight)
		
		if sun_light:
			sun_light.light_energy = lerp(1.0, space_sun_energy, darken_weight) 
			# Giấu hẳn node Mặt trời khi trời đã tối đen (đạt 99% quá trình làm tối)
			sun_light.visible = (darken_weight < 0.99)
