"""Build a review-ready Alden over a real MPFB human basemesh.

The earlier Living Realms character passes used procedural primitives for the
entire body.  This script keeps the useful procedural costume workflow, but
fits it to an editable human surface and the MPFB game-engine skeleton.  The
result is intentionally saved as a separate review source until it passes a
visual review and is safe to replace the current Godot asset.
"""

from __future__ import annotations

import math
import random
import sys
from pathlib import Path
from typing import Callable

import bpy
from mathutils import Vector

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from build_alden_sculpt import (
    add_beam_between,
    add_box,
    add_curve,
    add_cylinder,
    add_torus,
    add_uv_ellipsoid,
    bevel,
    build_tree_emblem,
    make_collection,
    make_material,
    move_to_collection,
    setup_studio,
    smooth,
)

from bl_ext.user_default.mpfb.services.humanservice import HumanService


ROOT = Path(__file__).resolve().parents[2]
SOURCE_DIR = ROOT / "assets" / "3d-source" / "characters" / "alden" / "hero-source"
BLEND_PATH = SOURCE_DIR / "alden_mpfb_hero_review.blend"
FRONT_RENDER = ROOT / "docs" / "phase-8-alden-mpfb-front-review.png"
BACK_RENDER = ROOT / "docs" / "phase-8-alden-mpfb-back-review.png"
FACE_RENDER = ROOT / "docs" / "phase-8-alden-mpfb-face-review.png"

random.seed(81729)

_EVALUATED_GEOMETRY: dict[int, tuple[list[Vector], list[Vector]]] = {}


def clean_review_scene() -> bpy.types.Object:
    """Retain the MPFB basemesh and remove the default studio objects."""
    human = bpy.data.objects.get("Human")
    if human is None:
        raise RuntimeError("Open alden_mpfb_base.blend before running this script")
    for obj in list(bpy.data.objects):
        if obj != human:
            bpy.data.objects.remove(obj, do_unlink=True)
    human.name = "Alden_Body"
    return human


def create_materials() -> dict[str, bpy.types.Material]:
    def linear_hex(srgb_hex: str) -> str:
        values = [int(srgb_hex[index:index + 2], 16) / 255.0 for index in (1, 3, 5)]
        linear = [value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4 for value in values]
        return "#" + "".join(f"{max(0, min(255, round(value * 255))):02x}" for value in linear)

    def mat(name: str, color: str, roughness: float, metallic: float = 0.0, **kwargs) -> bpy.types.Material:
        # Blender node colors are linear.  The original helper accepts hex
        # channel values directly, so convert art-directed sRGB swatches first.
        return make_material(name, linear_hex(color), roughness, metallic, **kwargs)

    return {
        "skin": mat("AldenReal_Skin", "#9d6a54", 0.52, noise_scale=92, bump_strength=0.045, subsurface=0.08),
        "eye_white": mat("AldenReal_EyeWhite", "#d9d4c9", 0.17),
        "iris": mat("AldenReal_SlateBlueIris", "#48636b", 0.14, noise_scale=32, bump_strength=0.035),
        "pupil": mat("AldenReal_Pupil", "#050607", 0.09),
        "hair": mat("AldenReal_DarkBrownHair", "#100a07", 0.62, noise_scale=96, bump_strength=0.14),
        "hair_warm": mat("AldenReal_HairHighlights", "#2d1c13", 0.6, noise_scale=82, bump_strength=0.1),
        "beard": mat("AldenReal_Beard", "#513a30", 0.62, noise_scale=145, bump_strength=0.08),
        "blue": mat("AldenReal_DeepBlueWool", "#142837", 0.85, noise_scale=170, bump_strength=0.3),
        "blue_worn": mat("AldenReal_WornBlueWool", "#071722", 0.84, noise_scale=150, bump_strength=0.27),
        "blue_dark": mat("AldenReal_BlueShadowCloth", "#0d1922", 0.88, noise_scale=160, bump_strength=0.28),
        "trousers": mat("AldenReal_CharcoalTrousers", "#171819", 0.9, noise_scale=120, bump_strength=0.24),
        "leather": mat("AldenReal_DarkLeather", "#302016", 0.59, noise_scale=74, bump_strength=0.24),
        "leather_worn": mat("AldenReal_WornLeather", "#49301f", 0.58, noise_scale=68, bump_strength=0.21),
        "boot": mat("AldenReal_BootLeather", "#211914", 0.67, noise_scale=78, bump_strength=0.31),
        "steel": mat("AldenReal_WeatheredSteel", "#596268", 0.31, 0.91, noise_scale=96, bump_strength=0.12),
        "steel_dark": mat("AldenReal_BlackenedSteel", "#262c30", 0.38, 0.86, noise_scale=108, bump_strength=0.15),
        "steel_edge": mat("AldenReal_PolishedSteelEdges", "#939a9d", 0.2, 0.94, noise_scale=76, bump_strength=0.06),
        "gold": mat("AldenReal_AntiqueGold", "#a9782c", 0.3, 0.79, noise_scale=88, bump_strength=0.1),
        "cloak": mat("AldenReal_WeatheredCloak", "#0b1d29", 0.9, noise_scale=138, bump_strength=0.33),
        "studio_floor": mat("AldenReal_StudioFloor", "#171a1e", 0.6, noise_scale=27, bump_strength=0.07),
    }


def set_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)


def body_vertex_indices(human: bpy.types.Object) -> set[int]:
    group = human.vertex_groups.get("body")
    if group is None:
        return set(range(len(human.data.vertices)))
    result: set[int] = set()
    for vertex in human.data.vertices:
        if any(member.group == group.index and member.weight > 0.5 for member in vertex.groups):
            result.add(vertex.index)
    return result


def evaluated_geometry(human: bpy.types.Object) -> tuple[list[Vector], list[Vector]]:
    """Return macro-shape coordinates, not the unmodified neutral basemesh."""
    cache_key = id(human)
    cached = _EVALUATED_GEOMETRY.get(cache_key)
    if cached is not None:
        return cached
    states = [(modifier, modifier.show_viewport) for modifier in human.modifiers]
    for modifier, _ in states:
        modifier.show_viewport = False
    bpy.context.view_layer.update()
    evaluated = human.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    coordinates = [vertex.co.copy() for vertex in mesh.vertices]
    normals = [vertex.normal.copy() for vertex in mesh.vertices]
    evaluated.to_mesh_clear()
    for modifier, state in states:
        modifier.show_viewport = state
    bpy.context.view_layer.update()
    if len(coordinates) != len(human.data.vertices):
        raise RuntimeError("Evaluated MPFB mesh changed topology; clothing weights would be unsafe")
    _EVALUATED_GEOMETRY[cache_key] = (coordinates, normals)
    return coordinates, normals


def extract_surface(
    name: str,
    human: bpy.types.Object,
    rig: bpy.types.Object,
    collection: bpy.types.Collection,
    material: bpy.types.Material,
    predicate: Callable[[Vector], bool],
    *,
    offset: float = 0.006,
    thickness: float = 0.008,
    root: bpy.types.Object | None = None,
) -> bpy.types.Object:
    """Copy a region of the MPFB body and preserve its rig weights.

    Surface-derived clothing follows the real shoulders, fingers, knees, and
    jaw instead of approximating them with primitive volumes.
    """
    valid_body = body_vertex_indices(human)
    evaluated_coordinates, evaluated_normals = evaluated_geometry(human)
    selected_polygons: list[bpy.types.MeshPolygon] = []
    for polygon in human.data.polygons:
        if not all(index in valid_body for index in polygon.vertices):
            continue
        center = sum((evaluated_coordinates[index] for index in polygon.vertices), Vector()) / len(polygon.vertices)
        if predicate(center):
            selected_polygons.append(polygon)
    if not selected_polygons:
        raise RuntimeError(f"Surface selection for {name} produced no polygons")

    source_to_new: dict[int, int] = {}
    new_to_source: list[int] = []
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    for polygon in selected_polygons:
        face: list[int] = []
        for source_index in polygon.vertices:
            if source_index not in source_to_new:
                source = human.data.vertices[source_index]
                source_to_new[source_index] = len(vertices)
                new_to_source.append(source_index)
                co = evaluated_coordinates[source_index] + evaluated_normals[source_index] * offset
                vertices.append(tuple(co))
            face.append(source_to_new[source_index])
        faces.append(tuple(face))

    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)

    group_map: dict[int, bpy.types.VertexGroup] = {}
    for source_group in human.vertex_groups:
        group_map[source_group.index] = obj.vertex_groups.new(name=source_group.name)
    for new_index, source_index in enumerate(new_to_source):
        for membership in human.data.vertices[source_index].groups:
            target_group = group_map.get(membership.group)
            if target_group is not None and membership.weight > 0:
                target_group.add([new_index], membership.weight, "REPLACE")

    armature = obj.modifiers.new("AldenGameRig", "ARMATURE")
    armature.object = rig
    if thickness > 0:
        solidify = obj.modifiers.new("TailoredThickness", "SOLIDIFY")
        solidify.thickness = thickness
        solidify.offset = 0.0
    # glTF requires the armature to be the direct parent of every skinned
    # mesh. Keeping these fitted garments under the decorative character root
    # caused Godot to import their geometry without usable skinning.
    obj.parent = rig
    return obj


def add_eye(
    side: str,
    center: Vector,
    materials: dict[str, bpy.types.Material],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    eye = add_uv_ellipsoid(
        f"AldenEye_{side}", tuple(center), (0.0175, 0.0145, 0.0145),
        materials["eye_white"], collection, root, 48, 24,
    )
    # The face points toward negative Y in the MPFB coordinate system.
    add_uv_ellipsoid(
        f"AldenIris_{side}", (center.x, center.y - 0.0134, center.z),
        (0.0072, 0.0022, 0.0072), materials["iris"], collection, root, 40, 20,
    )
    add_uv_ellipsoid(
        f"AldenPupil_{side}", (center.x, center.y - 0.0154, center.z),
        (0.0028, 0.0012, 0.0028), materials["pupil"], collection, root, 32, 16,
    )
    eye["living_realms_attachment"] = "head"


def group_center(human: bpy.types.Object, group_name: str) -> Vector:
    group = human.vertex_groups[group_name]
    indices = [
        vertex.index
        for vertex in human.data.vertices
        if any(member.group == group.index and member.weight > 0 for member in vertex.groups)
    ]
    coordinates, _ = evaluated_geometry(human)
    return sum((coordinates[index] for index in indices), Vector()) / len(indices)


def add_hair_ribbon(
    name: str,
    points: list[tuple[float, float, float]],
    widths: list[float],
    outward: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Create a tapered, softly beveled hair lock rather than a round cable."""
    if len(points) != len(widths):
        raise ValueError("Hair ribbon points and widths must match")
    normal = Vector(outward).normalized()
    vertices: list[tuple[float, float, float]] = []
    for index, (point_tuple, width) in enumerate(zip(points, widths)):
        point = Vector(point_tuple)
        previous = Vector(points[max(0, index - 1)])
        following = Vector(points[min(len(points) - 1, index + 1)])
        tangent = (following - previous).normalized()
        lateral = tangent.cross(normal)
        if lateral.length < 0.0001:
            lateral = Vector((1, 0, 0))
        lateral.normalize()
        vertices.extend((tuple(point - lateral * width), tuple(point + lateral * width)))
    faces = [(index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2) for index in range(len(points) - 1)]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("HairLockThickness", "SOLIDIFY")
    solidify.thickness = 0.0024
    solidify.offset = 0.0
    bevel_modifier = obj.modifiers.new("HairLockSoftEdges", "BEVEL")
    bevel_modifier.width = 0.0011
    bevel_modifier.segments = 2
    obj.parent = root
    return obj


def add_procedural_hair_strands(
    materials: dict[str, bpy.types.Material],
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Grow tapered swept strands over the fitted scalp in one curve object."""
    curve = bpy.data.curves.new("AldenSweptHairStrandsCurve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 1
    curve.bevel_depth = 0.00046
    curve.bevel_resolution = 2
    curve.resolution_u = 2
    center = Vector((0.0, -0.015, 1.69))
    radii = Vector((0.114, 0.095, 0.122))
    strand_count = 430
    for _ in range(strand_count):
        beta = random.uniform(-math.pi, math.pi)
        is_front = math.sin(beta) < -0.28
        maximum_alpha = 1.42 if is_front else 1.68
        cosine_alpha = 1.0 - random.random() * (1.0 - math.cos(maximum_alpha))
        alpha = math.acos(cosine_alpha)
        normal = Vector((
            math.sin(alpha) * math.cos(beta),
            math.sin(alpha) * math.sin(beta),
            math.cos(alpha),
        )).normalized()
        surface = center + Vector((normal.x * radii.x, normal.y * radii.y, normal.z * radii.z))
        gravity = Vector((0, 0, -1))
        downhill = gravity - normal * gravity.dot(normal)
        if downhill.length < 0.08:
            downhill = Vector((math.cos(beta), math.sin(beta), -0.08))
        downhill.normalize()
        lateral = normal.cross(downhill).normalized()
        length = random.uniform(0.038, 0.073) + max(0.0, alpha - 1.05) * random.uniform(0.035, 0.07)
        wave = random.uniform(-0.009, 0.009)
        root_point = surface + normal * 0.0085
        points = [
            root_point,
            root_point + downhill * (length * 0.33) + normal * 0.002 + lateral * wave * 0.35,
            root_point + downhill * (length * 0.7) + lateral * wave,
            root_point + downhill * length + lateral * wave * 0.55 + Vector((0, 0, -random.uniform(0.0, 0.012))),
        ]
        spline = curve.splines.new("BEZIER")
        spline.bezier_points.add(3)
        for index, (bezier, point) in enumerate(zip(spline.bezier_points, points)):
            bezier.co = point
            bezier.handle_left_type = "AUTO"
            bezier.handle_right_type = "AUTO"
            bezier.radius = (1.0, 0.92, 0.68, 0.16)[index] * random.uniform(0.82, 1.18)

    # A small number of longer fringe strands softens the frontal hairline.
    for index in range(24):
        x = random.uniform(-0.102, 0.102)
        root_point = Vector((x * 0.62, -0.102 + random.uniform(-0.004, 0.004), 1.765 + random.uniform(-0.01, 0.024)))
        direction = -1 if x < 0 else 1
        points = [
            root_point,
            Vector((x * 0.82, -0.121, root_point.z - 0.025)),
            Vector((x + direction * random.uniform(0.006, 0.03), -0.129, 1.7 + random.uniform(-0.018, 0.02))),
        ]
        spline = curve.splines.new("BEZIER")
        spline.bezier_points.add(2)
        for point_index, (bezier, point) in enumerate(zip(spline.bezier_points, points)):
            bezier.co = point
            bezier.handle_left_type = "AUTO"
            bezier.handle_right_type = "AUTO"
            bezier.radius = (1.0, 0.72, 0.12)[point_index]

    obj = bpy.data.objects.new("AldenSweptHairStrands", curve)
    collection.objects.link(obj)
    curve.materials.append(materials["hair"])
    obj.parent = root
    obj["living_realms_attachment"] = "head"
    return obj


def add_hair_and_face(
    human: bpy.types.Object,
    rig: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    detail = collections["detail"]
    hair_cap = extract_surface(
        "AldenFittedHairCap", human, rig, detail, materials["hair"],
        lambda p: (p.z > 1.715) or (p.z > 1.64 and p.y > -0.045),
        offset=0.007, thickness=0.006, root=root,
    )
    hair_cap["living_realms_attachment"] = "head"
    texture = bpy.data.textures.new("AldenHairSurfaceBreakup", type="CLOUDS")
    texture.noise_scale = 0.028
    texture.noise_depth = 2
    displacement = hair_cap.modifiers.new("AldenHairSurfaceBreakup", "DISPLACE")
    displacement.texture = texture
    displacement.strength = 0.0045
    displacement.texture_coords = "GLOBAL"
    # Stubble is assigned directly to the human surface.  This avoids the
    # mask-like raised edge created by a second facial shell.
    coordinates, _ = evaluated_geometry(human)
    human.data.materials.append(materials["beard"])
    valid_body = body_vertex_indices(human)
    for polygon in human.data.polygons:
        if not all(index in valid_body for index in polygon.vertices):
            continue
        center = sum((coordinates[index] for index in polygon.vertices), Vector()) / len(polygon.vertices)
        if center.y < -0.13 and 1.555 < center.z < 1.65 and abs(center.x) < 0.095:
            polygon.material_index = 1

    left_eye = group_center(human, "joint-l-eye")
    right_eye = group_center(human, "joint-r-eye")
    add_eye("L", left_eye, materials, detail, root)
    add_eye("R", right_eye, materials, detail, root)

    add_procedural_hair_strands(materials, detail, root)
    # Brows and moustache add expression without altering the human face mesh.
    for side, x in (("L", 0.031), ("R", -0.031)):
        direction = 1 if side == "L" else -1
        add_curve(
            f"AldenBrow_{side}",
            [(x - direction * 0.024, -0.15, 1.708), (x, -0.155, 1.716), (x + direction * 0.025, -0.15, 1.707)],
            0.0032, materials["hair"], detail, root,
        )
    add_curve("AldenMoustacheL", [(-0.002, -0.18, 1.626), (-0.022, -0.181, 1.623), (-0.047, -0.172, 1.616)], 0.00135, materials["beard"], detail, root)
    add_curve("AldenMoustacheR", [(0.002, -0.18, 1.626), (0.022, -0.181, 1.623), (0.047, -0.172, 1.616)], 0.00135, materials["beard"], detail, root)


def build_tabard(
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    columns_per_panel = 9
    columns = columns_per_panel * 2
    rows = 28
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 1.025 - t * 0.51
        width = 0.185 - t * 0.025
        center_gap = 0.014 + t * 0.02
        for column in range(columns):
            if column < columns_per_panel:
                local_u = column / (columns_per_panel - 1)
                x = -width + local_u * (width - center_gap)
            else:
                local_u = (column - columns_per_panel) / (columns_per_panel - 1)
                x = center_gap + local_u * (width - center_gap)
            arch = 1.0 - (local_u * 2.0 - 1.0) ** 2
            y = -0.225 - 0.024 * arch - 0.011 * math.cos(local_u * math.pi * 4.0) * (0.25 + 0.75 * t)
            hem = -0.027 * abs(math.sin(local_u * math.pi)) if row == rows - 1 else 0.0
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            if column == columns_per_panel - 1:
                continue
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new("AldenTailoredTabardMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("AldenTailoredTabard", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("TabardThickness", "SOLIDIFY")
    solidify.thickness = 0.008
    subdivision = obj.modifiers.new("TabardSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    obj.parent = root
    return obj


def build_cloak(
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    columns = 31
    rows = 42
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int, int]] = []
    for row in range(rows):
        t = row / (rows - 1)
        z = 1.45 - t * 1.13
        width = 0.25 + t * 0.18
        for column in range(columns):
            u = column / (columns - 1)
            x = (u * 2.0 - 1.0) * width
            fold = 0.018 * math.cos(u * math.pi * 9.0) * (0.35 + t * 0.65)
            y = 0.105 + fold + 0.015 * math.sin(t * math.pi * 2.0)
            hem = -0.035 * (0.35 + 0.65 * abs(math.sin(u * math.pi * 6.5))) if row == rows - 1 else 0.0
            vertices.append((x, y, z + hem))
    for row in range(rows - 1):
        for column in range(columns - 1):
            a = row * columns + column
            faces.append((a, a + 1, a + columns + 1, a + columns))
    mesh = bpy.data.meshes.new("AldenWeatheredCloakMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("AldenWeatheredCloak", mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    smooth(obj)
    solidify = obj.modifiers.new("CloakThickness", "SOLIDIFY")
    solidify.thickness = 0.009
    subdivision = obj.modifiers.new("CloakSmoothing", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 2
    obj.parent = root
    return obj


def add_boot_foot(
    name: str,
    x_center: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> bpy.types.Object:
    """Build a tapered medieval boot foot with a rounded toe and raised heel."""
    sections = [
        (0.035, 0.052, 0.02, 0.135),
        (-0.095, 0.066, 0.015, 0.12),
        (-0.225, 0.054, 0.018, 0.082),
    ]
    vertices: list[tuple[float, float, float]] = []
    for y, half_width, bottom, top in sections:
        vertices.extend([
            (x_center - half_width, y, bottom),
            (x_center + half_width, y, bottom),
            (x_center + half_width, y, top),
            (x_center - half_width, y, top),
        ])
    faces: list[tuple[int, ...]] = [(0, 1, 2, 3), (8, 11, 10, 9)]
    for section in range(len(sections) - 1):
        a = section * 4
        b = (section + 1) * 4
        faces.extend([
            (a, b, b + 1, a + 1),
            (a + 1, b + 1, b + 2, a + 2),
            (a + 2, b + 2, b + 3, a + 3),
            (a + 3, b + 3, b, a),
        ])
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    mesh.materials.append(material)
    bevel(obj, 0.018, 3)
    smooth(obj)
    obj.parent = root
    return obj


def parent_to_bone_keep_world(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    bone_name: str,
) -> bpy.types.Object:
    """Attach rigid equipment to a deforming bone without moving the object."""
    world_transform = obj.matrix_world.copy()
    obj.parent = rig
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world_transform
    return obj


def build_costume(
    human: bpy.types.Object,
    rig: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    collections: dict[str, bpy.types.Collection],
    root: bpy.types.Object,
) -> None:
    cloth = collections["cloth"]
    armor = collections["armor"]
    detail = collections["detail"]
    equipment = collections["equipment"]

    extract_surface(
        "AldenFittedGambesonTorso", human, rig, cloth, materials["blue"],
        lambda p: 0.88 < p.z < 1.43 and abs(p.x) < 0.31,
        offset=0.012, thickness=0.012, root=root,
    )
    extract_surface(
        "AldenFittedGambesonSleeves", human, rig, cloth, materials["blue_dark"],
        lambda p: 1.00 < p.z < 1.47 and 0.17 < abs(p.x) < 0.50,
        offset=0.012, thickness=0.011, root=root,
    )
    extract_surface(
        "AldenShoulderMantle", human, rig, cloth, materials["blue_dark"],
        lambda p: 1.375 < p.z < 1.50 and abs(p.x) < 0.30,
        offset=0.014, thickness=0.012, root=root,
    )
    extract_surface(
        "AldenFittedTrousers", human, rig, cloth, materials["trousers"],
        lambda p: 0.35 < p.z < 0.96,
        offset=0.011, thickness=0.01, root=root,
    )
    extract_surface(
        "AldenFittedBoots", human, rig, armor, materials["boot"],
        lambda p: 0.115 < p.z < 0.48,
        offset=0.013, thickness=0.014, root=root,
    )
    extract_surface(
        "AldenFittedGloves", human, rig, armor, materials["leather"],
        lambda p: abs(p.x) > 0.43 and 0.94 < p.z < 1.19,
        offset=0.009, thickness=0.009, root=root,
    )
    extract_surface(
        "AldenContouredBreastplate", human, rig, armor, materials["blue"],
        lambda p: 1.105 < p.z < 1.39 and abs(p.x) < 0.255 and p.y < -0.115,
        offset=0.021, thickness=0.014, root=root,
    )
    extract_surface(
        "AldenLeatherForearmBracers", human, rig, armor, materials["leather_worn"],
        lambda p: 0.34 < abs(p.x) < 0.49 and 1.04 < p.z < 1.255,
        offset=0.021, thickness=0.012, root=root,
    )

    build_tabard(materials["blue_worn"], cloth, root)
    build_cloak(materials["cloak"], cloth, root)

    # A layered wool cowl closes the bare neckline and visually joins the
    # fitted body to the cloak.
    for index, (z, radius) in enumerate(((1.493, 0.121), (1.507, 0.113), (1.52, 0.104))):
        add_torus(f"AldenCowlFold_{index}", (0, -0.005, z), radius, 0.013, (1.0, 0.74, 1.0), materials["blue_dark"], cloth, root)

    # Layered shoulder and forearm armor.  These retain hand-finished forms,
    # but their placement now follows the actual anatomical joints.
    for side in (-1, 1):
        shoulder_x = side * 0.215
        pauldron = add_uv_ellipsoid(
            f"AldenPauldron_{side}", (shoulder_x, -0.015, 1.43),
            (0.086, 0.105, 0.044), materials["steel_dark"], armor, root, 56, 28,
        )
        pauldron.rotation_euler[1] = math.radians(side * 10)
        for plate in range(3):
            add_uv_ellipsoid(
                f"AldenPauldronPlate_{side}_{plate}",
                (side * (0.225 + plate * 0.014), -0.02, 1.402 - plate * 0.04),
                (0.07 - plate * 0.004, 0.09, 0.019),
                materials["steel"] if plate % 2 == 0 else materials["steel_dark"],
                armor, root, 48, 24,
            )
        for rivet in range(4):
            add_uv_ellipsoid(
                f"AldenPauldronRivet_{side}_{rivet}",
                (side * (0.18 + rivet * 0.027), -0.116, 1.426 - rivet * 0.018),
                (0.008, 0.004, 0.008), materials["gold"], detail, root, 24, 12,
            )

    # Belt, crossed harness, edge bands, and pouches establish the same visual
    # language as the reference art without copying a premade commercial mesh.
    add_torus("AldenMainBelt", (0, 0, 0.995), 0.245, 0.021, (1.0, 0.66, 1.0), materials["leather"], armor, root)
    add_box("AldenBeltBuckle", (0, -0.182, 0.995), (0.085, 0.025, 0.07), materials["gold"], detail, root, edge=0.008)
    add_beam_between("AldenHarnessLeft", (-0.205, -0.213, 1.39), (0.15, -0.224, 1.01), 0.044, 0.018, materials["leather_worn"], armor, root, 0.006)
    for side in (-1, 1):
        add_box(f"AldenBeltPouch_{side}", (side * 0.19, -0.16, 0.91), (0.095, 0.06, 0.105), materials["leather_worn"], equipment, root, edge=0.012)
        foot_bone = "foot_l" if side > 0 else "foot_r"
        boot_shell = add_boot_foot(f"AldenBootFootShell_{side}", side * 0.205, materials["boot"], armor, root)
        boot_sole = add_box(f"AldenBootSole_{side}", (side * 0.205, -0.09, 0.004), (0.132, 0.28, 0.026), materials["boot"], armor, root, edge=0.011)
        parent_to_bone_keep_world(boot_shell, rig, foot_bone)
        parent_to_bone_keep_world(boot_sole, rig, foot_bone)

    # The custom boot shells replace the anatomical toes.  Remove the hidden
    # basemesh feet from MPFB's existing body mask only after garment fitting.
    coordinates, _ = evaluated_geometry(human)
    visible_body_group = human.vertex_groups.get("body")
    if visible_body_group is not None:
        hidden_foot_indices = [index for index in body_vertex_indices(human) if coordinates[index].z < 0.19]
        visible_body_group.remove(hidden_foot_indices)

    # Sword and scabbard sit clear of the cloak for a strong rear silhouette.
    add_beam_between("AldenScabbard", (-0.29, 0.13, 1.08), (-0.46, 0.16, 0.36), 0.066, 0.05, materials["leather"], equipment, root, 0.012)
    add_beam_between("AldenSwordGrip", (-0.27, 0.12, 1.09), (-0.23, 0.12, 1.28), 0.039, 0.036, materials["leather_worn"], equipment, root, 0.006)
    add_beam_between("AldenSwordGuard", (-0.35, 0.11, 1.17), (-0.16, 0.11, 1.13), 0.022, 0.026, materials["steel_edge"], equipment, root, 0.004)
    add_uv_ellipsoid("AldenSwordPommel", (-0.22, 0.12, 1.30), (0.034, 0.032, 0.044), materials["gold"], equipment, root, 32, 16)

    # Front and back Tree of Realms devices.
    build_tree_emblem(-0.257, 1.22, 0.285, True, materials["gold"], detail, root)
    build_tree_emblem(0.129, 0.94, 0.52, False, materials["gold"], detail, root)
    add_curve("AldenTabardLeftEdge", [(-0.18, -0.264, 1.0), (-0.17, -0.264, 0.76), (-0.155, -0.264, 0.52)], 0.0035, materials["gold"], detail, root)
    add_curve("AldenTabardRightEdge", [(0.18, -0.264, 1.0), (0.17, -0.264, 0.76), (0.155, -0.264, 0.52)], 0.0035, materials["gold"], detail, root)
    add_curve("AldenTabardLeftSlit", [(-0.014, -0.264, 1.0), (-0.023, -0.264, 0.76), (-0.034, -0.264, 0.5)], 0.0035, materials["gold"], detail, root)
    add_curve("AldenTabardRightSlit", [(0.014, -0.264, 1.0), (0.023, -0.264, 0.76), (0.034, -0.264, 0.5)], 0.0035, materials["gold"], detail, root)


def configure_character(
    human: bpy.types.Object,
    rig: bpy.types.Object,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    collection: bpy.types.Collection,
) -> None:
    human.name = "Alden_HumanBasemesh"
    set_material(human, materials["skin"])
    move_to_collection(human, collection)
    smooth(human)
    subdivision = human.modifiers.new("AldenRenderSubdivision", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 1
    rig.name = "Alden_GameEngineRig"
    rig.data.name = "Alden_GameEngineRig"
    rig.hide_render = True
    rig.show_in_front = True
    rig.parent = root
    human["living_realms_character"] = "Alden"
    human["living_realms_license"] = "CC0 basemesh with original Living Realms costume"


def render_and_save(
    root: bpy.types.Object,
    camera: bpy.types.Object,
) -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    FRONT_RENDER.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1400
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    camera.data.lens = 72
    camera.location = (1.72, -4.25, 1.92)
    target = Vector((0, 0, 0.94))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()

    root.rotation_euler[2] = math.radians(-10)
    scene.render.filepath = str(FRONT_RENDER)
    bpy.ops.render.render(write_still=True)
    root.rotation_euler[2] = math.radians(170)
    scene.render.filepath = str(BACK_RENDER)
    bpy.ops.render.render(write_still=True)

    # A close review makes face, eye, hair, and material regressions obvious.
    root.rotation_euler[2] = math.radians(-10)
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1000
    camera.location = (0.8, -2.35, 1.86)
    face_target = Vector((0, -0.02, 1.57))
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
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    human = clean_review_scene()
    collections = {
        "body": make_collection("01_RealHumanAnatomy"),
        "cloth": make_collection("02_TailoredCloth"),
        "armor": make_collection("03_FittedArmor"),
        "equipment": make_collection("04_Equipment"),
        "detail": make_collection("05_HairFaceAndInsignia"),
        "studio": make_collection("90_ReviewStudio"),
    }
    root = bpy.data.objects.new("AldenCharacterRoot", None)
    collections["body"].objects.link(root)
    materials = create_materials()
    rig = HumanService.add_builtin_rig(human, "game_engine", import_weights=True)
    configure_character(human, rig, root, materials, collections["body"])
    add_hair_and_face(human, rig, materials, collections, root)
    build_costume(human, rig, materials, collections, root)
    camera = setup_studio(root, collections["studio"], materials)
    render_and_save(root, camera)


if __name__ == "__main__":
    main()
