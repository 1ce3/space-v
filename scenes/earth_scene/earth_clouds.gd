extends Node3D
## Streaming cloud layer: clouds are hashed per grid cell around the follow target, so they appear
## anywhere the rocket flies and the same cell always has the same clouds.
## Clouds fade in when their cell is loaded and fade out before they are removed.

@export var cloud_scenes: Array[PackedScene] = []
@export var cell_size: float = 900.0
## Cells loaded around the target in each direction.
@export var cell_radius: int = 7
@export_range(0.0, 1.0) var cell_chance: float = 0.7
@export var clouds_per_cell_max: int = 2
@export var altitude_min: float = 800.0
@export var altitude_max: float = 4000.0
## Cloud width range (m).
@export var width_min: float = 350.0
@export var width_max: float = 900.0
## Squash factor on height so clouds look flat-bottomed.
@export var height_squash: float = 0.6
@export var cloud_seed: int = 7
@export var cloud_color: Color = Color(1, 1, 1, 1)
## Seconds for a cloud to fade in or out.
@export var fade_time: float = 3.0
@export_range(0.0, 1.0) var max_opacity: float = 0.95
@export var follow_target: Node3D

var _models: Array = []        # per model: Array of {mesh, xform} (unit width, centered)
var _cells: Dictionary = {}    # Vector2i -> {"nodes": Array[MeshInstance3D], "fade": float, "target": float}
var _last_cell := Vector2i(99999, 99999)
var _material: ShaderMaterial

func set_target_player(target: Node3D):
	follow_target = target
	_last_cell = Vector2i(99999, 99999)

func _ready():
	_material = ShaderMaterial.new()
	var shader := Shader.new()
	# fog_disabled keeps distant clouds white instead of melting into the ground haze.
	shader.code = "shader_type spatial;\nrender_mode fog_disabled, depth_prepass_alpha;\nuniform vec4 tint : source_color = vec4(1.0);\ninstance uniform float fade = 0.0;\nvoid fragment() { ALBEDO = tint.rgb; ROUGHNESS = 1.0; ALPHA = fade; }"
	_material.shader = shader
	_material.set_shader_parameter("tint", cloud_color)
	_prepare_models()
	_update_cells(Vector2i.ZERO)

func _process(delta):
	if follow_target != null:
		var p := follow_target.global_position - global_position
		var cell := Vector2i(floori(p.x / cell_size), floori(p.z / cell_size))
		if cell != _last_cell:
			_update_cells(cell)
	_step_fades(delta)

func _step_fades(delta: float):
	var step: float = delta / maxf(fade_time, 0.01) * max_opacity
	var dead: Array = []
	for key in _cells:
		var c: Dictionary = _cells[key]
		var f: float = c["fade"]
		var t: float = c["target"]
		if is_equal_approx(f, t):
			continue
		f = move_toward(f, t, step)
		c["fade"] = f
		for node in c["nodes"]:
			node.set_instance_shader_parameter("fade", f)
		if f <= 0.0 and t <= 0.0:
			dead.append(key)
	for key in dead:
		for node in _cells[key]["nodes"]:
			node.queue_free()
		_cells.erase(key)

func _prepare_models():
	for scene in cloud_scenes:
		if scene == null:
			continue
		var root = scene.instantiate()
		var entries: Array = []
		_collect(root, Transform3D.IDENTITY, entries)
		root.free()
		if entries.is_empty():
			continue
		var box: AABB
		var first := true
		for e in entries:
			var b: AABB = e["xform"] * e["mesh"].get_aabb()
			box = b if first else box.merge(b)
			first = false
		var width = max(box.size.x, box.size.z)
		if width <= 0.0001:
			continue
		var center := box.position + box.size * 0.5
		var norm := Transform3D(Basis.from_scale(Vector3.ONE / width), Vector3.ZERO) * Transform3D(Basis.IDENTITY, -center)
		for e in entries:
			e["xform"] = norm * e["xform"]
		_models.append(entries)

func _collect(node: Node, parent_xf: Transform3D, out: Array):
	var xf = parent_xf
	if node is Node3D:
		xf = parent_xf * node.transform
	if node is MeshInstance3D and node.mesh != null:
		out.append({"mesh": node.mesh, "xform": xf})
	for child in node.get_children():
		_collect(child, xf, out)

func _update_cells(center: Vector2i):
	_last_cell = center
	if _models.is_empty():
		return
	var needed: Dictionary = {}
	for cz in range(center.y - cell_radius, center.y + cell_radius + 1):
		for cx in range(center.x - cell_radius, center.x + cell_radius + 1):
			var key := Vector2i(cx, cz)
			needed[key] = true
			if _cells.has(key):
				_cells[key]["target"] = max_opacity  # came back into range while fading out
			else:
				_spawn_cell(key)
	for key in _cells:
		if not needed.has(key):
			_cells[key]["target"] = 0.0

func _spawn_cell(key: Vector2i):
	var nodes: Array = []
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(Vector3i(key.x, key.y, cloud_seed))
	if rng.randf() <= cell_chance:
		var count: int = rng.randi_range(1, maxi(1, clouds_per_cell_max))
		for k in range(count):
			var pos := Vector3((key.x + rng.randf()) * cell_size, rng.randf_range(altitude_min, altitude_max), (key.y + rng.randf()) * cell_size)
			var w := rng.randf_range(width_min, width_max)
			var model := rng.randi() % _models.size()
			var basis := Basis(Vector3.UP, rng.randf() * TAU).scaled(Vector3(w, w * height_squash, w))
			var cloud_xf := Transform3D(basis, pos)
			for e in _models[model]:
				var mi := MeshInstance3D.new()
				mi.mesh = e["mesh"]
				mi.material_override = _material
				mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
				mi.transform = cloud_xf * e["xform"]
				add_child(mi)
				mi.set_instance_shader_parameter("fade", 0.0)
				nodes.append(mi)
	_cells[key] = {"nodes": nodes, "fade": 0.0, "target": max_opacity}
