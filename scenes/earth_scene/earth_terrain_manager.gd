@tool
extends Node3D

const TerrainChunkScript = preload("res://scenes/earth_scene/earth_terrain_chunk.gd")

@export var player: Node3D:
	set(value):
		player = value
		if player and is_inside_tree():
			last_player_chunk = Vector2i(99999, 99999)

@export var earth_material: Material
@export var noise: FastNoiseLite

@export_group("Chunk Settings")
@export var chunk_size: int = 24
@export var grid_scale: float = 2.0
@export var height_multiplier: float = 14.0
@export var render_distance: int = 10
## Max finished chunks uploaded to the GPU per frame (spreads the cost).
@export var max_commits_per_frame: int = 2
## Chunks fade out between these camera distances (m) as the rocket climbs toward space.
@export var chunk_fade_start: float = 8000.0
@export var chunk_fade_end: float = 20000.0

@export var regenerate: bool = false:
	set(value):
		regenerate = false
		if is_inside_tree():
			_rebuild_editor_preview()

@export_group("Launch site")
## Radius around the origin kept flat (m), then blended into hills over flat_blend.
@export var flat_radius: float = 150.0
@export var flat_blend: float = 100.0

@export_group("Trees")
@export var tree_scenes: Array[PackedScene] = []
@export var trees_per_chunk: int = 10
@export var tree_scale_min: float = 0.8
@export var tree_scale_max: float = 1.4
## No trees closer than this to the origin (m).
@export var tree_clear_radius: float = 260.0
@export var tree_seed: int = 1

var _tree_models: Array = []

## Extracts mesh + local transform from each tree scene; base is dropped to y = 0.
func _prepare_tree_models():
	_tree_models = []
	for scene in tree_scenes:
		if scene == null:
			continue
		var root = scene.instantiate()
		var entries: Array = []
		_collect_meshes(root, Transform3D.IDENTITY, entries)
		root.free()
		if entries.is_empty():
			continue
		var min_y = INF
		for e in entries:
			var box: AABB = e["xform"] * e["mesh"].get_aabb()
			min_y = min(min_y, box.position.y)
		for e in entries:
			e["xform"] = Transform3D(e["xform"].basis, e["xform"].origin - Vector3(0, min_y, 0))
		_tree_models.append(entries)

func _collect_meshes(node: Node, parent_xf: Transform3D, out: Array):
	var xf = parent_xf
	if node is Node3D:
		xf = parent_xf * node.transform
	if node is MeshInstance3D and node.mesh != null:
		out.append({"mesh": node.mesh, "xform": xf})
	for child in node.get_children():
		_collect_meshes(child, xf, out)

var active_chunks: Dictionary = {}
var pending_chunks: Array = []
var last_player_chunk: Vector2i = Vector2i(99999, 99999)

func _ready():
	_prepare_tree_models()
	if not noise:
		noise = FastNoiseLite.new()
		noise.frequency = 0.012

	if Engine.is_editor_hint():
		_rebuild_editor_preview()
	elif not player:
		# No follow target (e.g. used as a body EnvironmentScene): build around the origin.
		last_player_chunk = Vector2i(0, 0)
		update_chunks()

func _rebuild_editor_preview():
	_clear_all_chunks()
	last_player_chunk = Vector2i(0, 0)
	update_chunks()

func _process(_delta):
	_commit_ready_chunks()
	if Engine.is_editor_hint() or not player:
		return

	var chunk_world_size = chunk_size * grid_scale
	var current_chunk_x = floori(player.global_position.x / chunk_world_size)
	var current_chunk_z = floori(player.global_position.z / chunk_world_size)
	var current_coord = Vector2i(current_chunk_x, current_chunk_z)

	if current_coord != last_player_chunk:
		last_player_chunk = current_coord
		update_chunks()

func set_target_player(target_node: Node3D):
	player = target_node

func update_chunks():
	var needed: Dictionary = {}
	var to_spawn: Array[Vector2i] = []

	for z in range(-render_distance, render_distance + 1):
		for x in range(-render_distance, render_distance + 1):
			var coord = Vector2i(last_player_chunk.x + x, last_player_chunk.y + z)
			needed[coord] = true
			if not active_chunks.has(coord):
				to_spawn.append(coord)

	# Nearest chunks first so the ground under the rocket appears before the far ring.
	var center = last_player_chunk
	to_spawn.sort_custom(func(a, b): return (a - center).length_squared() < (b - center).length_squared())
	for coord in to_spawn:
		_spawn_chunk(coord)

	var coords_to_remove: Array[Vector2i] = []
	for coord in active_chunks.keys():
		if not needed.has(coord):
			coords_to_remove.append(coord)

	for coord in coords_to_remove:
		var chunk = active_chunks[coord]
		if is_instance_valid(chunk):
			chunk.queue_free()
		active_chunks.erase(coord)

func _spawn_chunk(coord: Vector2i):
	var chunk = MeshInstance3D.new()
	chunk.set_script(TerrainChunkScript)
	add_child(chunk)
	chunk.tree_models = _tree_models
	chunk.trees_per_chunk = trees_per_chunk
	chunk.tree_scale_min = tree_scale_min
	chunk.tree_scale_max = tree_scale_max
	chunk.tree_clear_radius = tree_clear_radius
	chunk.tree_seed = tree_seed
	chunk.flat_radius = flat_radius
	chunk.flat_blend = flat_blend
	chunk.fade_start = chunk_fade_start
	chunk.fade_end = chunk_fade_end
	var threaded = not Engine.is_editor_hint()
	chunk.setup(coord, chunk_size, grid_scale, height_multiplier, noise, earth_material, threaded)
	active_chunks[coord] = chunk
	if threaded:
		pending_chunks.append(chunk)

func _commit_ready_chunks():
	if pending_chunks.is_empty():
		return
	var done = 0
	var still: Array = []
	for c in pending_chunks:
		if not is_instance_valid(c) or c.is_queued_for_deletion():
			continue
		if done < max_commits_per_frame and c.is_build_done():
			c.commit()
			done += 1
		else:
			still.append(c)
	pending_chunks = still

func _clear_all_chunks():
	for chunk in active_chunks.values():
		if is_instance_valid(chunk):
			chunk.queue_free()
	active_chunks.clear()
	pending_chunks.clear()
	for child in get_children():
		child.queue_free()
