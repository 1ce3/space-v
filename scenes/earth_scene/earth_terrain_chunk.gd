@tool
class_name EarthTerrainChunk
extends MeshInstance3D

# Mesh + tree placement are computed on a worker thread; the main thread only uploads (commit()).

var chunk_coord: Vector2i
var chunk_size: int
var grid_scale: float
var height_multiplier: float
var noise: FastNoiseLite
var flat_radius: float = 150.0
var flat_blend: float = 100.0
## Ground level inside the flat zone (m). Below 0 so the launch pad (top at y = 0) always stands above the ground.
var flat_height: float = -0.6

# Trees (set by the manager before setup()).
var tree_models: Array = []       # per model: Array of {mesh: Mesh, xform: Transform3D}
var trees_per_chunk: int = 0
var tree_scale_min: float = 0.8
var tree_scale_max: float = 1.4
var tree_clear_radius: float = 260.0
var tree_seed: int = 1
var fade_start: float = 8000.0
var fade_end: float = 20000.0

var _origin_x: float
var _origin_z: float
var _task_id: int = -1
var _arrays: Array = []
var _tree_xf: Array = []          # per model: Array[Transform3D] (chunk local)
var _committed: bool = false

func setup(coord: Vector2i, size: int, scale_val: float, height_mult: float, noise_res: FastNoiseLite, mat: Material, threaded: bool = true):
	chunk_coord = coord
	chunk_size = size
	grid_scale = scale_val
	height_multiplier = height_mult
	noise = noise_res
	material_override = mat

	_origin_x = chunk_coord.x * chunk_size * grid_scale
	_origin_z = chunk_coord.y * chunk_size * grid_scale
	position = Vector3(_origin_x, 0, _origin_z)

	if threaded:
		_task_id = WorkerThreadPool.add_task(_build_arrays)
	else:
		_build_arrays()
		commit()

## True when the worker thread finished and the mesh can be uploaded.
func is_build_done() -> bool:
	return _committed or _task_id == -1 or WorkerThreadPool.is_task_completed(_task_id)

## Uploads the prepared arrays (main thread, cheap).
func commit():
	if _committed:
		return
	_wait_task()
	_committed = true
	if not _arrays.is_empty():
		var m := ArrayMesh.new()
		m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, _arrays)
		mesh = m
		_arrays = []
	if not Engine.is_editor_hint() and fade_end > 0.0:
		visibility_range_end = fade_end
		visibility_range_end_margin = maxf(1.0, fade_end - fade_start)
		visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
	_commit_trees()

func _commit_trees():
	for m in range(_tree_xf.size()):
		var list: Array = _tree_xf[m]
		if list.is_empty():
			continue
		for entry in tree_models[m]:
			var mm := MultiMesh.new()
			mm.transform_format = MultiMesh.TRANSFORM_3D
			mm.mesh = entry["mesh"]
			mm.instance_count = list.size()
			for k in range(list.size()):
				mm.set_instance_transform(k, list[k] * entry["xform"])
			var mmi := MultiMeshInstance3D.new()
			mmi.multimesh = mm
			if not Engine.is_editor_hint() and fade_end > 0.0:
				mmi.visibility_range_end = fade_end
				mmi.visibility_range_end_margin = maxf(1.0, fade_end - fade_start)
				mmi.visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF
			add_child(mmi)
	_tree_xf = []

func _wait_task():
	if _task_id != -1:
		WorkerThreadPool.wait_for_task_completion(_task_id)
		_task_id = -1

func _exit_tree():
	_wait_task()

func _build_arrays():
	if not noise:
		return
	var n := chunk_size + 1
	var heights := PackedFloat32Array()
	heights.resize(n * n)
	for z in range(n):
		for x in range(n):
			heights[z * n + x] = _get_height(_origin_x + x * grid_scale, _origin_z + z * grid_scale)

	var count := chunk_size * chunk_size * 6
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	verts.resize(count)
	normals.resize(count)
	colors.resize(count)
	var i := 0
	for z in range(chunk_size):
		for x in range(chunk_size):
			var p1 := Vector3(x * grid_scale, heights[z * n + x], z * grid_scale)
			var p2 := Vector3((x + 1) * grid_scale, heights[z * n + x + 1], z * grid_scale)
			var p3 := Vector3(x * grid_scale, heights[(z + 1) * n + x], (z + 1) * grid_scale)
			var p4 := Vector3((x + 1) * grid_scale, heights[(z + 1) * n + x + 1], (z + 1) * grid_scale)
			i = _put_tri(verts, normals, colors, i, p1, p2, p3)
			i = _put_tri(verts, normals, colors, i, p2, p4, p3)

	_arrays.resize(Mesh.ARRAY_MAX)
	_arrays[Mesh.ARRAY_VERTEX] = verts
	_arrays[Mesh.ARRAY_NORMAL] = normals
	_arrays[Mesh.ARRAY_COLOR] = colors
	_place_trees()

func _place_trees():
	if tree_models.is_empty() or trees_per_chunk <= 0:
		return
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(Vector3i(chunk_coord.x, chunk_coord.y, tree_seed))
	_tree_xf.resize(tree_models.size())
	for m in range(tree_models.size()):
		_tree_xf[m] = []
	var extent := chunk_size * grid_scale
	for t in range(trees_per_chunk):
		var lx := rng.randf() * extent
		var lz := rng.randf() * extent
		var wx := _origin_x + lx
		var wz := _origin_z + lz
		var model := rng.randi() % tree_models.size()
		var s := rng.randf_range(tree_scale_min, tree_scale_max)
		var yaw := rng.randf() * TAU
		if Vector2(wx, wz).length() < tree_clear_radius:
			continue
		var y := _get_height(wx, wz) - 0.3 * s
		var basis := Basis(Vector3.UP, yaw).scaled(Vector3(s, s, s))
		_tree_xf[model].append(Transform3D(basis, Vector3(lx, y, lz)))

func _put_tri(verts: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray, i: int, v1: Vector3, v2: Vector3, v3: Vector3) -> int:
	var normal := (v2 - v1).cross(v3 - v1).normalized()
	verts[i] = v1
	verts[i + 1] = v2
	verts[i + 2] = v3
	normals[i] = normal
	normals[i + 1] = normal
	normals[i + 2] = normal
	# One color per face gives the low-poly faceted look.
	var c := _get_earth_color((v1.y + v2.y + v3.y) / 3.0)
	var r := fposmod(sin((v1.x + _origin_x) * 12.9898 + (v1.z + _origin_z) * 78.233 + v2.x * 3.1 + v3.z * 1.7) * 43758.5453, 1.0)
	c = c * (0.9 + 0.2 * r)
	c.a = 1.0
	colors[i] = c
	colors[i + 1] = c
	colors[i + 2] = c
	return i + 3

## Rolling hills, flattened around the launch site at the origin.
func _get_height(wx: float, wz: float) -> float:
	var h := noise.get_noise_2d(wx, wz) * height_multiplier
	var d := Vector2(wx, wz).length()
	var flat := smoothstep(flat_radius, flat_radius + flat_blend, d)
	return lerp(flat_height, h, flat)

func _get_earth_color(height: float) -> Color:
	var low_color = Color(0.30, 0.56, 0.17)   # bright grass
	var mid_color = Color(0.45, 0.72, 0.22)   # light grass
	var high_color = Color(0.68, 0.78, 0.34)  # sunlit hilltop

	var t = clamp((height + height_multiplier * 0.5) / height_multiplier, 0.0, 1.0)
	if t < 0.5:
		return low_color.lerp(mid_color, t * 2.0)
	else:
		return mid_color.lerp(high_color, (t - 0.5) * 2.0)
