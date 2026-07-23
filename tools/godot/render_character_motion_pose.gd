extends SceneTree


func _initialize() -> void:
	var stage := Node3D.new()
	root.add_child(stage)

	var world_environment := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("161719")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("c7ccd2")
	environment.ambient_light_energy = 0.18
	world_environment.environment = environment
	stage.add_child(world_environment)

	var key_light := DirectionalLight3D.new()
	key_light.rotation_degrees = Vector3(-42.0, -28.0, 0.0)
	key_light.light_color = Color("ffe0a2")
	key_light.light_energy = 0.72
	key_light.shadow_enabled = true
	stage.add_child(key_light)

	var fill_light := OmniLight3D.new()
	fill_light.position = Vector3(-2.2, 1.8, 2.8)
	fill_light.light_color = Color("9ebcff")
	fill_light.light_energy = 0.55
	fill_light.omni_range = 7.0
	stage.add_child(fill_light)

	var camera := Camera3D.new()
	camera.position = Vector3(0.0, 1.02, 4.25)
	camera.fov = 38.0
	camera.look_at_from_position(camera.position, Vector3(0.0, 0.95, 0.0))
	stage.add_child(camera)
	camera.current = true

	_add_posed_character(stage, "res://Assets/Characters3D/alden.glb", Vector3(-0.68, 0.0, 0.0), 0.19, 0.82)
	_add_posed_character(stage, "res://Assets/Characters3D/elara.glb", Vector3(0.68, 0.0, 0.0), 0.17, -0.82)

	for _frame in 6:
		for skeleton_node in stage.find_children("*", "Skeleton3D", true, false):
			var skeleton := skeleton_node as Skeleton3D
			var arm_width := skeleton.get_meta("motion_review_arm_width", 0.18) as float
			var stride := skeleton.get_meta("motion_review_stride", 0.0) as float
			_set_bone_direction(skeleton, "upperarm_l", Vector3(arm_width, -1.0, stride * 0.42))
			_set_bone_direction(skeleton, "upperarm_r", Vector3(-arm_width, -1.0, -stride * 0.42))
			_set_bone_direction(skeleton, "thigh_l", Vector3(0.07, -1.0, -stride * 0.34))
			_set_bone_direction(skeleton, "thigh_r", Vector3(-0.07, -1.0, stride * 0.34))
			_set_bone_local_rotation(skeleton, "calf_l", Quaternion(Vector3.RIGHT, -maxf(0.0, stride) * 0.55))
			_set_bone_local_rotation(skeleton, "calf_r", Quaternion(Vector3.RIGHT, -maxf(0.0, -stride) * 0.55))
		await process_frame

	var output_path := "C:/Users/Kelly/Documents/Living Realms/artifacts/character-motion-pose-review.png"
	DirAccess.make_dir_recursive_absolute(output_path.get_base_dir())
	var image := root.get_texture().get_image()
	var error := image.save_png(output_path)
	if error != OK:
		push_error("Unable to save pose review image: %s" % error_string(error))
		quit(2)
		return
	print("CHARACTER_MOTION_POSE_RENDERED path=%s" % output_path)
	quit(0)


func _add_posed_character(stage: Node3D, path: String, position: Vector3, arm_width: float, stride: float) -> void:
	var packed := load(path) as PackedScene
	if packed == null:
		push_error("Unable to load %s" % path)
		return
	var character := packed.instantiate() as Node3D
	character.position = position
	stage.add_child(character)
	var skeletons := character.find_children("*", "Skeleton3D", true, false)
	if skeletons.is_empty():
		push_error("No skeleton found in %s" % path)
		return
	var skeleton := skeletons[0] as Skeleton3D
	skeleton.set_meta("motion_review_arm_width", arm_width)
	skeleton.set_meta("motion_review_stride", stride)


func _set_bone_direction(skeleton: Skeleton3D, bone_name: String, desired_direction: Vector3) -> void:
	var bone_index := skeleton.find_bone(bone_name)
	if bone_index < 0:
		return
	var global_rest := skeleton.get_bone_global_rest(bone_index)
	var global_delta := Quaternion(global_rest.basis.y.normalized(), desired_direction.normalized())
	var desired_global_pose := Transform3D(Basis(global_delta) * global_rest.basis, global_rest.origin)
	skeleton.set_bone_global_pose(bone_index, desired_global_pose)


func _set_bone_local_rotation(skeleton: Skeleton3D, bone_name: String, rotation: Quaternion) -> void:
	var bone_index := skeleton.find_bone(bone_name)
	if bone_index >= 0:
		skeleton.set_bone_pose_rotation(bone_index, rotation.normalized())
