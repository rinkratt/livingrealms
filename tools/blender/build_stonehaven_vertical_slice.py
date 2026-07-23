"""Build the nine-grid Stonehaven stylized-realism environment.

Run with Blender in background mode. The script keeps an editable .blend source,
exports a Godot-ready GLB, and renders a preview image for review.
"""

from __future__ import annotations

import math
import random
import shutil
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = ROOT / "client" / "LivingRealms.Client" / "Assets" / "Environment"
SOURCE_DIR = ROOT / "assets" / "3d-source" / "stonehaven"
TEXTURE_DIR = SOURCE_DIR / "textures"
BLEND_PATH = SOURCE_DIR / "stonehaven_vertical_slice.blend"
GLB_PATH = ASSET_DIR / "stonehaven_vertical_slice.glb"
STAGED_GLB_PATH = SOURCE_DIR / "stonehaven_vertical_slice.export.glb"
PREVIEW_PATH = ROOT / "docs" / "nine-grid-terrain-realism-preview.png"

random.seed(81427)


def gpos(x: float, y: float, z: float) -> tuple[float, float, float]:
    """Convert Godot X/Y-up/Z coordinates to Blender X/Y/Z-up coordinates."""
    return (x, -z, y)


def gvec(value: tuple[float, float, float]) -> Vector:
    return Vector(gpos(*value))


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in bpy.data.meshes:
        if block.users == 0:
            bpy.data.meshes.remove(block)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)


def make_collection(name: str) -> bpy.types.Collection:
    result = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(result)
    return result


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for current in list(obj.users_collection):
        current.objects.unlink(obj)
    collection.objects.link(obj)


def make_material(
    name: str,
    color: str,
    roughness: float = 0.72,
    metallic: float = 0.0,
    alpha: float = 1.0,
    emission: str | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    rgba = tuple(int(color[index:index + 2], 16) / 255.0 for index in (1, 3, 5)) + (alpha,)
    material.diffuse_color = rgba
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = rgba
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Metallic"].default_value = metallic
    principled.inputs["Alpha"].default_value = alpha
    if emission:
        emission_rgba = tuple(int(emission[index:index + 2], 16) / 255.0 for index in (1, 3, 5)) + (1.0,)
        emission_input = principled.inputs.get("Emission Color") or principled.inputs.get("Emission")
        if emission_input:
            emission_input.default_value = emission_rgba
        strength_input = principled.inputs.get("Emission Strength")
        if strength_input:
            strength_input.default_value = emission_strength
    if alpha < 1.0 and hasattr(material, "surface_render_method"):
        material.surface_render_method = "DITHERED"

    base_color_path = TEXTURE_DIR / f"{name}_basecolor.png"
    roughness_path = TEXTURE_DIR / f"{name}_roughness.png"
    normal_path = TEXTURE_DIR / f"{name}_normal.png"
    texture_scale = 1.0
    if name.startswith(("Grass_", "Road_", "Earth_")):
        texture_scale = 7.0
    elif name.startswith(("Stone_", "Rock_")):
        texture_scale = 2.5
    elif name.startswith("Roof_"):
        texture_scale = 3.5
    elif name.startswith(("Timber_", "Plaster_", "Leaves_")):
        texture_scale = 2.0
    texture_coordinate = material.node_tree.nodes.new("ShaderNodeTexCoord")
    texture_mapping = material.node_tree.nodes.new("ShaderNodeMapping")
    texture_mapping.inputs["Scale"].default_value = (texture_scale, texture_scale, texture_scale)
    material.node_tree.links.new(texture_coordinate.outputs["UV"], texture_mapping.inputs["Vector"])
    if base_color_path.exists():
        base_image = bpy.data.images.load(str(base_color_path), check_existing=True)
        base_node = material.node_tree.nodes.new("ShaderNodeTexImage")
        base_node.name = name + "_BaseColor"
        base_node.image = base_image
        material.node_tree.links.new(texture_mapping.outputs["Vector"], base_node.inputs["Vector"])
        material.node_tree.links.new(base_node.outputs["Color"], principled.inputs["Base Color"])
    if roughness_path.exists():
        roughness_image = bpy.data.images.load(str(roughness_path), check_existing=True)
        roughness_image.colorspace_settings.name = "Non-Color"
        roughness_node = material.node_tree.nodes.new("ShaderNodeTexImage")
        roughness_node.name = name + "_Roughness"
        roughness_node.image = roughness_image
        material.node_tree.links.new(texture_mapping.outputs["Vector"], roughness_node.inputs["Vector"])
        material.node_tree.links.new(roughness_node.outputs["Color"], principled.inputs["Roughness"])
    if normal_path.exists():
        normal_image = bpy.data.images.load(str(normal_path), check_existing=True)
        normal_image.colorspace_settings.name = "Non-Color"
        normal_texture = material.node_tree.nodes.new("ShaderNodeTexImage")
        normal_texture.name = name + "_Normal"
        normal_texture.image = normal_image
        material.node_tree.links.new(texture_mapping.outputs["Vector"], normal_texture.inputs["Vector"])
        normal_map = material.node_tree.nodes.new("ShaderNodeNormalMap")
        normal_map.name = name + "_NormalMap"
        normal_map.inputs["Strength"].default_value = 0.38
        material.node_tree.links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
        material.node_tree.links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    return material


def apply_bevel(obj: bpy.types.Object, width: float, segments: int = 2) -> None:
    if width <= 0:
        return
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("HandWornEdges", "BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def assign(obj: bpy.types.Object, material: bpy.types.Material) -> bpy.types.Object:
    obj.data.materials.append(material)
    return obj


def add_box(
    name: str,
    position: tuple[float, float, float],
    size: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    bevel: float = 0.05,
    yaw: float = 0.0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=gpos(*position))
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (size[0], size[2], size[1])
    obj.rotation_euler[2] = -yaw
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    apply_bevel(obj, bevel)
    assign(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_world_terrain(
    name: str,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    """Add a single UV-mapped world surface so biome colors blend continuously."""
    vertices = [
        gpos(-144.0, 0.0, -144.0),
        gpos(-144.0, 0.0, 144.0),
        gpos(144.0, 0.0, 144.0),
        gpos(144.0, 0.0, -144.0),
    ]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], [(0, 1, 2, 3)])
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="WorldUV")
    uv_by_vertex = ((0.0, 0.0), (0.0, 1.0), (1.0, 1.0), (1.0, 0.0))
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv = uv_by_vertex[vertex_index]
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    assign(obj, material)
    return obj


def add_ribbon(
    name: str,
    points: list[tuple[float, float]],
    half_width: float,
    y: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    """Create a winding strip with independently weathered left and right edges."""
    vertices: list[tuple[float, float, float]] = []
    uvs: list[tuple[float, float]] = []
    distances = [0.0]
    for index in range(1, len(points)):
        distances.append(distances[-1] + math.dist(points[index - 1], points[index]))
    for index, (x, z) in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        tangent_x = following[0] - previous[0]
        tangent_z = following[1] - previous[1]
        length = max(0.001, math.hypot(tangent_x, tangent_z))
        normal_x = -tangent_z / length
        normal_z = tangent_x / length
        phase = (sum(ord(character) for character in name) % 97) * 0.071
        left_width = half_width * max(
            0.72,
            min(1.06, 0.88 + 0.12 * math.sin(index * 0.71 + phase)
                + 0.055 * math.sin(index * 2.03 + phase * 0.7)),
        )
        right_width = half_width * max(
            0.72,
            min(1.06, 0.89 + 0.11 * math.sin(index * 0.83 + phase + 1.4)
                + 0.06 * math.sin(index * 1.87 + phase * 1.2)),
        )
        vertices.append(gpos(x + normal_x * left_width, y, z + normal_z * left_width))
        vertices.append(gpos(x - normal_x * right_width, y, z - normal_z * right_width))
        texture_v = distances[index] / max(half_width * 2.0, 0.1)
        uvs.extend(((0.0, texture_v), (1.0, texture_v)))
    faces = [(index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2)
             for index in range(len(points) - 1)]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="RibbonUV")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            vertex_index = mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv = uvs[vertex_index]
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    assign(obj, material)
    return obj


def north_road_center(base_x: float, z: float) -> float:
    distance = z - 25.0
    return (
        base_x
        + math.sin(distance * 0.029) * 1.35
        + math.sin(distance * 0.071) * 0.72
        + math.sin(distance * 0.149) * 0.24
    )


def crossroad_center(x: float) -> float:
    return (
        12.0
        + math.sin(math.pi * x / 96.0) * 1.5
        + math.sin(math.tau * x / 96.0) * 0.62
        + math.sin(3.0 * math.pi * x / 96.0) * 0.28
    )


def river_center(x: float) -> float:
    return 25.0 + math.sin(math.pi * x / 96.0) * 2.25 + math.sin(math.tau * x / 96.0) * 0.58


def add_cylinder(
    name: str,
    position: tuple[float, float, float],
    radius: float,
    height: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    vertices: int = 12,
    bevel: float = 0.04,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=gpos(*position))
    obj = bpy.context.object
    obj.name = name
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) <= 4
    apply_bevel(obj, bevel)
    assign(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_cone(
    name: str,
    position: tuple[float, float, float],
    bottom_radius: float,
    top_radius: float,
    height: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    vertices: int = 8,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=bottom_radius,
        radius2=top_radius,
        depth=height,
        location=gpos(*position),
    )
    obj = bpy.context.object
    obj.name = name
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) <= 4
    apply_bevel(obj, 0.035)
    assign(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_ico(
    name: str,
    position: tuple[float, float, float],
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    subdivisions: int = 2,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1.0, location=gpos(*position))
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0], scale[2], scale[1])
    obj.rotation_euler = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    assign(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_beam(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    width: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    round_beam: bool = False,
) -> bpy.types.Object:
    start_b = gvec(start)
    end_b = gvec(end)
    direction = end_b - start_b
    midpoint = (start_b + end_b) * 0.5
    length = direction.length
    if round_beam:
        bpy.ops.mesh.primitive_cylinder_add(vertices=9, radius=width * 0.5, depth=length, location=midpoint)
    else:
        bpy.ops.mesh.primitive_cube_add(location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    if not round_beam:
        obj.dimensions = (width, width * 0.9, length)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        apply_bevel(obj, min(0.045, width * 0.18))
    assign(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_gable_roof(
    name: str,
    center: tuple[float, float, float],
    width: float,
    depth: float,
    rise: float,
    material: bpy.types.Material,
    collection: bpy.types.Collection,
) -> bpy.types.Object:
    x, y, z = center
    half_w = width * 0.5
    half_d = depth * 0.5
    godot_vertices = [
        (x - half_w, y, z - half_d),
        (x + half_w, y, z - half_d),
        (x, y + rise, z - half_d),
        (x - half_w, y, z + half_d),
        (x + half_w, y, z + half_d),
        (x, y + rise, z + half_d),
    ]
    vertices = [gpos(*vertex) for vertex in godot_vertices]
    faces = [
        (0, 2, 1), (3, 4, 5),
        (0, 3, 5, 2), (2, 5, 4, 1),
        (0, 1, 4, 3),
    ]
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    assign(obj, material)
    apply_bevel(obj, 0.07)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.03)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)
    return obj


def make_materials() -> dict[str, bpy.types.Material]:
    return {
        "world_terrain": make_material("WorldTerrain", "#33472b", 0.94),
        "grass": make_material("Grass_Meadow", "#33472b", 0.95),
        "grass_dark": make_material("Grass_Shadow", "#213523", 0.95),
        "grass_darkwood": make_material("Grass_DarkwoodReach", "#1d3023", 0.96),
        "grass_moor": make_material("Grass_NorthwatchMoor", "#48503b", 0.97),
        "grass_ironpine": make_material("Grass_Ironpine", "#374438", 0.96),
        "grass_amber": make_material("Grass_Amberfield", "#596039", 0.96),
        "grass_briar": make_material("Grass_Briarfen", "#2c4a3e", 0.97),
        "grass_willow": make_material("Grass_Willowmere", "#365543", 0.96),
        "grass_south": make_material("Grass_Southroad", "#53613b", 0.96),
        "earth_ashen": make_material("Earth_AshenQuarry", "#554f46", 0.98),
        "earth": make_material("Road_PackedEarth", "#655039", 0.96),
        "earth_dark": make_material("Earth_Damp", "#443629", 0.94),
        "cobble": make_material("Stone_Cobble", "#68665f", 0.88),
        "cobble_light": make_material("Stone_CobbleLight", "#817a6d", 0.87),
        "stone": make_material("Stone_Granite", "#4d5252", 0.9),
        "stone_light": make_material("Stone_WornFace", "#666b69", 0.88),
        "mortar": make_material("Stone_Mortar", "#363a39", 0.96),
        "plaster_oat": make_material("Plaster_Oat", "#8e775b", 0.93),
        "plaster_clay": make_material("Plaster_Clay", "#73533e", 0.94),
        "plaster_moss": make_material("Plaster_Moss", "#667057", 0.94),
        "wood": make_material("Timber_DarkOak", "#382317", 0.86),
        "wood_mid": make_material("Timber_WarmOak", "#5a3922", 0.84),
        "wood_light": make_material("Timber_WornEdge", "#765033", 0.86),
        "roof_red": make_material("Roof_Oxblood", "#57241e", 0.9),
        "roof_brown": make_material("Roof_WoodShingle", "#3e281f", 0.91),
        "metal": make_material("Iron_Blackened", "#2e3131", 0.48, 0.68),
        "gold": make_material("Stonehaven_Gold", "#b98a32", 0.42, 0.55),
        "window": make_material("Window_Firelight", "#e29a3a", 0.32, emission="#ffad45", emission_strength=2.4),
        "water": make_material("River_Water", "#1c5260", 0.18, 0.12, 0.78),
        "leaf_dark": make_material("Leaves_ForestShadow", "#17331f", 0.96),
        "leaf_mid": make_material("Leaves_Oak", "#2d572f", 0.95),
        "leaf_light": make_material("Leaves_Sunlit", "#60733b", 0.94),
        "rock": make_material("Rock_Valley", "#484b47", 0.93),
        "rock_light": make_material("Rock_Lichen", "#67695b", 0.94),
        "banner": make_material("Banner_StonehavenRed", "#7c211c", 0.86),
        "rope": make_material("Rope_Hemp", "#9a7443", 0.98),
        "flower_red": make_material("Flower_Crimson", "#8f271e", 0.82),
        "flower_gold": make_material("Flower_Gold", "#d29b38", 0.78),
        "leaf_fresh": make_material("Leaves_FreshGrowth", "#476b35", 0.95),
        "grass_blade_dark": make_material("GrassBlade_Forest", "#203a22", 0.96),
        "grass_blade_mid": make_material("GrassBlade_Meadow", "#456536", 0.95),
        "grass_blade_light": make_material("GrassBlade_Sunlit", "#708044", 0.94),
        "grass_blade_dry": make_material("GrassBlade_Dry", "#827b4c", 0.97),
    }


def build_terrain(collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    add_box("NineGridWorldFoundation", (0, -0.34, 0), (288, 0.62, 288), m["grass_dark"], collection, 0.15)
    add_world_terrain("NineGridLivingTerrain", m["world_terrain"], collection)

    def north_road(base_x: float) -> list[tuple[float, float]]:
        return [(north_road_center(base_x, float(z)), float(z)) for z in range(-140, 141, 4)]

    add_ribbon("CentralNorthRoad", north_road(0.0), 3.35, 0.052, m["earth"], collection)
    add_ribbon("WestNorthRoad", north_road(-96.0), 2.6, 0.051, m["earth_dark"], collection)
    add_ribbon("EastNorthRoad", north_road(96.0), 2.6, 0.051, m["earth_dark"], collection)
    crossroad = [
        (float(x), crossroad_center(float(x)))
        for x in range(-140, 141, 4)
    ]
    add_ribbon("RealmCrossroad", crossroad, 2.6, 0.053, m["earth"], collection)
    add_ribbon(
        "DarkwoodCampTrail",
        [(-116.0, -101.0), (-111.5, -99.0), (-106.0, -99.5), (-101.5, -98.0), (-97.5, -97.0)],
        1.35,
        0.054,
        m["earth_dark"],
        collection,
    )
    add_cylinder("VillageSquare", (0, 0.035, -13), 8.25, 0.07, m["earth_dark"], collection, 64, 0.02)
    river = [
        (float(x), river_center(float(x)))
        for x in range(-144, 145, 3)
    ]
    add_ribbon("River", river, 3.85, 0.042, m["water"], collection)

    for index in range(130):
        angle = random.random() * math.tau
        radius = math.sqrt(random.random()) * 7.55
        x = math.cos(angle) * radius
        z = -13 + math.sin(angle) * radius
        scale_x = random.uniform(0.22, 0.42)
        scale_z = random.uniform(0.18, 0.34)
        material = m["cobble_light"] if index % 4 == 0 else m["cobble"]
        add_ico(
            f"SquareCobble_{index:03}", (x, 0.075, z),
            (scale_x * 0.65, random.uniform(0.028, 0.048), scale_z * 0.65), material, collection, 2,
            (random.uniform(-0.08, 0.08), random.uniform(-0.08, 0.08), random.random() * math.tau),
        )

    ridge_specs = [
        ("West", (-144.5, 4.7, 0), (8.5, 10.0, 288.0)),
        ("East", (144.5, 4.7, 0), (8.5, 10.0, 288.0)),
        ("North", (0, 5.5, -144.5), (288.0, 12.0, 8.5)),
        ("South", (0, 4.7, 144.5), (288.0, 10.0, 8.5)),
    ]
    for name, position, size in ridge_specs:
        add_box(f"{name}RidgeCore", position, size, m["grass_dark"], collection, 1.0)
    for index in range(84):
        side = index % 4
        along = random.uniform(-136, 136)
        if side == 0:
            position = (-140.5, random.uniform(2.0, 6.8), along)
            scale = (random.uniform(3.2, 6.5), random.uniform(3.0, 7.5), random.uniform(3.5, 7.5))
        elif side == 1:
            position = (140.5, random.uniform(2.0, 6.8), along)
            scale = (random.uniform(3.2, 6.5), random.uniform(3.0, 7.5), random.uniform(3.5, 7.5))
        elif side == 2:
            position = (along, random.uniform(2.0, 7.5), -140.5)
            scale = (random.uniform(3.5, 7.5), random.uniform(3.0, 8.0), random.uniform(3.2, 6.5))
        else:
            position = (along, random.uniform(2.0, 6.0), 140.5)
            scale = (random.uniform(3.5, 7.5), random.uniform(3.0, 7.0), random.uniform(3.2, 6.5))
        add_ico(f"RidgeRock_{index:02}", position, scale, m["rock"] if index % 3 else m["rock_light"], collection, 3,
                (random.random(), random.random(), random.random()))


def build_bridge(
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
    x_offset: float = 0.0,
    prefix: str = "Central",
) -> None:
    for index, z in enumerate([19.3 + step * 0.82 for step in range(15)]):
        edge = abs(z - 25.0)
        y = 0.58 - max(0.0, edge - 4.1) * 0.095
        add_box(f"{prefix}BridgePlank_{index:02}", (x_offset, y, z), (6.8, 0.18, 0.73),
                m["wood_light"] if index % 3 == 0 else m["wood_mid"], collection, 0.055,
                random.uniform(-0.012, 0.012))
    add_box(f"{prefix}BridgeUnderBeamLeft", (x_offset - 2.5, 0.28, 25),
            (0.38, 0.42, 11.0), m["wood"], collection, 0.06)
    add_box(f"{prefix}BridgeUnderBeamRight", (x_offset + 2.5, 0.28, 25),
            (0.38, 0.42, 11.0), m["wood"], collection, 0.06)
    for side in (-1, 1):
        x = x_offset + side * 3.15
        for z in (20.2, 22.6, 25.0, 27.4, 29.8):
            add_cylinder(f"{prefix}BridgePost_{side}_{z}", (x, 1.3, z),
                         0.14, 1.8, m["wood"], collection, 9, 0.025)
        add_beam(f"{prefix}BridgeRail_{side}", (x, 1.85, 20.0),
                 (x, 1.85, 30.0), 0.18, m["wood_mid"], collection)
    for x in (x_offset - 2.75, x_offset + 2.75):
        for z in (22.0, 28.0):
            add_cylinder(f"{prefix}BridgePier_{x}_{z}", (x, -0.1, z),
                         0.28, 2.5, m["wood"], collection, 10, 0.04)


def stone_wall_segment(name: str, x: float, collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    width = 6.4
    add_box(name + "Mortar", (x, 1.05, 3.5), (width, 2.1, 0.72), m["mortar"], collection, 0.08)
    block_width = 1.22
    for course in range(3):
        y = 0.35 + course * 0.68
        offset = 0.58 if course % 2 else 0.0
        cursor = -width * 0.5 + block_width * 0.5 - offset
        block = 0
        while cursor < width * 0.5:
            actual_x = x + cursor
            if actual_x > x - width * 0.54 and actual_x < x + width * 0.54:
                add_box(
                    f"{name}_Stone_{course}_{block}", (actual_x, y, 3.5 + random.uniform(-0.025, 0.025)),
                    (block_width - 0.06, 0.6, 0.86),
                    m["stone_light"] if (course + block) % 5 == 0 else m["stone"], collection, 0.075,
                    random.uniform(-0.01, 0.01),
                )
            cursor += block_width
            block += 1
    for crenel_x in [x - 2.65, x - 1.75, x - 0.85, x + 0.05, x + 0.95, x + 1.85, x + 2.75]:
        add_box(f"{name}_Crenel", (crenel_x, 2.42, 3.5), (0.58, 0.65, 0.9), m["stone_light"], collection, 0.07)


def stone_tower(name: str, x: float, collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    add_box(name + "Core", (x, 2.45, 3.5), (3.2, 4.9, 3.2), m["stone"], collection, 0.14)
    for y in (0.45, 1.35, 2.25, 3.15, 4.05):
        for dx, dz in ((-1.48, -1.45), (1.48, -1.45), (-1.48, 1.45), (1.48, 1.45)):
            add_box(f"{name}_Quoin", (x + dx, y, 3.5 + dz), (0.55, 0.72, 0.55), m["stone_light"], collection, 0.07)
    add_box(name + "Parapet", (x, 5.02, 3.5), (3.65, 0.45, 3.65), m["stone_light"], collection, 0.08)
    for dx, dz in ((-1.5, -1.5), (0, -1.5), (1.5, -1.5), (-1.5, 1.5), (0, 1.5), (1.5, 1.5)):
        add_box(f"{name}_Crenel", (x + dx, 5.52, 3.5 + dz), (0.55, 0.62, 0.55), m["stone_light"], collection, 0.06)


def build_gate_and_walls(collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    for x in range(-28, 29, 7):
        if abs(x) >= 5:
            stone_wall_segment(f"Wall_{x}", float(x), collection, m)
    stone_tower("GateTowerLeft", -4.5, collection, m)
    stone_tower("GateTowerRight", 4.5, collection, m)
    add_box("GateCrossBeam", (0, 5.0, 3.5), (6.0, 0.9, 1.0), m["wood"], collection, 0.1)
    add_box("GateIronBand", (0, 5.0, 2.95), (6.15, 0.15, 0.12), m["metal"], collection, 0.02)
    add_box("GateBanner", (0, 4.05, 2.88), (1.4, 1.9, 0.08), m["banner"], collection, 0.025)
    add_cylinder("GateCrest", (0, 4.25, 2.79), 0.34, 0.12, m["gold"], collection, 18, 0.025)
    for tower_x in (-4.5, 4.5):
        add_cone(f"GateRoof_{tower_x}", (tower_x, 6.35, 3.5), 2.65, 0.05, 1.9, m["roof_red"], collection, 8)


def build_house(
    name: str,
    position: tuple[float, float, float],
    plaster: bpy.types.Material,
    roof: bpy.types.Material,
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
    variant: int,
) -> None:
    x, _, z = position
    add_box(name + "Foundation", (x, 0.32, z), (7.0, 0.64, 6.0), m["stone"], collection, 0.09)
    add_box(name + "Walls", (x, 1.83, z), (6.8, 2.4, 5.8), plaster, collection, 0.1)
    for beam_x in (-3.18, 0.0, 3.18):
        add_box(name + "FrontStud", (x + beam_x, 1.9, z + 2.96), (0.24, 2.65, 0.2), m["wood"], collection, 0.035)
        add_box(name + "BackStud", (x + beam_x, 1.9, z - 2.96), (0.24, 2.65, 0.2), m["wood"], collection, 0.035)
    for side_x in (-3.36, 3.36):
        for side_z in (-1.9, 0.0, 1.9):
            add_box(name + "SideStud", (x + side_x, 1.9, z + side_z), (0.2, 2.65, 0.24), m["wood"], collection, 0.035)
    for beam_y in (0.72, 2.75):
        add_box(name + "FrontRail", (x, beam_y, z + 2.99), (6.75, 0.22, 0.2), m["wood"], collection, 0.035)
        add_box(name + "BackRail", (x, beam_y, z - 2.99), (6.75, 0.22, 0.2), m["wood"], collection, 0.035)

    add_gable_roof(name + "Roof", (x, 3.05, z), 8.0, 7.0, 2.35, roof, collection)
    add_beam(name + "RoofRidge", (x, 5.43, z - 3.7), (x, 5.43, z + 3.7), 0.24, m["wood"], collection)
    for front_z in (z - 3.51, z + 3.51):
        add_beam(name + "GableLeft", (x - 4.0, 3.04, front_z), (x, 5.42, front_z), 0.2, m["wood_mid"], collection)
        add_beam(name + "GableRight", (x, 5.42, front_z), (x + 4.0, 3.04, front_z), 0.2, m["wood_mid"], collection)

    add_box(name + "Door", (x, 1.25, z + 3.06), (1.38, 2.45, 0.16), m["wood"], collection, 0.08)
    for rivet_y in (0.55, 1.25, 1.95):
        add_box(name + "DoorIron", (x, rivet_y, z + 3.155), (1.16, 0.09, 0.035), m["metal"], collection, 0.015)
    for window_x in (-2.05, 2.05):
        add_box(name + "Window", (x + window_x, 1.8, z + 3.075), (1.15, 0.9, 0.08), m["window"], collection, 0.035)
        add_box(name + "WindowFrameV", (x + window_x, 1.8, z + 3.13), (0.08, 1.03, 0.08), m["wood"], collection, 0.02)
        add_box(name + "WindowFrameH", (x + window_x, 1.8, z + 3.13), (1.28, 0.08, 0.08), m["wood"], collection, 0.02)
    chimney_x = x + (-2.15 if variant % 2 else 2.15)
    chimney_z = z - 1.4
    add_box(name + "Chimney", (chimney_x, 4.25, chimney_z), (0.75, 3.15, 0.75), m["stone"], collection, 0.08)
    add_box(name + "ChimneyCap", (chimney_x, 5.88, chimney_z), (0.95, 0.24, 0.95), m["stone_light"], collection, 0.05)

    if name == "Blacksmith":
        add_cone("BlacksmithForge", (x - 2.15, 0.72, z + 3.75), 0.7, 0.48, 1.25, m["stone"], collection, 8)
        add_box("BlacksmithAwning", (x + 2.2, 2.65, z + 3.8), (2.7, 0.16, 1.5), m["roof_red"], collection, 0.05)
    elif name == "WayfarerInn":
        add_beam("InnSignArm", (x + 3.05, 2.65, z + 3.2), (x + 4.4, 2.65, z + 3.2), 0.12, m["metal"], collection)
        add_box("InnHangingSign", (x + 4.35, 2.15, z + 3.2), (0.75, 0.85, 0.12), m["wood_mid"], collection, 0.055)
        add_cylinder("InnSignCrest", (x + 4.35, 2.15, z + 3.12), 0.2, 0.08, m["gold"], collection, 16, 0.02)
    elif name == "Herbalist":
        for pot_x in (-1.3, 0.0, 1.3):
            add_cone("HerbalistPot", (x + pot_x, 0.35, z + 3.55), 0.32, 0.23, 0.55, m["plaster_clay"], collection, 10)
            add_ico("HerbalistPlant", (x + pot_x, 0.92, z + 3.55), (0.42, 0.5, 0.42), m["leaf_mid"], collection, 1)
    else:
        for crate_x in (-2.1, 2.1):
            add_box("StorehouseCrate", (x + crate_x, 0.58, z + 3.75), (1.1, 1.1, 1.1), m["wood_mid"], collection, 0.07)
            add_box("StorehouseCrateBand", (x + crate_x, 0.58, z + 4.32), (1.15, 0.13, 0.08), m["metal"], collection, 0.02)


def build_well(collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    center_z = -13.0
    for course in range(2):
        for index in range(14):
            angle = math.tau * index / 14.0 + (course % 2) * 0.11
            x = math.cos(angle) * 1.12
            z = center_z + math.sin(angle) * 1.12
            add_box(f"WellStone_{course}_{index}", (x, 0.32 + course * 0.55, z),
                    (0.62, 0.5, 0.48), m["stone_light"] if index % 4 == 0 else m["stone"], collection, 0.08,
                    -angle)
    add_cylinder("WellWater", (0, 0.95, center_z), 0.86, 0.05, m["water"], collection, 28, 0.01)
    for x in (-1.18, 1.18):
        add_cylinder(f"WellPost_{x}", (x, 2.1, center_z), 0.13, 2.55, m["wood"], collection, 10, 0.025)
    add_beam("WellCrossbar", (-1.4, 2.55, center_z), (1.4, 2.55, center_z), 0.17, m["wood_mid"], collection)
    add_cylinder("WellCrank", (0, 2.25, center_z), 0.15, 2.4, m["wood_mid"], collection, 12, 0.025)
    add_cone("WellRoof", (0, 3.32, center_z), 2.15, 0.05, 1.0, m["roof_red"], collection, 8)


REGION_TREE_COUNTS = {
    (-1, -1): 20,
    (0, -1): 8,
    (1, -1): 17,
    (-1, 0): 11,
    (1, 0): 18,
    (-1, 1): 16,
    (0, 1): 9,
    (1, 1): 4,
}

REGION_ROCK_COUNTS = {
    (-1, -1): 6,
    (0, -1): 8,
    (1, -1): 9,
    (-1, 0): 5,
    (1, 0): 6,
    (-1, 1): 5,
    (0, 1): 6,
    (1, 1): 12,
}


def nature_hash01(grid_x: int, grid_z: int, index: int, salt: int) -> float:
    value = (
        ((index + 1) * 374761393)
        + ((grid_x + 2) * 668265263)
        + ((grid_z + 2) * 2246822519)
        + ((salt + 1) * 3266489917)
    ) & 0xFFFFFFFF
    value ^= value >> 13
    value = (value * 1274126177) & 0xFFFFFFFF
    value ^= value >> 16
    return (value & 0x00FFFFFF) / 16777216.0


def organic_nature_position(
    grid_x: int,
    grid_z: int,
    index: int,
    count: int,
    salt: int,
) -> tuple[float, float]:
    cluster_count = max(1, min(4, (count + 5) // 6))
    local_x = 0.0
    local_z = 0.0
    for attempt in range(6):
        candidate = index + attempt * count
        cluster = candidate % cluster_count
        cluster_angle = math.tau * nature_hash01(grid_x, grid_z, cluster, salt + 1)
        cluster_radius = 11.0 + 19.0 * nature_hash01(grid_x, grid_z, cluster, salt + 2)
        center_x = math.cos(cluster_angle) * cluster_radius
        center_z = math.sin(cluster_angle) * cluster_radius
        ring = candidate // cluster_count
        angle = math.tau * nature_hash01(grid_x, grid_z, candidate, salt + 3)
        radius = (2.3 + math.sqrt(ring + 1.0) * 3.6) * (
            0.72 + 0.55 * nature_hash01(grid_x, grid_z, candidate, salt + 4)
        )
        local_x = max(-42.0, min(42.0, center_x + math.cos(angle) * radius))
        local_z = max(-42.0, min(42.0, center_z + math.sin(angle) * radius))
        world_x = grid_x * 96.0 + local_x
        world_z = grid_z * 96.0 + local_z
        camp_clear = (world_x + 116.0) ** 2 + (world_z + 104.0) ** 2 >= 19.5 ** 2
        if abs(local_x) > 7.0 and abs(world_z - 25.0) > 6.2 and abs(world_z - 12.0) > 4.7 and camp_clear:
            break
    world_z = grid_z * 96.0 + local_z
    if abs(local_x) <= 7.0:
        local_x = 7.6 if nature_hash01(grid_x, grid_z, index, salt + 8) >= 0.5 else -7.6
    if abs(world_z - 25.0) <= 6.2:
        local_z += 7.0 if world_z >= 25.0 else -7.0
    world_z = grid_z * 96.0 + local_z
    if abs(world_z - 12.0) <= 4.7:
        local_z += 5.4 if world_z >= 12.0 else -5.4
    world_x = grid_x * 96.0 + local_x
    world_z = grid_z * 96.0 + local_z
    camp_dx = world_x + 116.0
    camp_dz = world_z + 104.0
    camp_distance = math.hypot(camp_dx, camp_dz)
    if camp_distance < 19.5:
        if camp_distance < 0.001:
            camp_dx, camp_dz, camp_distance = -1.0, 0.0, 1.0
        local_x = max(-42.0, min(42.0, -116.0 + camp_dx / camp_distance * 19.5 - grid_x * 96.0))
        local_z = max(-42.0, min(42.0, -104.0 + camp_dz / camp_distance * 19.5 - grid_z * 96.0))
    return grid_x * 96.0 + local_x, grid_z * 96.0 + local_z


def build_outer_tree_positions() -> list[tuple[float, float, float, float]]:
    positions: list[tuple[float, float, float, float]] = []
    for grid_z in (-1, 0, 1):
        for grid_x in (-1, 0, 1):
            if grid_x == 0 and grid_z == 0:
                continue
            count = REGION_TREE_COUNTS[(grid_x, grid_z)]
            for index in range(count):
                x, z = organic_nature_position(grid_x, grid_z, index, count, 100)
                scale = 0.78 + nature_hash01(grid_x, grid_z, index, 190) * 0.62
                positions.append((x, 0.0, z, scale))
    return positions


def build_outer_rock_specs() -> list[tuple[tuple[float, float, float], tuple[float, float, float]]]:
    specs: list[tuple[tuple[float, float, float], tuple[float, float, float]]] = []
    for grid_z in (-1, 0, 1):
        for grid_x in (-1, 0, 1):
            if grid_x == 0 and grid_z == 0:
                continue
            count = REGION_ROCK_COUNTS[(grid_x, grid_z)]
            for index in range(count):
                x, z = organic_nature_position(grid_x, grid_z, index, count, 700)
                size_x = 1.25 + nature_hash01(grid_x, grid_z, index, 790) * 1.55
                size_y = 0.75 + nature_hash01(grid_x, grid_z, index, 791) * 1.05
                size_z = 1.05 + nature_hash01(grid_x, grid_z, index, 792) * 1.45
                specs.append(((x, size_y * 0.42, z), (size_x * 0.52, size_y * 0.52, size_z * 0.52)))
    return specs


TREE_POSITIONS = [
    (-18, 0, -5, 1.15), (18, 0, -6, 1.2), (-20, 0, -33, 1.3), (20, 0, -34, 1.2),
    (-34, 0, 35, 0.9), (-27, 0, 29, 1.02), (-38, 0, 18, 1.14), (-31, 0, 8, 1.26),
    (-37, 0, -8, 0.9), (-32, 0, -22, 1.02), (-36, 0, -36, 1.14), (34, 0, 36, 1.26),
    (27, 0, 31, 0.9), (38, 0, 18, 1.02), (32, 0, 6, 1.14), (37, 0, -8, 1.26),
    (31, 0, -23, 0.9), (37, 0, -36, 1.02), (-17, 0, 37, 1.14), (17, 0, 38, 1.26),
    (-24, 0, -41, 0.9), (24, 0, -40, 1.02),
] + build_outer_tree_positions()


def build_pine_tree(
    index: int,
    x: float,
    z: float,
    scale: float,
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
) -> None:
    trunk_height = 4.9 * scale
    add_cone(f"Pine_{index}_Trunk", (x, trunk_height * 0.5, z), 0.42 * scale, 0.22 * scale,
             trunk_height, m["wood"], collection, 10)
    for layer in range(5):
        layer_height = (2.1 + layer * 0.92) * scale
        radius = (2.25 - layer * 0.29) * scale
        material = m["leaf_dark"] if layer % 2 == 0 else m["leaf_mid"]
        add_cone(f"Pine_{index}_Layer_{layer}", (x, layer_height, z), radius, 0.08 * scale,
                 1.75 * scale, material, collection, 11)


def build_willow_tree(
    index: int,
    x: float,
    z: float,
    scale: float,
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
) -> None:
    trunk_height = 3.9 * scale
    add_cone(f"Willow_{index}_Trunk", (x, trunk_height * 0.5, z), 0.52 * scale, 0.28 * scale,
             trunk_height, m["wood"], collection, 12)
    branch_base = (x, 2.85 * scale, z)
    for branch_index, angle in enumerate((0.2, 1.72, 3.22, 4.78)):
        end = (x + math.cos(angle) * 1.75 * scale, 4.15 * scale, z + math.sin(angle) * 1.75 * scale)
        add_beam(f"Willow_{index}_Branch_{branch_index}", branch_base, end, 0.24 * scale,
                 m["wood_mid"], collection, True)
        material = m["leaf_fresh"] if branch_index % 2 else m["leaf_mid"]
        for spray in range(3):
            droop = (0.18 + spray * 0.52) * scale
            side = (spray - 1) * 0.34 * scale
            add_ico(f"Willow_{index}_Crown_{branch_index}_{spray}",
                    (end[0] - math.sin(angle) * side, end[1] - droop, end[2] + math.cos(angle) * side),
                    ((0.72 - spray * 0.06) * scale, (0.88 + spray * 0.08) * scale,
                     (0.64 - spray * 0.04) * scale), material, collection, 2,
                    (0.1 + spray * 0.11, 0.15, angle))
    for crown in range(5):
        angle = crown * math.tau / 5.0 + 0.3
        add_ico(f"Willow_{index}_Crown_Top_{crown}",
                (x + math.cos(angle) * 0.62 * scale, (4.75 + (crown % 2) * 0.28) * scale,
                 z + math.sin(angle) * 0.62 * scale),
                (0.78 * scale, 0.70 * scale, 0.74 * scale),
                m["leaf_fresh"] if crown % 2 else m["leaf_mid"], collection, 2)


def build_dead_tree(
    index: int,
    x: float,
    z: float,
    scale: float,
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
) -> None:
    trunk_height = 4.6 * scale
    add_cone(f"DeadTree_{index}_Trunk", (x, trunk_height * 0.5, z), 0.47 * scale, 0.16 * scale,
             trunk_height, m["wood"], collection, 9)
    for branch_index, angle in enumerate((0.35, 1.65, 2.9, 4.35, 5.45)):
        start = (x, (2.15 + branch_index * 0.39) * scale, z)
        end = (x + math.cos(angle) * (1.1 + branch_index * 0.11) * scale,
               (3.25 + branch_index * 0.34) * scale,
               z + math.sin(angle) * (1.1 + branch_index * 0.11) * scale)
        add_beam(f"DeadTree_{index}_Branch_{branch_index}", start, end, 0.18 * scale,
                 m["wood_mid"], collection, True)


def build_tree(index: int, spec: tuple[float, float, float, float], collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    x, _, z, scale = spec
    grid_x = max(-1, min(1, round(x / 96.0)))
    grid_z = max(-1, min(1, round(z / 96.0)))
    if (grid_x, grid_z) == (1, -1):
        build_pine_tree(index, x, z, scale, collection, m)
        return
    if (grid_x, grid_z) == (-1, 1):
        build_willow_tree(index, x, z, scale, collection, m)
        return
    if (grid_x, grid_z) == (1, 1) or ((grid_x, grid_z) == (-1, -1) and index % 7 == 0):
        build_dead_tree(index, x, z, scale, collection, m)
        return
    rng = random.Random(7301 + index * 97)
    trunk_height = 4.7 * scale
    add_cone(f"Tree_{index}_Trunk", (x, trunk_height * 0.5, z), 0.52 * scale, 0.24 * scale,
             trunk_height, m["wood"], collection, 12)
    branch_ends: list[tuple[float, float, float]] = []
    for branch_index in range(8):
        angle = branch_index * math.tau / 8.0 + rng.uniform(-0.18, 0.18)
        reach = (1.45 + rng.uniform(0.0, 0.65)) * scale
        end_height = (4.25 + rng.uniform(0.0, 1.55)) * scale
        start = (x, (2.65 + (branch_index % 3) * 0.42) * scale, z)
        end = (x + math.cos(angle) * reach, end_height, z + math.sin(angle) * reach)
        branch_ends.append(end)
        add_beam(f"Tree_{index}_Branch_{branch_index}", start, end,
                 (0.22 - (branch_index % 2) * 0.025) * scale, m["wood_mid"], collection, True)
        twig_end = (end[0] + math.cos(angle + 0.52) * 0.62 * scale,
                    end[1] + 0.52 * scale,
                    end[2] + math.sin(angle + 0.52) * 0.62 * scale)
        add_beam(f"Tree_{index}_Twig_{branch_index}",
                 ((start[0] + end[0]) * 0.5, (start[1] + end[1]) * 0.5, (start[2] + end[2]) * 0.5),
                 twig_end, 0.10 * scale, m["wood_mid"], collection, True)

        tangent = (-math.sin(angle), math.cos(angle))
        for cluster in range(3):
            side = (cluster - 1) * 0.52 * scale
            position = (
                end[0] + tangent[0] * side + math.cos(angle) * (0.12 if cluster == 2 else 0.0) * scale,
                end[1] + (0.16 - cluster * 0.13) * scale,
                end[2] + tangent[1] * side + math.sin(angle) * (0.12 if cluster == 2 else 0.0) * scale,
            )
            leaf_scale = (0.62 + rng.uniform(0.0, 0.22)) * scale
            material = (m["leaf_dark"], m["leaf_mid"], m["leaf_fresh"], m["leaf_light"])[
                (index + branch_index + cluster) % 4
            ]
            add_ico(f"Tree_{index}_LeafCluster_{branch_index}_{cluster}", position,
                    (leaf_scale, leaf_scale * rng.uniform(0.72, 0.94), leaf_scale * rng.uniform(0.78, 1.05)),
                    material, collection, 2,
                    (rng.uniform(-0.35, 0.35), rng.uniform(-0.35, 0.35), rng.uniform(0.0, math.tau)))

    for crown_index in range(5):
        angle = crown_index * math.tau / 5.0 + 0.4
        crown_scale = (0.66 + rng.uniform(0.0, 0.18)) * scale
        add_ico(f"Tree_{index}_TopCluster_{crown_index}",
                (x + math.cos(angle) * 0.62 * scale, (5.55 + (crown_index % 2) * 0.36) * scale,
                 z + math.sin(angle) * 0.62 * scale),
                (crown_scale, crown_scale * 0.86, crown_scale * 0.94),
                (m["leaf_mid"], m["leaf_fresh"], m["leaf_light"])[(index + crown_index) % 3],
                collection, 2,
                (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), rng.uniform(0.0, math.tau)))


def build_grass_fields(collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    """Build thousands of clustered grass blades as one efficient mesh."""
    rng = random.Random(98231)
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, int, int]] = []
    material_indices: list[int] = []
    grass_materials = [
        m["grass_blade_dark"],
        m["grass_blade_mid"],
        m["grass_blade_light"],
        m["grass_blade_dry"],
    ]
    density_by_grid = {
        (-1, -1): 0.96,
        (0, -1): 0.56,
        (1, -1): 0.72,
        (-1, 0): 0.78,
        (0, 0): 0.70,
        (1, 0): 0.98,
        (-1, 1): 0.92,
        (0, 1): 0.66,
        (1, 1): 0.24,
    }

    for _ in range(30000):
        x = rng.uniform(-139.0, 139.0)
        z = rng.uniform(-139.0, 139.0)
        grid_x = max(-1, min(1, math.floor((x + 144.0) / 96.0) - 1))
        grid_z = max(-1, min(1, math.floor((z + 144.0) / 96.0) - 1))
        local_x = x - grid_x * 96.0
        road_x = north_road_center(grid_x * 96.0, z)
        crossroad_z = crossroad_center(x)
        river_z = river_center(x)
        if abs(x - road_x) < 4.25 or abs(z - crossroad_z) < 3.45 or abs(z - river_z) < 4.85:
            continue
        if (x + 116.0) ** 2 + (z + 104.0) ** 2 < 18.5 ** 2:
            continue
        if grid_x == 0 and grid_z == 0 and (x * x + (z + 13.0) * (z + 13.0)) < 145.0:
            continue
        patch = (
            0.50
            + math.sin(x * 0.071 + math.sin(z * 0.023) * 2.4) * 0.23
            + math.sin(z * 0.109 - x * 0.031) * 0.15
            + math.cos((x + z) * 0.047) * 0.12
        )
        patch = max(0.05, min(1.0, patch))
        if rng.random() > density_by_grid[(grid_x, grid_z)] * (0.22 + patch * patch * 0.88):
            continue

        if (grid_x, grid_z) == (1, 1):
            material_index = 3
        elif patch > 0.74:
            material_index = 2
        elif patch < 0.34 or (grid_x, grid_z) in {(-1, -1), (1, 0)}:
            material_index = 0
        else:
            material_index = 1
        tuft_height = rng.uniform(0.24, 0.62) * (0.82 + patch * 0.34)
        for blade in range(3):
            angle = rng.random() * math.tau + blade * 1.94
            side_x = math.cos(angle)
            side_z = math.sin(angle)
            width = rng.uniform(0.022, 0.045)
            offset_x = rng.uniform(-0.13, 0.13)
            offset_z = rng.uniform(-0.13, 0.13)
            lean_x = rng.uniform(-0.10, 0.10)
            lean_z = rng.uniform(-0.10, 0.10)
            base_index = len(vertices)
            vertices.extend(
                [
                    gpos(x + offset_x - side_x * width, 0.018, z + offset_z - side_z * width),
                    gpos(x + offset_x + side_x * width, 0.018, z + offset_z + side_z * width),
                    gpos(x + offset_x + lean_x, tuft_height, z + offset_z + lean_z),
                ]
            )
            faces.append((base_index, base_index + 1, base_index + 2))
            material_indices.append(material_index)

    mesh = bpy.data.meshes.new("ClusteredGrassFieldsMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    for material in grass_materials:
        mesh.materials.append(material)
        material.use_backface_culling = False
    for polygon, material_index in zip(mesh.polygons, material_indices):
        polygon.material_index = material_index
    obj = bpy.data.objects.new("ClusteredGrassFields", mesh)
    collection.objects.link(obj)


def build_nature(collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    for index, spec in enumerate(TREE_POSITIONS):
        build_tree(index, spec, collection, m)
    rock_specs = [
        ((-8, 0.68, 34), (1.3, 0.9, 1.0)), ((10, 0.52, 37), (1.0, 0.7, 1.25)),
        ((-27, 0.58, 14), (1.2, 0.8, 0.85)), ((28, 0.55, 11), (1.1, 0.75, 1.4)),
        ((-24, 0.75, -35), (1.5, 1.0, 1.1)),
    ] + build_outer_rock_specs()
    for index, (position, scale) in enumerate(rock_specs):
        add_ico(f"ValleyRock_{index}", position, scale, m["rock_light"] if index % 2 else m["rock"], collection, 3,
                (random.random() * 0.6, random.random() * 0.6, random.random() * math.tau))

    build_grass_fields(collection, m)


def add_uv_ellipsoid(
    name: str,
    position: tuple[float, float, float],
    scale: tuple[float, float, float],
    material: bpy.types.Material,
    collection: bpy.types.Collection,
    segments: int = 24,
    rings: int = 14,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=gpos(*position),
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = (scale[0], scale[2], scale[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(obj, material)
    move_to_collection(obj, collection)
    return obj


def add_barrel(
    name: str,
    position: tuple[float, float, float],
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
    scale: float = 1.0,
) -> None:
    x, y, z = position
    add_cylinder(name + "Body", (x, y + 0.48 * scale, z), 0.38 * scale, 0.96 * scale,
                 m["wood_mid"], collection, 18, 0.045)
    for band_y in (y + 0.17 * scale, y + 0.48 * scale, y + 0.79 * scale):
        add_cylinder(name + "IronBand", (x, band_y, z), 0.405 * scale, 0.075 * scale,
                     m["metal"], collection, 18, 0.018)


def add_fence_run(
    name: str,
    start: tuple[float, float, float],
    end: tuple[float, float, float],
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
) -> None:
    start_v = Vector(start)
    end_v = Vector(end)
    distance = (end_v - start_v).length
    posts = max(2, int(distance / 2.1) + 1)
    for index in range(posts):
        point = start_v.lerp(end_v, index / max(1, posts - 1))
        add_cone(f"{name}_Post_{index}", (point.x, 0.72, point.z), 0.105, 0.075,
                 1.45, m["wood"], collection, 8)
    for height in (0.52, 1.02):
        add_beam(f"{name}_Rail_{height}", (start[0], height, start[2]), (end[0], height, end[2]),
                 0.085, m["wood_mid"], collection)


def add_lantern_post(
    name: str,
    position: tuple[float, float, float],
    collection: bpy.types.Collection,
    m: dict[str, bpy.types.Material],
) -> None:
    x, _, z = position
    add_cylinder(name + "Post", (x, 1.35, z), 0.12, 2.7, m["wood"], collection, 10, 0.025)
    add_beam(name + "Arm", (x, 2.52, z), (x + 0.62, 2.52, z), 0.085, m["metal"], collection)
    add_box(name + "Lamp", (x + 0.62, 2.18, z), (0.34, 0.55, 0.34), m["window"], collection, 0.035)
    for dx, dz in ((-0.19, -0.19), (-0.19, 0.19), (0.19, -0.19), (0.19, 0.19)):
        add_beam(name + "Frame", (x + 0.62 + dx, 1.88, z + dz),
                 (x + 0.62 + dx, 2.48, z + dz), 0.025, m["metal"], collection)
    add_cone(name + "Cap", (x + 0.62, 2.56, z), 0.32, 0.04, 0.22, m["metal"], collection, 8)


def build_village_detail_pass(collection: bpy.types.Collection, m: dict[str, bpy.types.Material]) -> None:
    # Broken wheel ruts and scattered verge stones keep the road worn rather than engineered.
    def central_road_x(z: float) -> float:
        return north_road_center(0.0, z)

    rut_segments = [(-136, -104), (-94, -63), (-52, -25), (-6, 19), (32, 58), (72, 101), (114, 136)]
    for lane_index, offset in enumerate((-1.55, 1.55)):
        for segment_index, (start_z, end_z) in enumerate(rut_segments):
            points = [
                (
                    central_road_x(float(z)) + offset
                    + math.sin(z * 0.21 + lane_index * 1.8) * 0.10,
                    float(z),
                )
                for z in range(start_z, end_z + 1, 3)
            ]
            add_ribbon(
                f"RoadRut_{lane_index}_{segment_index}",
                points,
                0.14 + 0.025 * ((lane_index + segment_index) % 2),
                0.068,
                m["earth_dark"],
                collection,
            )
    for side in (-1, 1):
        for index, z in enumerate(range(-136, 137, 3)):
            if (index * 7 + (0 if side < 0 else 3)) % 11 in {0, 1, 5, 8}:
                continue
            verge = 3.15 + random.uniform(-0.20, 0.72)
            x = central_road_x(float(z)) + side * verge
            add_ico(f"RoadEdge_{side}_{index}",
                    (x + random.uniform(-0.22, 0.22), 0.105, z + random.uniform(-0.55, 0.55)),
                    (random.uniform(0.18, 0.36), random.uniform(0.07, 0.15), random.uniform(0.22, 0.48)),
                    m["cobble_light"] if index % 5 == 0 else m["cobble"], collection, 2,
                    (random.uniform(-0.12, 0.12), random.uniform(-0.12, 0.12), random.random() * math.tau))

    # Four tighter paths lead from the square to each occupied building.
    path_specs = [((-7.5, -13.0), (-10.5, -10.2)), ((7.5, -13.3), (10.5, -10.8)),
                  ((-5.5, -18.0), (-11.3, -22.6)), ((5.5, -18.0), (11.3, -23.6))]
    stone_index = 0
    for start, end in path_specs:
        start_v = Vector(start)
        end_v = Vector(end)
        for step in range(15):
            center = start_v.lerp(end_v, step / 14.0)
            direction = (end_v - start_v).normalized()
            side_v = Vector((-direction.y, direction.x))
            for lane in (-0.52, 0.0, 0.52):
                point = center + side_v * lane
                add_ico(f"HousePathStone_{stone_index:03}",
                        (point.x + random.uniform(-0.09, 0.09), 0.09, point.y + random.uniform(-0.09, 0.09)),
                        (random.uniform(0.26, 0.38), random.uniform(0.055, 0.095), random.uniform(0.28, 0.42)),
                        m["cobble_light"] if stone_index % 4 == 0 else m["cobble"], collection, 2,
                        (random.uniform(-0.06, 0.06), random.uniform(-0.06, 0.06), random.random() * math.tau))
                stone_index += 1

    # Civilian props make each work area distinct.
    for index, position in enumerate(((-14.0, 0, -9.4), (-8.2, 0, -9.5),
                                      (8.1, 0, -10.1), (14.0, 0, -10.3),
                                      (9.0, 0, -23.0), (14.2, 0, -23.2))):
        add_barrel(f"VillageBarrel_{index}", position, collection, m, random.uniform(0.85, 1.08))
    for index in range(10):
        x = -14.5 + (index % 5) * 0.42
        z = -17.0 + (index // 5) * 0.48
        add_beam(f"BlacksmithWoodPile_{index}", (x - 0.34, 0.22 + (index // 5) * 0.24, z),
                 (x + 0.34, 0.22 + (index // 5) * 0.24, z + random.uniform(-0.08, 0.08)),
                 0.11, m["wood_light"], collection, True)
    add_box("VillageBenchSeat", (-5.3, 0.55, -14.7), (3.2, 0.18, 0.62), m["wood_mid"], collection, 0.06)
    for x in (-6.55, -4.05):
        add_box("VillageBenchLeg", (x, 0.28, -14.7), (0.18, 0.55, 0.5), m["wood"], collection, 0.04)

    add_fence_run("HerbalistFenceBack", (-17.0, 0, -30.5), (-7.2, 0, -30.5), collection, m)
    add_fence_run("HerbalistFenceSide", (-17.0, 0, -30.5), (-17.0, 0, -22.3), collection, m)
    add_fence_run("StorehouseFenceBack", (7.2, 0, -31.0), (17.0, 0, -31.0), collection, m)
    for index, position in enumerate(((-7.0, 0, -5.5), (6.2, 0, -5.5),
                                      (-5.8, 0, -20.2), (5.2, 0, -20.5))):
        add_lantern_post(f"StonehavenLantern_{index}", position, collection, m)

    # Layered shrubs, flowers, roots, and small leaf clusters remove the toy-like silhouette.
    shrub_positions = [(-16, -20), (-15, -31), (-8, -31), (8, -32), (16, -31),
                       (-22, -12), (22, -14), (-21, 7), (21, 7), (-12, 15), (12, 15)]
    for index, (x, z) in enumerate(shrub_positions):
        for cluster in range(5):
            angle = math.tau * cluster / 5.0 + index * 0.31
            radius = 0.38 if cluster else 0.0
            add_uv_ellipsoid(
                f"Shrub_{index}_{cluster}",
                (x + math.cos(angle) * radius, 0.48 + (cluster == 0) * 0.18, z + math.sin(angle) * radius),
                (0.58, 0.52 + (cluster == 0) * 0.18, 0.50),
                (m["leaf_mid"], m["leaf_fresh"], m["leaf_dark"])[(index + cluster) % 3],
                collection, 20, 12)
        for flower in range(4):
            angle = math.tau * flower / 4.0 + 0.4
            add_ico(f"ShrubFlower_{index}_{flower}",
                    (x + math.cos(angle) * 0.48, 0.88, z + math.sin(angle) * 0.48),
                    (0.07, 0.06, 0.07), m["flower_gold"] if index % 2 else m["flower_red"], collection, 2)

    for tree_index, (x, _, z, scale) in enumerate(TREE_POSITIONS):
        for root_index, angle in enumerate((0.1, 1.45, 2.8, 4.2, 5.35)):
            add_beam(f"Tree_{tree_index}_Root_{root_index}",
                     (x, 0.16, z),
                     (x + math.cos(angle) * 0.95 * scale, 0.035,
                     z + math.sin(angle) * 0.95 * scale),
                     0.10 * scale, m["wood"], collection, True)
        grid_x = max(-1, min(1, round(x / 96.0)))
        grid_z = max(-1, min(1, round(z / 96.0)))
        if (grid_x, grid_z) in {(1, -1), (-1, 1), (1, 1)} or (
            (grid_x, grid_z) == (-1, -1) and tree_index % 7 == 0
        ):
            continue
        for leaf_index in range(5):
            angle = math.tau * leaf_index / 5.0 + tree_index * 0.27
            radius = (0.52 + (leaf_index % 2) * 0.18) * scale
            add_uv_ellipsoid(
                f"Tree_{tree_index}_LeafLayer_{leaf_index}",
                (x + math.cos(angle) * radius, (5.05 + (leaf_index % 3) * 0.32) * scale,
                 z + math.sin(angle) * radius),
                (0.46 * scale, 0.38 * scale, 0.43 * scale),
                (m["leaf_dark"], m["leaf_mid"], m["leaf_fresh"], m["leaf_light"])[(tree_index + leaf_index) % 4],
                collection, 14, 8)

    # Reeds and bank stones give the river a natural boundary.
    for side in (-1, 1):
        for index in range(96):
            x = -137 + index * 2.86 + random.uniform(-0.4, 0.4)
            river_z = river_center(x)
            bank_z = river_z + side * 4.05
            for reed in range(3):
                height = random.uniform(0.48, 1.05)
                add_beam(f"RiverReed_{side}_{index}_{reed}",
                         (x + reed * 0.08, 0.02, bank_z + random.uniform(-0.25, 0.25)),
                         (x + reed * 0.08 + random.uniform(-0.06, 0.06), height,
                          bank_z + random.uniform(-0.25, 0.25)),
                         0.018, m["leaf_fresh"], collection)
            if index % 2 == 0:
                add_ico(f"RiverBankStone_{side}_{index}",
                        (x, 0.12, bank_z - side * 0.18),
                        (random.uniform(0.28, 0.58), random.uniform(0.10, 0.24), random.uniform(0.30, 0.62)),
                        m["rock_light"] if index % 4 == 0 else m["rock"], collection, 2,
                        (random.random() * 0.3, random.random() * 0.3, random.random() * math.tau))


def setup_preview() -> None:
    world = bpy.data.worlds.new("StonehavenWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.085, 0.115, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.82
    bpy.context.scene.world = world

    bpy.ops.object.light_add(type="SUN", location=(18, -24, 36))
    sun = bpy.context.object
    sun.name = "PreviewSun"
    sun.data.energy = 2.8
    sun.data.angle = math.radians(8.0)
    sun.data.color = (1.0, 0.76, 0.52)
    light_target = Vector((0, 0, 0))
    sun.rotation_euler = (light_target - sun.location).to_track_quat("-Z", "Y").to_euler()

    bpy.ops.object.light_add(type="AREA", location=(-24, -8, 22))
    fill = bpy.context.object
    fill.name = "PreviewFill"
    fill.data.energy = 1700
    fill.data.shape = "DISK"
    fill.data.size = 18
    fill.data.color = (0.48, 0.62, 0.78)
    fill.rotation_euler = (light_target - fill.location).to_track_quat("-Z", "Y").to_euler()

    bpy.ops.object.camera_add(location=(175, -175, 190))
    camera = bpy.context.object
    camera.name = "PreviewCamera"
    target = Vector((0, 0, 0))
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 48
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 1.15
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    reset_scene()

    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    materials = make_materials()
    terrain = make_collection("01_Terrain")
    architecture = make_collection("02_StonehavenArchitecture")
    props = make_collection("03_StonehavenProps")
    nature = make_collection("04_Nature")

    build_terrain(terrain, materials)
    build_bridge(props, materials, -96.0, "West")
    build_bridge(props, materials, 0.0, "Central")
    build_bridge(props, materials, 96.0, "East")
    build_gate_and_walls(architecture, materials)
    houses = [
        ("Blacksmith", (-11, 0, -13), materials["plaster_clay"], materials["roof_brown"]),
        ("WayfarerInn", (11, 0, -14), materials["plaster_oat"], materials["roof_red"]),
        ("Herbalist", (-12, 0, -26), materials["plaster_moss"], materials["roof_brown"]),
        ("Storehouse", (12, 0, -27), materials["plaster_clay"], materials["roof_red"]),
    ]
    for variant, (name, position, plaster, roof) in enumerate(houses):
        build_house(name, position, plaster, roof, architecture, materials, variant)
    build_well(props, materials)
    build_nature(nature, materials)
    build_village_detail_pass(props, materials)
    setup_preview()

    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    bpy.ops.render.render(write_still=True)
    bpy.ops.export_scene.gltf(
        filepath=str(STAGED_GLB_PATH),
        export_format="GLB",
        export_apply=True,
        export_cameras=False,
        export_lights=False,
        export_yup=True,
    )
    shutil.copy2(STAGED_GLB_PATH, GLB_PATH)
    STAGED_GLB_PATH.unlink(missing_ok=True)
    print(f"BLEND={BLEND_PATH}")
    print(f"GLB={GLB_PATH}")
    print(f"PREVIEW={PREVIEW_PATH}")


if __name__ == "__main__":
    main()
