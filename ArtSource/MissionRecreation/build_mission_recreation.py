import bpy
import math
import os
import random
from mathutils import Vector


PROJECT_ROOT = "/Users/andrelopes/Under Static"
SOURCE_DIR = os.path.join(PROJECT_ROOT, "ArtSource", "MissionRecreation")
MODEL_DIR = os.path.join(
    PROJECT_ROOT, "Assets", "UnderStatic", "Resources", "Art", "MissionRecreation", "Models"
)
TEXTURE_DIR = os.path.join(
    PROJECT_ROOT, "Assets", "UnderStatic", "Resources", "Art", "MissionRecreation", "Textures"
)
CAPTURE_DIR = os.path.join(PROJECT_ROOT, "Assets", "UnderStatic", "Captures")
BLEND_PATH = os.path.join(SOURCE_DIR, "Mission_Recreation.blend")
SCENE_NAME = "UnderStatic_Mission_Recreation"
TRIANGLE_LIMIT = 1000
TEXTURE_SIZE = 128
TILE_COUNT = 4


def ensure_directories():
    for path in (SOURCE_DIR, MODEL_DIR, TEXTURE_DIR, CAPTURE_DIR):
        os.makedirs(path, exist_ok=True)


def create_scene():
    old_scene = bpy.data.scenes.get(SCENE_NAME)
    if old_scene is not None:
        bpy.data.scenes.remove(old_scene)
    for obj in list(bpy.data.objects):
        if obj.name.startswith("MR_") or obj.name.startswith("Preview."):
            bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection.name.startswith("MR_Assets") or collection.name.startswith("MR_Preview"):
            bpy.data.collections.remove(collection)

    scene = bpy.data.scenes.new(SCENE_NAME)
    bpy.context.window.scene = scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 540
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.world = bpy.data.worlds.new(f"{SCENE_NAME}_World")
    scene.world.color = (0.025, 0.03, 0.027)
    return scene


def clamp_byte(value):
    return max(0, min(255, int(value)))


def make_texture(name, palette, seed, pattern):
    image = bpy.data.images.get(name)
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(name, width=TEXTURE_SIZE, height=TEXTURE_SIZE, alpha=True)
    rng = random.Random(seed)
    pixels = [0.0] * (TEXTURE_SIZE * TEXTURE_SIZE * 4)
    tile_size = TEXTURE_SIZE // TILE_COUNT

    for y in range(TEXTURE_SIZE):
        for x in range(TEXTURE_SIZE):
            tile_x = x // tile_size
            tile_y = y // tile_size
            tile = tile_y * TILE_COUNT + tile_x
            local_x = x % tile_size
            local_y = y % tile_size
            base = palette[tile % len(palette)]
            checker = 5 if ((local_x // 4) + (local_y // 4)) % 2 == 0 else -4
            noise = rng.randint(-9, 9)
            value = checker + noise
            if local_x < 2 or local_y < 2 or local_x >= tile_size - 2 or local_y >= tile_size - 2:
                value -= 12
            if pattern == "earth":
                if (local_x * 3 + local_y * 5 + tile) % 23 < 3:
                    value += 18
                if local_y in (8, 9, 22) and 5 < local_x < 27:
                    value -= 13
            elif pattern == "road":
                if local_x in (7, 8, 23, 24):
                    value -= 17
                if (local_x + local_y * 2) % 29 < 2:
                    value += 14
            elif pattern == "foliage":
                if (local_x + local_y + tile) % 9 < 3:
                    value += 14
                if local_x % 11 < 2:
                    value -= 9
            elif pattern == "target":
                if local_y % 7 == 0:
                    value += 13
                if (local_x * 5 + local_y * 3) % 31 == 0:
                    value -= 24
            elif pattern == "structure":
                if local_x % 8 < 2:
                    value -= 12
                if local_y in (10, 11, 24):
                    value += 11

            r = clamp_byte(base[0] + value)
            g = clamp_byte(base[1] + value)
            b = clamp_byte(base[2] + value)
            offset = (y * TEXTURE_SIZE + x) * 4
            pixels[offset:offset + 4] = (r / 255.0, g / 255.0, b / 255.0, 1.0)

    image.pixels.foreach_set(pixels)
    image.filepath_raw = os.path.join(TEXTURE_DIR, f"{name}.png")
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def make_material(name, image):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    shader.inputs["Roughness"].default_value = 0.88
    shader.inputs["Metallic"].default_value = 0.04
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def apply_transform_and_uv(obj, tile):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    uv_layer = obj.data.uv_layers.active
    tile_size = 1.0 / TILE_COUNT
    padding = 0.018
    tile_x = tile % TILE_COUNT
    tile_y = tile // TILE_COUNT
    for loop in uv_layer.data:
        loop.uv.x = tile_x * tile_size + padding + max(0.0, min(1.0, loop.uv.x)) * (tile_size - padding * 2.0)
        loop.uv.y = tile_y * tile_size + padding + max(0.0, min(1.0, loop.uv.y)) * (tile_size - padding * 2.0)
    obj.select_set(False)


def add_box(parts, name, location, dimensions, material, tile=0, bevel=0.0, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0.0:
        modifier = obj.modifiers.new("Authored edge bevel", "BEVEL")
        modifier.width = min(bevel, min(dimensions) * 0.22)
        modifier.segments = 1
        modifier.affect = "EDGES"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


def add_cylinder(parts, name, location, radius, depth, material, tile=0, vertices=8, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=depth, end_fill_type="NGON",
        location=location, rotation=rotation
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


def add_cone(parts, name, location, radius1, radius2, depth, material, tile=0, vertices=8, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices, radius1=radius1, radius2=radius2, depth=depth,
        end_fill_type="NGON", location=location, rotation=rotation
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


def add_icosphere(parts, name, location, radius, material, tile=0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=radius, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


def add_beam(parts, name, start, end, radius, material, tile=0, vertices=6):
    start = Vector(start)
    end = Vector(end)
    direction = end - start
    rotation = direction.to_track_quat("Z", "Y").to_euler()
    return add_cylinder(parts, name, (start + end) * 0.5, radius, direction.length,
                        material, tile, vertices, rotation)


def finish_asset(name, parts, collection, material):
    bpy.ops.object.select_all(action="DESELECT")
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    asset = bpy.context.object
    asset.name = name
    asset.data.name = f"{name}_Mesh"
    asset.data.materials.clear()
    asset.data.materials.append(material)
    for polygon in asset.data.polygons:
        polygon.use_smooth = False
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    asset.data.update(calc_edges=True)
    asset.data.calc_loop_triangles()
    triangles = len(asset.data.loop_triangles)
    if triangles >= TRIANGLE_LIMIT:
        raise RuntimeError(f"{name} has {triangles} triangles; budget is < {TRIANGLE_LIMIT}")
    asset["under_static_style"] = "PSX-inspired grounded low-poly"
    asset["triangle_count"] = triangles
    asset["triangle_budget"] = TRIANGLE_LIMIT
    asset["texture_resolution"] = TEXTURE_SIZE
    for old_collection in list(asset.users_collection):
        old_collection.objects.unlink(asset)
    collection.objects.link(asset)
    return asset, triangles


def export_asset(asset):
    bpy.ops.object.select_all(action="DESELECT")
    asset.hide_render = False
    asset.hide_viewport = False
    asset.select_set(True)
    bpy.context.view_layer.objects.active = asset
    path = os.path.join(MODEL_DIR, f"{asset.name}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=True,
        add_leaf_bones=False, bake_anim=False, path_mode="RELATIVE"
    )
    return path


def build_road_segment(collection, material):
    parts = []
    add_box(parts, "Road.Surface", (0, 0, 0.02), (2.25, 1.0, 0.08), material, 0, 0.018)
    add_box(parts, "Road.Shoulder.Left", (-1.28, 0, 0), (0.34, 1.0, 0.06), material, 4, 0.012)
    add_box(parts, "Road.Shoulder.Right", (1.28, 0, 0), (0.34, 1.0, 0.06), material, 4, 0.012)
    for x in (-0.52, 0.52):
        add_box(parts, "Road.Rut", (x, 0, 0.066), (0.11, 0.92, 0.018), material, 8, 0.003)
    add_box(parts, "Road.Patch", (0.18, 0.12, 0.074), (0.54, 0.36, 0.018), material, 9, 0.004,
            (0, 0, math.radians(7)))
    return finish_asset("MR_RoadSegment", parts, collection, material)


def build_pine_tree(collection, material):
    parts = []
    add_cone(parts, "Pine.Trunk", (0, 0, 1.05), 0.20, 0.11, 2.1, material, 12, 7)
    for index, (z, radius, depth) in enumerate(((1.05, 0.92, 1.35), (1.65, 0.72, 1.25), (2.18, 0.52, 1.05))):
        add_cone(parts, f"Pine.Canopy.{index}", (0, 0, z), radius, 0.07, depth, material,
                 1 + index, 8)
    for index, angle in enumerate((20, 135, 250)):
        radians = math.radians(angle)
        add_beam(parts, f"Pine.Branch.{index}", (0, 0, 1.45),
                 (math.cos(radians) * 0.62, math.sin(radians) * 0.62, 1.25),
                 0.035, material, 13, 6)
    return finish_asset("MR_PineTree", parts, collection, material)


def build_dead_tree(collection, material):
    parts = []
    add_cone(parts, "DeadTree.Trunk", (0, 0, 1.0), 0.24, 0.11, 2.0, material, 12, 7)
    branches = (
        ((0, 0, 1.1), (-0.72, 0.12, 1.62)),
        ((0, 0, 1.25), (0.64, 0.25, 1.86)),
        ((0, 0, 1.55), (-0.38, -0.58, 2.22)),
        ((0, 0, 1.72), (0.35, -0.34, 2.35)),
    )
    for index, (start, end) in enumerate(branches):
        add_beam(parts, f"DeadTree.Branch.{index}", start, end, 0.055, material, 13, 6)
    return finish_asset("MR_DeadTree", parts, collection, material)


def build_scrub(collection, material):
    parts = []
    clusters = ((-0.36, -0.12, 0.34, 0.38), (0.18, 0.05, 0.42, 0.44),
                (0.48, -0.08, 0.26, 0.3), (-0.05, 0.32, 0.28, 0.33),
                (-0.45, 0.28, 0.22, 0.27), (0.3, 0.32, 0.22, 0.29))
    for index, (x, y, z, radius) in enumerate(clusters):
        add_icosphere(parts, f"Scrub.Cluster.{index}", (x, y, z), radius, material, index % 4)
    for index, angle in enumerate((15, 95, 170, 245, 315)):
        radians = math.radians(angle)
        add_beam(parts, f"Scrub.Stem.{index}", (0, 0, 0.05),
                 (math.cos(radians) * 0.45, math.sin(radians) * 0.45, 0.48),
                 0.025, material, 12, 5)
    return finish_asset("MR_ScrubCluster", parts, collection, material)


def build_artillery(collection, material):
    parts = []
    add_box(parts, "Artillery.Carriage", (0, 0.08, 0.52), (2.25, 1.42, 0.62), material, 0, 0.12)
    add_box(parts, "Artillery.Shield", (0, 0.25, 1.18), (1.85, 0.16, 1.05), material, 1, 0.08,
            (math.radians(8), 0, 0))
    add_box(parts, "Artillery.Breech", (0, 0.18, 1.12), (0.68, 0.86, 0.54), material, 5, 0.07)
    add_beam(parts, "Artillery.Barrel", (0, 0.45, 1.42), (0, 3.55, 2.48), 0.16, material, 6, 10)
    add_cylinder(parts, "Artillery.Muzzle", (0, 3.6, 2.5), 0.23, 0.34, material, 7, 10,
                 (math.radians(71), 0, 0))
    add_beam(parts, "Artillery.Axle", (-1.35, 0, 0.5), (1.35, 0, 0.5), 0.12, material, 6, 8)
    for side in (-1, 1):
        add_cylinder(parts, f"Artillery.Wheel.{side}", (side * 1.18, 0, 0.56), 0.72, 0.28,
                     material, 8, 10, (0, math.pi / 2, 0))
        add_cylinder(parts, f"Artillery.Hub.{side}", (side * 1.33, 0, 0.56), 0.23, 0.08,
                     material, 6, 8, (0, math.pi / 2, 0))
        add_beam(parts, f"Artillery.Trail.{side}", (side * 0.44, -0.45, 0.35),
                 (side * 0.86, -2.5, 0.16), 0.16, material, 2, 6)
        add_box(parts, f"Artillery.Spade.{side}", (side * 0.87, -2.58, 0.14),
                (0.52, 0.36, 0.18), material, 2, 0.035)
    add_box(parts, "Artillery.Sight", (-0.48, 0.16, 1.62), (0.18, 0.22, 0.34), material, 10, 0.03)
    return finish_asset("MR_TowedArtillery", parts, collection, material)


def build_command_post(collection, material):
    parts = []
    add_box(parts, "Post.EarthBerm", (0, 0, 0.36), (4.2, 3.5, 0.72), material, 0, 0.16)
    add_box(parts, "Post.Shelter", (0, 0.25, 1.0), (3.1, 2.45, 1.65), material, 4, 0.10)
    add_box(parts, "Post.Roof", (0, 0.25, 1.9), (3.45, 2.75, 0.22), material, 5, 0.07)
    add_box(parts, "Post.Door", (0, -1.03, 0.95), (0.82, 0.09, 1.36), material, 9, 0.035)
    add_box(parts, "Post.Window", (-0.95, -1.08, 1.18), (0.68, 0.07, 0.42), material, 10, 0.02)
    for row in range(2):
        for column in range(6):
            x = -1.75 + column * 0.7
            y = -1.48 + row * 0.22
            z = 0.32 + row * 0.24
            add_box(parts, f"Post.Sandbag.{row}.{column}", (x, y, z),
                    (0.58, 0.32, 0.22), material, 2 + (column % 2), 0.04,
                    (0, 0, math.radians(4 if column % 2 else -3)))
    add_beam(parts, "Post.Antenna", (1.12, 0.72, 1.85), (1.12, 0.72, 4.15),
             0.045, material, 7, 6)
    for z in (2.35, 2.95, 3.55):
        add_beam(parts, "Post.AntennaBrace", (1.12, 0.72, z), (1.48, 0.72, z - 0.35),
                 0.025, material, 7, 5)
    add_box(parts, "Post.Crate", (1.45, -0.75, 0.44), (0.72, 0.62, 0.58), material, 12, 0.06)
    return finish_asset("MR_FieldCommandPost", parts, collection, material)


def add_figure(parts, prefix, origin, material, stance):
    x, y, z = origin
    crouch = 0.22 if stance == "crouch" else 0.0
    add_cone(parts, f"{prefix}.Torso", (x, y, z + 1.04 - crouch), 0.28, 0.20, 0.72,
             material, 1 + (prefix[-1:].isdigit() and int(prefix[-1]) % 3), 7)
    add_icosphere(parts, f"{prefix}.Head", (x, y, z + 1.54 - crouch), 0.18, material, 8)
    add_cylinder(parts, f"{prefix}.Helmet", (x, y, z + 1.66 - crouch), 0.21, 0.11,
                 material, 5, 8)
    leg_spread = 0.16 if stance != "crouch" else 0.22
    for side in (-1, 1):
        hip = (x + side * 0.11, y, z + 0.72 - crouch)
        foot = (x + side * leg_spread, y + (0.12 if side > 0 else -0.08), z + 0.12)
        add_beam(parts, f"{prefix}.Leg.{side}", hip, foot, 0.075, material, 3, 6)
        add_beam(parts, f"{prefix}.Arm.{side}", (x + side * 0.2, y, z + 1.28 - crouch),
                 (x + side * 0.34, y + 0.08, z + 0.92 - crouch), 0.06, material, 2, 6)


def build_infantry_group(collection, material):
    parts = []
    add_figure(parts, "Figure.0", (-1.0, 0.12, 0), material, "stand")
    add_figure(parts, "Figure.1", (0.05, -0.28, 0), material, "crouch")
    add_figure(parts, "Figure.2", (1.05, 0.22, 0), material, "stand")
    for x, y in ((-1.0, 0.12), (0.05, -0.28), (1.05, 0.22)):
        add_box(parts, "Figure.GroundShadow", (x, y, 0.025), (0.58, 0.36, 0.04), material, 12, 0.015)
    return finish_asset("MR_DistantInfantryGroup", parts, collection, material)


def build_empty_position(collection, material):
    parts = []
    add_box(parts, "EmptyPosition.DisturbedEarth", (0, 0, 0.04), (2.4, 2.4, 0.08), material, 0, 0.12,
            (0, 0, math.radians(7)))
    positions = ((-0.75, -0.55), (-0.25, -0.78), (0.3, -0.74), (0.78, -0.48),
                 (-0.75, 0.5), (0.75, 0.52))
    for index, (x, y) in enumerate(positions):
        add_box(parts, f"EmptyPosition.Sandbag.{index}", (x, y, 0.18), (0.52, 0.3, 0.22),
                material, 2 + index % 2, 0.035, (0, 0, math.radians(index * 9 - 18)))
    add_box(parts, "EmptyPosition.BrokenBoard", (0.1, 0.36, 0.18), (1.2, 0.12, 0.08),
            material, 12, 0.02, (0, 0, math.radians(-22)))
    return finish_asset("MR_EmptyPosition", parts, collection, material)


def build_preview(scene, assets, materials):
    preview = bpy.data.collections.new("MR_Preview")
    scene.collection.children.link(preview)
    bpy.ops.mesh.primitive_plane_add(size=34, location=(0, 0, -0.05))
    ground = bpy.context.object
    ground.name = "Preview.Ground"
    ground.data.materials.append(materials["terrain"])
    for old in list(ground.users_collection):
        old.objects.unlink(ground)
    preview.objects.link(ground)

    placements = {
        "MR_RoadSegment": ((0, 0, 0), (1, 12, 1)),
        "MR_PineTree": ((-5.2, 2.8, 0), (1, 1, 1)),
        "MR_DeadTree": ((-4.2, -1.6, 0), (1, 1, 1)),
        "MR_ScrubCluster": ((-2.8, 3.4, 0), (1, 1, 1)),
        "MR_TowedArtillery": ((2.8, 2.2, 0), (0.85, 0.85, 0.85)),
        "MR_FieldCommandPost": ((5.4, -2.2, 0), (0.82, 0.82, 0.82)),
        "MR_DistantInfantryGroup": ((-2.8, -3.1, 0), (0.85, 0.85, 0.85)),
        "MR_EmptyPosition": ((1.2, -4.3, 0.02), (0.9, 0.9, 0.9)),
    }
    for name, (location, scale) in placements.items():
        duplicate = assets[name].copy()
        duplicate.data = assets[name].data.copy()
        duplicate.name = f"Preview.{name}"
        duplicate.location = location
        duplicate.scale = scale
        duplicate.hide_viewport = False
        duplicate.hide_render = False
        preview.objects.link(duplicate)

    bpy.ops.object.light_add(type="AREA", location=(-4.5, -5.5, 8.5))
    key = bpy.context.object
    key.name = "Preview.Key"
    key.data.energy = 1150
    key.data.color = (1.0, 0.76, 0.52)
    key.data.shape = "DISK"
    key.data.size = 7.0
    key.rotation_euler = (math.radians(22), 0, math.radians(-32))
    bpy.ops.object.light_add(type="AREA", location=(6, 2, 5))
    fill = bpy.context.object
    fill.name = "Preview.Fill"
    fill.data.energy = 750
    fill.data.color = (0.38, 0.55, 0.72)
    fill.data.size = 6.0

    bpy.ops.object.camera_add(location=(12.8, -15.5, 10.5))
    camera = bpy.context.object
    camera.name = "Preview.Camera"
    direction = Vector((0, -0.1, 1.0)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 46
    scene.camera = camera
    scene.render.filepath = os.path.join(CAPTURE_DIR, "mission_recreation_blender_preview.png")
    bpy.ops.render.render(write_still=True)


def main():
    ensure_directories()
    scene = create_scene()
    asset_collection = bpy.data.collections.new("MR_Assets")
    scene.collection.children.link(asset_collection)

    palettes = {
        "terrain": [(78, 72, 48), (93, 82, 52), (61, 64, 43), (105, 88, 55),
                    (69, 58, 41), (112, 94, 61), (55, 61, 42), (88, 72, 49)],
        "road": [(82, 72, 53), (96, 82, 59), (67, 62, 50), (115, 95, 63),
                 (58, 55, 46), (88, 74, 55), (124, 104, 72), (72, 64, 51)],
        "vegetation": [(40, 66, 37), (50, 82, 42), (29, 51, 32), (64, 91, 45),
                       (82, 70, 43), (98, 82, 49), (51, 62, 35), (71, 94, 48)],
        "targets": [(63, 75, 64), (76, 88, 72), (46, 54, 49), (94, 87, 66),
                    (49, 55, 51), (109, 95, 68), (116, 69, 43), (38, 43, 42),
                    (27, 29, 28), (139, 126, 91), (63, 78, 70), (80, 61, 44)],
        "structures": [(78, 73, 58), (91, 83, 62), (58, 61, 49), (112, 96, 67),
                       (66, 75, 67), (48, 55, 53), (93, 75, 51), (129, 111, 76),
                       (39, 43, 42), (58, 47, 36), (44, 61, 61), (87, 69, 46)],
    }
    images = {
        key: make_texture(f"MR_{key.title()}_128", palette, 700 + index * 97, key if key != "targets" else "target")
        for index, (key, palette) in enumerate(palettes.items())
    }
    materials = {key: make_material(f"MR {key.title()}", image) for key, image in images.items()}

    builders = (
        (build_road_segment, materials["road"]),
        (build_pine_tree, materials["vegetation"]),
        (build_dead_tree, materials["vegetation"]),
        (build_scrub, materials["vegetation"]),
        (build_artillery, materials["targets"]),
        (build_command_post, materials["structures"]),
        (build_infantry_group, materials["targets"]),
        (build_empty_position, materials["terrain"]),
    )
    assets = {}
    triangle_counts = {}
    for builder, material in builders:
        asset, triangles = builder(asset_collection, material)
        assets[asset.name] = asset
        triangle_counts[asset.name] = triangles
        export_asset(asset)
        asset.hide_viewport = True
        asset.hide_render = True

    build_preview(scene, assets, materials)
    for asset in assets.values():
        asset.hide_viewport = False
        asset.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("MISSION_RECREATION_COMPLETE")
    for name, count in triangle_counts.items():
        print(f"{name}: {count} triangles")


main()
