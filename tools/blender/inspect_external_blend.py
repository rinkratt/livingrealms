"""Print a compact import-readiness report for an external Blender asset.

Run with Blender after opening the source file:

    blender --background source.blend --python inspect_external_blend.py
"""

from __future__ import annotations

from collections import Counter
from pathlib import Path

import bpy
from mathutils import Vector


def mesh_world_bounds() -> tuple[Vector, Vector] | None:
    corners: list[Vector] = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH" or obj.hide_render:
            continue
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not corners:
        return None
    return (
        Vector(min(corner[index] for corner in corners) for index in range(3)),
        Vector(max(corner[index] for corner in corners) for index in range(3)),
    )


def main() -> None:
    object_types = Counter(obj.type for obj in bpy.data.objects)
    mesh_vertices = sum(len(mesh.vertices) for mesh in bpy.data.meshes)
    mesh_polygons = sum(len(mesh.polygons) for mesh in bpy.data.meshes)
    bounds = mesh_world_bounds()

    print("LIVING_REALMS_BLEND_REPORT_BEGIN")
    print(f"FILE={bpy.data.filepath}")
    print(f"BLENDER_VERSION={bpy.app.version_string}")
    print(f"SCENES={len(bpy.data.scenes)}")
    print(f"COLLECTIONS={len(bpy.data.collections)}")
    print(f"OBJECTS={len(bpy.data.objects)}")
    print("OBJECT_TYPES=" + ",".join(f"{key}:{value}" for key, value in sorted(object_types.items())))
    print(f"MESH_DATABLOCKS={len(bpy.data.meshes)}")
    print(f"VERTICES={mesh_vertices}")
    print(f"POLYGONS={mesh_polygons}")
    print(f"MATERIALS={len(bpy.data.materials)}")
    print(f"IMAGES={len(bpy.data.images)}")
    print(f"ARMATURES={len(bpy.data.armatures)}")
    print(f"ACTIONS={len(bpy.data.actions)}")

    if bounds is not None:
        minimum, maximum = bounds
        dimensions = maximum - minimum
        print(f"BOUNDS_MIN={minimum.x:.4f},{minimum.y:.4f},{minimum.z:.4f}")
        print(f"BOUNDS_MAX={maximum.x:.4f},{maximum.y:.4f},{maximum.z:.4f}")
        print(f"DIMENSIONS={dimensions.x:.4f},{dimensions.y:.4f},{dimensions.z:.4f}")

    for collection in sorted(bpy.data.collections, key=lambda item: item.name.lower()):
        object_names = ",".join(obj.name for obj in collection.objects)
        collection_corners = [
            obj.matrix_world @ Vector(corner)
            for obj in collection.objects
            if obj.type in {"MESH", "CURVE"} and not obj.hide_render
            for corner in obj.bound_box
        ]
        if collection_corners:
            collection_minimum = Vector(
                min(corner[index] for corner in collection_corners) for index in range(3)
            )
            collection_maximum = Vector(
                max(corner[index] for corner in collection_corners) for index in range(3)
            )
            collection_dimensions = collection_maximum - collection_minimum
            bounds_text = (
                f"|minimum={collection_minimum.x:.3f},{collection_minimum.y:.3f},{collection_minimum.z:.3f}"
                f"|maximum={collection_maximum.x:.3f},{collection_maximum.y:.3f},{collection_maximum.z:.3f}"
                f"|dimensions={collection_dimensions.x:.3f},{collection_dimensions.y:.3f},{collection_dimensions.z:.3f}"
            )
        else:
            bounds_text = ""
        print(f"COLLECTION={collection.name}{bounds_text}|objects={object_names}")

    for obj in sorted(bpy.data.objects, key=lambda item: item.name.lower()):
        if obj.type == "MESH":
            modifiers = ",".join(modifier.type for modifier in obj.modifiers) or "none"
            print(
                f"MESH={obj.name}|verts={len(obj.data.vertices)}|polys={len(obj.data.polygons)}"
                f"|materials={len(obj.material_slots)}|modifiers={modifiers}|parent={obj.parent.name if obj.parent else 'none'}"
                f"|location={obj.location.x:.3f},{obj.location.y:.3f},{obj.location.z:.3f}"
                f"|dimensions={obj.dimensions.x:.3f},{obj.dimensions.y:.3f},{obj.dimensions.z:.3f}"
                f"|hidden={obj.hide_render}"
            )
        elif obj.type == "ARMATURE":
            deform_bones = [bone.name for bone in obj.data.bones if bone.use_deform]
            print(
                f"ARMATURE={obj.name}|bones={len(obj.data.bones)}"
                f"|deform={len(deform_bones)}"
                f"|parent={obj.parent.name if obj.parent else 'none'}"
            )
            print(f"DEFORM_BONES={obj.name}|{','.join(deform_bones)}")

    for action in sorted(bpy.data.actions, key=lambda item: item.name.lower()):
        start, end = action.frame_range
        print(f"ACTION={action.name}|frames={start:.2f}-{end:.2f}|slots={len(action.slots)}")

    for image in sorted(bpy.data.images, key=lambda item: item.name.lower()):
        if image.source == "GENERATED":
            state = "generated"
        elif image.packed_file is not None:
            state = "packed"
        else:
            raw_path = bpy.path.abspath(image.filepath)
            state = "external-present" if Path(raw_path).exists() else "external-missing"
        print(
            f"IMAGE={image.name}|{state}|{image.size[0]}x{image.size[1]}"
            f"|{image.filepath}"
        )

    print("LIVING_REALMS_BLEND_REPORT_END")


if __name__ == "__main__":
    main()
