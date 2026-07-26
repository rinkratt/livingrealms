extends SceneTree


func _initialize() -> void:
	call_deferred("_run_verification")


func _run_verification() -> void:
	var scene := load("res://Scenes/StonehavenValley.tscn") as PackedScene
	if scene == null:
		push_error("Unable to load Stonehaven Valley.")
		quit(2)
		return

	var valley := scene.instantiate()
	root.add_child(valley)
	for frame in 90:
		await process_frame

	var player := valley.find_child("Alden", true, false) as Node3D
	var rat := valley.find_child("Creature-Brambletail", true, false) as Node3D
	var wolf := valley.find_child("Creature-Ashfang", true, false) as Node3D
	var goblin := valley.find_child("Creature-Skrit", true, false) as Node3D
	var chief := valley.find_child("Creature-Gorvak", true, false) as Node3D
	if player == null or rat == null or wolf == null or goblin == null or chief == null:
		push_error("The preview player and persistent combat creatures were not created.")
		quit(3)
		return

	player.global_position = Vector3(94.0, 0.08, 88.0)
	rat.global_position = Vector3(90.0, 0.08, 82.0)
	wolf.global_position = Vector3(98.0, 0.08, 80.0)
	rat.set("AiEnabled", false)
	wolf.set("AiEnabled", false)
	goblin.set("AiEnabled", false)
	chief.set("AiEnabled", false)
	wolf.call("SetPlayerSelected", true)
	for frame in 90:
		await process_frame

	for creature in [rat, wolf, goblin, chief]:
		if creature.global_position.y < -0.5:
			push_error("%s fell below its streamed world grid." % creature.name)
			quit(4)
			return
		var model := creature.find_child("CreatureModel", true, false) as Node3D
		if model == null or not model.visible:
			push_error("%s has no visible CreatureModel." % creature.name)
			quit(4)
			return
		var meshes := model.find_children("*", "MeshInstance3D", true, false)
		if meshes.is_empty():
			push_error("%s has no MeshInstance3D visuals." % creature.name)
			quit(4)
			return
		var visible_meshes := 0
		for mesh_node in meshes:
			var mesh_instance := mesh_node as MeshInstance3D
			if mesh_instance != null and mesh_instance.visible and mesh_instance.mesh != null:
				visible_meshes += 1
		if visible_meshes == 0:
			push_error("%s has no visible mesh surfaces." % creature.name)
			quit(4)
			return
		if creature == wolf:
			var target_ring := creature.find_child("PlayerTargetGroundRing", true, false) as MeshInstance3D
			if target_ring == null or not target_ring.visible:
				push_error("The selected training creature has no visible ground marker.")
				quit(4)
				return
		print(
			"TRAINING_CREATURE_VISUAL name=%s position=%s model_scale=%s meshes=%d visible_meshes=%d" %
			[creature.name, creature.global_position, model.scale, meshes.size(), visible_meshes]
		)

	var args := OS.get_cmdline_user_args()
	if not args.is_empty():
		var image := root.get_viewport().get_texture().get_image()
		var error := image.save_png(args[0])
		if error != OK:
			push_error("Unable to save training-creature screenshot: %s" % error)
			quit(5)
			return
		print("TRAINING_CREATURE_SCREENSHOT=%s" % args[0])

	valley.queue_free()
	await process_frame
	quit()
