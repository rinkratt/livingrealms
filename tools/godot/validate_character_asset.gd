extends SceneTree


func _initialize() -> void:
	var arguments := OS.get_cmdline_user_args()
	var path := "res://Assets/Characters3D/alden.glb"
	if not arguments.is_empty():
		path = arguments[0]
	var packed := load(path) as PackedScene
	if packed == null:
		push_error("Unable to load %s" % path)
		quit(2)
		return
	var instance := packed.instantiate()
	root.add_child(instance)
	var meshes := instance.find_children("*", "MeshInstance3D", true, false)
	var skeletons := instance.find_children("*", "Skeleton3D", true, false)
	var total_surfaces := 0
	for mesh_node in meshes:
		var mesh_instance := mesh_node as MeshInstance3D
		if mesh_instance != null and mesh_instance.mesh != null:
			total_surfaces += mesh_instance.mesh.get_surface_count()
	print("CHARACTER_ASSET_OK path=%s meshes=%d surfaces=%d skeletons=%d" % [path, meshes.size(), total_surfaces, skeletons.size()])
	instance.queue_free()
	quit(0)
