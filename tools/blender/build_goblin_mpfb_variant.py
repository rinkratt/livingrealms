"""Turn the accepted Alden MPFB human into a rigged Darkwood goblin variant.

Run with Blender after opening alden_mpfb_hero_review.blend.  The real human
surface, fitted clothing, and game-engine skeleton are retained; palette,
silhouette, ears, and tusks are changed for the Darkwood raider family.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "assets" / "3d-source" / "characters" / "goblin"
BLEND_PATH = SOURCE_DIR / "goblin_mpfb_rigged.blend"


def srgb(value: str) -> tuple[float, float, float, float]:
    channels = [int(value[index:index + 2], 16) / 255.0 for index in (1, 3, 5)]
    linear = [channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4 for channel in channels]
    return (*linear, 1.0)


def set_material_color(material: bpy.types.Material, color: str, roughness: float | None = None) -> None:
    rgba = srgb(color)
    material.diffuse_color = rgba
    if not material.use_nodes or material.node_tree is None:
        return
    principled = material.node_tree.nodes.get("Principled BSDF")
    if principled is None:
        return
    principled.inputs["Base Color"].default_value = rgba
    if roughness is not None:
        principled.inputs["Roughness"].default_value = roughness


def new_material(name: str, color: str, roughness: float, metallic: float = 0.0) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    set_material_color(material, color, roughness)
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Metallic"].default_value = metallic
    return material


def parent_to_head(obj: bpy.types.Object, rig: bpy.types.Object) -> None:
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = "head"
    obj.matrix_world = world


def add_ear(name: str, side: float, rig: bpy.types.Object, skin: bpy.types.Material) -> None:
    bpy.ops.mesh.primitive_cone_add(
        vertices=32,
        radius1=0.054,
        radius2=0.008,
        depth=0.28,
        location=(side * 0.155, -0.002, 1.64),
        rotation=(0.0, side * math.radians(82), 0.0),
    )
    ear = bpy.context.object
    ear.name = name
    ear.scale = (1.0, 0.52, 1.0)
    ear.data.materials.append(skin)
    for polygon in ear.data.polygons:
        polygon.use_smooth = True
    parent_to_head(ear, rig)


def add_tusk(name: str, side: float, rig: bpy.types.Object, ivory: bpy.types.Material) -> None:
    bpy.ops.mesh.primitive_cone_add(
        vertices=24,
        radius1=0.016,
        radius2=0.0015,
        depth=0.095,
        location=(side * 0.048, -0.105, 1.525),
        rotation=(math.radians(-7), 0.0, side * math.radians(4)),
    )
    tusk = bpy.context.object
    tusk.name = name
    tusk.data.materials.append(ivory)
    for polygon in tusk.data.polygons:
        polygon.use_smooth = True
    parent_to_head(tusk, rig)


def main() -> None:
    root = bpy.data.objects.get("AldenCharacterRoot")
    rig = bpy.data.objects.get("Alden_GameEngineRig")
    human = bpy.data.objects.get("Alden_HumanBasemesh")
    if root is None or rig is None or human is None:
        raise RuntimeError("Open alden_mpfb_hero_review.blend before building the goblin variant")

    remove_tokens = (
        "Hair", "Moustache", "Cloak", "TreeEmblem", "Sword", "Scabbard",
        "TabardLeft", "TabardRight", "CowlFold",
    )
    for obj in list(bpy.data.objects):
        if obj in (root, rig, human):
            continue
        if any(token in obj.name for token in remove_tokens):
            bpy.data.objects.remove(obj, do_unlink=True)

    palette = {
        "AldenReal_Skin": ("#64783d", 0.72),
        "AldenReal_DeepBlueWool": ("#4b211c", 0.82),
        "AldenReal_WornBlueWool": ("#69281f", 0.80),
        "AldenReal_BlueShadowCloth": ("#251a16", 0.86),
        "AldenReal_WeatheredCloak": ("#321511", 0.88),
        "AldenReal_CharcoalTrousers": ("#211b18", 0.88),
        "AldenReal_DarkLeather": ("#24150f", 0.68),
        "AldenReal_WornLeather": ("#4a2917", 0.66),
        "AldenReal_BootLeather": ("#18120f", 0.72),
        "AldenReal_AntiqueGold": ("#8d6426", 0.34),
        "AldenReal_WeatheredSteel": ("#4f5654", 0.39),
        "AldenReal_BlackenedSteel": ("#202725", 0.42),
    }
    for name, (color, roughness) in palette.items():
        material = bpy.data.materials.get(name)
        if material is not None:
            material.name = name.replace("AldenReal", "DarkwoodReal")
            set_material_color(material, color, roughness)

    skin = bpy.data.materials.get("DarkwoodReal_Skin") or new_material("DarkwoodReal_Skin", "#64783d", 0.72)
    ivory = new_material("DarkwoodReal_Ivory", "#d8cfad", 0.42)
    add_ear("GoblinEar_L", -1.0, rig, skin)
    add_ear("GoblinEar_R", 1.0, rig, skin)
    add_tusk("GoblinTusk_L", -1.0, rig, ivory)
    add_tusk("GoblinTusk_R", 1.0, rig, ivory)

    root.name = "GoblinCharacterRoot"
    rig.name = "Goblin_GameEngineRig"
    rig.data.name = "Goblin_GameEngineRig"
    human.name = "Goblin_HumanBasemesh"
    human["living_realms_character"] = "Darkwood Goblin"
    human["living_realms_license"] = "CC0 MPFB basemesh with original Living Realms costume and goblin design"

    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print(f"GOBLIN_BLEND={BLEND_PATH}")


if __name__ == "__main__":
    main()
