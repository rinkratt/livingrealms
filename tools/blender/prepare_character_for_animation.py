"""Repair a Living Realms hero hierarchy for Godot skeletal animation.

Run after opening an Alden or Elara hero .blend. The script makes every
armature-deformed mesh a child of its rig (required by glTF skin export),
attaches the rigid boot shells to the matching foot bones, and saves the file.
"""

from __future__ import annotations

import argparse
import sys

import bpy


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--rig", required=True)
    parser.add_argument("--character", choices=("alden", "elara"), required=True)
    return parser.parse_args(raw)


def parent_object_keep_world(obj: bpy.types.Object, parent: bpy.types.Object) -> None:
    world_transform = obj.matrix_world.copy()
    obj.parent = parent
    obj.parent_type = "OBJECT"
    obj.parent_bone = ""
    obj.matrix_world = world_transform


def parent_bone_keep_world(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    bone_name: str,
) -> None:
    world_transform = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world_transform


def main() -> None:
    args = parse_args()
    rig = bpy.data.objects.get(args.rig)
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError(f"Armature not found: {args.rig}")

    skinned_count = 0
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        uses_rig = any(
            modifier.type == "ARMATURE" and modifier.object == rig
            for modifier in obj.modifiers
        )
        if uses_rig and obj.parent != rig:
            parent_object_keep_world(obj, rig)
            skinned_count += 1

    prefixes = (
        ("AldenBootFootShell_", "AldenBootSole_")
        if args.character == "alden"
        else ("ElaraBootFoot_", "ElaraBootSole_")
    )
    attached_count = 0
    for obj in bpy.data.objects:
        if not obj.name.startswith(prefixes):
            continue
        suffix = obj.name.rsplit("_", 1)[-1]
        bone_name = "foot_l" if suffix == "1" else "foot_r"
        parent_bone_keep_world(obj, rig, bone_name)
        attached_count += 1

    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=bpy.data.filepath)
    print(
        f"ANIMATION_HIERARCHY_READY character={args.character} "
        f"skinned_reparented={skinned_count} rigid_boots_attached={attached_count}"
    )


if __name__ == "__main__":
    main()
