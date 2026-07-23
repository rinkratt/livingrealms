"""Create Elara, the original Living Realms ranger review model.

Elara deliberately shares Alden's accepted first-pass visual language while
using a distinct silhouette, face, layered ranger leathers, bow, quiver,
arrows, and green Living Realms field clothing.
"""

from __future__ import annotations

import math
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
    bevel,
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
SOURCE_DIR = ROOT / "assets" / "3d-source" / "characters" / "elara" / "source"
BLEND_PATH = SOURCE_DIR / "elara_highpoly_review.blend"
FRONT_RENDER = ROOT / "docs" / "phase-8-elara-front-review.png"
BACK_RENDER = ROOT / "docs" / "phase-8-elara-back-review.png"


def create_materials() -> dict[str, bpy.types.Material]:
    return {
        "skin": make_material("Elara_Skin", "#9f6a50", 0.55, noise_scale=100, bump_strength=0.07, subsurface=0.07),
        "nostril": make_material("Elara_Nostril", "#40231e", 0.64),
        "lip": make_material("Elara_Lips", "#8d5048", 0.5, noise_scale=70, bump_strength=0.05),
        "eye_white": make_material("Elara_EyeWhite", "#ded7cc", 0.22),
        "iris": make_material("Elara_Iris", "#5c7354", 0.18, noise_scale=24, bump_strength=0.08),
        "pupil": make_material("Elara_Pupil", "#070908", 0.12),
        "hair": make_material("Elara_Hair", "#2a190f", 0.5, noise_scale=65, bump_strength=0.2),
        "green_cloth": make_material("Elara_ForestGreen", "#253527", 0.86, noise_scale=135, bump_strength=0.3),
        "green_light": make_material("Elara_MossGreen", "#415438", 0.82, noise_scale=120, bump_strength=0.26),
        "green_trim": make_material("Elara_GreenTrim", "#6b7942", 0.72, 0.08, noise_scale=80, bump_strength=0.14),
        "trousers": make_material("Elara_Trousers", "#181918", 0.9, noise_scale=90, bump_strength=0.24),
        "leather": make_material("Elara_LeatherDark", "#35231a", 0.66, noise_scale=52, bump_strength=0.24),
        "leather_mid": make_material("Elara_LeatherMid", "#5b3a25", 0.62, noise_scale=58, bump_strength=0.25),
        "leather_light": make_material("Elara_LeatherWorn", "#795034", 0.61, noise_scale=60, bump_strength=0.22),
        "boot": make_material("Elara_Boots", "#2a2019", 0.7, noise_scale=58, bump_strength=0.3),
        "bow_wood": make_material("Elara_BowYew", "#704421", 0.53, noise_scale=32, bump_strength=0.2),
        "bow_wrap": make_material("Elara_BowGrip", "#2c1e16", 0.74, noise_scale=68, bump_strength=0.23),
        "bow_string": make_material("Elara_BowString", "#c2aa79", 0.6),
        "arrow_wood": make_material("Elara_ArrowShaft", "#8b6740", 0.58, noise_scale=36, bump_strength=0.12),
        "steel": make_material("Elara_Steel", "#555b59", 0.44, 0.84, noise_scale=80, bump_strength=0.14),
        "gold": make_material("Elara_AntiqueGold", "#ae8132", 0.36, 0.7, noise_scale=70, bump_strength=0.11),
        "feather": make_material("Elara_ArrowFeather", "#6d3825", 0.82, noise_scale=90, bump_strength=0.2),
        "studio_floor": make_material("ElaraStudioFloor", "#202226", 0.62, noise_scale=24, bump_strength=0.08),
    }


def add_ranger_coat(
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Build a fitted, split-tailed ranger coat with soft longitudinal folds."""
    columns = 24
    rows = 34
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 1.42 - t * 1.02
        width = 0.30 + t * 0.11
        for column in range(columns):
            u = column / (columns - 1)
            x = (u * 2.0 - 1.0) * width
            fold = 0.026 * math.cos(u * math.pi * 7.0) * (0.3 + 0.7 * t)
            y = 0.185 + fold
            # A shallow inverted-V split reads clearly in the back review.
            split = 0.0
            if t > 0.65:
                center = abs(u - 0.5) * 2.0
                split = 0.12 * (1.0 - center) * ((t - 0.65) / 0.35)
            hem = -0.025 * (0.5 + 0.5 * math.sin(u * math.pi * 9.0)) if row == rows - 1 else 0.0
            vertices.append((x, y, z + split + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new("ElaraRangerCoatMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("ElaraRangerCoat", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("CoatThickness", "SOLIDIFY")
    solidify.thickness = 0.011
    solidify.offset = 0.0
    subdivision = obj.modifiers.new("CoatSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    return parent_character(obj, root)


def build_face(
    materials: dict[str, bpy.types.Material],
    body: bpy.types.Collection,
    detail: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    skin = materials["skin"]
    hair = materials["hair"]

    add_uv_ellipsoid("ElaraSkull", (0, 0.012, 1.785), (0.125, 0.11, 0.16), skin, body, root)
    add_uv_ellipsoid("ElaraJaw", (0, -0.045, 1.705), (0.098, 0.072, 0.09), skin, body, root)
    add_uv_ellipsoid("ElaraFacePlane", (0, -0.09, 1.79), (0.105, 0.037, 0.12), skin, body, root)
    add_uv_ellipsoid("ElaraChin", (0, -0.096, 1.665), (0.043, 0.032, 0.039), skin, body, root, 48, 24)
    add_uv_ellipsoid("ElaraEarL", (0.126, 0.0, 1.785), (0.023, 0.016, 0.041), skin, body, root, 40, 20)
    add_uv_ellipsoid("ElaraEarR", (-0.126, 0.0, 1.785), (0.023, 0.016, 0.041), skin, body, root, 40, 20)

    add_uv_ellipsoid("ElaraNoseBridge", (0, -0.119, 1.79), (0.021, 0.031, 0.058), skin, detail, root, 48, 24)
    add_uv_ellipsoid("ElaraNoseTip", (0, -0.143, 1.762), (0.027, 0.024, 0.025), skin, detail, root, 48, 24)
    add_uv_ellipsoid("ElaraNostrilL", (0.019, -0.139, 1.758), (0.01, 0.008, 0.009), materials["nostril"], detail, root, 32, 16)
    add_uv_ellipsoid("ElaraNostrilR", (-0.019, -0.139, 1.758), (0.01, 0.008, 0.009), materials["nostril"], detail, root, 32, 16)

    for side, x in (("L", 0.044), ("R", -0.044)):
        add_uv_ellipsoid(f"ElaraEyeWhite{side}", (x, -0.122, 1.807), (0.027, 0.012, 0.017), materials["eye_white"], detail, root, 48, 24)
        add_uv_ellipsoid(f"ElaraIris{side}", (x, -0.134, 1.807), (0.011, 0.004, 0.011), materials["iris"], detail, root, 40, 20)
        add_uv_ellipsoid(f"ElaraPupil{side}", (x, -0.138, 1.807), (0.0045, 0.002, 0.0045), materials["pupil"], detail, root, 32, 16)
        outer = 0.038 if side == "L" else -0.038
        add_curve(
            f"ElaraEyebrow{side}",
            [(x + outer, -0.137, 1.84), (x, -0.144, 1.851), (x - outer, -0.137, 1.844)],
            0.006,
            hair,
            detail,
            root,
        )

    add_curve("ElaraUpperLip", [(-0.036, -0.14, 1.718), (0, -0.15, 1.723), (0.036, -0.14, 1.718)], 0.006, materials["lip"], detail, root)
    add_curve("ElaraLowerLip", [(-0.031, -0.138, 1.708), (0, -0.147, 1.703), (0.031, -0.138, 1.708)], 0.0055, materials["lip"], detail, root)

    # An upswept hair mass and bun echo the supplied ranger concept sheet.
    add_uv_ellipsoid("ElaraHairMass", (0, 0.025, 1.865), (0.136, 0.118, 0.125), hair, body, root)
    add_uv_ellipsoid("ElaraHairBun", (0, 0.085, 1.985), (0.082, 0.073, 0.078), hair, body, root, 56, 28)
    for index, points in enumerate(
        [
            [(-0.11, -0.08, 1.89), (-0.08, -0.13, 1.85), (-0.06, -0.125, 1.79)],
            [(-0.055, -0.105, 1.92), (-0.025, -0.145, 1.87), (-0.015, -0.13, 1.82)],
            [(0.025, -0.108, 1.93), (0.045, -0.145, 1.88), (0.04, -0.13, 1.82)],
            [(0.095, -0.075, 1.9), (0.115, -0.12, 1.84), (0.09, -0.12, 1.79)],
        ]
    ):
        add_curve(f"ElaraForeheadHair_{index}", points, 0.011, hair, detail, root)
    for side in (-1, 1):
        add_curve(
            f"ElaraLooseTempleHair_{side}",
            [(side * 0.115, -0.04, 1.88), (side * 0.135, -0.04, 1.79), (side * 0.105, -0.055, 1.69)],
            0.008,
            hair,
            detail,
            root,
        )


def build_body_and_costume(
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    body = collections["body"]
    cloth = collections["cloth"]
    armor = collections["armor"]
    detail = collections["detail"]
    equipment = collections["equipment"]

    add_cylinder("ElaraNeck", (0, 0, 1.62), 0.078, 0.19, materials["skin"], body, root, 40, (1.0, 0.9))
    build_face(materials, body, detail, root)

    add_uv_ellipsoid("ElaraTunicTorso", (0, 0.0, 1.30), (0.305, 0.19, 0.32), materials["green_cloth"], cloth, root)
    add_uv_ellipsoid("ElaraWaist", (0, 0.0, 1.05), (0.25, 0.17, 0.20), materials["green_cloth"], cloth, root)
    add_uv_ellipsoid("ElaraLeatherBodice", (0, -0.16, 1.29), (0.285, 0.055, 0.25), materials["leather"], armor, root)
    add_box("ElaraBodiceCenter", (0, -0.218, 1.26), (0.11, 0.025, 0.42), materials["leather_mid"], armor, root, edge=0.018)

    add_tabard_panel("ElaraFrontSkirt", 0.0, -0.205, 1.06, 0.49, 0.44, 0.52, materials["green_cloth"], cloth, root)
    add_tabard_panel("ElaraLeftSkirt", -0.22, -0.01, 1.02, 0.48, 0.24, 0.30, materials["green_cloth"], cloth, root)
    add_tabard_panel("ElaraRightSkirt", 0.22, -0.01, 1.02, 0.48, 0.24, 0.30, materials["green_cloth"], cloth, root)
    add_ranger_coat(materials["green_light"], cloth, root)

    shoulder_points = {
        "L": ((0.315, 0.0, 1.45), (0.395, -0.006, 1.18), (0.38, -0.02, 0.90)),
        "R": ((-0.315, 0.0, 1.45), (-0.395, -0.006, 1.18), (-0.38, -0.02, 0.90)),
    }
    for side, (shoulder, elbow, wrist) in shoulder_points.items():
        sign = 1 if side == "L" else -1
        add_capsule_between(f"ElaraUpperArm_{side}", shoulder, elbow, 0.084, materials["green_cloth"], cloth, root, 1.05)
        add_capsule_between(f"ElaraForearm_{side}", elbow, wrist, 0.068, materials["leather"], armor, root)
        hand = (wrist[0], wrist[1] - 0.012, wrist[2] - 0.095)
        add_uv_ellipsoid(f"ElaraGlovePalm_{side}", hand, (0.046, 0.038, 0.082), materials["leather"], armor, root, 48, 24)
        for finger in range(4):
            finger_x = hand[0] + (finger - 1.5) * (0.016 if side == "L" else -0.016)
            add_capsule_between(
                f"ElaraGloveFinger_{side}_{finger}",
                (finger_x, hand[1] - 0.008, hand[2] - 0.03),
                (finger_x, hand[1] - 0.01, hand[2] - 0.085),
                0.009,
                materials["leather"],
                armor,
                root,
            )

        add_uv_ellipsoid(f"ElaraPauldron_{side}", (sign * 0.335, -0.005, 1.455), (0.12, 0.13, 0.055), materials["leather_mid"], armor, root, 56, 28)
        for plate in range(3):
            add_uv_ellipsoid(
                f"ElaraShoulderPlate_{side}_{plate}",
                (sign * (0.35 + plate * 0.012), -0.006, 1.405 - plate * 0.053),
                (0.09 - plate * 0.006, 0.112, 0.032),
                materials["leather"] if plate % 2 else materials["leather_mid"],
                armor,
                root,
                48,
                24,
            )
        add_capsule_between(f"ElaraBracer_{side}", elbow, wrist, 0.078, materials["leather_mid"], armor, root, 1.04)
        for t in (0.22, 0.52, 0.82):
            point = Vector(elbow).lerp(Vector(wrist), t)
            add_uv_ellipsoid(f"ElaraBracerBand_{side}_{t}", tuple(point), (0.078, 0.075, 0.024), materials["leather_light"], armor, root, 40, 20)

    for side, x in (("L", 0.145), ("R", -0.145)):
        knee = (x, 0.0, 0.47)
        ankle = (x, 0.01, 0.13)
        add_capsule_between(f"ElaraThigh_{side}", (x, 0.0, 0.86), knee, 0.105, materials["trousers"], cloth, root, 1.02)
        add_capsule_between(f"ElaraCalf_{side}", knee, ankle, 0.095, materials["boot"], armor, root, 0.97)
        add_uv_ellipsoid(f"ElaraKneeGuard_{side}", (x, -0.085, 0.47), (0.102, 0.054, 0.086), materials["leather_mid"], armor, root, 48, 24)
        add_uv_ellipsoid(f"ElaraBootFoot_{side}", (x, -0.085, 0.055), (0.105, 0.155, 0.066), materials["boot"], armor, root, 56, 28)
        for strap_z in (0.21, 0.31):
            add_cylinder(f"ElaraBootStrap_{side}_{strap_z}", (x, 0.01, strap_z), 0.102, 0.045, materials["leather_mid"], armor, root, 40, (1.0, 0.82))

    add_cylinder("ElaraBelt", (0, 0, 1.02), 0.282, 0.09, materials["leather"], armor, root, 64, (1.0, 0.68))
    add_box("ElaraBeltBuckle", (0, -0.195, 1.02), (0.105, 0.03, 0.085), materials["gold"], detail, root, edge=0.015)
    add_box("ElaraBuckleOpening", (0, -0.214, 1.02), (0.055, 0.01, 0.04), materials["leather"], detail, root, edge=0.008)
    add_box("ElaraPouchL", (0.245, -0.035, 0.91), (0.14, 0.105, 0.17), materials["leather_light"], equipment, root, (0, 0.1, -0.06), 0.022)
    add_box("ElaraPouchR", (-0.245, -0.03, 0.91), (0.13, 0.10, 0.16), materials["leather_mid"], equipment, root, (0, -0.1, 0.06), 0.022)

    add_beam_between("ElaraCrossBodyStrap", (-0.265, -0.218, 1.49), (0.215, -0.225, 1.06), 0.067, 0.028, materials["leather_mid"], armor, root, 0.01)
    for t in (0.2, 0.5, 0.8):
        point = Vector((-0.265, -0.238, 1.49)).lerp(Vector((0.215, -0.238, 1.06)), t)
        add_uv_ellipsoid(f"ElaraStrapRivet_{t}", tuple(point), (0.014, 0.007, 0.014), materials["gold"], detail, root, 24, 12)

    add_torus("ElaraScarfLower", (0, 0.0, 1.57), 0.205, 0.058, (1.0, 0.78, 0.82), materials["green_light"], cloth, root)
    add_torus("ElaraScarfUpper", (0, -0.004, 1.615), 0.18, 0.05, (1.0, 0.8, 0.76), materials["green_cloth"], cloth, root)


def build_bow_and_quiver(
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    equipment = collections["equipment"]
    detail = collections["detail"]

    # The bow rests along Elara's left side in the front review.
    bow_points = [
        (-0.53, -0.025, 0.30),
        (-0.64, -0.035, 0.54),
        (-0.68, -0.04, 0.83),
        (-0.64, -0.04, 1.12),
        (-0.53, -0.025, 1.43),
    ]
    add_curve("ElaraYewBow", bow_points, 0.018, materials["bow_wood"], equipment, root)
    add_beam_between("ElaraBowStringUpper", (-0.53, -0.025, 1.43), (-0.61, -0.045, 0.865), 0.006, 0.004, materials["bow_string"], detail, root, 0.001)
    add_beam_between("ElaraBowStringLower", (-0.61, -0.045, 0.865), (-0.53, -0.025, 0.30), 0.006, 0.004, materials["bow_string"], detail, root, 0.001)
    add_beam_between("ElaraBowGrip", (-0.62, -0.04, 0.79), (-0.61, -0.04, 0.94), 0.04, 0.035, materials["bow_wrap"], equipment, root, 0.006)

    # Angled leather quiver and six visible arrows sit high on the back.
    quiver_bottom = Vector((0.23, 0.205, 0.72))
    quiver_top = Vector((0.37, 0.22, 1.48))
    add_beam_between("ElaraQuiver", tuple(quiver_bottom), tuple(quiver_top), 0.16, 0.12, materials["leather_mid"], equipment, root, 0.022)
    add_beam_between("ElaraQuiverRim", (0.325, 0.215, 1.405), (0.38, 0.22, 1.52), 0.19, 0.14, materials["leather_light"], equipment, root, 0.015)
    for index in range(6):
        x = 0.30 + index * 0.027
        lower = (x, 0.225 + (index % 2) * 0.008, 1.32)
        upper = (x + 0.12, 0.23 + (index % 2) * 0.008, 1.78 + (index % 3) * 0.025)
        add_beam_between(f"ElaraArrowShaft_{index}", lower, upper, 0.012, 0.01, materials["arrow_wood"], equipment, root, 0.002)
        add_box(
            f"ElaraArrowFeather_{index}",
            (upper[0] - 0.018, upper[1], upper[2] - 0.065),
            (0.035, 0.012, 0.09),
            materials["feather"],
            detail,
            root,
            (0.0, 0.12, -0.25),
            0.004,
        )


def add_costume_details(
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    detail = collections["detail"]
    gold = materials["gold"]

    build_tree_emblem(-0.225, 1.28, 0.34, True, gold, detail, root)
    build_tree_emblem(0.205, 0.94, 0.60, False, gold, detail, root)

    for z in (1.10, 1.22, 1.34, 1.46):
        add_box(f"ElaraBodiceBand_{z}", (0, -0.222, z), (0.47 - abs(z - 1.29) * 0.38, 0.012, 0.018), materials["leather_light"], detail, root, edge=0.004)
    for side in (-1, 1):
        for index in range(4):
            add_uv_ellipsoid(
                f"ElaraPauldronRivet_{side}_{index}",
                (side * (0.32 + index * 0.018), -0.17, 1.48 - index * 0.052),
                (0.013, 0.007, 0.013),
                gold,
                detail,
                root,
                24,
                12,
            )
    for side in (-1, 1):
        x = side * 0.16
        add_curve(
            f"ElaraSkirtVine_{side}",
            [(x, -0.224, 0.58), (x + side * 0.02, -0.225, 0.74), (x, -0.225, 0.92)],
            0.005,
            materials["gold"],
            detail,
            root,
        )


def build_rig(collection: bpy.types.Collection) -> bpy.types.Object:
    armature = bpy.data.armatures.new("ElaraProductionRig")
    rig = bpy.data.objects.new("ElaraProductionRig", armature)
    collection.objects.link(rig)
    rig.show_in_front = True
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    bones: dict[str, bpy.types.EditBone] = {}

    def bone(name: str, head: tuple[float, float, float], tail: tuple[float, float, float], parent: str | None = None) -> None:
        result = armature.edit_bones.new(name)
        result.head = head
        result.tail = tail
        if parent:
            result.parent = bones[parent]
        bones[name] = result

    bone("root", (0, 0, 0.02), (0, 0, 0.22))
    bone("pelvis", (0, 0, 0.84), (0, 0, 1.03), "root")
    bone("spine", (0, 0, 1.03), (0, 0, 1.29), "pelvis")
    bone("chest", (0, 0, 1.29), (0, 0, 1.48), "spine")
    bone("neck", (0, 0, 1.48), (0, 0, 1.65), "chest")
    bone("head", (0, 0, 1.65), (0, 0, 1.98), "neck")
    for side, sign in (("L", 1), ("R", -1)):
        bone(f"clavicle.{side}", (0, 0, 1.45), (sign * 0.315, 0, 1.45), "chest")
        bone(f"upper_arm.{side}", (sign * 0.315, 0, 1.45), (sign * 0.395, 0, 1.18), f"clavicle.{side}")
        bone(f"forearm.{side}", (sign * 0.395, 0, 1.18), (sign * 0.38, -0.02, 0.90), f"upper_arm.{side}")
        bone(f"hand.{side}", (sign * 0.38, -0.02, 0.90), (sign * 0.39, -0.03, 0.75), f"forearm.{side}")
        bone(f"thigh.{side}", (sign * 0.145, 0, 0.86), (sign * 0.145, 0, 0.47), "pelvis")
        bone(f"shin.{side}", (sign * 0.145, 0, 0.47), (sign * 0.145, 0, 0.13), f"thigh.{side}")
        bone(f"foot.{side}", (sign * 0.145, 0, 0.13), (sign * 0.145, -0.20, 0.06), f"shin.{side}")

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
        "body": make_collection("01_Anatomy"),
        "cloth": make_collection("02_Cloth"),
        "armor": make_collection("03_Armor"),
        "equipment": make_collection("04_RangerEquipment"),
        "detail": make_collection("05_DetailAndInsignia"),
        "rig": make_collection("06_Rig"),
        "studio": make_collection("90_ReviewStudio"),
    }
    root = bpy.data.objects.new("ElaraCharacterRoot", None)
    collections["body"].objects.link(root)
    materials = create_materials()
    build_body_and_costume(materials, collections, root)
    build_bow_and_quiver(materials, collections, root)
    add_costume_details(materials, collections, root)
    build_rig(collections["rig"])
    camera = setup_studio(root, collections["studio"], materials)
    camera.name = "ElaraReviewCamera"
    camera.data.lens = 74

    root.rotation_euler[2] = math.radians(-9)
    bpy.context.scene.render.filepath = str(FRONT_RENDER)
    bpy.ops.render.render(write_still=True)

    root.rotation_euler[2] = math.radians(171)
    bpy.context.scene.render.filepath = str(BACK_RENDER)
    bpy.ops.render.render(write_still=True)

    root.rotation_euler[2] = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(f"BLEND={BLEND_PATH}")
    print(f"FRONT={FRONT_RENDER}")
    print(f"BACK={BACK_RENDER}")


if __name__ == "__main__":
    main()
