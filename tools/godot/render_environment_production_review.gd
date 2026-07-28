extends SceneTree


const OUTPUT := "C:/Users/Kelly/Documents/Living Realms/artifacts/environment-previews/production-environment-review.png"


func _initialize() -> void:
	var stage := Node3D.new()
	root.add_child(stage)

	var world_environment := WorldEnvironment.new()
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("171b18")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color("bfd0bc")
	environment.ambient_light_energy = 0.34
	world_environment.environment = environment
	stage.add_child(world_environment)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48.0, -28.0, 0.0)
	sun.light_color = Color("ffe0aa")
	sun.light_energy = 1.05
	sun.shadow_enabled = true
	stage.add_child(sun)

	var ground := MeshInstance3D.new()
	var ground_mesh := PlaneMesh.new()
	ground_mesh.size = Vector2(32.0, 28.0)
	ground_mesh.material = _material(Color("35432d"), 0.98)
	ground.mesh = ground_mesh
	stage.add_child(ground)

	_add_asset(stage, "res://Assets/Environment/Production/medieval-farmhouse.glb", Vector3.ZERO, 0.0)
	_add_asset(stage, "res://Assets/Environment/Production/meadow-oak.glb", Vector3(-8.0, 0.0, -1.0), -0.35)
	_add_asset(stage, "res://Assets/Environment/Production/meadow-birch.glb", Vector3(8.0, 0.0, -0.5), 0.42)
	_add_asset(stage, "res://Assets/Environment/Production/mature-broadleaf.glb", Vector3(7.5, 0.0, -7.0), -0.7)
	_add_asset(stage, "res://Assets/Environment/Production/woodland-bush.glb", Vector3(-4.7, 0.0, 4.4), 0.25)
	_add_asset(stage, "res://Assets/Environment/Production/woodland-bush.glb", Vector3(5.0, 0.0, 4.8), -0.55)
	_add_asset(stage, "res://Assets/Environment/Production/moss-rock-01.glb", Vector3(-6.5, 0.0, 5.7), 0.2)
	_add_asset(stage, "res://Assets/Environment/Production/moss-rock-02.glb", Vector3(6.4, 0.0, 5.9), -0.2)
	_add_asset(stage, "res://Assets/Environment/Production/moss-rock-03.glb", Vector3(3.6, 0.0, 6.2), 0.45)
	for index in 24:
		var angle := index * TAU / 24.0
		var radius := 6.5 + (index % 4) * 0.75
		_add_asset(
			stage,
			"res://Assets/Environment/Production/meadow-grass-clump.glb",
			Vector3(cos(angle) * radius, 0.0, sin(angle) * radius + 1.0),
			angle
		)
	_add_human_scale_figure(stage, Vector3(0.0, 0.0, 5.2))

	var camera := Camera3D.new()
	camera.position = Vector3(14.5, 9.0, 20.0)
	camera.fov = 52.0
	camera.look_at_from_position(camera.position, Vector3(0.0, 3.2, 0.8))
	stage.add_child(camera)
	camera.current = true

	for _frame in 10:
		await process_frame
	DirAccess.make_dir_recursive_absolute(OUTPUT.get_base_dir())
	var image := root.get_texture().get_image()
	var error := image.save_png(OUTPUT)
	if error != OK:
		push_error("Unable to save environment review: %s" % error_string(error))
		quit(2)
		return
	print("ENVIRONMENT_REVIEW_RENDERED path=%s" % OUTPUT)
	quit()


func _add_asset(stage: Node3D, path: String, position: Vector3, yaw: float) -> void:
	var packed := load(path) as PackedScene
	if packed == null:
		push_error("Unable to load %s" % path)
		return
	var instance := packed.instantiate() as Node3D
	instance.position = position
	instance.rotation.y = yaw
	stage.add_child(instance)


func _add_human_scale_figure(stage: Node3D, position: Vector3) -> void:
	var material := _material(Color("8a1f19"), 0.72)
	var body := MeshInstance3D.new()
	var body_mesh := CapsuleMesh.new()
	body_mesh.radius = 0.30
	body_mesh.height = 1.35
	body_mesh.material = material
	body.mesh = body_mesh
	body.position = position + Vector3(0.0, 0.88, 0.0)
	stage.add_child(body)
	var head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.22
	head_mesh.height = 0.44
	head_mesh.material = _material(Color("ba8060"), 0.78)
	head.mesh = head_mesh
	head.position = position + Vector3(0.0, 1.67, 0.0)
	stage.add_child(head)


func _material(color: Color, roughness: float) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = roughness
	return material
