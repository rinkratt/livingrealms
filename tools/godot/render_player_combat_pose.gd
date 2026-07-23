extends SceneTree


func _initialize() -> void:
	var stage := Node3D.new()
	root.add_child(stage)
	_build_environment(stage)
	var floor := StaticBody3D.new()
	var floor_shape := CollisionShape3D.new()
	var floor_box := BoxShape3D.new()
	floor_box.size = Vector3(12.0, 0.2, 12.0)
	floor_shape.shape = floor_box
	floor_shape.position = Vector3(0, -0.1, 0)
	floor.add_child(floor_shape)
	stage.add_child(floor)

	var player_script := load("res://Scripts/ThirdPersonPlayer.cs")
	var alden = player_script.new()
	alden.name = "AldenCombatReview"
	alden.position = Vector3(-0.72, 0.0, 0.0)
	alden.Configure("Alden", "Vanguard")
	alden.InputEnabled = false
	stage.add_child(alden)

	var elara = player_script.new()
	elara.name = "ElaraCombatReview"
	elara.position = Vector3(0.72, 0.0, 0.0)
	elara.Configure("Elara", "Ranger")
	elara.InputEnabled = false
	stage.add_child(elara)

	var camera := Camera3D.new()
	camera.position = Vector3(2.65, 1.35, 4.8)
	camera.fov = 36.0
	stage.add_child(camera)
	camera.look_at_from_position(camera.position, Vector3(0.0, 0.95, 0.0))
	camera.current = true

	for _frame in 4:
		await process_frame

	alden.PlayCombatAttack(Vector3(-0.72, 0.8, 8.0))
	elara.PlayCombatAttack(Vector3(0.72, 0.8, 8.0))
	await create_timer(0.40).timeout
	camera.current = true
	await process_frame

	var output_path := "C:/Users/Kelly/Documents/Living Realms/artifacts/player-combat-pose-review.png"
	DirAccess.make_dir_recursive_absolute(output_path.get_base_dir())
	var image := root.get_texture().get_image()
	var error := image.save_png(output_path)
	if error != OK:
		push_error("Unable to save combat pose review image: %s" % error_string(error))
		quit(2)
		return
	print("PLAYER_COMBAT_POSE_RENDERED path=%s" % output_path)
	quit(0)


func _build_environment(stage: Node3D) -> void:
	var world_environment := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("161719")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("c7ccd2")
	environment.ambient_light_energy = 0.24
	world_environment.environment = environment
	stage.add_child(world_environment)

	var key_light := DirectionalLight3D.new()
	key_light.rotation_degrees = Vector3(-42.0, -28.0, 0.0)
	key_light.light_color = Color("ffe0a2")
	key_light.light_energy = 0.82
	key_light.shadow_enabled = true
	stage.add_child(key_light)

	var fill_light := OmniLight3D.new()
	fill_light.position = Vector3(-2.2, 1.8, 2.8)
	fill_light.light_color = Color("9ebcff")
	fill_light.light_energy = 0.65
	fill_light.omni_range = 7.0
	stage.add_child(fill_light)
