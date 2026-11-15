extends MultiMeshInstance3D

@export var tree_count: int = 500
@export var area_size: Vector2 = Vector2(100, 100) # The X and Z size of your forest

func _ready():
	# Tell the MultiMesh how many trees to draw
	multimesh.instance_count = tree_count
	
	# Get the physics state for raycasting
	var space_state = get_world_3d().direct_space_state
	
	for i in range(tree_count):
		# 1. Generate random X and Z positions
		var rand_x = randf_range(-area_size.x / 2, area_size.x / 2)
		var rand_z = randf_range(-area_size.y / 2, area_size.y / 2)
		
		# 2. Shoot a raycast from the sky downward to find the terrain height
		var start_pos = Vector3(rand_x, 1000, rand_z)
		var end_pos = Vector3(rand_x, -1000, rand_z)
		
		var query = PhysicsRayQueryParameters3D.create(start_pos, end_pos)
		var result = space_state.intersect_ray(query)
		
		var final_pos = Vector3(rand_x, 0, rand_z)
		if result:
			final_pos.y = result.position.y # Snap to the terrain height
			
		# 3. Create a transform with random rotation and scale for variety
		var t = Transform3D()
		t = t.rotated(Vector3.UP, randf() * TAU) # Random Y rotation
		
		var random_scale = randf_range(0.7, 1.3) # Vary size by 30%
		t = t.scaled_local(Vector3(random_scale, random_scale, random_scale))
		
		t.origin = final_pos # Move tree to the final position
		
		# 4. Apply the transform to the specific instance
		multimesh.set_instance_transform(i, t)
