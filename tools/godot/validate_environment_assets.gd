extends SceneTree


const ASSETS := {
	"Medieval Farmhouse": "res://Assets/Environment/Production/medieval-farmhouse.glb",
	"Meadow Oak": "res://Assets/Environment/Production/meadow-oak.glb",
	"Meadow Birch": "res://Assets/Environment/Production/meadow-birch.glb",
	"Mature Broadleaf": "res://Assets/Environment/Production/mature-broadleaf.glb",
	"Woodland Bush": "res://Assets/Environment/Production/woodland-bush.glb",
	"Meadow Grass": "res://Assets/Environment/Production/meadow-grass-clump.glb",
	"Moss Rock 01": "res://Assets/Environment/Production/moss-rock-01.glb",
	"Moss Rock 02": "res://Assets/Environment/Production/moss-rock-02.glb",
	"Moss Rock 03": "res://Assets/Environment/Production/moss-rock-03.glb",
}


func _initialize() -> void:
	var failures: Array[String] = []
	for label in ASSETS:
		var path: String = ASSETS[label]
		var packed := load(path) as PackedScene
		if packed == null:
			failures.append("%s could not be loaded" % label)
			continue
		var root := packed.instantiate()
		var result := {
			"found": false,
			"aabb": AABB(),
			"meshes": 0,
			"surfaces": 0,
		}
		_accumulate_mesh_bounds(root, Transform3D.IDENTITY, result)
		if not result.found:
			failures.append("%s contains no mesh geometry" % label)
		else:
			var bounds: AABB = result.aabb
			if not bounds.position.is_finite() or not bounds.size.is_finite():
				failures.append("%s has non-finite bounds" % label)
			print(
				"ENVIRONMENT_ASSET %s meshes=%d surfaces=%d position=%s size=%s" %
				[label, result.meshes, result.surfaces, bounds.position, bounds.size]
			)
		root.free()

	if not failures.is_empty():
		for failure in failures:
			push_error(failure)
		quit(1)
		return
	print("ENVIRONMENT_ASSET_VALIDATION=PASS")
	quit()


func _accumulate_mesh_bounds(
	node: Node,
	parent_transform: Transform3D,
	result: Dictionary
) -> void:
	var world_transform := parent_transform
	if node is Node3D:
		world_transform = parent_transform * (node as Node3D).transform
	if node is MeshInstance3D:
		var mesh_instance := node as MeshInstance3D
		if mesh_instance.mesh != null:
			var transformed := world_transform * mesh_instance.mesh.get_aabb()
			if result.found:
				result.aabb = (result.aabb as AABB).merge(transformed)
			else:
				result.aabb = transformed
				result.found = true
			result.meshes += 1
			result.surfaces += mesh_instance.mesh.get_surface_count()
	for child in node.get_children():
		_accumulate_mesh_bounds(child, world_transform, result)
