extends SceneTree


func _initialize() -> void:
	var arguments := OS.get_cmdline_user_args()
	if arguments.is_empty():
		push_error("Usage: -- <res://character.glb> [required bone ...]")
		quit(2)
		return

	var scene_path := arguments[0]
	var packed := load(scene_path) as PackedScene
	if packed == null:
		push_error("Unable to load character scene: %s" % scene_path)
		quit(3)
		return

	var root := packed.instantiate()
	var meshes: Array[MeshInstance3D] = []
	var skeletons: Array[Skeleton3D] = []
	collect_nodes(root, meshes, skeletons)
	if meshes.is_empty() or skeletons.is_empty():
		push_error("Character must contain visible meshes and a skeleton.")
		root.free()
		quit(4)
		return

	var bone_names: Dictionary = {}
	for skeleton in skeletons:
		for index in range(skeleton.get_bone_count()):
			bone_names[skeleton.get_bone_name(index)] = true

	var missing: Array[String] = []
	for bone_name in arguments.slice(1):
		if not bone_names.has(StringName(bone_name)):
			missing.append(bone_name)

	print("CHARACTER_SCENE=%s" % scene_path)
	print("MESHES=%d" % meshes.size())
	print("SKELETONS=%d" % skeletons.size())
	print("BONES=%d" % bone_names.size())
	if not missing.is_empty():
		push_error("Missing required bones: %s" % ", ".join(missing))
		root.free()
		quit(5)
		return

	print("CHARACTER_VALIDATION_OK")
	root.free()
	quit(0)


func collect_nodes(
	node: Node,
	meshes: Array[MeshInstance3D],
	skeletons: Array[Skeleton3D]
) -> void:
	if node is MeshInstance3D:
		meshes.append(node)
	elif node is Skeleton3D:
		skeletons.append(node)
	for child in node.get_children():
		collect_nodes(child, meshes, skeletons)
