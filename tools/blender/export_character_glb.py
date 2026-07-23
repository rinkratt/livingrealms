"""Export one accepted Living Realms Blender character for Godot.

Usage (after Blender's -- separator):
    --root AldenCharacterRoot --output C:/path/to/alden.glb

The .blend is opened by Blender on the command line. Only the named character
root and its descendants are exported; review cameras, lights, floor, and the
unbound production skeleton are intentionally excluded.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import bpy


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--output", required=True)
    return parser.parse_args(raw)


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result = [root]
    pending = list(root.children)
    while pending:
        current = pending.pop()
        result.append(current)
        pending.extend(current.children)
    return result


def prepare_portable_pbr_materials(objects: list[bpy.types.Object]) -> None:
    """Keep authored colors when procedural Blender nodes cannot enter glTF.

    The hero review materials use Noise and Color Ramp nodes for close-up
    surface variation. glTF cannot translate that procedural chain and would
    otherwise export a white material. Disconnecting only unsupported inputs
    lets the Principled shader's authored base color, metallic, and roughness
    export cleanly; Blender source materials remain untouched on disk.
    """
    materials = {
        slot.material
        for obj in objects
        if obj.type == "MESH"
        for slot in obj.material_slots
        if slot.material is not None
    }
    for material in materials:
        if not material.use_nodes or material.node_tree is None:
            continue
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled is None:
            continue
        for input_name in ("Base Color", "Normal"):
            shader_input = principled.inputs.get(input_name)
            if shader_input is None:
                continue
            for link in list(shader_input.links):
                material.node_tree.links.remove(link)


def main() -> None:
    args = parse_args()
    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    root = bpy.data.objects.get(args.root)
    if root is None:
        raise RuntimeError(f"Character root not found: {args.root}")

    root.rotation_euler = (0.0, 0.0, 0.0)
    bpy.ops.object.select_all(action="DESELECT")
    character_objects = descendants(root)

    # Curves carry hair strands and insignia. Convert them to ordinary mesh
    # geometry so Godot receives the same visible detail as the review render.
    for obj in list(character_objects):
        if obj.type != "CURVE":
            continue
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target="MESH")
        obj.select_set(False)

    character_objects = descendants(root)
    prepare_portable_pbr_materials(character_objects)
    for obj in character_objects:
        obj.hide_set(False)
        obj.hide_render = False
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root

    bpy.ops.export_scene.gltf(
        filepath=str(output),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_yup=True,
    )
    print(f"EXPORTED={output}")


if __name__ == "__main__":
    main()
