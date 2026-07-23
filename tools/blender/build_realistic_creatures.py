"""Build higher-detail stylized-realism rat and wolf assets for Godot."""

from __future__ import annotations

import math
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "client" / "LivingRealms.Client" / "Assets" / "Creatures3D"
SOURCE_DIR = ROOT / "assets" / "3d-source" / "creatures"


def srgb(value: str) -> tuple[float, float, float, float]:
    channels = [int(value[index:index + 2], 16) / 255.0 for index in (1, 3, 5)]
    linear = [channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4 for channel in channels]
    return (*linear, 1.0)


def material(name: str, color: str, roughness: float, metallic: float = 0.0) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    rgba = srgb(color)
    result.diffuse_color = rgba
    shader = result.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return result


def reset(name: str) -> bpy.types.Object:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    return root


def smooth(obj: bpy.types.Object, subdivision: int = 1) -> bpy.types.Object:
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        if subdivision:
            modifier = obj.modifiers.new("SculptSurface", "SUBSURF")
            modifier.levels = subdivision
            modifier.render_levels = subdivision
    return obj


def ellipsoid(name: str, location, scale, mat, root, segments=32, rings=20) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    obj.parent = root
    return smooth(obj)


def cone(name: str, location, radius, depth, rotation, mat, root, vertices=24, scale=(1, 1, 1)) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=0.002, depth=depth,
                                    location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    obj.parent = root
    return smooth(obj, 0)


def cylinder(name: str, location, radius, depth, rotation, mat, root, scale=(1, 1, 1)) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    obj.parent = root
    bevel = obj.modifiers.new("SoftEdges", "BEVEL")
    bevel.width = min(radius * 0.25, 0.018)
    bevel.segments = 2
    return smooth(obj, 0)


def frustum(name: str, location, lower_radius, upper_radius, depth, rotation, mat, root, scale=(1, 1, 1)) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(vertices=28, radius1=lower_radius, radius2=upper_radius, depth=depth,
                                    location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.data.materials.append(mat)
    obj.parent = root
    bevel = obj.modifiers.new("AnatomicalSoftness", "BEVEL")
    bevel.width = min(lower_radius * 0.18, 0.014)
    bevel.segments = 2
    return smooth(obj, 0)


def beam(name: str, start, end, radius, mat, root) -> bpy.types.Object:
    from mathutils import Vector
    a, b = Vector(start), Vector(end)
    delta = b - a
    obj = cylinder(name, (a + b) * 0.5, radius, delta.length, (0, 0, 0), mat, root)
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return obj


def build_rat() -> bpy.types.Object:
    root = reset("ForestRatRoot")
    fur = material("RatReal_CharcoalFur", "#353638", 0.94)
    fur_light = material("RatReal_SilverFur", "#5f5d59", 0.95)
    fur_dark = material("RatReal_ShadowFur", "#18191b", 0.96)
    skin = material("RatReal_EarAndTail", "#805b5a", 0.80)
    black = material("RatReal_NosePupil", "#09090a", 0.34)
    amber = material("RatReal_DarkEye", "#17120d", 0.18)
    ivory = material("RatReal_Whisker", "#c7bca9", 0.68)

    ellipsoid("RatBody", (0, 0.03, 0.33), (0.29, 0.52, 0.27), fur, root)
    ellipsoid("RatChest", (0, -0.32, 0.37), (0.22, 0.29, 0.24), fur_light, root)
    ellipsoid("RatHead", (0, -0.55, 0.49), (0.205, 0.245, 0.215), fur_light, root)
    ellipsoid("RatHeadMask", (0, -0.705, 0.585), (0.11, 0.040, 0.065), fur_dark, root, 28, 16)
    ellipsoid("RatMuzzle", (0, -0.735, 0.43), (0.108, 0.135, 0.085), skin, root)
    ellipsoid("RatNose", (0, -0.845, 0.445), (0.052, 0.040, 0.044), black, root, 24, 14)
    for side, x in (("Left", -0.15), ("Right", 0.15)):
        ellipsoid(f"Rat{side}EarOuter", (x, -0.49, 0.69), (0.115, 0.045, 0.13), fur_dark, root, 28, 16)
        ellipsoid(f"Rat{side}EarInner", (x, -0.523, 0.69), (0.074, 0.018, 0.086), skin, root, 24, 14)
        ellipsoid(f"Rat{side}Eye", (x * 0.55, -0.736, 0.565), (0.029, 0.015, 0.029), amber, root, 24, 14)
        ellipsoid(f"Rat{side}Pupil", (x * 0.55, -0.750, 0.565), (0.011, 0.005, 0.014), black, root, 20, 12)
    for side in (-1, 1):
        for index in range(4):
            start = (side * (0.10 + index * 0.012), -0.79, 0.42 + index * 0.025)
            end = (side * (0.42 + index * 0.035), -0.92, 0.39 + index * 0.02)
            beam(f"RatWhisker{side}_{index}", start, end, 0.003, ivory, root)
    for name, x, y in (("FrontLeft", -0.18, -0.34), ("FrontRight", 0.18, -0.34),
                       ("BackLeft", -0.20, 0.30), ("BackRight", 0.20, 0.30)):
        frustum(f"Rat{name}Leg", (x, y, 0.15), 0.043, 0.058, 0.22, (0, 0, 0), fur_dark, root)
        ellipsoid(f"Rat{name}Paw", (x, y - 0.045, 0.048), (0.060, 0.095, 0.038), skin, root, 24, 14)
    beam("RatTailBase", (0, 0.44, 0.31), (0, 0.98, 0.17), 0.055, skin, root)
    beam("RatTailTip", (0, 0.96, 0.17), (0.08, 1.53, 0.12), 0.027, skin, root)
    return root


def build_wolf() -> bpy.types.Object:
    root = reset("PrairieWolfRoot")
    fur = material("WolfReal_SlateFur", "#3c4247", 0.95)
    fur_light = material("WolfReal_SilverFur", "#565b5e", 0.96)
    fur_dark = material("WolfReal_ShadowFur", "#1b2024", 0.97)
    muzzle = material("WolfReal_Muzzle", "#54534f", 0.94)
    black = material("WolfReal_NosePupil", "#08090a", 0.32)
    amber = material("WolfReal_AmberEye", "#a96e17", 0.20)
    ivory = material("WolfReal_Fang", "#ddd5bd", 0.42)

    ellipsoid("WolfBody", (0, 0.05, 0.78), (0.43, 0.82, 0.46), fur, root, 40, 24)
    ellipsoid("WolfChest", (0, -0.46, 0.86), (0.40, 0.46, 0.55), fur_light, root, 36, 22)
    ellipsoid("WolfHaunches", (0, 0.58, 0.76), (0.43, 0.53, 0.46), fur_dark, root, 36, 22)
    ellipsoid("WolfMane", (0, -0.58, 1.03), (0.43, 0.40, 0.45), fur_dark, root, 36, 22)
    ellipsoid("WolfHead", (0, -0.88, 1.17), (0.32, 0.38, 0.35), fur_light, root, 40, 24)
    ellipsoid("WolfHeadMask", (0, -1.135, 1.33), (0.18, 0.065, 0.105), fur_dark, root, 34, 20)
    ellipsoid("WolfHeadCheekLeft", (-0.19, -1.05, 1.10), (0.105, 0.105, 0.14), fur_light, root, 30, 18)
    ellipsoid("WolfHeadCheekRight", (0.19, -1.05, 1.10), (0.105, 0.105, 0.14), fur_light, root, 30, 18)
    ellipsoid("WolfMuzzle", (0, -1.17, 1.08), (0.20, 0.24, 0.16), muzzle, root, 36, 22)
    ellipsoid("WolfNose", (0, -1.365, 1.10), (0.092, 0.060, 0.070), black, root, 28, 16)
    for side, x in (("Left", -0.24), ("Right", 0.24)):
        cone(f"Wolf{side}Ear", (x, -0.78, 1.54), 0.17, 0.43,
             (0, side == "Left" and -0.10 or 0.10, 0), fur_dark, root, 28, (0.72, 0.72, 1.0))
        eye_x = -0.18 if side == "Left" else 0.18
        ellipsoid(f"Wolf{side}Eye", (eye_x, -1.235, 1.285), (0.036, 0.016, 0.029), amber, root, 28, 16)
        ellipsoid(f"Wolf{side}Pupil", (eye_x, -1.250, 1.285), (0.011, 0.005, 0.019), black, root, 20, 12)
        beam(f"WolfHeadBrow{side}", (eye_x - 0.047, -1.246, 1.33), (eye_x + 0.047, -1.246, 1.319), 0.010, fur_dark, root)
    for side, x in (("Left", -0.10), ("Right", 0.10)):
        cone(f"WolfFang{side}", (x, -1.33, 1.00), 0.021, 0.10, (math.pi, 0, 0), ivory, root, 20)
    for name, x, y in (("FrontLeft", -0.29, -0.50), ("FrontRight", 0.29, -0.50),
                       ("BackLeft", -0.31, 0.50), ("BackRight", 0.31, 0.50)):
        frustum(f"Wolf{name}Upper", (x, y, 0.49), 0.105, 0.145, 0.48, (0, 0, 0), fur_dark, root, (0.92, 0.92, 1.0))
        frustum(f"Wolf{name}Lower", (x, y - 0.04, 0.21), 0.080, 0.105, 0.36, (0, 0, 0), fur_light, root, (0.88, 0.88, 1.0))
        ellipsoid(f"Wolf{name}Paw", (x, y - 0.10, 0.065), (0.115, 0.16, 0.070), fur_dark, root, 28, 16)
        for toe in (-1, 0, 1):
            cone(f"Wolf{name}Claw{toe}", (x + toe * 0.038, y - 0.245, 0.052), 0.012, 0.06,
                 (math.radians(76), 0, 0), black, root, 14, (0.8, 0.8, 1.0))
    beam("WolfTailBase", (0, 0.86, 0.92), (0, 1.43, 0.72), 0.18, fur_dark, root)
    beam("WolfTailTip", (0, 1.39, 0.73), (0.10, 1.88, 0.50), 0.11, fur, root)
    for side in (-1, 1):
        for index in range(4):
            cone(f"WolfManeTuft{side}_{index}", (side * (0.18 + index * 0.035), -0.52 + index * 0.03, 1.30 - index * 0.10),
                 0.055, 0.20, (0, side * 0.18, side * 0.45), fur_dark, root, 18, (0.65, 0.65, 1.0))
    return root


def descendants(root: bpy.types.Object) -> list[bpy.types.Object]:
    result = [root]
    pending = list(root.children)
    while pending:
        item = pending.pop()
        result.append(item)
        pending.extend(item.children)
    return result


def save_and_export(root: bpy.types.Object, slug: str) -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_DIR / f"{slug}.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.gltf(
        filepath=str(OUTPUT_DIR / f"{slug}.glb"), export_format="GLB", use_selection=True,
        export_apply=True, export_animations=False, export_cameras=False, export_lights=False, export_yup=True,
    )
    print(f"CREATURE_EXPORTED={OUTPUT_DIR / f'{slug}.glb'}")


def main() -> None:
    bpy.context.scene.unit_settings.system = "METRIC"
    rat = build_rat()
    save_and_export(rat, "forest-rat")
    wolf = build_wolf()
    save_and_export(wolf, "prairie-wolf")


if __name__ == "__main__":
    main()
