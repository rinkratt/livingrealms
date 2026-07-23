"""Build Elara on the same rigged real-human foundation as Alden."""

from __future__ import annotations

import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from bl_ext.user_default.mpfb.services.humanservice import HumanService
from build_alden_mpfb_hero import (
    add_boot_foot,
    body_vertex_indices,
    evaluated_geometry,
    extract_surface,
    group_center,
    parent_to_bone_keep_world,
)
from build_alden_sculpt import (
    add_beam_between,
    add_box,
    add_curve,
    add_cylinder,
    add_torus,
    add_uv_ellipsoid,
    build_tree_emblem,
    make_collection,
    make_material,
    move_to_collection,
    reset_scene,
    setup_studio,
    smooth,
)


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "assets" / "3d-source" / "characters" / "elara" / "hero-source"
BASE_BLEND = SOURCE_DIR / "elara_mpfb_base.blend"
BLEND_PATH = SOURCE_DIR / "elara_mpfb_hero_review.blend"
FRONT_RENDER = ROOT / "docs" / "phase-8-elara-mpfb-front-review.png"
BACK_RENDER = ROOT / "docs" / "phase-8-elara-mpfb-back-review.png"
FACE_RENDER = ROOT / "docs" / "phase-8-elara-mpfb-face-review.png"

random.seed(72291)


def create_materials() -> dict[str, bpy.types.Material]:
    def linear_hex(srgb_hex: str) -> str:
        values = [int(srgb_hex[index:index + 2], 16) / 255.0 for index in (1, 3, 5)]
        linear = [value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4 for value in values]
        return "#" + "".join(f"{max(0, min(255, round(value * 255))):02x}" for value in linear)

    def mat(name: str, color: str, roughness: float, metallic: float = 0.0, **kwargs) -> bpy.types.Material:
        return make_material(name, linear_hex(color), roughness, metallic, **kwargs)

    return {
        "skin": mat("ElaraReal_Skin", "#b98266", 0.53, noise_scale=96, bump_strength=0.04, subsurface=0.085),
        "eye_white": mat("ElaraReal_EyeWhite", "#d9d4c9", 0.17),
        "iris": mat("ElaraReal_HazelIris", "#617147", 0.15, noise_scale=30, bump_strength=0.03),
        "pupil": mat("ElaraReal_Pupil", "#050607", 0.09),
        "hair": mat("ElaraReal_ChestnutHair", "#100906", 0.64, noise_scale=92, bump_strength=0.11),
        "hair_warm": mat("ElaraReal_HairHighlights", "#321b10", 0.62, noise_scale=82, bump_strength=0.09),
        "green": mat("ElaraReal_ForestGreenWool", "#142a20", 0.86, noise_scale=165, bump_strength=0.3),
        "green_worn": mat("ElaraReal_WornGreenWool", "#274633", 0.84, noise_scale=148, bump_strength=0.27),
        "green_dark": mat("ElaraReal_GreenShadowCloth", "#0b1812", 0.89, noise_scale=158, bump_strength=0.29),
        "trousers": mat("ElaraReal_CharcoalTrousers", "#171819", 0.9, noise_scale=116, bump_strength=0.23),
        "leather": mat("ElaraReal_DarkLeather", "#1c110c", 0.63, noise_scale=73, bump_strength=0.24),
        "leather_worn": mat("ElaraReal_WornLeather", "#382318", 0.61, noise_scale=68, bump_strength=0.2),
        "boot": mat("ElaraReal_BootLeather", "#211814", 0.68, noise_scale=78, bump_strength=0.3),
        "steel": mat("ElaraReal_WeatheredSteel", "#555e63", 0.34, 0.9, noise_scale=96, bump_strength=0.11),
        "gold": mat("ElaraReal_AntiqueGold", "#a9782c", 0.31, 0.78, noise_scale=88, bump_strength=0.1),
        "wood": mat("ElaraReal_BowWood", "#704326", 0.55, noise_scale=65, bump_strength=0.18),
        "string": mat("ElaraReal_BowString", "#c1b296", 0.7),
        "studio_floor": mat("ElaraReal_StudioFloor", "#171a1e", 0.6, noise_scale=27, bump_strength=0.07),
    }


def create_human() -> bpy.types.Object:
    macro = {
        "gender": 0.08,
        "age": 0.43,
        "muscle": 0.48,
        "weight": 0.46,
        "proportions": 0.6,
        "height": 0.56,
        "cupsize": 0.56,
        "firmness": 0.66,
        "race": {"asian": 0.05, "caucasian": 0.9, "african": 0.05},
    }
    return HumanService.create_human(
        mask_helpers=True,
        detailed_helpers=True,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=macro,
    )


def add_eye(
    side: str,
    center: Vector,
    materials: dict[str, bpy.types.Material],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    add_uv_ellipsoid(f"ElaraEye_{side}", tuple(center), (0.017, 0.014, 0.014), materials["eye_white"], collection, root, 48, 24)
    add_uv_ellipsoid(f"ElaraIris_{side}", (center.x, center.y - 0.013, center.z), (0.0068, 0.002, 0.0068), materials["iris"], collection, root, 40, 20)
    add_uv_ellipsoid(f"ElaraPupil_{side}", (center.x, center.y - 0.015, center.z), (0.0026, 0.001, 0.0026), materials["pupil"], collection, root, 32, 16)


def add_swept_hair(
    eye_height: float,
    top_height: float,
    materials: dict[str, bpy.types.Material],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    curve = bpy.data.curves.new("ElaraSweptHairStrandsCurve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = 0.00042
    curve.bevel_resolution = 2
    center = Vector((0, -0.012, eye_height + 0.012))
    radii = Vector((0.108, 0.09, max(0.105, top_height - center.z)))
    bun_center = Vector((0, 0.095, top_height - 0.07))
    for _ in range(330):
        beta = random.uniform(-math.pi, math.pi)
        is_front = math.sin(beta) < -0.25
        maximum_alpha = 1.34 if is_front else 1.58
        cosine_alpha = 1.0 - random.random() * (1.0 - math.cos(maximum_alpha))
        alpha = math.acos(cosine_alpha)
        normal = Vector((math.sin(alpha) * math.cos(beta), math.sin(alpha) * math.sin(beta), math.cos(alpha))).normalized()
        surface = center + Vector((normal.x * radii.x, normal.y * radii.y, normal.z * radii.z))
        root_point = surface + normal * 0.007
        toward_bun = bun_center - root_point
        side_wave = normal.cross(toward_bun.normalized()) * random.uniform(-0.006, 0.006)
        points = [
            root_point,
            root_point + toward_bun * 0.33 + normal * 0.006 + side_wave,
            root_point + toward_bun * 0.68 + normal * 0.004 - side_wave,
            bun_center + Vector((random.uniform(-0.015, 0.015), random.uniform(-0.008, 0.012), random.uniform(-0.015, 0.015))),
        ]
        spline = curve.splines.new("BEZIER")
        spline.bezier_points.add(3)
        for index, (bezier, point) in enumerate(zip(spline.bezier_points, points)):
            bezier.co = point
            bezier.handle_left_type = "AUTO"
            bezier.handle_right_type = "AUTO"
            bezier.radius = (1.0, 0.9, 0.62, 0.16)[index] * random.uniform(0.85, 1.15)
    obj = bpy.data.objects.new("ElaraSweptHairStrands", curve)
    collection.objects.link(obj)
    curve.materials.append(materials["hair"])
    obj.parent = root

    add_uv_ellipsoid("ElaraHairBun", tuple(bun_center), (0.069, 0.061, 0.071), materials["hair"], collection, root, 56, 28)
    bun_binding = add_torus("ElaraBunBinding", (bun_center.x, bun_center.y - 0.01, bun_center.z), 0.064, 0.008, (1.0, 0.78, 1.0), materials["leather_worn"], collection, root)
    bun_binding.rotation_euler[0] = math.radians(90)


def build_skirt(material: bpy.types.Material, collection: bpy.types.Collection, root: bpy.types.Object) -> bpy.types.Object:
    columns_per_panel = 9
    columns = columns_per_panel * 2
    rows = 29
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 0.92 - t * 0.49
        width = 0.18 - t * 0.018
        gap = 0.014 + t * 0.024
        for column in range(columns):
            if column < columns_per_panel:
                local_u = column / (columns_per_panel - 1)
                x = -width + local_u * (width - gap)
            else:
                local_u = (column - columns_per_panel) / (columns_per_panel - 1)
                x = gap + local_u * (width - gap)
            arch = 1.0 - (local_u * 2.0 - 1.0) ** 2
            y = -0.215 - 0.022 * arch - 0.012 * math.cos(local_u * math.pi * 4.0) * (0.3 + 0.7 * t)
            hem = -0.03 * abs(math.sin(local_u * math.pi)) if row == rows - 1 else 0.0
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            if column == columns_per_panel - 1:
                continue
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new("ElaraRangerSkirtMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("ElaraRangerSkirt", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("RangerSkirtThickness", "SOLIDIFY")
    solidify.thickness = 0.008
    subdivision = obj.modifiers.new("RangerSkirtSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    obj.parent = root
    return obj


def build_back_coat(material: bpy.types.Material, collection: bpy.types.Collection, root: bpy.types.Object) -> bpy.types.Object:
    columns = 27
    rows = 38
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 1.31 - t * 0.9
        width = 0.22 + t * 0.12
        for column in range(columns):
            u = column / (columns - 1)
            x = (u * 2.0 - 1.0) * width
            y = 0.105 + 0.016 * math.cos(u * math.pi * 8.0) * (0.35 + 0.65 * t)
            hem = -0.035 * abs(math.sin(u * math.pi * 5.5)) if row == rows - 1 else 0.0
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new("ElaraBackCoatMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("ElaraBackCoat", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("BackCoatThickness", "SOLIDIFY")
    solidify.thickness = 0.008
    subdivision = obj.modifiers.new("BackCoatSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    obj.parent = root
    return obj


def build_equipment(materials: dict[str, bpy.types.Material], collections: dict[str, bpy.types.Collection], root: bpy.types.Object) -> None:
    armor = collections["armor"]
    equipment = collections["equipment"]
    detail = collections["detail"]
    add_torus("ElaraMainBelt", (0, 0, 0.88), 0.205, 0.018, (1.0, 0.66, 1.0), materials["leather"], armor, root)
    add_box("ElaraBeltBuckle", (0, -0.155, 0.88), (0.065, 0.02, 0.055), materials["gold"], detail, root, edge=0.007)
    add_beam_between("ElaraQuiver", (-0.17, 0.13, 1.2), (-0.28, 0.15, 0.52), 0.09, 0.06, materials["leather"], equipment, root, 0.013)
    for index in range(7):
        x = -0.23 + index * 0.016
        add_beam_between(f"ElaraArrowShaft_{index}", (x, 0.155, 0.96), (x + 0.02, 0.155, 1.43 + index * 0.006), 0.007, 0.007, materials["wood"], equipment, root, 0.002)
        add_uv_ellipsoid(f"ElaraArrowHead_{index}", (x + 0.02, 0.155, 1.45 + index * 0.006), (0.011, 0.008, 0.024), materials["steel"], equipment, root, 24, 12)

    # Recurved bow stored along the right side/back in the neutral stance.
    bow_points = [(0.34, 0.12, 1.42), (0.43, 0.14, 1.22), (0.46, 0.14, 0.98), (0.42, 0.14, 0.74), (0.34, 0.12, 0.53)]
    add_curve("ElaraRecurveBow", bow_points, 0.012, materials["wood"], equipment, root)
    add_curve("ElaraBowString", [bow_points[0], (0.38, 0.13, 0.98), bow_points[-1]], 0.0015, materials["string"], equipment, root)
    add_beam_between("ElaraBowGrip", (0.445, 0.125, 0.91), (0.445, 0.125, 1.05), 0.032, 0.025, materials["leather_worn"], equipment, root, 0.005)

    add_beam_between("ElaraChestStrap", (-0.16, -0.19, 1.24), (0.13, -0.2, 0.91), 0.032, 0.014, materials["leather_worn"], armor, root, 0.005)
    build_tree_emblem(-0.225, 1.08, 0.22, True, materials["gold"], detail, root)


def build_character(
    human: bpy.types.Object,
    rig: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    body = collections["body"]
    cloth = collections["cloth"]
    armor = collections["armor"]
    detail = collections["detail"]
    human.name = "Elara_HumanBasemesh"
    human.data.materials.clear()
    human.data.materials.append(materials["skin"])
    move_to_collection(human, body)
    smooth(human)
    subdivision = human.modifiers.new("ElaraRenderSubdivision", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 1
    rig.name = "Elara_GameEngineRig"
    rig.data.name = "Elara_GameEngineRig"
    rig.hide_render = True
    rig.show_in_front = True
    rig.parent = root

    coordinates, _ = evaluated_geometry(human)
    valid_body = body_vertex_indices(human)
    top_height = max(coordinates[index].z for index in valid_body)
    eye_left = group_center(human, "joint-l-eye")
    eye_right = group_center(human, "joint-r-eye")
    add_eye("L", eye_left, materials, detail, root)
    add_eye("R", eye_right, materials, detail, root)

    hair_cap = extract_surface(
        "ElaraFittedHairCap", human, rig, detail, materials["hair"],
        lambda p: (p.z > eye_left.z + 0.055) or (p.z > eye_left.z - 0.015 and p.y > -0.04),
        offset=0.0065, thickness=0.005, root=root,
    )
    texture = bpy.data.textures.new("ElaraHairSurfaceBreakup", type="CLOUDS")
    texture.noise_scale = 0.027
    displacement = hair_cap.modifiers.new("ElaraHairSurfaceBreakup", "DISPLACE")
    displacement.texture = texture
    displacement.strength = 0.004
    displacement.texture_coords = "GLOBAL"
    add_swept_hair(eye_left.z, top_height, materials, detail, root)
    for side, center in (("L", eye_left), ("R", eye_right)):
        direction = 1 if side == "L" else -1
        add_curve(
            f"ElaraBrow_{side}",
            [(center.x - direction * 0.024, center.y - 0.016, center.z + 0.026), (center.x, center.y - 0.021, center.z + 0.033), (center.x + direction * 0.025, center.y - 0.016, center.z + 0.026)],
            0.0025, materials["hair"], detail, root,
        )

    extract_surface("ElaraFittedTunic", human, rig, cloth, materials["green"], lambda p: 0.79 < p.z < 1.27 and abs(p.x) < 0.27, offset=0.012, thickness=0.011, root=root)
    extract_surface("ElaraFittedSleeves", human, rig, cloth, materials["green_dark"], lambda p: 0.88 < p.z < 1.31 and 0.14 < abs(p.x) < 0.45, offset=0.012, thickness=0.011, root=root)
    extract_surface("ElaraShoulderMantle", human, rig, cloth, materials["green_dark"], lambda p: 1.2 < p.z < 1.34 and abs(p.x) < 0.26, offset=0.014, thickness=0.011, root=root)
    extract_surface("ElaraLeatherBodice", human, rig, armor, materials["leather"], lambda p: 0.94 < p.z < 1.22 and abs(p.x) < 0.22 and p.y < -0.09, offset=0.02, thickness=0.013, root=root)
    extract_surface("ElaraFittedTrousers", human, rig, cloth, materials["trousers"], lambda p: 0.29 < p.z < 0.86, offset=0.011, thickness=0.01, root=root)
    extract_surface("ElaraFittedBootShafts", human, rig, armor, materials["boot"], lambda p: 0.1 < p.z < 0.48, offset=0.013, thickness=0.014, root=root)
    extract_surface("ElaraFittedGloves", human, rig, armor, materials["leather"], lambda p: abs(p.x) > 0.37 and 0.83 < p.z < 1.08, offset=0.009, thickness=0.009, root=root)
    extract_surface("ElaraForearmBracers", human, rig, armor, materials["leather_worn"], lambda p: 0.29 < abs(p.x) < 0.44 and 0.9 < p.z < 1.13, offset=0.02, thickness=0.011, root=root)

    build_skirt(materials["green_worn"], cloth, root)
    build_back_coat(materials["green_dark"], cloth, root)
    for index, (z, radius) in enumerate(((1.325, 0.108), (1.339, 0.101), (1.352, 0.095))):
        add_torus(f"ElaraCowlFold_{index}", (0, -0.004, z), radius, 0.012, (1.0, 0.74, 1.0), materials["green_dark"], cloth, root)

    # Use the actual female foot centers from the morphed MPFB body.
    foot_centers: dict[int, float] = {}
    for side in (-1, 1):
        foot_x = [coordinates[index].x for index in valid_body if coordinates[index].z < 0.19 and coordinates[index].x * side > 0]
        foot_centers[side] = sum(foot_x) / len(foot_x)
        foot_bone = "foot_l" if side > 0 else "foot_r"
        boot_shell = add_boot_foot(f"ElaraBootFoot_{side}", foot_centers[side], materials["boot"], armor, root)
        boot_sole = add_box(f"ElaraBootSole_{side}", (foot_centers[side], -0.09, 0.004), (0.13, 0.28, 0.026), materials["boot"], armor, root, edge=0.011)
        parent_to_bone_keep_world(boot_shell, rig, foot_bone)
        parent_to_bone_keep_world(boot_sole, rig, foot_bone)

    visible_body_group = human.vertex_groups.get("body")
    if visible_body_group is not None:
        visible_body_group.remove([index for index in valid_body if coordinates[index].z < 0.19])
    build_equipment(materials, collections, root)


def render_and_save(root: bpy.types.Object, camera: bpy.types.Object) -> None:
    scene = bpy.context.scene
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    camera.data.lens = 72
    camera.location = (1.62, -4.0, 1.78)
    target = Vector((0, 0, 0.84))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    root.rotation_euler[2] = math.radians(-10)
    scene.render.filepath = str(FRONT_RENDER)
    bpy.ops.render.render(write_still=True)
    root.rotation_euler[2] = math.radians(170)
    scene.render.filepath = str(BACK_RENDER)
    bpy.ops.render.render(write_still=True)
    root.rotation_euler[2] = math.radians(-10)
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1000
    camera.location = (0.73, -2.16, 1.7)
    face_target = Vector((0, -0.02, 1.4))
    camera.rotation_euler = (face_target - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(FACE_RENDER)
    bpy.ops.render.render(write_still=True)
    root.rotation_euler[2] = 0
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(f"BLEND={BLEND_PATH}")
    print(f"FRONT={FRONT_RENDER}")
    print(f"BACK={BACK_RENDER}")
    print(f"FACE={FACE_RENDER}")


def main() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    FRONT_RENDER.parent.mkdir(parents=True, exist_ok=True)
    reset_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    human = create_human()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BASE_BLEND))

    collections = {
        "body": make_collection("01_RealHumanAnatomy"),
        "cloth": make_collection("02_TailoredCloth"),
        "armor": make_collection("03_FittedArmor"),
        "equipment": make_collection("04_RangerEquipment"),
        "detail": make_collection("05_HairFaceAndInsignia"),
        "studio": make_collection("90_ReviewStudio"),
    }
    root = bpy.data.objects.new("ElaraCharacterRoot", None)
    collections["body"].objects.link(root)
    materials = create_materials()
    rig = HumanService.add_builtin_rig(human, "game_engine", import_weights=True)
    build_character(human, rig, materials, collections, root)
    camera = setup_studio(root, collections["studio"], materials)
    render_and_save(root, camera)


if __name__ == "__main__":
    main()
