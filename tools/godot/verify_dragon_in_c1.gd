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

	var dragon := valley.find_child("WillowmereDragonReview", true, false)
	if dragon == null or not dragon.is_inside_tree():
		push_error("The C1 dragon did not enter the active region tree.")
		quit(3)
		return

	var player := _find_animation_player(dragon)
	if player == null or not player.has_animation("Idle"):
		push_error("The C1 dragon Idle animation is unavailable.")
		quit(4)
		return

	print(
		"DRAGON_C1_READY position=%s animation=%s playing=%s" %
		[dragon.global_position, player.current_animation, player.is_playing()]
	)

	var args := OS.get_cmdline_user_args()
	if not args.is_empty():
		var image := root.get_viewport().get_texture().get_image()
		var error := image.save_png(args[0])
		if error != OK:
			push_error("Unable to save dragon review screenshot: %s" % error)
			quit(5)
			return
		print("DRAGON_C1_SCREENSHOT=%s" % args[0])

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
