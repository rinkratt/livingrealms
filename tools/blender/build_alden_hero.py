"""Build the second-generation Alden hero character entirely in Blender.

This pass replaces the toy-like proportions of the early procedural review
with an adult silhouette, smaller facial features, fitted layered clothing,
weathered materials, articulated hands, and denser costume detail.  It is kept
separate from the currently playable asset until its review renders pass.
"""

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

from build_alden_sculpt import (
    add_beam_between,
    add_box,
    add_capsule_between,
    add_curve,
    add_cylinder,
    add_tabard_panel,
    add_torus,
    add_uv_ellipsoid,
    build_tree_emblem,
    make_collection,
    make_material,
    move_to_collection,
    parent_character,
    reset_scene,
    setup_studio,
    smooth,
)


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "assets" / "3d-source" / "characters" / "alden" / "hero-source"
BLEND_PATH = SOURCE_DIR / "alden_hero_review.blend"
FRONT_RENDER = ROOT / "docs" / "phase-8-alden-hero-front-review.png"
BACK_RENDER = ROOT / "docs" / "phase-8-alden-hero-back-review.png"

random.seed(3917)


def create_materials() -> dict[str, bpy.types.Material]:
    return {
        "skin": make_material("AldenHero_Skin", "#8f5d49", 0.48, noise_scale=125, bump_strength=0.065, subsurface=0.075),
        "skin_warm": make_material("AldenHero_SkinWarm", "#a46e55", 0.5, noise_scale=110, bump_strength=0.055, subsurface=0.065),
        "nostril": make_material("AldenHero_Nostril", "#3c211e", 0.58),
        "lip": make_material("AldenHero_Lip", "#74433e", 0.47, noise_scale=85, bump_strength=0.035),
        "eye_white": make_material("AldenHero_EyeWhite", "#d8d1c5", 0.19),
        "iris": make_material("AldenHero_Iris", "#526a72", 0.16, noise_scale=30, bump_strength=0.04),
        "pupil": make_material("AldenHero_Pupil", "#070909", 0.1),
        "hair": make_material("AldenHero_Hair", "#25170f", 0.42, noise_scale=78, bump_strength=0.22),
        "hair_highlight": make_material("AldenHero_HairHighlight", "#493020", 0.46, noise_scale=68, bump_strength=0.18),
        "beard": make_material("AldenHero_Beard", "#302019", 0.54, noise_scale=155, bump_strength=0.16),
        "blue": make_material("AldenHero_DeepBlueCloth", "#142837", 0.82, noise_scale=175, bump_strength=0.32),
        "blue_mid": make_material("AldenHero_WornBlue", "#223d4d", 0.8, noise_scale=150, bump_strength=0.28),
        "blue_dark": make_material("AldenHero_BlueShadow", "#0d1922", 0.86, noise_scale=160, bump_strength=0.3),
        "blue_trim": make_material("AldenHero_BlueGoldTrim", "#82632d", 0.58, 0.2, noise_scale=95, bump_strength=0.14),
        "trousers": make_material("AldenHero_Trousers", "#131516", 0.88, noise_scale=105, bump_strength=0.25),
        "leather": make_material("AldenHero_DarkLeather", "#2b1c14", 0.56, noise_scale=72, bump_strength=0.28),
        "leather_mid": make_material("AldenHero_WornLeather", "#543622", 0.55, noise_scale=68, bump_strength=0.26),
        "leather_edge": make_material("AldenHero_LeatherEdge", "#846041", 0.53, noise_scale=65, bump_strength=0.2),
        "steel": make_material("AldenHero_WeatheredSteel", "#596166", 0.33, 0.9, noise_scale=95, bump_strength=0.15),
        "steel_dark": make_material("AldenHero_BlackenedSteel", "#252b2f", 0.39, 0.86, noise_scale=110, bump_strength=0.18),
        "steel_edge": make_material("AldenHero_PolishedEdges", "#92989a", 0.22, 0.94, noise_scale=72, bump_strength=0.08),
        "gold": make_material("AldenHero_AntiqueGold", "#a9792c", 0.29, 0.78, noise_scale=88, bump_strength=0.12),
        "boot": make_material("AldenHero_BootLeather", "#211a15", 0.61, noise_scale=75, bump_strength=0.32),
        "studio_floor": make_material("AldenHero_StudioFloor", "#171a1e", 0.58, noise_scale=28, bump_strength=0.08),
    }


def build_head(
    materials: dict[str, bpy.types.Material],
    body: bpy.types.Collection,
    detail: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    skin = materials["skin"]
    warm = materials["skin_warm"]
    hair = materials["hair"]
    hair_hi = materials["hair_highlight"]

    # Adult head: roughly one seventh of total height, with a narrower jaw and
    # facial features sized for a stern rather than toy-like expression.
    add_uv_ellipsoid("HeroSkull", (0, 0.015, 1.785), (0.125, 0.105, 0.155), skin, body, root)
    add_uv_ellipsoid("HeroFace", (0, -0.075, 1.775), (0.108, 0.046, 0.128), skin, body, root)
    add_uv_ellipsoid("HeroJaw", (0, -0.055, 1.69), (0.092, 0.068, 0.082), skin, body, root)
    add_uv_ellipsoid("HeroChin", (0, -0.105, 1.655), (0.043, 0.032, 0.038), warm, detail, root, 48, 24)
    add_uv_ellipsoid("HeroCheekL", (-0.071, -0.103, 1.753), (0.052, 0.026, 0.052), warm, detail, root, 48, 24)
    add_uv_ellipsoid("HeroCheekR", (0.071, -0.103, 1.753), (0.052, 0.026, 0.052), warm, detail, root, 48, 24)
    add_uv_ellipsoid("HeroEarL", (-0.128, 0.003, 1.78), (0.022, 0.014, 0.039), warm, body, root, 40, 20)
    add_uv_ellipsoid("HeroEarR", (0.128, 0.003, 1.78), (0.022, 0.014, 0.039), warm, body, root, 40, 20)

    add_uv_ellipsoid("HeroNoseBridge", (0, -0.125, 1.795), (0.018, 0.025, 0.058), skin, detail, root, 48, 24)
    add_uv_ellipsoid("HeroNoseTip", (0, -0.151, 1.762), (0.026, 0.022, 0.024), warm, detail, root, 48, 24)
    add_uv_ellipsoid("HeroNostrilL", (-0.019, -0.148, 1.756), (0.009, 0.006, 0.008), materials["nostril"], detail, root, 32, 16)
    add_uv_ellipsoid("HeroNostrilR", (0.019, -0.148, 1.756), (0.009, 0.006, 0.008), materials["nostril"], detail, root, 32, 16)

    for side, x in (("L", -0.043), ("R", 0.043)):
        add_uv_ellipsoid(f"HeroEyeWhite{side}", (x, -0.123, 1.814), (0.024, 0.011, 0.013), materials["eye_white"], detail, root, 48, 24)
        add_uv_ellipsoid(f"HeroIris{side}", (x, -0.133, 1.814), (0.009, 0.004, 0.009), materials["iris"], detail, root, 40, 20)
        add_uv_ellipsoid(f"HeroPupil{side}", (x, -0.137, 1.814), (0.0035, 0.0018, 0.0035), materials["pupil"], detail, root, 32, 16)
        sign = -1 if side == "L" else 1
        add_curve(
            f"HeroUpperLid{side}",
            [(x - sign * 0.025, -0.137, 1.818), (x, -0.143, 1.827), (x + sign * 0.025, -0.137, 1.818)],
            0.0034,
            skin,
            detail,
            root,
        )
        add_curve(
            f"HeroBrow{side}",
            [(x - sign * 0.031, -0.141, 1.851), (x, -0.149, 1.858), (x + sign * 0.034, -0.142, 1.845)],
            0.006,
            hair,
            detail,
            root,
        )

    add_curve("HeroUpperLip", [(-0.035, -0.145, 1.706), (0, -0.153, 1.713), (0.035, -0.145, 1.706)], 0.0055, materials["lip"], detail, root)
    add_curve("HeroLowerLip", [(-0.031, -0.143, 1.696), (0, -0.151, 1.691), (0.031, -0.143, 1.696)], 0.005, materials["lip"], detail, root)

    # Close-cropped beard volume and separate moustache break up the smooth
    # facial shell without inflating the jaw.
    add_uv_ellipsoid("HeroBeardMask", (0, -0.094, 1.69), (0.092, 0.027, 0.082), materials["beard"], detail, root, 64, 32)
    add_curve("HeroMustacheL", [(-0.003, -0.157, 1.724), (-0.025, -0.159, 1.719), (-0.052, -0.148, 1.714)], 0.0045, hair, detail, root)
    add_curve("HeroMustacheR", [(0.003, -0.157, 1.724), (0.025, -0.159, 1.719), (0.052, -0.148, 1.714)], 0.0045, hair, detail, root)

    # Hair cap plus many narrow clumps produces a deliberately messy medieval
    # cut while retaining a single readable mass at game distance.
    add_uv_ellipsoid("HeroHairCap", (0, 0.025, 1.885), (0.137, 0.115, 0.106), hair, body, root)
    front_roots = [(-0.105, 1.92), (-0.075, 1.944), (-0.042, 1.955), (-0.008, 1.958), (0.028, 1.955), (0.065, 1.942), (0.101, 1.918)]
    for index, (x, z) in enumerate(front_roots):
        direction = -1 if x < 0 else 1
        end_x = x + direction * random.uniform(0.008, 0.026)
        end_z = z - random.uniform(0.075, 0.13)
        add_curve(
            f"HeroFrontHair_{index}",
            [(x, -0.075, z), (x * 1.08, -0.125, z - 0.04), (end_x, -0.132, end_z)],
            random.uniform(0.0065, 0.009),
            hair_hi if index % 3 == 0 else hair,
            detail,
            root,
        )
    for side in (-1, 1):
        for index in range(9):
            z = 1.93 - index * 0.018
            x = side * (0.112 + index * 0.002)
            add_curve(
                f"HeroTempleHair_{side}_{index}",
                [(x, -0.025, z), (x + side * 0.02, -0.055, z - 0.045), (x + side * 0.008, -0.065, z - 0.095)],
                random.uniform(0.0045, 0.007),
                hair_hi if index % 4 == 0 else hair,
                detail,
                root,
            )


def add_hero_cloak(material: bpy.types.Material, collection: bpy.types.Collection, root: bpy.types.Object) -> bpy.types.Object:
    columns = 32
    rows = 44
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 1.49 - t * 1.20
        width = 0.35 + t * 0.12
        for column in range(columns):
            u = column / (columns - 1)
            x = (u * 2 - 1) * width
            fold = 0.024 * math.cos(u * math.pi * 9) * (0.25 + t * 0.75)
            y = 0.205 + fold + 0.012 * math.sin(t * math.pi * 2.5)
            hem = -0.035 * (0.5 + 0.5 * math.sin(u * math.pi * 13)) if row == rows - 1 else 0.0
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new("AldenHeroCloakMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("AldenHeroCloak", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("HeroCloakThickness", "SOLIDIFY")
    solidify.thickness = 0.009
    solidify.offset = 0.0
    subdivision = obj.modifiers.new("HeroCloakSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    return parent_character(obj, root)


def build_costume(
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    body = collections["body"]
    cloth = collections["cloth"]
    armor = collections["armor"]
    equipment = collections["equipment"]
    detail = collections["detail"]

    add_cylinder("HeroNeck", (0, 0, 1.585), 0.082, 0.18, materials["skin"], body, root, 48, (1.0, 0.92))
    build_head(materials, body, detail, root)

    # A fitted adult torso replaces the wide spherical early prototype.
    add_uv_ellipsoid("HeroPaddedTorso", (0, 0.0, 1.285), (0.31, 0.19, 0.31), materials["blue"], cloth, root)
    add_uv_ellipsoid("HeroWaist", (0, 0.0, 1.02), (0.25, 0.16, 0.19), materials["blue_dark"], cloth, root)
    add_uv_ellipsoid("HeroChestFront", (0, -0.155, 1.31), (0.285, 0.045, 0.245), materials["blue_mid"], cloth, root)

    add_tabard_panel("HeroFrontTabard", 0, -0.205, 1.07, 0.42, 0.44, 0.54, materials["blue"], cloth, root)
    add_tabard_panel("HeroLeftSkirt", -0.20, -0.015, 1.02, 0.43, 0.24, 0.29, materials["blue_dark"], cloth, root)
    add_tabard_panel("HeroRightSkirt", 0.20, -0.015, 1.02, 0.43, 0.24, 0.29, materials["blue_dark"], cloth, root)

    shoulder_points = {
        "L": ((-0.315, 0, 1.45), (-0.395, -0.005, 1.18), (-0.38, -0.015, 0.91)),
        "R": ((0.315, 0, 1.45), (0.395, -0.005, 1.18), (0.38, -0.015, 0.91)),
    }
    for side, (shoulder, elbow, wrist) in shoulder_points.items():
        sign = -1 if side == "L" else 1
        add_capsule_between(f"HeroUpperArm_{side}", shoulder, elbow, 0.082, materials["blue_dark"], cloth, root, 1.02)
        add_capsule_between(f"HeroForearm_{side}", elbow, wrist, 0.069, materials["leather"], armor, root, 1.0)
        hand_center = (wrist[0], wrist[1] - 0.008, wrist[2] - 0.075)
        add_uv_ellipsoid(f"HeroGlovePalm_{side}", hand_center, (0.045, 0.036, 0.072), materials["leather"], armor, root, 48, 24)
        for finger in range(4):
            finger_x = hand_center[0] + sign * (finger - 1.5) * 0.014
            add_capsule_between(
                f"HeroGloveFinger_{side}_{finger}",
                (finger_x, hand_center[1] - 0.005, hand_center[2] - 0.026),
                (finger_x, hand_center[1] - 0.006, hand_center[2] - 0.075),
                0.008,
                materials["leather"],
                armor,
                root,
            )
        thumb_start = (hand_center[0] - sign * 0.035, hand_center[1] - 0.008, hand_center[2] - 0.005)
        thumb_end = (hand_center[0] - sign * 0.055, hand_center[1] - 0.012, hand_center[2] - 0.045)
        add_capsule_between(f"HeroGloveThumb_{side}", thumb_start, thumb_end, 0.009, materials["leather"], armor, root)

        add_uv_ellipsoid(f"HeroPauldron_{side}", (sign * 0.335, -0.005, 1.46), (0.125, 0.14, 0.06), materials["steel_dark"], armor, root, 64, 32)
        add_uv_ellipsoid(f"HeroPauldronEdge_{side}", (sign * 0.342, -0.025, 1.445), (0.132, 0.145, 0.022), materials["steel_edge"], detail, root, 56, 28)
        for plate in range(3):
            add_uv_ellipsoid(
                f"HeroPauldronPlate_{side}_{plate}",
                (sign * (0.35 + plate * 0.012), -0.002, 1.395 - plate * 0.052),
                (0.092 - plate * 0.005, 0.115, 0.031),
                materials["steel"] if plate % 2 == 0 else materials["steel_dark"],
                armor,
                root,
                56,
                28,
            )
        add_capsule_between(f"HeroBracer_{side}", elbow, wrist, 0.079, materials["steel_dark"], armor, root, 1.03)
        for t in (0.18, 0.48, 0.78):
            point = Vector(elbow).lerp(Vector(wrist), t)
            add_uv_ellipsoid(f"HeroBracerBand_{side}_{t}", tuple(point), (0.079, 0.076, 0.019), materials["steel_edge"], detail, root, 48, 24)

    for side, x in (("L", -0.145), ("R", 0.145)):
        knee = (x, 0.0, 0.47)
        ankle = (x, 0.005, 0.13)
        add_capsule_between(f"HeroThigh_{side}", (x, 0, 0.86), knee, 0.105, materials["trousers"], cloth, root, 1.02)
        add_capsule_between(f"HeroShin_{side}", knee, ankle, 0.092, materials["boot"], armor, root, 0.98)
        add_uv_ellipsoid(f"HeroKneeGuard_{side}", (x, -0.075, 0.47), (0.102, 0.052, 0.082), materials["steel_dark"], armor, root, 48, 24)
        add_uv_ellipsoid(f"HeroBootFoot_{side}", (x, -0.082, 0.055), (0.105, 0.15, 0.064), materials["boot"], armor, root, 56, 28)
        for strap_z in (0.20, 0.30):
            add_cylinder(f"HeroBootStrap_{side}_{strap_z}", (x, 0.005, strap_z), 0.102, 0.043, materials["leather_mid"], armor, root, 44, (1.0, 0.82))

    add_cylinder("HeroBelt", (0, 0, 1.01), 0.285, 0.09, materials["leather"], armor, root, 64, (1.0, 0.68))
    add_box("HeroBeltBuckle", (0, -0.195, 1.01), (0.105, 0.03, 0.085), materials["gold"], detail, root, edge=0.014)
    add_box("HeroBuckleInset", (0, -0.214, 1.01), (0.055, 0.01, 0.038), materials["leather"], detail, root, edge=0.006)
    add_box("HeroPouchL", (-0.26, -0.025, 0.90), (0.14, 0.10, 0.17), materials["leather_mid"], equipment, root, (0, 0.1, 0.06), 0.02)
    add_box("HeroPouchR", (0.26, -0.025, 0.90), (0.14, 0.10, 0.17), materials["leather"], equipment, root, (0, -0.1, -0.06), 0.02)

    add_beam_between("HeroCrossBodyStrap", (-0.26, -0.222, 1.51), (0.21, -0.228, 1.06), 0.064, 0.026, materials["leather_mid"], armor, root, 0.009)
    for t in (0.18, 0.43, 0.68, 0.90):
        point = Vector((-0.26, -0.242, 1.51)).lerp(Vector((0.21, -0.242, 1.06)), t)
        add_uv_ellipsoid(f"HeroStrapRivet_{t}", tuple(point), (0.011, 0.006, 0.011), materials["gold"], detail, root, 24, 12)

    add_torus("HeroScarfLower", (0, 0, 1.55), 0.205, 0.052, (1.0, 0.78, 0.82), materials["blue_mid"], cloth, root)
    add_torus("HeroScarfUpper", (0, -0.003, 1.592), 0.176, 0.044, (1.0, 0.80, 0.76), materials["blue"], cloth, root)
    add_hero_cloak(materials["blue_dark"], cloth, root)

    # Sword and scabbard sit behind the right hip, preserving the readable
    # vanguard profile without intersecting the hands.
    add_beam_between("HeroScabbard", (0.31, 0.07, 0.93), (0.48, 0.09, 0.19), 0.072, 0.055, materials["leather"], equipment, root, 0.012)
    add_beam_between("HeroSwordGrip", (0.30, 0.07, 1.01), (0.26, 0.07, 1.18), 0.043, 0.04, materials["leather_edge"], equipment, root, 0.006)
    add_beam_between("HeroSwordGuard", (0.20, 0.065, 1.08), (0.37, 0.065, 1.04), 0.022, 0.025, materials["steel_edge"], equipment, root, 0.004)
    add_uv_ellipsoid("HeroSwordPommel", (0.25, 0.07, 1.20), (0.038, 0.034, 0.045), materials["gold"], equipment, root, 32, 16)

    build_tree_emblem(-0.214, 1.30, 0.32, True, materials["gold"], detail, root)
    build_tree_emblem(0.218, 0.93, 0.58, False, materials["gold"], detail, root)
    for x in (-0.24, -0.16, -0.08, 0.08, 0.16, 0.24):
        add_beam_between(f"HeroGambesonSeam_{x}", (x, -0.207, 1.10), (x, -0.207, 1.48), 0.008, 0.006, materials["blue_trim"], detail, root, 0.002)
    for z in (0.60, 0.72, 0.84):
        add_box(f"HeroTabardTrim_{z}", (0, -0.226, z), (0.44 + (0.84 - z) * 0.10, 0.009, 0.014), materials["blue_trim"], detail, root, edge=0.003)


def build_rig(collection: bpy.types.Collection) -> bpy.types.Object:
    armature = bpy.data.armatures.new("AldenHeroRig")
    rig = bpy.data.objects.new("AldenHeroRig", armature)
    collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bones: dict[str, bpy.types.EditBone] = {}

    def add_bone(name: str, head: tuple[float, float, float], tail: tuple[float, float, float], parent: str | None = None) -> None:
        bone = armature.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        if parent:
            bone.parent = bones[parent]
        bones[name] = bone

    add_bone("root", (0, 0, 0.02), (0, 0, 0.20))
    add_bone("pelvis", (0, 0, 0.84), (0, 0, 1.02), "root")
    add_bone("spine", (0, 0, 1.02), (0, 0, 1.28), "pelvis")
    add_bone("chest", (0, 0, 1.28), (0, 0, 1.47), "spine")
    add_bone("neck", (0, 0, 1.47), (0, 0, 1.62), "chest")
    add_bone("head", (0, 0, 1.62), (0, 0, 1.94), "neck")
    for side, sign in (("L", -1), ("R", 1)):
        add_bone(f"clavicle.{side}", (0, 0, 1.45), (sign * 0.315, 0, 1.45), "chest")
        add_bone(f"upper_arm.{side}", (sign * 0.315, 0, 1.45), (sign * 0.395, 0, 1.18), f"clavicle.{side}")
        add_bone(f"forearm.{side}", (sign * 0.395, 0, 1.18), (sign * 0.38, -0.015, 0.91), f"upper_arm.{side}")
        add_bone(f"hand.{side}", (sign * 0.38, -0.015, 0.91), (sign * 0.38, -0.02, 0.76), f"forearm.{side}")
        add_bone(f"thigh.{side}", (sign * 0.145, 0, 0.86), (sign * 0.145, 0, 0.47), "pelvis")
        add_bone(f"shin.{side}", (sign * 0.145, 0, 0.47), (sign * 0.145, 0, 0.13), f"thigh.{side}")
        add_bone(f"foot.{side}", (sign * 0.145, 0, 0.13), (sign * 0.145, -0.20, 0.055), f"shin.{side}")
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    rig.hide_render = True
    return rig


def main() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    FRONT_RENDER.parent.mkdir(parents=True, exist_ok=True)
    reset_scene()
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    collections = {
        "body": make_collection("01_HeroAnatomy"),
        "cloth": make_collection("02_HeroCloth"),
        "armor": make_collection("03_HeroArmor"),
        "equipment": make_collection("04_HeroEquipment"),
        "detail": make_collection("05_HeroDetail"),
        "rig": make_collection("06_HeroRig"),
        "studio": make_collection("90_HeroReviewStudio"),
    }
    root = bpy.data.objects.new("AldenHeroCharacterRoot", None)
    collections["body"].objects.link(root)
    materials = create_materials()
    build_costume(materials, collections, root)
    build_rig(collections["rig"])
    camera = setup_studio(root, collections["studio"], materials)
    camera.name = "AldenHeroReviewCamera"
    camera.data.lens = 82
    camera.location = (1.85, -5.35, 2.20)
    target = Vector((0, 0, 1.03))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.render.resolution_x = 1100
    bpy.context.scene.render.resolution_y = 1400
    bpy.context.scene.view_settings.exposure = 0.12

    root.rotation_euler[2] = math.radians(-7)
    bpy.context.scene.render.filepath = str(FRONT_RENDER)
    bpy.ops.render.render(write_still=True)
    root.rotation_euler[2] = math.radians(173)
    bpy.context.scene.render.filepath = str(BACK_RENDER)
    bpy.ops.render.render(write_still=True)
    root.rotation_euler[2] = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(f"BLEND={BLEND_PATH}")
    print(f"FRONT={FRONT_RENDER}")
    print(f"BACK={BACK_RENDER}")


if __name__ == "__main__":
    main()
