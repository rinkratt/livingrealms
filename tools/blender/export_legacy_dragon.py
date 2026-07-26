"""Convert the credited BGE Dragon 2.0 asset into an animated Godot GLB.

Open the original Blender file on the Blender command line, then pass:
    --output C:/path/to/dragon.glb
    --base-color C:/path/to/Dragon_Bump_Col2.jpg
    --normal C:/path/to/Dragon_Nor.jpg
    --normal-mirrored C:/path/to/Dragon_Nor_mirror2.jpg

The original Blender 2.6 material texture slots do not survive intact in
modern Blender. This exporter reconnects the authored atlas and normal maps
to portable Principled materials without modifying the source .blend.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--base-color", required=True)
    parser.add_argument("--normal", required=True)
    parser.add_argument("--normal-mirrored", required=True)
    return parser.parse_args(raw)


def load_image(path: str, *, non_color: bool = False) -> bpy.types.Image:
    image = bpy.data.images.load(str(Path(path).resolve()), check_existing=True)
    if non_color:
        image.colorspace_settings.name = "Non-Color"
    return image


def rebuild_material(
    material: bpy.types.Material,
    base_color: bpy.types.Image,
    normal: bpy.types.Image,
) -> None:
    material.use_nodes = True
    material.diffuse_color = (0.22, 0.24, 0.25, 1.0)
    material.metallic = 0.02
    material.roughness = 0.72
    material.use_backface_culling = False

    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()

    output = nodes.new("ShaderNodeOutputMaterial")
    output.location = (560, 0)
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (260, 0)
    shader.inputs["Roughness"].default_value = 0.72
    shader.inputs["Metallic"].default_value = 0.02
    shader.inputs["Specular IOR Level"].default_value = 0.34

    color_node = nodes.new("ShaderNodeTexImage")
    color_node.name = "DragonColorAtlas"
    color_node.image = base_color
    color_node.location = (-420, 120)
    color_node.interpolation = "Linear"

    normal_node = nodes.new("ShaderNodeTexImage")
    normal_node.name = "DragonNormalAtlas"
    normal_node.image = normal
    normal_node.location = (-420, -220)
    normal_node.interpolation = "Linear"

    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_map.location = (-20, -180)
    normal_map.inputs["Strength"].default_value = 0.72

    links.new(color_node.outputs["Color"], shader.inputs["Base Color"])
    links.new(normal_node.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], shader.inputs["Normal"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])


def main() -> None:
    args = parse_args()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    armature = bpy.data.objects.get("Armature")
    dragon = bpy.data.objects.get("Dragon_Mesh")
    if armature is None or armature.type != "ARMATURE":
        raise RuntimeError("Dragon armature was not found.")
    if dragon is None or dragon.type != "MESH":
        raise RuntimeError("Dragon mesh was not found.")

    base_color = load_image(args.base_color)
    normal = load_image(args.normal, non_color=True)
    mirrored_normal = load_image(args.normal_mirrored, non_color=True)

    for slot in dragon.material_slots:
        material = slot.material
        if material is None:
            continue
        selected_normal = (
            mirrored_normal
            if material.name in {"Game_dragon.003", "Material.004"}
            else normal
        )
        rebuild_material(material, base_color, selected_normal)

    action_names = {
        "Fly_New": "Fly",
        "Idel_New": "Idle",
        "Run_New": "Run",
        "Walk_New": "Walk",
    }
    for action in bpy.data.actions:
        action.name = action_names.get(action.name, action.name)

    bpy.ops.object.select_all(action="DESELECT")
    armature.hide_set(False)
    dragon.hide_set(False)
    armature.hide_render = False
    dragon.hide_render = False
    armature.select_set(True)
    dragon.select_set(True)
    bpy.context.view_layer.objects.active = armature

    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_apply=False,
        export_yup=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_force_sampling=True,
        export_frame_step=1,
        export_anim_slide_to_zero=True,
        export_optimize_animation_size=True,
        export_anim_single_armature=True,
        export_skins=True,
        export_all_influences=False,
        export_cameras=False,
        export_lights=False,
    )
    print(
        f"EXPORTED_DRAGON={output} "
        f"vertices={len(dragon.data.vertices)} "
        f"polygons={len(dragon.data.polygons)} "
        f"actions={','.join(sorted(action.name for action in bpy.data.actions))}"
    )


if __name__ == "__main__":
    main()
