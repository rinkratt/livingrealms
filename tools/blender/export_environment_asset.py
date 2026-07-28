"""Export one optimized Living Realms environment asset from a supplied .blend.

The source blend is opened by Blender. Arguments after ``--`` select the asset:

    --kind house
    --kind object --object-name "moss rock 08" --asset-name RockMoss01
    --kind grass --object-name "grass_mix_lo" --asset-name MeadowGrassClump

Every export is centered on the ground, normalized to a requested height or
width, written as a Godot-ready GLB, and optionally saved as a compact .blend.
"""

from __future__ import annotations

import argparse
import math
import re
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


HOUSE_COLLECTIONS = {
    "HOUSE_003_FLOOR",
    "HOUSE_003_GROUNDFLOOR",
    "HOUSE_003_ROOF.",
}


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--kind", choices=("house", "object", "grass"), required=True)
    parser.add_argument("--object-name")
    parser.add_argument("--asset-name", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-output")
    parser.add_argument("--target-height", type=float)
    parser.add_argument("--target-width", type=float)
    parser.add_argument("--max-polygons", type=int, default=0)
    parser.add_argument("--texture-size", type=int, default=1024)
    return parser.parse_args(raw)


def keep_house_objects() -> list[bpy.types.Object]:
    keep: set[bpy.types.Object] = set()
    for collection_name in HOUSE_COLLECTIONS:
        collection = bpy.data.collections.get(collection_name)
        if collection is None:
            raise RuntimeError(f"House collection is missing: {collection_name}")
        keep.update(collection.all_objects)

    for obj in list(keep):
        parent = obj.parent
        while parent is not None:
            if not any(collection.name == "HOUSE_003_RIG" for collection in parent.users_collection):
                keep.add(parent)
            parent = parent.parent
    remove_everything_except(keep)
    return [obj for obj in bpy.context.scene.objects if obj.type in {"MESH", "CURVE", "EMPTY"}]


def keep_named_object(name: str) -> list[bpy.types.Object]:
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Mesh object not found: {name}")
    keep: set[bpy.types.Object] = {obj}
    parent = obj.parent
    while parent is not None:
        keep.add(parent)
        parent = parent.parent
    remove_everything_except(keep)
    obj.hide_set(False)
    obj.hide_render = False
    return [obj]


def remove_everything_except(keep: set[bpy.types.Object]) -> None:
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)


def tune_modifiers(objects: list[bpy.types.Object]) -> None:
    for obj in objects:
        if obj.type != "MESH":
            continue
        for modifier in obj.modifiers:
            if modifier.type == "SUBSURF":
                modifier.levels = 0
                modifier.render_levels = 0
            elif modifier.type == "BEVEL" and hasattr(modifier, "segments"):
                modifier.segments = min(modifier.segments, 2)


def bake_and_join_house(objects: list[bpy.types.Object], asset_name: str) -> list[bpy.types.Object]:
    geometry = [
        obj
        for obj in objects
        if obj.type in {"MESH", "CURVE"} and not obj.hide_render
    ]
    bpy.ops.object.select_all(action="DESELECT")
    for obj in geometry:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = geometry[0]
    bpy.ops.object.convert(target="MESH")
    converted = [
        obj
        for obj in bpy.context.selected_objects
        if obj.type == "MESH"
    ]
    fallback_material = bpy.data.materials.new(asset_name + "Fallback")
    fallback_material.diffuse_color = (0.38, 0.27, 0.18, 1.0)
    groups: dict[bpy.types.Material, list[bpy.types.Object]] = {}
    for obj in converted:
        slots = list(obj.data.materials)
        material_counts: dict[bpy.types.Material, int] = {}
        for polygon in obj.data.polygons:
            material = (
                slots[polygon.material_index]
                if polygon.material_index < len(slots) and slots[polygon.material_index] is not None
                else fallback_material
            )
            material_counts[material] = material_counts.get(material, 0) + 1
        dominant = (
            max(material_counts, key=material_counts.get)
            if material_counts
            else fallback_material
        )
        obj.data.materials.clear()
        obj.data.materials.append(dominant)
        for polygon in obj.data.polygons:
            polygon.material_index = 0
        groups.setdefault(dominant, []).append(obj)

    joined_groups: list[bpy.types.Object] = []
    for index, (material, group) in enumerate(groups.items(), start=1):
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        if len(group) > 1:
            bpy.ops.object.join()
        joined = group[0]
        safe_material_name = re.sub(r"[^A-Za-z0-9]+", "", material.name) or f"Material{index}"
        joined.name = f"{asset_name}{safe_material_name}Geometry"
        joined_groups.append(joined)
    return joined_groups


def decimate_mesh(obj: bpy.types.Object, maximum_polygons: int) -> None:
    if maximum_polygons <= 0 or obj.type != "MESH":
        return
    polygon_count = len(obj.data.polygons)
    if polygon_count <= maximum_polygons:
        return
    modifier = obj.modifiers.new("LivingRealmsOptimization", "DECIMATE")
    modifier.ratio = max(0.02, maximum_polygons / polygon_count)
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def make_grass_clump(source: bpy.types.Object, asset_name: str) -> list[bpy.types.Object]:
    copies: list[bpy.types.Object] = []
    for index in range(15):
        angle = index * math.tau / 15.0 + (index % 3) * 0.19
        radius = 0.12 + (index % 5) * 0.12
        copy = source.copy()
        copy.data = source.data.copy()
        copy.name = f"{asset_name}Blade{index + 1:02d}"
        bpy.context.collection.objects.link(copy)
        copy.animation_data_clear()
        copy.constraints.clear()
        height_scale = 0.72 + (index % 4) * 0.10
        copy.matrix_world = (
            Matrix.Translation(
                (
                    math.cos(angle) * radius,
                    math.sin(angle) * radius,
                    0.0,
                )
            )
            @ Matrix.Rotation(angle + (index % 2) * math.pi * 0.5, 4, "Z")
            @ Matrix.Diagonal(
                (
                    0.82 + (index % 3) * 0.08,
                    height_scale,
                    height_scale,
                    1.0,
                )
            )
        )
        copies.append(copy)
    bpy.data.objects.remove(source, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    for copy in copies:
        copy.select_set(True)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    copies[0].name = asset_name + "Geometry"
    return [copies[0]]


def house_material_color(name: str) -> tuple[float, float, float, float] | None:
    lowered = name.lower()
    if "stucco" in lowered:
        return (0.62, 0.48, 0.28, 1.0)
    if "wood" in lowered:
        return (0.20, 0.065, 0.022, 1.0)
    if "brick" in lowered:
        return (0.34, 0.15, 0.075, 1.0)
    if "concrete" in lowered or "stone" in lowered:
        return (0.24, 0.25, 0.23, 1.0)
    if "rope" in lowered:
        return (0.42, 0.28, 0.10, 1.0)
    if "iron" in lowered:
        return (0.075, 0.082, 0.086, 1.0)
    if "window" in lowered or "glass" in lowered:
        return (0.035, 0.075, 0.11, 1.0)
    if "vanta" in lowered or "stroke" in lowered:
        return (0.012, 0.010, 0.009, 1.0)
    if "ground" in lowered:
        return (0.12, 0.10, 0.075, 1.0)
    return None


def best_color_image(material: bpy.types.Material) -> tuple[bpy.types.Image | None, bool]:
    if material.node_tree is None:
        return None, False
    candidates: list[tuple[int, bpy.types.Image, bool]] = []
    material_name = material.name.lower()
    for node in material.node_tree.nodes:
        if node.type != "TEX_IMAGE" or node.image is None:
            continue
        image_name = node.image.name.lower()
        if any(token in image_name for token in ("normal", "rough", "height", "noise")):
            continue
        alpha_linked = bool(node.outputs.get("Alpha") and node.outputs["Alpha"].is_linked)
        score = 0
        if alpha_linked:
            score += 5
        if "birch" in material_name and "birch" in image_name:
            score += 4
        if image_name.endswith(".png"):
            score += 2
        if image_name.endswith((".jpg", ".jpeg")):
            score += 1
        candidates.append((score, node.image, alpha_linked))
    if not candidates:
        return None, False
    candidates.sort(key=lambda item: item[0], reverse=True)
    _, image, alpha_linked = candidates[0]
    return image, alpha_linked


def legacy_diffuse_color(material: bpy.types.Material) -> tuple[float, float, float, float]:
    if material.node_tree is not None:
        for node in material.node_tree.nodes:
            if node.type == "BSDF_DIFFUSE" and node.inputs.get("Color") is not None:
                return tuple(node.inputs["Color"].default_value)
            if node.type == "BSDF_PRINCIPLED" and node.inputs.get("Base Color") is not None:
                return tuple(node.inputs["Base Color"].default_value)
    return tuple(material.diffuse_color)


def make_materials_portable(objects: list[bpy.types.Object], asset_name: str) -> None:
    materials: set[bpy.types.Material] = {
        slot.material
        for obj in objects
        if obj.type == "MESH"
        for slot in obj.material_slots
        if slot.material is not None
    }
    is_house = asset_name == "MedievalFarmhouse"
    for material in materials:
        image, alpha_linked = best_color_image(material)
        color = house_material_color(material.name) if is_house else None
        color = color or legacy_diffuse_color(material)
        lowered = material.name.lower()
        solid_tree_foliage = asset_name in {"MeadowOak", "MeadowBirch"} and any(
            token in lowered for token in ("leaves", "branches")
        )
        if solid_tree_foliage:
            color = (
                (0.12, 0.30, 0.085, 1.0)
                if "leaves" in lowered
                else (0.085, 0.22, 0.065, 1.0)
            )
        transparent_cutout = alpha_linked or any(
            token in lowered for token in ("leaves", "branches", "grass")
        )

        material.use_nodes = True
        nodes = material.node_tree.nodes
        nodes.clear()
        output = nodes.new("ShaderNodeOutputMaterial")
        principled = nodes.new("ShaderNodeBsdfPrincipled")
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Roughness"].default_value = 0.86
        if "iron" in lowered:
            principled.inputs["Metallic"].default_value = 0.72
        material.node_tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])

        if image is not None:
            texture = nodes.new("ShaderNodeTexImage")
            texture.image = image
            if not solid_tree_foliage:
                material.node_tree.links.new(texture.outputs["Color"], principled.inputs["Base Color"])
            if transparent_cutout:
                material.node_tree.links.new(texture.outputs["Alpha"], principled.inputs["Alpha"])
        if transparent_cutout:
            if hasattr(material, "surface_render_method"):
                material.surface_render_method = "DITHERED"
            elif hasattr(material, "blend_method"):
                material.blend_method = "HASHED"
        material.diffuse_color = color
        material.use_backface_culling = False


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in objects
        if obj.type in {"MESH", "CURVE"} and not obj.hide_render
        for corner in obj.bound_box
    ]
    if not corners:
        raise RuntimeError("The selected asset has no visible geometry.")
    return (
        Vector(min(corner[index] for corner in corners) for index in range(3)),
        Vector(max(corner[index] for corner in corners) for index in range(3)),
    )


def add_normalizing_root(
    objects: list[bpy.types.Object],
    asset_name: str,
    target_height: float | None,
    target_width: float | None,
) -> bpy.types.Object:
    minimum, maximum = world_bounds(objects)
    dimensions = maximum - minimum
    if target_height is not None:
        scale = target_height / max(dimensions.z, 0.001)
    elif target_width is not None:
        scale = target_width / max(dimensions.x, dimensions.y, 0.001)
    else:
        scale = 1.0
    center = (minimum + maximum) * 0.5

    root = bpy.data.objects.new(asset_name, None)
    bpy.context.collection.objects.link(root)
    top_level = [obj for obj in objects if obj.parent is None or obj.parent not in objects]
    for obj in top_level:
        matrix_world = obj.matrix_world.copy()
        obj.parent = root
        obj.matrix_world = matrix_world
    root.scale = (scale, scale, scale)
    root.location = (-center.x * scale, -center.y * scale, -minimum.z * scale)
    return root


def used_images(objects: list[bpy.types.Object]) -> set[bpy.types.Image]:
    images: set[bpy.types.Image] = set()
    for obj in objects:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            material = slot.material
            if material is None or not material.use_nodes or material.node_tree is None:
                continue
            if hasattr(material, "surface_render_method") and "grass" in material.name.lower():
                material.surface_render_method = "DITHERED"
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image is not None:
                    images.add(node.image)
    return images


def optimize_images(objects: list[bpy.types.Object], maximum_size: int) -> None:
    for image in used_images(objects):
        width, height = image.size
        if width <= 0 or height <= 0:
            continue
        largest = max(width, height)
        if largest > maximum_size:
            ratio = maximum_size / largest
            image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
        image.pack()


def export_glb(objects: list[bpy.types.Object], root: bpy.types.Object, output_path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    output_path.parent.mkdir(parents=True, exist_ok=True)
    supported = bpy.ops.export_scene.gltf.get_rna_type().properties.keys()
    options = {
        "filepath": str(output_path),
        "export_format": "GLB",
        "use_selection": True,
        "export_apply": True,
        "export_animations": False,
        "export_cameras": False,
        "export_lights": False,
        "export_yup": True,
        "export_skins": False,
        "export_morph": False,
        "export_image_format": "WEBP",
        "export_image_quality": 84,
    }
    bpy.ops.export_scene.gltf(**{key: value for key, value in options.items() if key in supported})


def save_compact_source(source_path: Path) -> None:
    source_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path), compress=True)


def main() -> None:
    args = parse_args()
    if (args.target_height is None) == (args.target_width is None):
        raise RuntimeError(
            "Every Living Realms asset must specify exactly one game-size "
            "normalization target: --target-height or --target-width."
        )
    if args.kind == "house":
        objects = keep_house_objects()
    else:
        if not args.object_name:
            raise RuntimeError("--object-name is required for object and grass assets.")
        objects = keep_named_object(args.object_name)
        if args.kind == "grass":
            objects = make_grass_clump(objects[0], args.asset_name)

    bpy.data.orphans_purge(do_recursive=True)
    tune_modifiers(objects)
    make_materials_portable(objects, args.asset_name)
    if args.kind == "house":
        objects = bake_and_join_house(objects, args.asset_name)
    if args.max_polygons > 0 and len([obj for obj in objects if obj.type == "MESH"]) == 1:
        decimate_mesh(next(obj for obj in objects if obj.type == "MESH"), args.max_polygons)
    optimize_images(objects, args.texture_size)
    root = add_normalizing_root(
        objects,
        args.asset_name,
        args.target_height,
        args.target_width,
    )
    output_path = Path(args.output).resolve()
    export_glb(objects, root, output_path)
    if args.source_output:
        save_compact_source(Path(args.source_output).resolve())

    minimum, maximum = world_bounds(objects)
    print(f"ENVIRONMENT_ASSET={args.asset_name}")
    print(f"ENVIRONMENT_GLB={output_path}")
    print(f"ENVIRONMENT_SOURCE={Path(args.source_output).resolve() if args.source_output else 'none'}")
    print(f"ENVIRONMENT_DIMENSIONS={(maximum - minimum)[:]}")
    print(
        "ENVIRONMENT_POLYGONS="
        + str(sum(len(obj.data.polygons) for obj in objects if obj.type == "MESH"))
    )
    print(f"ENVIRONMENT_TEXTURE_MAX={args.texture_size}")


if __name__ == "__main__":
    main()
