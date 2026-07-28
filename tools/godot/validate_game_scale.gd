extends SceneTree


const CHARACTER_ASSETS := {
	"Alden": {
		"path": "res://Assets/Characters3D/alden.glb",
		"scale": 1.0,
		"minimum_height": 1.45,
		"maximum_height": 2.05,
	},
	"Elara": {
		"path": "res://Assets/Characters3D/elara.glb",
		"scale": 0.98,
		"minimum_height": 1.45,
		"maximum_height": 2.05,
	},
	"Elowen": {
		"path": "res://Assets/Characters3D/elowen-herbalist.glb",
		"scale": 0.5376,
		"minimum_height": 1.45,
		"maximum_height": 2.05,
	},
}

const ENVIRONMENT_ASSETS := {
	"Medieval Farmhouse": {
		"path": "res://Assets/Environment/Production/medieval-farmhouse.glb",
		"minimum": Vector3(6.0, 5.0, 6.0),
		"maximum": Vector3(10.0, 10.0, 11.0),
	},
	"Meadow Oak": {
		"path": "res://Assets/Environment/Production/meadow-oak.glb",
		"minimum": Vector3(3.0, 5.0, 3.0),
		"maximum": Vector3(9.0, 10.0, 9.0),
	},
	"Meadow Birch": {
		"path": "res://Assets/Environment/Production/meadow-birch.glb",
		"minimum": Vector3(3.0, 5.0, 3.0),
		"maximum": Vector3(10.0, 10.0, 9.0),
	},
	"Mature Broadleaf": {
		"path": "res://Assets/Environment/Production/mature-broadleaf.glb",
		"minimum": Vector3(3.0, 5.0, 3.0),
		"maximum": Vector3(9.0, 10.0, 9.0),
	},
	"Woodland Bush": {
		"path": "res://Assets/Environment/Production/woodland-bush.glb",
		"minimum": Vector3(0.8, 0.6, 0.8),
		"maximum": Vector3(3.0, 2.5, 3.0),
	},
	"Meadow Grass": {
		"path": "res://Assets/Environment/Production/meadow-grass-clump.glb",
		"minimum": Vector3(0.3, 0.2, 0.3),
		"maximum": Vector3(2.0, 1.0, 2.0),
	},
	"Moss Rock 01": {
		"path": "res://Assets/Environment/Production/moss-rock-01.glb",
		"minimum": Vector3(0.5, 0.3, 0.5),
		"maximum": Vector3(3.0, 2.0, 3.0),
	},
	"Moss Rock 02": {
		"path": "res://Assets/Environment/Production/moss-rock-02.glb",
		"minimum": Vector3(0.5, 0.3, 0.5),
		"maximum": Vector3(3.0, 2.0, 3.0),
	},
	"Moss Rock 03": {
		"path": "res://Assets/Environment/Production/moss-rock-03.glb",
		"minimum": Vector3(0.5, 0.3, 0.5),
		"maximum": Vector3(3.0, 2.0, 3.0),
	},
}


func _initialize() -> void:
	var failures: Array[String] = []
	for label in CHARACTER_ASSETS:
		var specification: Dictionary = CHARACTER_ASSETS[label]
		var bounds := _load_bounds(specification.path)
		if bounds.size == Vector3.ZERO:
			failures.append("%s contains no mesh geometry" % label)
			continue
		var scaled_height: float = bounds.size.y * float(specification.scale)
		print(
			"GAME_SCALE character=%s raw_height=%.3f scale=%.3f game_height=%.3f" %
				[label, bounds.size.y, specification.scale, scaled_height]
		)
		if scaled_height < float(specification.minimum_height) or \
				scaled_height > float(specification.maximum_height):
			failures.append(
				"%s is %.3fm tall; adult characters must remain between %.2fm and %.2fm" %
					[
						label,
						scaled_height,
						specification.minimum_height,
						specification.maximum_height,
					]
			)

	for label in ENVIRONMENT_ASSETS:
		var specification: Dictionary = ENVIRONMENT_ASSETS[label]
		var bounds := _load_bounds(specification.path)
		if bounds.size == Vector3.ZERO:
			failures.append("%s contains no mesh geometry" % label)
			continue
		print("GAME_SCALE environment=%s size=%s" % [label, bounds.size])
		var minimum: Vector3 = specification.minimum
		var maximum: Vector3 = specification.maximum
		if not _is_between(bounds.size, minimum, maximum):
			failures.append(
				"%s has size %s; expected every axis between %s and %s" %
					[label, bounds.size, minimum, maximum]
			)

	if not failures.is_empty():
		for failure in failures:
			push_error(failure)
		quit(1)
		return
	print("GAME_SCALE_VALIDATION=PASS")
	quit()


func _load_bounds(path: String) -> AABB:
	var packed := load(path) as PackedScene
	if packed == null:
		return AABB()
	var root := packed.instantiate()
	var result := {"found": false, "aabb": AABB()}
	_accumulate_mesh_bounds(root, Transform3D.IDENTITY, result)
	root.free()
	return result.aabb if result.found else AABB()


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
	for child in node.get_children():
		_accumulate_mesh_bounds(child, world_transform, result)


func _is_between(value: Vector3, minimum: Vector3, maximum: Vector3) -> bool:
	return (
		value.x >= minimum.x and value.x <= maximum.x and
		value.y >= minimum.y and value.y <= maximum.y and
		value.z >= minimum.z and value.z <= maximum.z
	)
