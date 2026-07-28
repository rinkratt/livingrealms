extends SceneTree


const OUTPUT := "C:/Users/Kelly/Documents/Living Realms/artifacts/environment-previews/npc-scale-review.png"


func _initialize() -> void:
	var stage := Node3D.new()
	root.add_child(stage)

	var world_environment := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("17191d")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("d8d2c3")
	environment.ambient_light_energy = 0.48
	world_environment.environment = environment
	stage.add_child(world_environment)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-42.0, -32.0, 0.0)
	sun.light_color = Color("ffe0b2")
	sun.light_energy = 1.15
	stage.add_child(sun)

	var ground := MeshInstance3D.new()
	var ground_mesh := PlaneMesh.new()
	ground_mesh.size = Vector2(10.0, 6.0)
	ground_mesh.material = _material(Color("3d4536"))
	ground.mesh = ground_mesh
	stage.add_child(ground)

	_add_character(
		stage,
		"res://Assets/Characters3D/elara.glb",
		Vector3(-1.25, 0.0, 0.0),
		0.98
	)
	_add_character(
		stage,
		"res://Assets/Characters3D/elowen-herbalist.glb",
		Vector3(1.25, 0.0, 0.0),
		0.5376
	)
	_add_height_marker(stage, Vector3(-3.1, 0.0, 0.0), 1.8)

	var camera := Camera3D.new()
	camera.position = Vector3(0.0, 1.65, 7.1)
	camera.fov = 42.0
	camera.look_at_from_position(camera.position, Vector3(0.0, 1.05, 0.0))
	stage.add_child(camera)
	camera.current = true

	for _frame in 12:
		await process_frame
	DirAccess.make_dir_recursive_absolute(OUTPUT.get_base_dir())
	var image := root.get_texture().get_image()
	var error := image.save_png(OUTPUT)
	if error != OK:
		push_error("Unable to save NPC scale review: %s" % error_string(error))
		quit(2)
		return
	print("NPC_SCALE_REVIEW_RENDERED path=%s" % OUTPUT)
	quit()


func _add_character(
	stage: Node3D,
	path: String,
	position: Vector3,
	uniform_scale: float
) -> void:
	var packed := load(path) as PackedScene
	if packed == null:
		push_error("Unable to load %s" % path)
		return
	var instance := packed.instantiate() as Node3D
	instance.position = position
	instance.scale = Vector3.ONE * uniform_scale
	stage.add_child(instance)


func _add_height_marker(stage: Node3D, position: Vector3, height: float) -> void:
	var material := _material(Color("d4a33e"))
	var marker := MeshInstance3D.new()
	var marker_mesh := BoxMesh.new()
	marker_mesh.size = Vector3(0.06, height, 0.06)
	marker_mesh.material = material
	marker.mesh = marker_mesh
	marker.position = position + Vector3(0.0, height * 0.5, 0.0)
	stage.add_child(marker)

	for y in [0.0, 1.0, height]:
		var tick := MeshInstance3D.new()
		var tick_mesh := BoxMesh.new()
		tick_mesh.size = Vector3(0.35, 0.035, 0.035)
		tick_mesh.material = material
		tick.mesh = tick_mesh
		tick.position = position + Vector3(0.15, y, 0.0)
		stage.add_child(tick)


func _material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.88
	return material
