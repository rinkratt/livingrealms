"""Print material slots and shader-node summaries from the open Blender file."""

from __future__ import annotations

import bpy


def color_text(color) -> str:
    return ",".join(f"{component:.3f}" for component in color)


for obj in sorted(bpy.data.objects, key=lambda item: item.name.lower()):
    if obj.type != "MESH" or not obj.material_slots:
        continue
    print(
        f"OBJECT_MATERIALS={obj.name}|"
        + ",".join(slot.material.name if slot.material else "none" for slot in obj.material_slots)
    )

for material in sorted(bpy.data.materials, key=lambda item: item.name.lower()):
    print(
        f"MATERIAL={material.name}|diffuse={color_text(material.diffuse_color)}"
        f"|nodes={material.use_nodes}"
    )
    if not material.use_nodes or material.node_tree is None:
        continue
    for node in material.node_tree.nodes:
        if node.type == "BSDF_PRINCIPLED":
            base = node.inputs.get("Base Color")
            alpha = node.inputs.get("Alpha")
            roughness = node.inputs.get("Roughness")
            print(
                f"  PRINCIPLED={node.name}"
                f"|base={color_text(base.default_value) if base else 'none'}"
                f"|alpha={alpha.default_value if alpha else 'none'}"
                f"|roughness={roughness.default_value if roughness else 'none'}"
            )
        elif node.type == "TEX_IMAGE":
            print(
                f"  IMAGE_NODE={node.name}|image={node.image.name if node.image else 'none'}"
                f"|alpha-linked={bool(node.outputs.get('Alpha') and node.outputs['Alpha'].is_linked)}"
                f"|color-linked={bool(node.outputs.get('Color') and node.outputs['Color'].is_linked)}"
            )
        elif node.type in {"OUTPUT_MATERIAL", "BSDF_DIFFUSE", "BSDF_TRANSPARENT", "MIX_SHADER"}:
            print(f"  NODE={node.name}|type={node.type}")
