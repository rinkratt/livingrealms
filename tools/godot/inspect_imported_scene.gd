extends SceneTree


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		push_error("Pass one res:// scene path after --.")
		quit(2)
		return
	var scene := load(args[0]) as PackedScene
	if scene == null:
		push_error("Unable to load scene: %s" % args[0])
		quit(3)
		return
	var root := scene.instantiate()
	_print_node(root, "")
	root.free()
	quit()


func _print_node(node: Node, indent: String) -> void:
	print("%s%s <%s>" % [indent, node.name, node.get_class()])
	if node is AnimationPlayer:
		var player := node as AnimationPlayer
		for library_name in player.get_animation_library_list():
			var library := player.get_animation_library(library_name)
			for animation_name in library.get_animation_list():
				var animation := library.get_animation(animation_name)
				print(
					"%s  ANIMATION library=%s name=%s length=%.3f" %
					[indent, library_name, animation_name, animation.length]
				)
	if node is MeshInstance3D:
		var mesh_instance := node as MeshInstance3D
		if mesh_instance.mesh != null:
			print("%s  AABB=%s" % [indent, mesh_instance.mesh.get_aabb()])
	for child in node.get_children():
		_print_node(child, indent + "  ")
