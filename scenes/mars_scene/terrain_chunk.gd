@tool
class_name TerrainChunk
extends MeshInstance3D

# Mesh data is computed on a worker thread; the main thread only uploads it (commit()).

var chunk_coord: Vector2i
var chunk_size: int
var grid_scale: float
var height_multiplier: float
var noise: FastNoiseLite
var fade_start: float = 8000.0
var fade_end: float = 20000.0

var _origin_x: float
var _origin_z: float
var _task_id: int = -1
var _arrays: Array = []
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
	if _arrays.is_empty():
		return
	var m := ArrayMesh.new()
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, _arrays)
	mesh = m
	_arrays = []
	if not Engine.is_editor_hint() and fade_end > 0.0:
		visibility_range_end = fade_end
		visibility_range_end_margin = maxf(1.0, fade_end - fade_start)
		visibility_range_fade_mode = GeometryInstance3D.VISIBILITY_RANGE_FADE_SELF

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
	# Height grid: each vertex is sampled once instead of ~6 times.
	var heights := PackedFloat32Array()
	heights.resize(n * n)
	for z in range(n):
		for x in range(n):
			var wx := _origin_x + x * grid_scale
			var wz := _origin_z + z * grid_scale
			heights[z * n + x] = noise.get_noise_2d(wx, wz) * height_multiplier + _get_procedural_crater(wx, wz)

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

func _put_tri(verts: PackedVector3Array, normals: PackedVector3Array, colors: PackedColorArray, i: int, v1: Vector3, v2: Vector3, v3: Vector3) -> int:
	var normal := (v2 - v1).cross(v3 - v1).normalized()
	verts[i] = v1
	verts[i + 1] = v2
	verts[i + 2] = v3
	normals[i] = normal
	normals[i + 1] = normal
	normals[i + 2] = normal
	var c := _get_mars_color((v1.y + v2.y + v3.y) / 3.0)
	var r := fposmod(sin((v1.x + _origin_x) * 12.9898 + (v1.z + _origin_z) * 78.233 + v2.x * 3.1 + v3.z * 1.7) * 43758.5453, 1.0)
	c = c * (0.9 + 0.2 * r)
	c.a = 1.0
	colors[i] = c
	colors[i + 1] = c
	colors[i + 2] = c
	return i + 3

func _get_procedural_crater(wx: float, wz: float) -> float:
	var cell_size = 80.0
	var cell_x = floor(wx / cell_size)
	var cell_z = floor(wz / cell_size)

	var h = abs(int(cell_x * 73856093) ^ int(cell_z * 19349663))
	if h % 10 > 5:
		return 0.0

	var center_x = (cell_x + 0.5 + ((h % 100) / 100.0 - 0.5) * 0.4) * cell_size
	var center_z = (cell_z + 0.5 + (((h / 100) % 100) / 100.0 - 0.5) * 0.4) * cell_size
	var radius = 12.0 + (h % 15)
	var depth = 4.0 + (h % 5)

	var dist = Vector2(wx, wz).distance_to(Vector2(center_x, center_z))
	if dist < radius:
		var norm_dist = dist / radius
		var crater_shape = pow(norm_dist, 2.5) * 2.0 - 1.0
		var rim = sin(norm_dist * PI) * 0.4
		return (crater_shape + rim) * depth
	return 0.0

func _get_mars_color(height: float) -> Color:
	var low_color = Color(0.24, 0.06, 0.03)
	var mid_color = Color(0.55, 0.16, 0.07)
	var high_color = Color(0.75, 0.30, 0.12)

	var t = clamp((height + height_multiplier * 0.5) / height_multiplier, 0.0, 1.0)
	if t < 0.5:
		return low_color.lerp(mid_color, t * 2.0)
	else:
		return mid_color.lerp(high_color, (t - 0.5) * 2.0)
