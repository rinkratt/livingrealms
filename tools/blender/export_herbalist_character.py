"""Prepare the user-supplied magic-girl character for Living Realms.

The source scene contains Rigify widgets, a separate facial control rig, and
4K/8K packed textures. This exporter keeps the visible character, preserves
the body-deformation rig, bakes the resting facial shape, limits textures to
2K, saves a compact editable source scene, exports a Godot-ready GLB, and
creates a review render.

Usage (after Blender's -- separator):

    --output C:/path/to/elowen-herbalist.glb
    --source-output C:/path/to/elowen-herbalist.blend
    --preview C:/path/to/elowen-herbalist-preview.png
"""

from __future__ import annotations

import argparse
import math
import re
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector


CHARACTER_MESHES = {
    "boot",
    "bra",
    "chubby_body",
    "Corset",
    "eyes_brow",
    "eyes_L",
    "eyes_lashes",
    "eyes_R",
    "hair",
    "hat",
    "skirt",
    "teeth_down",
    "teeth_up",
    "tongue",
    "top",
}
BODY_RIG_NAME = "magic girl  pbr_Rigify"
FACE_RIG_NAME = "FaceitRig"
MAX_TEXTURE_SIZE = 1024


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--source-output", required=True)
    parser.add_argument("--preview", required=True)
    return parser.parse_args(raw)


def remove_facial_rig() -> None:
    face_rig = bpy.data.objects.get(FACE_RIG_NAME)
    if face_rig is None:
        return

    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        for modifier in list(obj.modifiers):
            if modifier.type == "ARMATURE" and modifier.object == face_rig:
                obj.modifiers.remove(modifier)

    bpy.data.objects.remove(face_rig, do_unlink=True)


def remove_production_helpers() -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    body_rig = bpy.data.objects.get(BODY_RIG_NAME)
    if body_rig is None or body_rig.type != "ARMATURE":
        raise RuntimeError(f"Body armature not found: {BODY_RIG_NAME}")

    remove_facial_rig()
    keep_names = set(CHARACTER_MESHES)
    keep_names.add(BODY_RIG_NAME)
    for obj in list(bpy.data.objects):
        if obj.name not in keep_names:
            bpy.data.objects.remove(obj, do_unlink=True)

    meshes: list[bpy.types.Object] = []
    for name in sorted(CHARACTER_MESHES):
        obj = bpy.data.objects.get(name)
        if obj is None or obj.type != "MESH":
            raise RuntimeError(f"Character mesh not found: {name}")
        obj.hide_set(False)
        obj.hide_render = False
        meshes.append(obj)

    body_rig.hide_set(False)
    body_rig.hide_render = False
    body_rig.animation_data_clear()
    for pose_bone in body_rig.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.rotation_quaternion.identity()
        pose_bone.location = Vector((0.0, 0.0, 0.0))
        pose_bone.scale = Vector((1.0, 1.0, 1.0))
    bpy.context.scene.frame_set(0)
    return body_rig, meshes


def used_images(meshes: list[bpy.types.Object]) -> set[bpy.types.Image]:
    images: set[bpy.types.Image] = set()
    for obj in meshes:
        for slot in obj.material_slots:
            material = slot.material
            if material is None or not material.use_nodes or material.node_tree is None:
                continue
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image is not None:
                    images.add(node.image)
    return images


def optimize_images(meshes: list[bpy.types.Object]) -> None:
    for image in used_images(meshes):
        width, height = image.size
        if width <= 0 or height <= 0:
            continue
        largest = max(width, height)
        if largest > MAX_TEXTURE_SIZE:
            ratio = MAX_TEXTURE_SIZE / largest
            image.scale(max(1, round(width * ratio)), max(1, round(height * ratio)))
        if image.source != "GENERATED":
            image.pack()


def portable_materials(meshes: list[bpy.types.Object]) -> None:
    """Remove shader features unsupported by glTF while keeping PBR maps."""
    for obj in meshes:
        for slot in obj.material_slots:
            material = slot.material
            if material is None:
                continue
            if hasattr(material, "surface_render_method"):
                material.surface_render_method = "DITHERED"
            elif hasattr(material, "blend_method"):
                material.blend_method = "HASHED"


def mesh_world_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    corners = [
        obj.matrix_world @ Vector(corner)
        for obj in meshes
        for corner in obj.bound_box
    ]
    return (
        Vector(min(corner[index] for corner in corners) for index in range(3)),
        Vector(max(corner[index] for corner in corners) for index in range(3)),
    )


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def add_preview_scene(meshes: list[bpy.types.Object], preview_path: Path) -> list[bpy.types.Object]:
    minimum, maximum = mesh_world_bounds(meshes)
    center = (minimum + maximum) * 0.5
    height = maximum.z - minimum.z

    world = bpy.context.scene.world or bpy.data.worlds.new("ElowenPreviewWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.018, 0.021, 0.025, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.20

    preview_objects: list[bpy.types.Object] = []
    ground_data = bpy.data.meshes.new("PreviewGroundMesh")
    ground = bpy.data.objects.new("PreviewGround", ground_data)
    bpy.context.collection.objects.link(ground)
    preview_objects.append(ground)
    vertices = [(-4, -4, 0), (4, -4, 0), (4, 4, 0), (-4, 4, 0)]
    ground_data.from_pydata(vertices, [], [(0, 1, 2, 3)])
    ground.location.z = minimum.z - 0.015
    ground_material = bpy.data.materials.new("PreviewGroundMaterial")
    ground_material.diffuse_color = (0.035, 0.045, 0.040, 1.0)
    ground.data.materials.append(ground_material)

    key_data = bpy.data.lights.new("PreviewKey", "AREA")
    key_data.energy = 1100
    key_data.shape = "DISK"
    key_data.size = 4.0
    key = bpy.data.objects.new("PreviewKey", key_data)
    bpy.context.collection.objects.link(key)
    key.location = (3.5, -4.0, maximum.z + 2.0)
    look_at(key, center)
    preview_objects.append(key)

    fill_data = bpy.data.lights.new("PreviewFill", "AREA")
    fill_data.energy = 650
    fill_data.color = (0.52, 0.68, 1.0)
    fill_data.size = 3.0
    fill = bpy.data.objects.new("PreviewFill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = (-3.5, -2.0, center.z + 0.8)
    look_at(fill, center)
    preview_objects.append(fill)

    rim_data = bpy.data.lights.new("PreviewRim", "AREA")
    rim_data.energy = 900
    rim_data.color = (1.0, 0.60, 0.23)
    rim_data.size = 2.0
    rim = bpy.data.objects.new("PreviewRim", rim_data)
    bpy.context.collection.objects.link(rim)
    rim.location = (1.8, 3.0, maximum.z + 0.6)
    look_at(rim, center)
    preview_objects.append(rim)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (height * 1.05, -height * 2.15, center.z + height * 0.18)
    camera_data.lens = 58
    look_at(camera, center + Vector((0, 0, height * 0.02)))
    bpy.context.scene.camera = camera
    preview_objects.append(camera)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(preview_path)
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "Medium High Contrast"
    bpy.ops.render.render(write_still=True)
    return preview_objects


def save_compact_source(
    source_path: Path,
    preview_objects: list[bpy.types.Object],
    meshes: list[bpy.types.Object],
) -> None:
    for obj in preview_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    source_path.parent.mkdir(parents=True, exist_ok=True)
    texture_path = source_path.parent / "textures"
    texture_path.mkdir(parents=True, exist_ok=True)
    used_names: set[str] = set()
    for image in sorted(used_images(meshes), key=lambda item: item.name.lower()):
        if image.size[0] <= 0 or image.size[1] <= 0:
            continue
        base_name = re.sub(r"[^a-z0-9]+", "-", image.name.lower()).strip("-") or "texture"
        candidate = base_name
        suffix = 2
        while candidate in used_names:
            candidate = f"{base_name}-{suffix}"
            suffix += 1
        used_names.add(candidate)
        target = texture_path / f"{candidate}.webp"
        image.filepath_raw = str(target)
        image.file_format = "WEBP"
        image.save()
        if image.packed_file is not None:
            image.unpack(method="REMOVE")
        image.filepath = f"//textures/{target.name}"
        image.source = "FILE"
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path), compress=True)
    shutil.rmtree(texture_path)


def export_glb(
    body_rig: bpy.types.Object,
    meshes: list[bpy.types.Object],
    output_path: Path,
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    body_rig.select_set(True)
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = body_rig

    output_path.parent.mkdir(parents=True, exist_ok=True)
    supported = bpy.ops.export_scene.gltf.get_rna_type().properties.keys()
    options = {
        "filepath": str(output_path),
        "export_format": "GLB",
        "use_selection": True,
        "export_apply": False,
        "export_animations": False,
        "export_cameras": False,
        "export_lights": False,
        "export_yup": True,
        "export_skins": True,
        "export_morph": False,
        "export_def_bones": True,
        "export_image_format": "WEBP",
        "export_image_quality": 88,
    }
    bpy.ops.export_scene.gltf(**{key: value for key, value in options.items() if key in supported})


def main() -> None:
    args = parse_args()
    output_path = Path(args.output).resolve()
    source_path = Path(args.source_output).resolve()
    preview_path = Path(args.preview).resolve()
    preview_path.parent.mkdir(parents=True, exist_ok=True)

    body_rig, meshes = remove_production_helpers()
    optimize_images(meshes)
    portable_materials(meshes)
    preview_objects = add_preview_scene(meshes, preview_path)
    export_glb(body_rig, meshes, output_path)
    save_compact_source(source_path, preview_objects, meshes)

    print(f"HERBALIST_SOURCE={source_path}")
    print(f"HERBALIST_GLB={output_path}")
    print(f"HERBALIST_PREVIEW={preview_path}")
    print(f"HERBALIST_VERTICES={sum(len(obj.data.vertices) for obj in meshes)}")
    print(f"HERBALIST_POLYGONS={sum(len(obj.data.polygons) for obj in meshes)}")
    print(f"HERBALIST_TEXTURE_MAX={MAX_TEXTURE_SIZE}")


if __name__ == "__main__":
    main()
