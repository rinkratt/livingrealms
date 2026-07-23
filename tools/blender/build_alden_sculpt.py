"""Create the original-from-scratch Alden high-detail review sculpt.

This is the first character-art gate: anatomy, face, layered costume, equipment,
materials, insignia, and a production skeleton are authored in Blender before
retopology and game integration.
"""

from __future__ import annotations

import math
import random
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "assets" / "3d-source" / "characters" / "alden" / "source"
BLEND_PATH = SOURCE_DIR / "alden_highpoly_review.blend"
FRONT_RENDER = ROOT / "docs" / "phase-8-alden-front-review.png"
BACK_RENDER = ROOT / "docs" / "phase-8-alden-back-review.png"

random.seed(1701)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)


def make_collection(name: str) -> bpy.types.Collection:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    return collection


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def hex_rgb(value: str) -> tuple[float, float, float, float]:
    return tuple(int(value[index:index + 2], 16) / 255.0 for index in (1, 3, 5)) + (1.0,)


def make_material(
    name: str,
    base_color: str,
    roughness: float,
    metallic: float = 0.0,
    noise_scale: float = 0.0,
    bump_strength: float = 0.0,
    subsurface: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    rgba = hex_rgb(base_color)
    principled.inputs["Base Color"].default_value = rgba
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    subsurface_input = principled.inputs.get("Subsurface Weight") or principled.inputs.get("Subsurface")
    if subsurface_input:
        subsurface_input.default_value = subsurface

    if noise_scale > 0:
        noise = nodes.new("ShaderNodeTexNoise")
        noise.name = name + "_SurfaceVariation"
        noise.inputs["Scale"].default_value = noise_scale
        noise.inputs["Detail"].default_value = 6.0
        noise.inputs["Roughness"].default_value = 0.72
        ramp = nodes.new("ShaderNodeValToRGB")
        dark = tuple(max(0.0, channel * 0.62) for channel in rgba[:3]) + (1.0,)
        light = tuple(min(1.0, channel * 1.22 + 0.025) for channel in rgba[:3]) + (1.0,)
        ramp.color_ramp.elements[0].color = dark
        ramp.color_ramp.elements[1].color = light
        links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], principled.inputs["Base Color"])
        if bump_strength > 0:
            bump = nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value = bump_strength
            bump.inputs["Distance"].default_value = 0.025
            links.new(noise.outputs["Fac"], bump.inputs["Height"])
            links.new(bump.outputs["Normal"], principled.inputs["Normal"])
    return material


def smooth(obj: bpy.types.Object) -> None:
    if obj.type == "MESH":
        for polygon in obj.data.polygons:
            polygon.use_smooth = True


def bevel(obj: bpy.types.Object, width: float, segments: int = 3) -> None:
    if width <= 0:
        return
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("HandFinishedEdges", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def parent_character(obj: bpy.types.Object, root: bpy.types.Object) -> bpy.types.Object:
    obj.parent = root
    return obj


def add_uv_ellipsoid(
    name: str,
    location: tuple[float, float, float],
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    segments: int = 64,
    rings: int = 32,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    smooth(obj)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return parent_character(obj, root)


def add_box(
    name: str,
    location: tuple[float, float, float],
    size: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
    edge: float = 0.015,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel(obj, edge)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return parent_character(obj, root)


def add_cylinder(
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    vertices: int = 32,
    scale_xy: tuple[float, float] = (1.0, 1.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale.x = scale_xy[0]
    obj.scale.y = scale_xy[1]
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    smooth(obj)
    bevel(obj, min(0.012, radius * 0.15), 2)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return parent_character(obj, root)


def add_torus(
    name: str,
    location: tuple[float, float, float],
    major_radius: float,
    minor_radius: float,
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_radius,
        minor_radius=minor_radius,
        major_segments=64,
        minor_segments=16,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    smooth(obj)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return parent_character(obj, root)


def add_capsule_between(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    radius: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    width_scale: float = 1.0,
) -> bpy.types.Object:
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    midpoint = (start_v + end_v) * 0.5
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.scale = (radius * width_scale, radius, direction.length * 0.56)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    smooth(obj)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return parent_character(obj, root)


def add_beam_between(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    depth: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    edge: float = 0.008,
) -> bpy.types.Object:
    start_v = Vector(start)
    end_v = Vector(end)
    direction = end_v - start_v
    bpy.ops.mesh.primitive_cube_add(location=(start_v + end_v) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    obj.dimensions = (width, depth, direction.length)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bevel(obj, edge, 2)
    obj.data.materials.append(material)
    move_to_collection(obj, collection)
    return parent_character(obj, root)


def add_curve(
    name: str,
    points: list[tuple[float, float, float]],
    bevel_depth: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    curve = bpy.data.curves.new(name + "Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 3
    curve.bevel_depth = bevel_depth
    curve.bevel_resolution = 3
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for bezier, point in zip(spline.bezier_points, points):
        bezier.co = point
        bezier.handle_left_type = "AUTO"
        bezier.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    curve.materials.append(material)
    return parent_character(obj, root)


def add_cloak(
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    columns = 24
    rows = 34
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 1.56 - t * 1.28
        width = 0.40 + t * 0.14
        if row == rows - 1:
            z += 0.035 * math.sin(5.0 * math.pi * (row + 1) / rows)
        for column in range(columns):
            u = column / (columns - 1)
            x = (u * 2.0 - 1.0) * width
            fold = 0.035 * math.cos(u * math.pi * 7.0) * (0.35 + 0.65 * t)
            y = 0.225 + fold + 0.018 * math.sin(t * math.pi * 3.0)
            hem = 0.0
            if row == rows - 1:
                hem = -0.035 * (0.5 + 0.5 * math.sin(u * math.pi * 11.0))
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            b = a + 1
            c = a + columns + 1
            d = a + columns
            faces.append((a, b, c, d))
    mesh = bpy.data.meshes.new("AldenCloakMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("AldenCloak", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("ClothThickness", "SOLIDIFY")
    solidify.thickness = 0.012
    solidify.offset = 0.0
    subdivision = obj.modifiers.new("ClothSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    return parent_character(obj, root)


def add_tabard_panel(
    name: str,
    center_x: float,
    front_y: float,
    top_z: float,
    bottom_z: float,
    top_width: float,
    bottom_width: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    columns = 12
    rows = 20
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = top_z + (bottom_z - top_z) * t
        width = top_width + (bottom_width - top_width) * t
        for column in range(columns):
            u = column / (columns - 1)
            x = center_x + (u * 2.0 - 1.0) * width * 0.5
            y = front_y - 0.012 * math.cos(u * math.pi * 5.0) * t
            hem = -0.025 * math.sin(u * math.pi * 4.0) if row == rows - 1 else 0.0
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    solidify = obj.modifiers.new("FabricThickness", "SOLIDIFY")
    solidify.thickness = 0.008
    subdivision = obj.modifiers.new("FabricSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    return parent_character(obj, root)


def build_face(
    materials: dict[str, bpy.types.Material],
    body: bpy.types.Collection,
    detail: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    existing_children = set(root.children)
    skin = materials["skin"]
    hair = materials["hair"]
    add_uv_ellipsoid("Skull", (0, 0.015, 1.855), (0.265, 0.235, 0.325), skin, body, root)
    add_uv_ellipsoid("Jaw", (0, -0.085, 1.69), (0.218, 0.168, 0.19), skin, body, root)
    add_uv_ellipsoid("FacePlane", (0, -0.188, 1.83), (0.225, 0.075, 0.255), skin, body, root)
    add_uv_ellipsoid("Chin", (0, -0.19, 1.63), (0.105, 0.075, 0.085), skin, body, root, 48, 24)
    add_uv_ellipsoid("EarL", (0.268, 0.0, 1.82), (0.048, 0.032, 0.085), skin, body, root, 40, 20)
    add_uv_ellipsoid("EarR", (-0.268, 0.0, 1.82), (0.048, 0.032, 0.085), skin, body, root, 40, 20)

    add_uv_ellipsoid("NoseBridge", (0, -0.255, 1.84), (0.047, 0.067, 0.125), skin, body, root, 48, 24)
    add_uv_ellipsoid("NoseTip", (0, -0.305, 1.785), (0.062, 0.052, 0.055), skin, body, root, 48, 24)
    add_uv_ellipsoid("NostrilL", (0.043, -0.295, 1.78), (0.024, 0.018, 0.022), materials["nostril"], detail, root, 32, 16)
    add_uv_ellipsoid("NostrilR", (-0.043, -0.295, 1.78), (0.024, 0.018, 0.022), materials["nostril"], detail, root, 32, 16)

    for side, x in (("L", 0.09), ("R", -0.09)):
        add_uv_ellipsoid(f"EyeWhite{side}", (x, -0.254, 1.875), (0.052, 0.024, 0.033), materials["eye_white"], detail, root, 48, 24)
        add_uv_ellipsoid(f"Iris{side}", (x, -0.277, 1.875), (0.021, 0.008, 0.021), materials["iris"], detail, root, 40, 20)
        add_uv_ellipsoid(f"Pupil{side}", (x, -0.285, 1.875), (0.009, 0.004, 0.009), materials["pupil"], detail, root, 32, 16)
        eyebrow_points = [
            (x + (0.075 if side == "L" else -0.075), -0.276, 1.935),
            (x, -0.292, 1.952),
            (x + (-0.072 if side == "L" else 0.072), -0.276, 1.94),
        ]
        add_curve(f"Eyebrow{side}", eyebrow_points, 0.012, hair, detail, root)

    add_curve("UpperLip", [(-0.078, -0.276, 1.704), (0, -0.3, 1.713), (0.078, -0.276, 1.704)], 0.012, materials["lip"], detail, root)
    add_curve("LowerLip", [(-0.065, -0.273, 1.687), (0, -0.294, 1.677), (0.065, -0.273, 1.687)], 0.011, materials["lip"], detail, root)

    add_uv_ellipsoid("HairMass", (0, 0.045, 2.035), (0.292, 0.253, 0.225), hair, body, root, 64, 32)
    strand_specs = [
        [(-0.22, -0.195, 2.05), (-0.19, -0.245, 1.98), (-0.15, -0.25, 1.92)],
        [(-0.13, -0.225, 2.13), (-0.1, -0.275, 2.04), (-0.07, -0.26, 1.96)],
        [(-0.02, -0.235, 2.16), (0.02, -0.278, 2.08), (0.04, -0.255, 2.0)],
        [(0.1, -0.22, 2.15), (0.14, -0.27, 2.06), (0.12, -0.25, 1.97)],
        [(0.2, -0.18, 2.08), (0.23, -0.235, 2.0), (0.2, -0.245, 1.92)],
    ]
    for index, points in enumerate(strand_specs):
        add_curve(f"ForeheadHair_{index}", points, 0.021, hair, detail, root)
    for side in (-1, 1):
        for index in range(5):
            x = side * (0.23 + index * 0.008)
            add_curve(
                f"TempleHair_{side}_{index}",
                [(x, -0.12 + index * 0.012, 2.04), (x + side * 0.035, -0.08, 1.94), (x + side * 0.025, -0.02, 1.83)],
                0.015,
                hair,
                detail,
                root,
            )

    add_uv_ellipsoid("BeardShadow", (0, -0.202, 1.69), (0.205, 0.06, 0.175), materials["beard"], detail, root, 64, 32)
    add_curve("MustacheLeft", [(-0.005, -0.309, 1.735), (-0.045, -0.31, 1.726), (-0.093, -0.283, 1.72)], 0.01, hair, detail, root)
    add_curve("MustacheRight", [(0.005, -0.309, 1.735), (0.045, -0.31, 1.726), (0.093, -0.283, 1.72)], 0.01, hair, detail, root)

    # The face is sculpted at a comfortable working size and then reduced around
    # an anatomical pivot to an adult rather than toy-like proportion.
    head_parts = [child for child in root.children if child not in existing_children]
    pivot = Vector((0, 0, 1.747))
    head_scale = 0.46
    for part in head_parts:
        part.location = pivot + (part.location - pivot) * head_scale
        part.scale *= head_scale


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

    add_cylinder("Neck", (0, 0, 1.575), 0.105, 0.22, materials["skin"], body, root, 40, (1.0, 0.9))
    build_face(materials, body, detail, root)

    add_uv_ellipsoid("GambesonTorso", (0, 0.0, 1.265), (0.39, 0.235, 0.38), materials["blue_cloth"], cloth, root)
    add_uv_ellipsoid("WaistLayer", (0, 0.0, 0.99), (0.33, 0.22, 0.22), materials["blue_cloth"], cloth, root)
    add_uv_ellipsoid("ChestPadding", (0, -0.19, 1.31), (0.355, 0.065, 0.29), materials["blue_cloth"], cloth, root)

    add_tabard_panel("FrontTabard", 0.0, -0.235, 1.04, 0.42, 0.5, 0.62, materials["blue_cloth"], cloth, root)
    add_tabard_panel("LeftSkirtPanel", -0.23, -0.02, 0.98, 0.43, 0.28, 0.32, materials["blue_cloth"], cloth, root)
    add_tabard_panel("RightSkirtPanel", 0.23, -0.02, 0.98, 0.43, 0.28, 0.32, materials["blue_cloth"], cloth, root)

    shoulder_points = {
        "L": ((0.39, 0.0, 1.46), (0.49, -0.005, 1.16), (0.47, -0.025, 0.87)),
        "R": ((-0.39, 0.0, 1.46), (-0.49, -0.005, 1.16), (-0.47, -0.025, 0.87)),
    }
    for side, (shoulder, elbow, wrist) in shoulder_points.items():
        add_capsule_between(f"UpperArm_{side}", shoulder, elbow, 0.115, materials["blue_cloth"], cloth, root, 1.08)
        add_capsule_between(f"Forearm_{side}", elbow, wrist, 0.092, materials["leather"], armor, root, 1.0)
        hand = (wrist[0] * 1.01, wrist[1] - 0.012, wrist[2] - 0.115)
        add_uv_ellipsoid(f"GlovePalm_{side}", hand, (0.062, 0.048, 0.105), materials["glove"], armor, root, 48, 24)
        for finger in range(4):
            finger_x = hand[0] + (finger - 1.5) * (0.022 if side == "L" else -0.022)
            add_capsule_between(
                f"GloveFinger_{side}_{finger}",
                (finger_x, hand[1] - 0.01, hand[2] - 0.04),
                (finger_x, hand[1] - 0.012, hand[2] - 0.112),
                0.013,
                materials["glove"], armor, root,
            )

        sign = 1 if side == "L" else -1
        add_uv_ellipsoid(f"PauldronMain_{side}", (sign * 0.41, -0.005, 1.47), (0.155, 0.17, 0.075), materials["steel"], armor, root, 56, 28)
        for plate in range(3):
            add_uv_ellipsoid(
                f"PauldronPlate_{side}_{plate}",
                (sign * (0.43 + plate * 0.018), -0.005, 1.39 - plate * 0.065),
                (0.112 - plate * 0.009, 0.145, 0.04),
                materials["steel_dark"] if plate % 2 else materials["steel"], armor, root, 48, 24,
            )
        add_capsule_between(f"Bracer_{side}", elbow, wrist, 0.105, materials["steel"], armor, root, 1.05)
        for band in (0.27, 0.58, 0.84):
            point = Vector(elbow).lerp(Vector(wrist), band)
            add_uv_ellipsoid(f"BracerBand_{side}_{band}", tuple(point), (0.105, 0.10, 0.035), materials["steel_dark"], armor, root, 40, 20)

    hip_z = 0.84
    for side, x in (("L", 0.18), ("R", -0.18)):
        knee = (x, 0.0, 0.48)
        ankle = (x, 0.01, 0.13)
        add_capsule_between(f"Thigh_{side}", (x, 0.0, hip_z), knee, 0.14, materials["trousers"], cloth, root, 1.03)
        add_capsule_between(f"Calf_{side}", knee, ankle, 0.12, materials["boot"], armor, root, 0.98)
        add_uv_ellipsoid(f"KneeGuard_{side}", (x, -0.105, 0.48), (0.135, 0.07, 0.11), materials["leather"], armor, root, 48, 24)
        add_uv_ellipsoid(f"BootFoot_{side}", (x, -0.09, 0.055), (0.13, 0.18, 0.075), materials["boot"], armor, root, 56, 28)
        for strap_z in (0.2, 0.31):
            add_cylinder(f"BootStrap_{side}_{strap_z}", (x, 0.01, strap_z), 0.128, 0.055, materials["leather_light"], armor, root, 40, (1.0, 0.82))

    add_cylinder("Belt", (0, 0, 0.99), 0.365, 0.115, materials["leather"], armor, root, 64, (1.0, 0.67))
    add_box("BeltBuckle", (0, -0.25, 0.99), (0.13, 0.035, 0.105), materials["gold"], detail, root, edge=0.018)
    add_box("BuckleOpening", (0, -0.272, 0.99), (0.072, 0.012, 0.052), materials["leather"], detail, root, edge=0.01)
    add_box("BeltPouchL", (0.31, -0.05, 0.87), (0.17, 0.12, 0.20), materials["leather_light"], equipment, root, (0, 0.1, -0.08), 0.025)
    add_box("BeltPouchR", (-0.30, -0.04, 0.87), (0.16, 0.11, 0.18), materials["leather"], equipment, root, (0, -0.1, 0.08), 0.025)

    add_beam_between("CrossBodyStrap", (-0.31, -0.277, 1.52), (0.24, -0.285, 1.02), 0.088, 0.035, materials["leather"], armor, root, 0.012)
    for t in (0.18, 0.50, 0.82):
        point = Vector((-0.31, -0.295, 1.52)).lerp(Vector((0.24, -0.295, 1.02)), t)
        add_uv_ellipsoid(f"StrapRivet_{t}", tuple(point), (0.018, 0.008, 0.018), materials["gold"], detail, root, 24, 12)

    add_torus("ScarfLower", (0, 0.0, 1.54), 0.27, 0.075, (1.0, 0.78, 0.82), materials["blue_cloth"], cloth, root)
    add_torus("ScarfUpper", (0, -0.005, 1.59), 0.235, 0.064, (1.0, 0.80, 0.76), materials["blue_cloth"], cloth, root)
    add_cloak(materials["cloak"], cloth, root)

    add_beam_between("Scabbard", (-0.36, 0.08, 0.92), (-0.53, 0.10, 0.18), 0.082, 0.06, materials["scabbard"], equipment, root, 0.015)
    add_beam_between("SwordGrip", (-0.35, 0.08, 1.0), (-0.31, 0.075, 1.18), 0.048, 0.045, materials["leather_light"], equipment, root, 0.008)
    add_beam_between("SwordGuard", (-0.43, 0.075, 1.08), (-0.24, 0.075, 1.04), 0.025, 0.03, materials["steel"], equipment, root, 0.005)
    add_uv_ellipsoid("SwordPommel", (-0.30, 0.075, 1.20), (0.045, 0.04, 0.055), materials["gold"], equipment, root, 32, 16)


def build_tree_emblem(
    y: float,
    z_offset: float,
    scale: float,
    front: bool,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    direction = -1 if front else 1
    surface_y = y + direction * 0.002
    def transform(point: tuple[float, float]) -> tuple[float, float, float]:
        return (point[0] * scale, surface_y, z_offset + point[1] * scale)

    branches = [
        [(0.0, -0.18), (0.0, 0.0), (0.0, 0.24)],
        [(0.0, 0.02), (-0.13, 0.15), (-0.24, 0.21)],
        [(0.0, 0.04), (0.14, 0.16), (0.25, 0.23)],
        [(-0.06, 0.1), (-0.19, 0.27), (-0.28, 0.3)],
        [(0.06, 0.12), (0.2, 0.28), (0.29, 0.32)],
        [(-0.13, 0.15), (-0.14, 0.31), (-0.1, 0.39)],
        [(0.14, 0.16), (0.12, 0.31), (0.08, 0.4)],
        [(0.0, -0.17), (-0.12, -0.29), (-0.22, -0.31)],
        [(0.0, -0.17), (0.13, -0.28), (0.23, -0.30)],
        [(-0.02, -0.16), (-0.05, -0.33), (-0.1, -0.38)],
        [(0.02, -0.16), (0.06, -0.32), (0.12, -0.37)],
    ]
    for index, branch in enumerate(branches):
        add_curve(f"TreeEmblem_{'Front' if front else 'Back'}_{index}", [transform(point) for point in branch], 0.008 * scale, material, collection, root)


def add_armor_details(
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    detail = collections["detail"]
    armor = collections["armor"]
    gold = materials["gold"]
    steel_dark = materials["steel_dark"]

    build_tree_emblem(-0.274, 1.28, 0.42, True, gold, detail, root)
    build_tree_emblem(0.255, 0.92, 0.78, False, gold, detail, root)

    for x in (-0.33, -0.22, -0.11, 0.11, 0.22, 0.33):
        add_beam_between(f"GambesonSeam_{x}", (x, -0.258, 1.08), (x, -0.258, 1.47), 0.012, 0.008, materials["blue_trim"], detail, root, 0.003)
    for side in (-1, 1):
        for index in range(5):
            x = side * (0.39 + index * 0.026)
            z = 1.51 - index * 0.065
            add_uv_ellipsoid(f"PauldronRivet_{side}_{index}", (x, -0.23, z), (0.018, 0.01, 0.018), gold, detail, root, 24, 12)
    for z in (0.70, 0.78, 0.86):
        add_box(f"TabardTrim_{z}", (0, -0.255, z), (0.48 + (0.86 - z) * 0.12, 0.012, 0.018), materials["blue_trim"], detail, root, edge=0.004)


def build_rig(collection: bpy.types.Collection) -> bpy.types.Object:
    armature = bpy.data.armatures.new("AldenProductionRig")
    rig = bpy.data.objects.new("AldenProductionRig", armature)
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
    bone("pelvis", (0, 0, 0.82), (0, 0, 1.0), "root")
    bone("spine", (0, 0, 1.0), (0, 0, 1.27), "pelvis")
    bone("chest", (0, 0, 1.27), (0, 0, 1.50), "spine")
    bone("neck", (0, 0, 1.50), (0, 0, 1.66), "chest")
    bone("head", (0, 0, 1.66), (0, 0, 2.08), "neck")
    for side, sign in (("L", 1), ("R", -1)):
        bone(f"clavicle.{side}", (0, 0, 1.47), (sign * 0.39, 0, 1.46), "chest")
        bone(f"upper_arm.{side}", (sign * 0.39, 0, 1.46), (sign * 0.49, 0, 1.16), f"clavicle.{side}")
        bone(f"forearm.{side}", (sign * 0.49, 0, 1.16), (sign * 0.47, -0.02, 0.87), f"upper_arm.{side}")
        bone(f"hand.{side}", (sign * 0.47, -0.02, 0.87), (sign * 0.48, -0.03, 0.69), f"forearm.{side}")
        bone(f"thigh.{side}", (sign * 0.18, 0, 0.87), (sign * 0.18, 0, 0.48), "pelvis")
        bone(f"shin.{side}", (sign * 0.18, 0, 0.48), (sign * 0.18, 0, 0.13), f"thigh.{side}")
        bone(f"foot.{side}", (sign * 0.18, 0, 0.13), (sign * 0.18, -0.22, 0.06), f"shin.{side}")

    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    rig.hide_render = True
    return rig


def setup_studio(root: bpy.types.Object, studio: bpy.types.Collection, materials: dict[str, bpy.types.Material]) -> bpy.types.Object:
    world = bpy.data.worlds.new("AldenStudioWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.014, 0.018, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.22
    bpy.context.scene.world = world

    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.035))
    floor = bpy.context.object
    floor.name = "StudioFloor"
    floor.data.materials.append(materials["studio_floor"])
    move_to_collection(floor, studio)

    target = Vector((0, 0, 1.05))
    light_specs = [
        ("KeyLight", (3.6, -4.5, 5.2), 1250, 4.0, (1.0, 0.72, 0.48)),
        ("FillLight", (-3.2, -2.5, 3.1), 800, 3.5, (0.42, 0.56, 0.78)),
        ("RimLight", (1.8, 3.2, 4.4), 1150, 3.0, (0.86, 0.9, 1.0)),
    ]
    for name, location, energy, size, color in light_specs:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = color
        light.rotation_euler = (target - light.location).to_track_quat("-Z", "Y").to_euler()
        move_to_collection(light, studio)

    bpy.ops.object.camera_add(location=(2.15, -5.25, 2.35))
    camera = bpy.context.object
    camera.name = "AldenReviewCamera"
    camera.data.lens = 76
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    move_to_collection(camera, studio)
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.28
    scene.render.film_transparent = False
    return camera


def create_materials() -> dict[str, bpy.types.Material]:
    return {
        "skin": make_material("Alden_Skin", "#8f5b45", 0.56, noise_scale=95, bump_strength=0.08, subsurface=0.06),
        "nostril": make_material("Alden_Nostril", "#3b1f1b", 0.62),
        "lip": make_material("Alden_Lips", "#7b443b", 0.52, noise_scale=65, bump_strength=0.06),
        "eye_white": make_material("Alden_EyeWhite", "#d8d0c5", 0.22),
        "iris": make_material("Alden_Iris", "#526d73", 0.18, noise_scale=22, bump_strength=0.08),
        "pupil": make_material("Alden_Pupil", "#08090a", 0.12),
        "hair": make_material("Alden_Hair", "#21150f", 0.48, noise_scale=55, bump_strength=0.18),
        "beard": make_material("Alden_BeardShadow", "#34221b", 0.58, noise_scale=120, bump_strength=0.08),
        "blue_cloth": make_material("Alden_BlueGambeson", "#1d2f3b", 0.84, noise_scale=140, bump_strength=0.28),
        "blue_trim": make_material("Alden_BlueTrim", "#9a7634", 0.62, 0.18, noise_scale=70, bump_strength=0.12),
        "cloak": make_material("Alden_Cloak", "#172938", 0.88, noise_scale=110, bump_strength=0.32),
        "trousers": make_material("Alden_Trousers", "#171718", 0.9, noise_scale=85, bump_strength=0.24),
        "leather": make_material("Alden_LeatherDark", "#3a2519", 0.64, noise_scale=48, bump_strength=0.23),
        "leather_light": make_material("Alden_LeatherWorn", "#654329", 0.61, noise_scale=52, bump_strength=0.22),
        "glove": make_material("Alden_Gloves", "#2b2019", 0.72, noise_scale=70, bump_strength=0.2),
        "boot": make_material("Alden_Boots", "#2e2118", 0.68, noise_scale=54, bump_strength=0.28),
        "scabbard": make_material("Alden_Scabbard", "#201914", 0.56, noise_scale=45, bump_strength=0.16),
        "steel": make_material("Alden_Steel", "#5a5f62", 0.42, 0.88, noise_scale=75, bump_strength=0.13),
        "steel_dark": make_material("Alden_DarkSteel", "#2c3032", 0.46, 0.84, noise_scale=82, bump_strength=0.16),
        "gold": make_material("Alden_AntiqueGold", "#b18435", 0.35, 0.72, noise_scale=68, bump_strength=0.11),
        "studio_floor": make_material("StudioFloor", "#202226", 0.62, noise_scale=24, bump_strength=0.08),
    }


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
        "equipment": make_collection("04_Equipment"),
        "detail": make_collection("05_DetailAndInsignia"),
        "rig": make_collection("06_Rig"),
        "studio": make_collection("90_ReviewStudio"),
    }
    root = bpy.data.objects.new("AldenCharacterRoot", None)
    collections["body"].objects.link(root)
    materials = create_materials()
    build_body_and_costume(materials, collections, root)
    add_armor_details(materials, collections, root)
    build_rig(collections["rig"])
    setup_studio(root, collections["studio"], materials)

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
