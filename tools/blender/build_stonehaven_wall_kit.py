"""Build the Living Realms modular Stonehaven masonry wall.

The resulting module is four meters long, 0.85 meters deep, 2.5 meters
tall, and grounded at Z=0 in Blender. Godot scales and rotates the module
to fit each persistent curtain-wall section.
"""

from pathlib import Path
import math

import bpy


ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = ROOT / "artifacts" / "blender" / "stonehaven-stone-wall.blend"
GLB_PATH = (
    ROOT
    / "client"
    / "LivingRealms.Client"
    / "Assets"
    / "Environment"
    / "Production"
    / "stonehaven-stone-wall.glb"
)


def material(name: str, color: tuple[float, float, float, float], roughness: float = 0.88):
    value = bpy.data.materials.new(name)
    value.diffuse_color = color
    value.use_nodes = True
    shader = value.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Roughness"].default_value = roughness
    return value


def cube(
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    surface,
    bevel: float = 0.035,
):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(surface)
    if bevel > 0:
        modifier = obj.modifiers.new("Weathered edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
    return obj


def build():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    stone_dark = material("Stonehaven granite dark", (0.25, 0.27, 0.27, 1.0))
    stone_mid = material("Stonehaven granite", (0.36, 0.38, 0.37, 1.0))
    stone_light = material("Stonehaven granite light", (0.45, 0.46, 0.44, 1.0))
    mortar = material("Stonehaven lime mortar", (0.20, 0.21, 0.20, 1.0), 0.96)
    surfaces = (stone_dark, stone_mid, stone_light, stone_mid)

    # A recessed mortar core closes all seams, while the individually beveled
    # stones create readable masonry corners instead of a flat brown box.
    cube("MortarCore", (0.0, 0.0, 1.22), (3.94, 0.69, 2.40), mortar, 0.015)

    course_height = 0.50
    depth = 0.82
    widths = (
        (0.78, 1.04, 0.70, 0.92, 0.72),
        (0.92, 0.72, 1.08, 0.66, 0.72),
        (0.70, 1.02, 0.82, 0.70, 0.76),
        (1.02, 0.66, 0.82, 0.78, 0.72),
    )
    for course in range(4):
        selected_widths = widths[course]
        scale = 3.94 / sum(selected_widths)
        cursor = -1.97
        for index, source_width in enumerate(selected_widths):
            width = source_width * scale
            x = cursor + width * 0.5
            cursor += width
            z = 0.06 + course * (course_height + 0.035) + course_height * 0.5
            y = 0.012 * math.sin((course + 1) * (index + 2))
            stone = cube(
                f"Course{course + 1}Stone{index + 1}",
                (x, y, z),
                (width - 0.035, depth, course_height),
                surfaces[(course + index) % len(surfaces)],
                0.045,
            )
            stone.rotation_euler[2] = math.radians(
                ((course * 7 + index * 5) % 5 - 2) * 0.45
            )

    # Slightly projecting capstones give each level a finished silhouette and
    # conceal the small course variations at module seams.
    cap_widths = (0.92, 1.08, 0.86, 1.10)
    cursor = -2.0
    for index, width in enumerate(cap_widths):
        x = cursor + width * 0.5
        cursor += width
        cube(
            f"Capstone{index + 1}",
            (x, 0.0, 2.36),
            (width - 0.025, 0.94, 0.27),
            surfaces[(index + 1) % len(surfaces)],
            0.05,
        )

    # Small outer-face buttresses visually strengthen long runs while retaining
    # a clear walkable inner face.
    for index, x in enumerate((-1.72, 1.72)):
        buttress = cube(
            f"Buttress{index + 1}",
            (x, 0.47, 0.70),
            (0.42, 0.40, 1.36),
            stone_dark if index == 0 else stone_mid,
            0.045,
        )
        buttress.rotation_euler[0] = math.radians(-2.5 if index == 0 else 2.5)

    bpy.ops.object.select_all(action="SELECT")
    bpy.context.scene.render.engine = "BLENDER_EEVEE"
    bpy.context.scene.world.color = (0.025, 0.025, 0.03)
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    GLB_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
    )
    print(f"Saved {BLEND_PATH}")
    print(f"Exported {GLB_PATH}")


if __name__ == "__main__":
    build()
