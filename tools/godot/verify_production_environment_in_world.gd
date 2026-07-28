extends SceneTree


func _initialize() -> void:
	var started := Time.get_ticks_msec()
	var packed := load("res://Scenes/StonehavenValley.tscn") as PackedScene
	if packed == null:
		push_error("Unable to load Stonehaven Valley.")
		quit(2)
		return
	var world := packed.instantiate()
	root.add_child(world)
	await process_frame

	var counts := {
		"houses": 0,
		"trees": 0,
		"rocks": 0,
		"bushes": 0,
		"grass_fields": 0,
		"grass_instances": 0,
		"visible_placeholders": 0,
	}
	_count_nodes(world, counts)
	var failures: Array[String] = []
	if counts.houses != 2:
		failures.append("Expected 2 Medieval farmhouses; found %d." % counts.houses)
	if counts.trees < 20:
		failures.append("Expected production trees; found %d." % counts.trees)
	if counts.rocks < 10:
		failures.append("Expected production rocks; found %d." % counts.rocks)
	if counts.bushes < 5:
		failures.append("Expected production bushes; found %d." % counts.bushes)
	if counts.grass_fields != 5:
		failures.append("Expected the 5 currently streamed grass fields; found %d." % counts.grass_fields)
	if counts.grass_instances < 110:
		failures.append("Expected at least 110 streamed grass clumps; found %d." % counts.grass_instances)
	if counts.visible_placeholders != 0:
		failures.append("Found %d visible legacy nature placeholders." % counts.visible_placeholders)

	var elapsed := Time.get_ticks_msec() - started
	print(
		"PRODUCTION_WORLD houses=%d trees=%d rocks=%d bushes=%d grass_fields=%d grass_instances=%d build_ms=%d" %
		[
			counts.houses,
			counts.trees,
			counts.rocks,
			counts.bushes,
			counts.grass_fields,
			counts.grass_instances,
			elapsed,
		]
	)
	root.remove_child(world)
	world.free()
	await process_frame
	if not failures.is_empty():
		for failure in failures:
			push_error(failure)
		quit(1)
		return
	print("PRODUCTION_WORLD_VALIDATION=PASS")
	quit()


func _count_nodes(node: Node, counts: Dictionary) -> void:
	var name := String(node.name)
	if name.ends_with("MedievalHouse"):
		counts.houses += 1
	elif name.begins_with("ProductionTree"):
		counts.trees += 1
	elif name.begins_with("ProductionRock"):
		counts.rocks += 1
	elif name == "WoodlandBush":
		counts.bushes += 1
	elif name.begins_with("ProductionGrassField") and node is MultiMeshInstance3D:
		counts.grass_fields += 1
		var field := node as MultiMeshInstance3D
		if field.multimesh != null:
			counts.grass_instances += field.multimesh.instance_count

	if node is GeometryInstance3D and (node as GeometryInstance3D).visible:
		if (
			name.begins_with("Tree_")
			or name.begins_with("Pine_")
			or name.begins_with("Willow_")
			or name.begins_with("DeadTree_")
			or name.begins_with("Shrub_")
			or name.begins_with("ShrubFlower_")
			or name.begins_with("ValleyRock_")
			or name.begins_with("ClusteredGrassFields")
		):
			counts.visible_placeholders += 1
	for child in node.get_children():
		_count_nodes(child, counts)
