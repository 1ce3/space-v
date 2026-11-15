extends Node3D
## Places the launch tower and the NASA center, scaled to a target height with the base on y = 0.

@export var holder_scene: PackedScene
@export var holder_height: float = 70.0
@export var holder_position: Vector3 = Vector3.ZERO
@export var holder_yaw_deg: float = 0.0

@export var nasa_scene: PackedScene
@export var nasa_height: float = 35.0
@export var nasa_position: Vector3 = Vector3(170, 0, -60)
@export var nasa_yaw_deg: float = -90.0

func _ready():
	_place(holder_scene, holder_height, holder_position, holder_yaw_deg)
	_place(nasa_scene, nasa_height, nasa_position, nasa_yaw_deg)

func _place(scene: PackedScene, height: float, pos: Vector3, yaw_deg: float):
	if scene == null:
		return
	var wrapper := Node3D.new()
	add_child(wrapper)
	var inst := scene.instantiate()
	wrapper.add_child(inst)
	var box := _combined_aabb(inst, wrapper)
	if box.size.y <= 0.0001:
		return
	var s := height / box.size.y
	inst.scale = Vector3.ONE * s
	var center := box.position + box.size * 0.5
	inst.position = -Vector3(center.x, box.position.y, center.z) * s
	wrapper.position = pos
	wrapper.rotation_degrees.y = yaw_deg

func _combined_aabb(node: Node, wrapper: Node3D) -> AABB:
	var result := AABB()
	var first := true
	var inv := wrapper.global_transform.affine_inverse()
	var stack: Array = [node]
	while not stack.is_empty():
		var n: Node = stack.pop_back()
		if n is MeshInstance3D and n.mesh != null:
			var b: AABB = inv * n.global_transform * n.get_aabb()
			result = b if first else result.merge(b)
			first = false
		stack.append_array(n.get_children())
	return result
