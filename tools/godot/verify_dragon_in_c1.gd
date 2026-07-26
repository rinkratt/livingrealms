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
	valley.call(
		"Configure",
		"Elara",
		"Ranger",
		1,
		0,
		100,
		100,
		"Willowmere",
		Vector3(-112.0, 0.08, 118.0),
		true
	)
	root.add_child(valley)

	for frame in 120:
		await process_frame

	var red_dragon := valley.find_child("EmberwingRedDragon", true, false) as Node3D
	var black_dragon := valley.find_child("NightveilBlackDragon", true, false) as Node3D
	if red_dragon == null or black_dragon == null:
		push_error("Both roaming C1 dragons were not created.")
		quit(3)
		return
	if not red_dragon.is_inside_tree() or not black_dragon.is_inside_tree():
		push_error("Both roaming dragons must remain in the active world tree.")
		quit(3)
		return

	for dragon in [red_dragon, black_dragon]:
		if not bool(dragon.get("RoamsWholeMap")):
			push_error("%s is not configured for all nine grids." % dragon.name)
			quit(4)
			return
		var player := _find_animation_player(dragon)
		if player == null:
			push_error("%s has no AnimationPlayer." % dragon.name)
			quit(4)
			return
		for mode in ["Idle", "Walk", "Run", "Fly"]:
			if not player.has_animation(mode):
				push_error("%s is missing the %s animation." % [dragon.name, mode])
				quit(4)
				return

	var red_color := _find_material_color(red_dragon)
	var black_color := _find_material_color(black_dragon)
	if red_color.r <= red_color.g * 2.0 or red_color.r <= red_color.b * 1.5:
		push_error("Emberwing is not visibly painted red: %s" % red_color)
		quit(5)
		return
	if max(black_color.r, max(black_color.g, black_color.b)) >= 0.15:
		push_error("Nightveil is not visibly painted black: %s" % black_color)
		quit(5)
		return

	var args := OS.get_cmdline_user_args()
	if not args.is_empty():
		var viewport_texture := root.get_viewport().get_texture()
		if viewport_texture == null:
			push_error("The active renderer did not provide a screenshot texture.")
			quit(8)
			return
		var image := viewport_texture.get_image()
		if image == null:
			push_error("The active renderer did not provide a screenshot image.")
			quit(8)
			return
		var error := image.save_png(args[0])
		if error != OK:
			push_error("Unable to save dragon review screenshot: %s" % error)
			quit(8)
			return
		print("DRAGON_C1_SCREENSHOT=%s" % args[0])

	var red_start := red_dragon.global_position
	var black_start := black_dragon.global_position
	await create_timer(7.0).timeout
	if red_dragon.global_position.distance_to(red_start) < 0.4:
		push_error("Emberwing did not begin roaming.")
		quit(6)
		return
	if black_dragon.global_position.distance_to(black_start) < 0.4:
		push_error("Nightveil did not begin roaming.")
		quit(6)
		return

	var player_character := valley.find_child("Elara", true, false) as Node3D
	if player_character != null:
		player_character.global_position = Vector3(-110.0, 0.08, -110.0)
	for frame in 20:
		await process_frame
	if not red_dragon.is_inside_tree() or not black_dragon.is_inside_tree():
		push_error("A dragon disappeared when the player crossed streamed grids.")
		quit(7)
		return

	print(
		"DRAGONS_READY red=%s mode=%s color=%s black=%s mode=%s color=%s whole_map=true" %
		[
			red_dragon.global_position,
			red_dragon.get("CurrentMode"),
			red_color,
			black_dragon.global_position,
			black_dragon.get("CurrentMode"),
			black_color
		]
	)

	valley.queue_free()
	await process_frame
	quit()


func _find_animation_player(node: Node) -> AnimationPlayer:
	if node is AnimationPlayer:
		return node as AnimationPlayer
	for child in node.get_children():
		var found := _find_animation_player(child)
		if found != null:
			return found
	return null


func _find_material_color(node: Node) -> Color:
	if node is MeshInstance3D:
		var mesh_instance := node as MeshInstance3D
		if mesh_instance.mesh != null:
			for surface in mesh_instance.mesh.get_surface_count():
				var material := mesh_instance.get_active_material(surface)
				if material is StandardMaterial3D:
					return (material as StandardMaterial3D).albedo_color
	for child in node.get_children():
		var color := _find_material_color(child)
		if color != Color(-1, -1, -1, -1):
			return color
	return Color(-1, -1, -1, -1)
