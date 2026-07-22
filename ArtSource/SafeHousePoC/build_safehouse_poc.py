import bpy
import math
import os
import random
from mathutils import Vector


PROJECT_ROOT = "/Users/andrelopes/Under Static"
SOURCE_DIR = os.path.join(PROJECT_ROOT, "ArtSource", "SafeHousePoC")
MODEL_DIR = os.path.join(
    PROJECT_ROOT, "Assets", "UnderStatic", "Resources", "Art", "SafeHousePoC", "Models"
)
TEXTURE_DIR = os.path.join(
    PROJECT_ROOT, "Assets", "UnderStatic", "Resources", "Art", "SafeHousePoC", "Textures"
)
CAPTURE_DIR = os.path.join(PROJECT_ROOT, "Assets", "UnderStatic", "Captures")
BLEND_PATH = os.path.join(SOURCE_DIR, "SafeHouse_PoC.blend")

SCENE_NAME = "UnderStatic_SafeHouse_PoC"
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

    preview_names = {
        "PreviewFloor",
        "POC_Camera",
        "WarmWorkshopKey",
        "ColdWindowFill",
        "LowAmberBounce",
    }
    for obj in list(bpy.data.objects):
        if obj.name.startswith("SH_POC_") or obj.name.split(".")[0] in preview_names:
            bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection.name.startswith("SH_POC_Assets") or collection.name.startswith("POC_PreviewStage"):
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
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new(f"{SCENE_NAME}_World")
    scene.world.color = (0.018, 0.022, 0.025)
    return scene


def clamp_byte(value):
    return max(0, min(255, int(value)))


def make_texture(name, palette, seed):
    """Create a deterministic 4x4 material swatch atlas with chunky PSX wear."""
    image = bpy.data.images.get(name)
    if image is not None:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(name, width=TEXTURE_SIZE, height=TEXTURE_SIZE, alpha=True)

    rng = random.Random(seed)
    pixels = [0.0] * (TEXTURE_SIZE * TEXTURE_SIZE * 4)
    tile_size = TEXTURE_SIZE // TILE_COUNT
    scratch_rows = {
        tile: {rng.randint(4, tile_size - 5) for _ in range(2 + tile % 3)}
        for tile in range(TILE_COUNT * TILE_COUNT)
    }

    for y in range(TEXTURE_SIZE):
        for x in range(TEXTURE_SIZE):
            tile_x = x // tile_size
            tile_y = y // tile_size
            tile = tile_y * TILE_COUNT + tile_x
            base = palette[tile % len(palette)]
            local_x = x % tile_size
            local_y = y % tile_size
            checker = 5 if ((local_x // 4) + (local_y // 4)) % 2 == 0 else -4
            noise = rng.randint(-8, 8)
            edge = local_x < 2 or local_x >= tile_size - 2 or local_y < 2 or local_y >= tile_size - 2
            wear = 16 if edge and tile in (0, 1, 4, 5, 8, 9) else 0

            r = clamp_byte(base[0] + checker + noise + wear)
            g = clamp_byte(base[1] + checker + noise + wear)
            b = clamp_byte(base[2] + checker + noise + wear)

            if local_y in scratch_rows[tile] and 5 < local_x < tile_size - 5:
                r = clamp_byte(r + 25)
                g = clamp_byte(g + 20)
                b = clamp_byte(b + 13)
            if tile in (6, 10) and local_x in (9, 10, 11):
                r, g, b = clamp_byte(r + 38), clamp_byte(g + 30), clamp_byte(b + 8)
            if "Concrete" not in name and tile in (3, 7) and (local_x + local_y) % 13 == 0:
                r, g, b = 184, 70, 35

            offset = (y * TEXTURE_SIZE + x) * 4
            pixels[offset : offset + 4] = (r / 255.0, g / 255.0, b / 255.0, 1.0)

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
    shader.inputs["Roughness"].default_value = 0.82
    shader.inputs["Metallic"].default_value = 0.08
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
        u = max(0.0, min(1.0, loop.uv.x))
        v = max(0.0, min(1.0, loop.uv.y))
        loop.uv.x = tile_x * tile_size + padding + u * (tile_size - padding * 2.0)
        loop.uv.y = tile_y * tile_size + padding + v * (tile_size - padding * 2.0)
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


def add_cylinder(
    parts,
    name,
    location,
    radius,
    depth,
    material,
    tile=1,
    vertices=8,
    rotation=(0, 0, 0),
):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        end_fill_type="NGON",
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


def add_cone(
    parts,
    name,
    location,
    radius1,
    radius2,
    depth,
    material,
    tile=1,
    vertices=10,
    rotation=(0, 0, 0),
):
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius1,
        radius2=radius2,
        depth=depth,
        end_fill_type="NGON",
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


def add_torus(parts, name, location, major_radius, minor_radius, material, tile=1, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(
        major_segments=8,
        minor_segments=4,
        location=location,
        major_radius=major_radius,
        minor_radius=minor_radius,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material)
    apply_transform_and_uv(obj, tile)
    parts.append(obj)
    return obj


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
    asset["texture_filter"] = "Point"

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
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_triangles=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
    )
    return path


def build_radio(collection, material):
    parts = []
    add_box(parts, "Radio.Chassis", (0, 0, 0), (0.78, 0.36, 0.36), material, 0, 0.035)
    add_box(parts, "Radio.FrontPanel", (0, -0.192, 0), (0.70, 0.028, 0.29), material, 1, 0.012)
    add_cylinder(parts, "Radio.Speaker", (-0.18, -0.215, 0.025), 0.115, 0.026, material, 2, 12, (math.pi / 2, 0, 0))
    for index in range(5):
        add_box(parts, f"Radio.Grille.{index}", (-0.18, -0.234, -0.055 + index * 0.04), (0.18, 0.012, 0.014), material, 1, 0.003)
    add_box(parts, "Radio.Display", (0.145, -0.221, 0.062), (0.22, 0.024, 0.085), material, 6, 0.008)
    add_box(parts, "Radio.Label", (0.145, -0.235, -0.045), (0.21, 0.009, 0.042), material, 10, 0.004)
    for index, x in enumerate((0.095, 0.21, 0.31)):
        add_cylinder(parts, f"Radio.Knob.{index}", (x, -0.238, -0.115), 0.036 if index < 2 else 0.025, 0.035, material, 5, 8, (math.pi / 2, 0, 0))
    add_box(parts, "Radio.HandleTop", (0, 0, 0.262), (0.54, 0.045, 0.045), material, 4, 0.01)
    add_box(parts, "Radio.HandleLeft", (-0.255, 0, 0.19), (0.045, 0.045, 0.16), material, 4, 0.01, (0, math.radians(-10), 0))
    add_box(parts, "Radio.HandleRight", (0.255, 0, 0.19), (0.045, 0.045, 0.16), material, 4, 0.01, (0, math.radians(10), 0))
    add_cylinder(parts, "Radio.Antenna", (-0.31, 0.05, 0.52), 0.009, 0.65, material, 9, 8, (0, math.radians(-7), 0))
    add_cylinder(parts, "Radio.AntennaBase", (-0.31, 0.025, 0.205), 0.025, 0.07, material, 5, 8)
    add_cylinder(parts, "Radio.Indicator", (0.315, -0.242, 0.075), 0.013, 0.022, material, 7, 8, (math.pi / 2, 0, 0))
    return finish_asset("SH_POC_FieldRadio", parts, collection, material)


def build_generator(collection, material):
    parts = []
    add_box(parts, "Generator.Body", (0, 0, 0), (0.61, 0.51, 0.64), material, 0, 0.045)
    add_box(parts, "Generator.Control", (0, -0.272, 0.085), (0.52, 0.035, 0.29), material, 1, 0.012)
    add_box(parts, "Generator.Display", (-0.11, -0.294, 0.145), (0.17, 0.012, 0.075), material, 6, 0.005)
    for index, x in enumerate((0.07, 0.16, 0.25)):
        add_cylinder(parts, f"Generator.Socket.{index}", (x, -0.298, 0.09), 0.042, 0.025, material, 5, 8, (math.pi / 2, 0, 0))
    add_box(parts, "Generator.Warning", (-0.09, -0.299, -0.025), (0.19, 0.01, 0.055), material, 10, 0.004)
    for index in range(5):
        add_box(parts, f"Generator.Vent.{index}", (0.318, -0.15 + index * 0.075, 0.02), (0.018, 0.052, 0.34), material, 5, 0.004)
    for x in (-0.37, 0.37):
        add_box(parts, "Generator.FramePost", (x, 0, 0), (0.045, 0.61, 0.82), material, 4, 0.012)
    add_box(parts, "Generator.FrameTop", (0, 0, 0.39), (0.78, 0.61, 0.045), material, 4, 0.012)
    add_box(parts, "Generator.FrameBottom", (0, 0, -0.39), (0.78, 0.61, 0.045), material, 4, 0.012)
    add_cylinder(parts, "Generator.Exhaust", (0.19, 0.16, 0.43), 0.055, 0.30, material, 9, 8)
    add_cylinder(parts, "Generator.ExhaustCap", (0.19, 0.16, 0.59), 0.069, 0.035, material, 5, 8)
    for x in (-0.25, 0.25):
        add_cylinder(parts, "Generator.Foot", (x, -0.16, -0.44), 0.065, 0.045, material, 5, 8, (math.pi / 2, 0, 0))
    return finish_asset("SH_POC_PortableGenerator", parts, collection, material)


def build_breaker(collection, material):
    parts = []
    add_box(parts, "Breaker.Cabinet", (0, 0, 0), (0.58, 0.13, 0.78), material, 0, 0.035)
    add_box(parts, "Breaker.Door", (0, -0.078, 0), (0.50, 0.035, 0.67), material, 1, 0.018)
    add_box(parts, "Breaker.Label", (0, -0.102, 0.22), (0.28, 0.012, 0.085), material, 10, 0.005)
    add_box(parts, "Breaker.SwitchPlate", (0, -0.107, -0.07), (0.16, 0.014, 0.31), material, 5, 0.008)
    add_box(parts, "Breaker.Handle", (0, -0.145, -0.04), (0.055, 0.055, 0.24), material, 4, 0.01, (0, 0, math.radians(-18)))
    add_cylinder(parts, "Breaker.RedLamp", (0.16, -0.125, 0.09), 0.025, 0.028, material, 7, 8, (math.pi / 2, 0, 0))
    add_cylinder(parts, "Breaker.AmberLamp", (0.16, -0.125, 0.015), 0.025, 0.028, material, 6, 8, (math.pi / 2, 0, 0))
    for x in (-0.16, 0, 0.16):
        add_cylinder(parts, "Breaker.Conduit", (x, 0, 0.49), 0.025, 0.30, material, 9, 8)
        add_torus(parts, "Breaker.ConduitCollar", (x, 0, 0.365), 0.032, 0.008, material, 5)
    return finish_asset("SH_POC_BreakerPanel", parts, collection, material)


def build_crate(collection, material):
    parts = []
    add_box(parts, "Crate.Body", (0, 0, -0.005), (0.40, 0.54, 0.22), material, 0, 0.018)
    add_box(parts, "Crate.Lid", (0, 0, 0.125), (0.43, 0.57, 0.055), material, 1, 0.012)
    for x in (-0.19, 0.19):
        add_box(parts, "Crate.EdgeX", (x, 0, 0), (0.035, 0.55, 0.25), material, 4, 0.006)
    for y in (-0.26, 0.26):
        add_box(parts, "Crate.EdgeY", (0, y, 0), (0.41, 0.035, 0.25), material, 4, 0.006)
    add_box(parts, "Crate.Label", (0, -0.286, 0.015), (0.22, 0.012, 0.09), material, 10, 0.004)
    add_box(parts, "Crate.StrapA", (-0.10, 0, 0.158), (0.045, 0.575, 0.015), material, 5, 0.003)
    add_box(parts, "Crate.StrapB", (0.10, 0, 0.158), (0.045, 0.575, 0.015), material, 5, 0.003)
    for x in (-0.145, 0.145):
        add_box(parts, "Crate.Stencil", (x, -0.294, -0.055), (0.055, 0.009, 0.035), material, 6, 0.003)
    return finish_asset("SH_POC_RuggedCrate", parts, collection, material)


def build_floor_slab(collection, material):
    parts = []
    add_box(parts, "Floor.Slab", (0, 0, 0), (6.6, 6.4, 0.12), material, 0, 0.025)
    patches = (
        ((-1.8, 1.55, 0.067), (1.15, 0.82, 0.018), math.radians(4)),
        ((1.65, -1.25, 0.067), (0.92, 1.22, 0.018), math.radians(-7)),
        ((2.35, 1.85, 0.067), (0.65, 0.72, 0.018), math.radians(3)),
    )
    for index, (location, dimensions, angle) in enumerate(patches):
        add_box(parts, f"Floor.Patch.{index}", location, dimensions, material, 4 + index, 0.008, (0, 0, angle))
    cracks = (
        ((-0.8, -1.8, 0.08), (0.75, 0.025, 0.012), -18),
        ((-0.47, -1.67, 0.08), (0.42, 0.022, 0.012), 24),
        ((1.1, 1.35, 0.08), (0.62, 0.025, 0.012), 12),
        ((1.38, 1.48, 0.08), (0.35, 0.022, 0.012), -27),
        ((-2.35, 0.25, 0.08), (0.5, 0.024, 0.012), 31),
    )
    for index, (location, dimensions, angle) in enumerate(cracks):
        add_box(parts, f"Floor.Crack.{index}", location, dimensions, material, 12, 0.002, (0, 0, math.radians(angle)))
    for index in range(5):
        add_box(parts, f"Floor.Drain.{index}", (2.45 + index * 0.07, -2.25, 0.086), (0.035, 0.55, 0.018), material, 13, 0.002)
    return finish_asset("SH_POC_FloorSlab", parts, collection, material)


def build_workbench(collection, material):
    parts = []
    add_box(parts, "Workbench.Top", (0, 0, 0.39), (3.4, 1.2, 0.13), material, 0, 0.035)
    add_box(parts, "Workbench.FrontEdge", (0, -0.61, 0.33), (3.34, 0.07, 0.18), material, 1, 0.018)
    for side in (-1, 1):
        x = side * 1.16
        add_box(parts, f"Workbench.Cabinet.{side}", (x, 0.08, -0.05), (0.78, 0.98, 0.72), material, 2, 0.035)
        for drawer in range(3):
            z = 0.19 - drawer * 0.2
            add_box(parts, f"Workbench.Drawer.{side}.{drawer}", (x, -0.425, z), (0.68, 0.035, 0.16), material, 4 + drawer, 0.01)
            add_box(parts, f"Workbench.Pull.{side}.{drawer}", (x, -0.456, z), (0.22, 0.025, 0.035), material, 9, 0.006)
    add_box(parts, "Workbench.KneeBack", (0, 0.45, -0.08), (1.38, 0.08, 0.55), material, 3, 0.015)
    add_box(parts, "Workbench.LowerBrace", (0, 0.36, -0.38), (1.5, 0.12, 0.10), material, 8, 0.015)
    add_box(parts, "Workbench.Mat", (0.15, -0.02, 0.468), (1.45, 0.78, 0.025), material, 12, 0.006)
    return finish_asset("SH_POC_Workbench", parts, collection, material)


def build_battery_charger(collection, material):
    """Five-position cabled charger; only plug pair one is populated initially."""
    parts = []
    add_box(parts, "Charger.Housing", (0, 0, 0.22), (1.04, 0.68, 0.42), material, 4, 0.045)
    add_box(parts, "Charger.TopDeck", (0, 0.015, 0.455), (0.96, 0.59, 0.075), material, 0, 0.025)
    add_box(parts, "Charger.FrontPanel", (0, -0.352, 0.205), (0.98, 0.035, 0.33), material, 1, 0.018)
    add_box(parts, "Charger.WarningLabel", (-0.16, 0.02, 0.505), (0.56, 0.32, 0.018), material, 10)

    # Rugged folding carry handle. Batteries remain on the bench beside the charger;
    # the top surface is deliberately only a label and handle, never a battery rack.
    for x in (-0.39, 0.39):
        add_box(parts, f"Charger.HandleRail.{x}", (x, 0.02, 0.565), (0.055, 0.49, 0.055), material, 8)
    add_box(parts, "Charger.HandleBridge", (0, 0.235, 0.565), (0.82, 0.055, 0.055), material, 8)

    # Five front-panel plug positions communicate the upgrade path. Plug one exposes a
    # power connector plus balance connector; positions two through five remain physically
    # blanked until their future workshop upgrades are installed.
    channel_x = (-0.39, -0.195, 0.0, 0.195, 0.39)
    for index, x in enumerate(channel_x, start=1):
        tile = 6 if index == 1 else 5
        add_box(parts, f"Charger.ChannelFace.{index}", (x, -0.379, 0.115), (.15, .025, .13), material, tile)
        add_cylinder(parts, f"Charger.ChannelLamp.{index}", (x, -.397, .175), .018, .018,
                     material, 7 if index == 1 else 14, 6, (math.pi / 2, 0, 0))
        if index == 1:
            add_box(parts, "Charger.ActivePowerPlug", (x, -.404, .085), (.09, .03, .052), material, 10, .008)
            add_box(parts, "Charger.ActivePowerPlugKey", (x + .027, -.422, .101), (.022, .016, .018), material, 7)
            add_box(parts, "Charger.ActiveBalancePlug", (x, -.404, .135), (.075, .027, .026), material, 5, .005)
        else:
            add_box(parts, f"Charger.LockedBlank.{index}", (x, -.402, .11), (.11, .018, .075), material, 14, .006)
            for bolt_x in (-.038, .038):
                add_cylinder(parts, f"Charger.LockedBlankBolt.{index}.{bolt_x}",
                             (x + bolt_x, -.414, .11), .008, .012, material, 5, 6,
                             (math.pi / 2, 0, 0))

    add_box(parts, "Charger.Lcd", (-.18, -.402, .285), (.36, .025, .09), material, 6)
    for index, x in enumerate((.08, .18, .28, .38)):
        add_cylinder(parts, f"Charger.Button.{index}", (x, -.405, .285), .026, .025,
                     material, 5, 6, (math.pi / 2, 0, 0))
    for index in range(5):
        add_box(parts, f"Charger.SideVent.{index}", (.527, -.2 + index * .1, .25),
                (.018, .055, .18), material, 5)
    add_box(parts, "Charger.PowerLead", (.45, .25, .2), (.22, .035, .035), material, 8, 0,
            (0, 0, math.radians(-18)))
    return finish_asset("SH_POC_BatteryCharger", parts, collection, material)


def build_radio_desk(collection, material):
    parts = []
    add_box(parts, "Desk.Top", (0, 0, 0.29), (1.45, 0.68, 0.12), material, 0, 0.03)
    for x in (-0.62, 0.62):
        for y in (-0.25, 0.25):
            add_box(parts, "Desk.Leg", (x, y, -0.05), (0.09, 0.09, 0.66), material, 8, 0.015)
    add_box(parts, "Desk.BackBrace", (0, 0.27, -0.05), (1.2, 0.07, 0.11), material, 8, 0.012)
    add_box(parts, "Desk.Drawer", (0.35, -0.30, 0.17), (0.52, 0.06, 0.17), material, 3, 0.012)
    add_box(parts, "Desk.Pull", (0.35, -0.342, 0.17), (0.17, 0.025, 0.035), material, 9, 0.005)
    return finish_asset("SH_POC_RadioDesk", parts, collection, material)


def build_window(collection, material):
    parts = []
    add_box(parts, "Window.Glass", (0, 0.015, 0), (1.35, 0.045, 0.92), material, 6, 0.012)
    for x in (-0.72, 0.72):
        add_box(parts, "Window.FrameVertical", (x, 0, 0), (0.10, 0.10, 1.12), material, 8, 0.018)
    for z in (-0.51, 0.51):
        add_box(parts, "Window.FrameHorizontal", (0, 0, z), (1.52, 0.10, 0.10), material, 8, 0.018)
    board_specs = (
        ((0.02, -0.08, -0.28), (1.53, 0.12, 0.16), -5),
        ((-0.03, -0.09, -0.02), (1.48, 0.12, 0.16), 2),
        ((0.04, -0.08, 0.24), (1.55, 0.12, 0.16), 7),
        ((-0.12, -0.075, 0.42), (1.15, 0.10, 0.13), -4),
    )
    for index, (location, dimensions, angle) in enumerate(board_specs):
        add_box(parts, f"Window.Board.{index}", location, dimensions, material, index, 0.012, (0, math.radians(angle), math.radians(angle)))
        for side in (-1, 1):
            add_cylinder(parts, f"Window.Nail.{index}.{side}", (side * 0.58, -0.155, location[2]), 0.018, 0.025, material, 9, 8, (math.pi / 2, 0, 0))
    return finish_asset("SH_POC_BoardedWindow", parts, collection, material)


def build_concealed_door(collection, material):
    parts = []
    add_box(parts, "Door.Core", (0, 0, 0), (1.35, 0.12, 2.24), material, 0, 0.025)
    for index in range(5):
        x = -0.52 + index * 0.26
        add_box(parts, f"Door.Plank.{index}", (x, -0.072, 0), (0.235, 0.045, 2.15), material, index % 4, 0.012)
    add_box(parts, "Door.BraceA", (0, -0.115, 0), (1.24, 0.075, 0.12), material, 5, 0.012, (0, math.radians(34), 0))
    add_box(parts, "Door.BraceB", (0, -0.13, 0), (1.24, 0.075, 0.12), material, 5, 0.012, (0, math.radians(-34), 0))
    for z in (-0.66, 0.64):
        add_box(parts, "Door.Hinge", (-0.58, -0.135, z), (0.18, 0.035, 0.12), material, 9, 0.008)
        add_cylinder(parts, "Door.HingePin", (-0.68, -0.15, z), 0.022, 0.16, material, 9, 8)
    add_box(parts, "Door.LatchPlate", (0.48, -0.14, 0.05), (0.13, 0.035, 0.25), material, 9, 0.008)
    add_box(parts, "Door.Latch", (0.48, -0.18, 0.05), (0.055, 0.065, 0.16), material, 10, 0.008, (0, 0, math.radians(-12)))
    return finish_asset("SH_POC_ConcealedDoor", parts, collection, material)


def build_tactical_map(collection, material):
    parts = []
    add_box(parts, "Map.Back", (0, 0.02, 0), (1.9, 0.10, 1.45), material, 8, 0.025)
    add_box(parts, "Map.Sheet", (0, -0.048, 0), (1.68, 0.025, 1.25), material, 0, 0.01)
    add_box(parts, "Map.Shelf", (0, -0.27, -0.70), (1.95, 0.55, 0.08), material, 9, 0.018)
    route_specs = ((-0.15, -18, 1.20), (0.05, 2, 1.05), (0.23, 19, 0.92))
    for index, (z, angle, length) in enumerate(route_specs):
        add_box(parts, f"Map.Route.{index}", (0, -0.075, z), (length, 0.018, 0.035), material, 10 + index, 0.004, (0, 0, math.radians(angle)))
    for index, (x, z) in enumerate(((-0.52, 0.18), (-0.15, -0.22), (0.28, 0.28), (0.55, -0.05))):
        add_cylinder(parts, f"Map.Pin.{index}", (x, -0.09, z), 0.024, 0.025, material, 6 + index % 2, 8, (math.pi / 2, 0, 0))
    return finish_asset("SH_POC_TacticalMap", parts, collection, material)


def build_ready_shelf(collection, material):
    parts = []
    for x in (-0.23, 0.23):
        for y in (-0.69, 0.69):
            add_box(parts, "Shelf.Post", (x, y, 0), (0.075, 0.075, 2.25), material, 8, 0.012)
    for index, z in enumerate((-0.75, -0.25, 0.25, 0.75)):
        add_box(parts, f"Shelf.Deck.{index}", (0, 0, z), (0.55, 1.50, 0.08), material, index, 0.016)
        add_box(parts, f"Shelf.Lip.{index}", (-0.28, 0, z + 0.06), (0.055, 1.46, 0.08), material, 9, 0.009)
    add_box(parts, "Shelf.BackBraceA", (0.25, 0, 0), (0.045, 1.78, 0.08), material, 8, 0.008, (math.radians(28), 0, 0))
    add_box(parts, "Shelf.BackBraceB", (0.25, 0, 0), (0.045, 1.78, 0.08), material, 8, 0.008, (math.radians(-28), 0, 0))
    return finish_asset("SH_POC_ReadyShelf", parts, collection, material)


def build_parts_rack(collection, material):
    parts = []
    add_box(parts, "Rack.Back", (0.18, 0, 0), (0.10, 1.55, 1.90), material, 8, 0.018)
    for y in (-0.75, 0.75):
        add_box(parts, "Rack.Side", (0, y, 0), (0.46, 0.06, 1.90), material, 8, 0.012)
    for z in (-0.92, 0.92):
        add_box(parts, "Rack.Cap", (0, 0, z), (0.48, 1.55, 0.07), material, 8, 0.012)
    for row in range(3):
        for column in range(2):
            y = -0.38 + column * 0.76
            z = -0.55 + row * 0.52
            add_box(parts, "Rack.Bin", (-0.04, y, z), (0.40, 0.62, 0.32), material, row + column, 0.012)
            add_box(parts, "Rack.BinLip", (-0.255, y, z - 0.02), (0.04, 0.58, 0.22), material, 4 + row)
            add_box(parts, "Rack.BinLabel", (-0.282, y, z), (0.018, 0.24, 0.11), material, 10)
    return finish_asset("SH_POC_PartsRack", parts, collection, material)


def build_field_cot(collection, material):
    parts = []
    for y in (-0.29, 0.29):
        add_box(parts, "Cot.SideRail", (0, y, 0), (1.60, 0.06, 0.08), material, 8, 0.012)
    for x in (-0.76, 0.76):
        add_box(parts, "Cot.EndRail", (x, 0, 0), (0.06, 0.68, 0.08), material, 8, 0.012)
        for y in (-0.27, 0.27):
            add_box(parts, "Cot.Leg", (x, y, -0.22), (0.055, 0.055, 0.45), material, 9, 0.01, (0, math.radians(5 * (-1 if x < 0 else 1)), 0))
    add_box(parts, "Cot.Canvas", (0, 0, 0.065), (1.48, 0.58, 0.07), material, 0, 0.025)
    add_box(parts, "Cot.BlanketBase", (0.12, 0, 0.135), (1.1, 0.55, 0.08), material, 3, 0.025)
    for index in range(3):
        add_box(parts, f"Cot.BlanketFold.{index}", (-0.48 + index * 0.10, 0, 0.20 + index * 0.035), (0.26, 0.52, 0.065), material, 4 + index, 0.022)
    return finish_asset("SH_POC_FieldCot", parts, collection, material)


def build_mug(collection, material):
    parts = []
    add_cylinder(parts, "Mug.Body", (0, 0, 0), 0.06, 0.15, material, 0, 12)
    add_cylinder(parts, "Mug.Rim", (0, 0, 0.078), 0.068, 0.018, material, 8, 12)
    add_cylinder(parts, "Mug.Coffee", (0, 0, 0.089), 0.052, 0.008, material, 12, 12)
    add_torus(parts, "Mug.Handle", (0.07, 0, 0), 0.048, 0.010, material, 8, (math.pi / 2, 0, 0))
    return finish_asset("SH_POC_EnamelMug", parts, collection, material)


def build_ceiling_beam(collection, material):
    parts = []
    add_box(parts, "Beam.Core", (0, 0, 0), (0.11, 6.15, 0.16), material, 0, 0.02)
    add_box(parts, "Beam.PatchA", (-0.018, -1.4, -0.088), (0.07, 0.55, 0.025), material, 4, 0.004, (0, 0, math.radians(3)))
    add_box(parts, "Beam.PatchB", (0.022, 1.65, -0.088), (0.06, 0.70, 0.025), material, 5, 0.004, (0, 0, math.radians(-2)))
    return finish_asset("SH_POC_CeilingBeam", parts, collection, material)


def build_pipe_bundle(collection, material):
    parts = []
    for index, y in enumerate((-0.18, 0, 0.18)):
        add_cylinder(parts, f"Pipe.{index}", (0, y, 0), 0.036, 2.50, material, index, 10)
        for z in (-0.86, 0.86):
            add_torus(parts, f"Pipe.Collar.{index}.{z}", (0, y, z), 0.043, 0.008, material, 8)
    for z in (-0.92, 0.92):
        add_box(parts, "Pipe.WallBracket", (0.055, 0, z), (0.08, 0.48, 0.075), material, 9, 0.01)
        add_box(parts, "Pipe.BracketFoot", (0.095, 0, z), (0.035, 0.58, 0.15), material, 8, 0.008)
    return finish_asset("SH_POC_UtilityPipes", parts, collection, material)


def build_caged_lamp(collection, material):
    parts = []
    add_cone(parts, "Lamp.Shade", (0, 0, 0.08), 0.24, 0.10, 0.16, material, 0, 12)
    add_cylinder(parts, "Lamp.Socket", (0, 0, 0.19), 0.07, 0.14, material, 8, 10)
    add_torus(parts, "Lamp.CageTop", (0, 0, 0.035), 0.13, 0.012, material, 9)
    add_torus(parts, "Lamp.CageBottom", (0, 0, -0.14), 0.10, 0.012, material, 9)
    for index in range(6):
        angle = index / 6 * math.pi * 2
        x = math.cos(angle) * 0.115
        y = math.sin(angle) * 0.115
        add_cylinder(parts, f"Lamp.CageBar.{index}", (x, y, -0.05), 0.008, 0.19, material, 9, 6)
    add_cylinder(parts, "Lamp.Cord", (0, 0, 0.43), 0.014, 0.40, material, 12, 8)
    return finish_asset("SH_POC_CagedLamp", parts, collection, material)


def add_preview_stage(scene, assets):
    preview_collection = bpy.data.collections.new("POC_PreviewStage")
    scene.collection.children.link(preview_collection)

    preview_positions = {
        "SH_POC_FloorSlab": (0, 0, -0.52),
        "SH_POC_Workbench": (0, -0.25, 0),
        "SH_POC_BatteryCharger": (1.05, -0.22, 0.52),
        "SH_POC_RadioDesk": (-2.55, -0.45, 0),
        "SH_POC_FieldRadio": (-2.55, -0.55, 0.58),
        "SH_POC_PortableGenerator": (2.55, -1.65, 0),
        "SH_POC_BreakerPanel": (1.40, 2.85, 0.65),
        "SH_POC_RuggedCrate": (2.45, 0.80, 0.05),
        "SH_POC_BoardedWindow": (-1.45, 3.0, 1.55),
        "SH_POC_ConcealedDoor": (0.15, 3.02, 1.05),
        "SH_POC_TacticalMap": (-2.80, 1.70, 1.35),
        "SH_POC_ReadyShelf": (2.75, 1.15, 0.64),
        "SH_POC_PartsRack": (2.85, -0.20, 0.45),
        "SH_POC_FieldCot": (-2.10, -2.10, -0.18),
        "SH_POC_EnamelMug": (-2.35, -0.55, 0.49),
        "SH_POC_CeilingBeam": (-2.4, 0, 2.80),
        "SH_POC_UtilityPipes": (3.05, 2.10, 0.75),
        "SH_POC_CagedLamp": (0, -0.25, 1.55),
    }
    for asset in assets:
        asset.location = preview_positions[asset.name]

    bpy.ops.object.camera_add(location=(8.8, -11.5, 6.8))
    camera = bpy.context.object
    camera.name = "POC_Camera"
    scene.camera = camera
    direction = Vector((0, 0.25, 0.75)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 54

    bpy.ops.object.light_add(type="AREA", location=(-2.2, -2.4, 4.2))
    key = bpy.context.object
    key.name = "WarmWorkshopKey"
    key.data.energy = 1100
    key.data.shape = "DISK"
    key.data.size = 3.0
    key.data.color = (1.0, 0.55, 0.31)
    key.rotation_euler = (math.radians(24), 0, math.radians(-22))

    bpy.ops.object.light_add(type="AREA", location=(2.5, -0.5, 2.7))
    fill = bpy.context.object
    fill.name = "ColdWindowFill"
    fill.data.energy = 900
    fill.data.size = 2.5
    fill.data.color = (0.32, 0.52, 1.0)
    direction = Vector((0.4, 0, 0.1)) - fill.location
    fill.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    bpy.ops.object.light_add(type="POINT", location=(0, -1.2, 1.1))
    rim = bpy.context.object
    rim.name = "LowAmberBounce"
    rim.data.energy = 230
    rim.data.color = (1.0, 0.32, 0.12)


def main():
    ensure_directories()
    scene = create_scene()
    asset_collection = bpy.data.collections.new("SH_POC_Assets")
    scene.collection.children.link(asset_collection)

    palettes = {
        "Radio": [
            (63, 74, 59), (39, 48, 43), (77, 82, 72), (104, 55, 36),
            (30, 33, 31), (93, 96, 88), (94, 126, 116), (175, 48, 34),
            (54, 61, 51), (124, 117, 94), (179, 166, 117), (82, 77, 60),
            (43, 49, 43), (114, 93, 62), (26, 30, 29), (80, 88, 75),
        ],
        "Generator": [
            (44, 62, 53), (31, 43, 39), (61, 70, 63), (113, 60, 35),
            (28, 31, 30), (101, 102, 94), (91, 119, 111), (169, 54, 30),
            (49, 64, 54), (91, 84, 64), (188, 152, 49), (83, 75, 55),
            (38, 45, 40), (108, 88, 59), (22, 27, 26), (72, 87, 74),
        ],
        "Breaker": [
            (48, 67, 63), (34, 48, 47), (71, 80, 75), (104, 53, 36),
            (28, 32, 31), (108, 106, 94), (184, 143, 46), (173, 46, 31),
            (55, 69, 64), (105, 100, 77), (183, 174, 133), (86, 79, 62),
            (39, 48, 46), (117, 90, 57), (24, 29, 29), (76, 91, 83),
        ],
        "Crate": [
            (91, 69, 42), (70, 52, 34), (116, 88, 52), (108, 55, 31),
            (39, 40, 35), (103, 97, 80), (177, 145, 60), (147, 54, 31),
            (82, 63, 40), (116, 98, 61), (191, 175, 127), (92, 74, 47),
            (51, 48, 38), (126, 88, 49), (31, 33, 30), (112, 91, 57),
        ],
        "Concrete": [
            (62, 66, 63), (55, 61, 59), (71, 72, 67), (76, 68, 58),
            (48, 55, 54), (82, 80, 72), (66, 75, 72), (90, 61, 43),
            (57, 64, 62), (78, 74, 64), (94, 91, 80), (68, 65, 57),
            (44, 50, 49), (83, 67, 49), (36, 42, 42), (73, 78, 72),
        ],
        "Timber": [
            (91, 60, 34), (74, 47, 29), (108, 72, 39), (121, 61, 31),
            (50, 40, 31), (112, 92, 66), (139, 104, 57), (154, 59, 31),
            (84, 56, 34), (122, 88, 51), (169, 133, 78), (98, 71, 43),
            (57, 45, 34), (133, 82, 43), (39, 36, 31), (112, 82, 48),
        ],
        "Architecture": [
            (92, 62, 36), (72, 48, 31), (111, 75, 42), (118, 56, 31),
            (53, 45, 36), (98, 100, 91), (84, 127, 144), (161, 55, 31),
            (44, 50, 48), (120, 112, 89), (178, 160, 111), (89, 78, 57),
            (36, 42, 41), (126, 82, 47), (26, 31, 31), (91, 94, 82),
        ],
        "Furniture": [
            (93, 61, 34), (70, 46, 30), (112, 76, 43), (113, 57, 33),
            (41, 44, 40), (94, 97, 89), (91, 111, 100), (168, 54, 31),
            (46, 53, 49), (111, 83, 51), (170, 142, 89), (90, 69, 46),
            (37, 41, 39), (128, 78, 43), (26, 31, 30), (93, 90, 70),
        ],
        "Map": [
            (91, 100, 65), (77, 84, 57), (108, 112, 72), (117, 67, 39),
            (42, 47, 43), (100, 101, 88), (105, 128, 91), (169, 50, 31),
            (45, 55, 51), (124, 108, 67), (188, 153, 54), (105, 91, 57),
            (39, 48, 44), (137, 87, 43), (28, 34, 33), (89, 105, 72),
        ],
        "Storage": [
            (50, 67, 61), (38, 50, 47), (68, 79, 70), (104, 56, 34),
            (33, 36, 34), (100, 99, 88), (148, 119, 56), (164, 53, 30),
            (88, 65, 39), (114, 92, 55), (178, 158, 104), (91, 72, 47),
            (42, 48, 44), (126, 82, 43), (27, 31, 30), (82, 91, 72),
        ],
        "Living": [
            (60, 75, 55), (47, 60, 46), (75, 87, 63), (108, 57, 34),
            (41, 43, 38), (99, 99, 88), (112, 126, 95), (167, 51, 31),
            (87, 64, 39), (118, 93, 55), (174, 151, 100), (96, 74, 47),
            (36, 42, 39), (127, 82, 45), (25, 29, 28), (90, 98, 75),
        ],
        "Utility": [
            (53, 67, 62), (39, 48, 46), (74, 82, 75), (112, 57, 34),
            (31, 35, 34), (111, 108, 96), (171, 126, 52), (176, 50, 30),
            (49, 59, 54), (105, 92, 66), (189, 164, 101), (88, 78, 59),
            (37, 44, 42), (126, 82, 48), (24, 29, 28), (79, 91, 82),
        ],
    }

    materials = {}
    for index, (name, palette) in enumerate(palettes.items()):
        image = make_texture(f"SH_POC_{name}_128", palette, 100 + index * 71)
        materials[name] = make_material(f"SH_POC_{name}_Material", image)

    assets_and_counts = [
        build_radio(asset_collection, materials["Radio"]),
        build_generator(asset_collection, materials["Generator"]),
        build_breaker(asset_collection, materials["Breaker"]),
        build_crate(asset_collection, materials["Crate"]),
        build_floor_slab(asset_collection, materials["Concrete"]),
        build_workbench(asset_collection, materials["Furniture"]),
        build_battery_charger(asset_collection, materials["Utility"]),
        build_radio_desk(asset_collection, materials["Furniture"]),
        build_window(asset_collection, materials["Architecture"]),
        build_concealed_door(asset_collection, materials["Architecture"]),
        build_tactical_map(asset_collection, materials["Map"]),
        build_ready_shelf(asset_collection, materials["Storage"]),
        build_parts_rack(asset_collection, materials["Storage"]),
        build_field_cot(asset_collection, materials["Living"]),
        build_mug(asset_collection, materials["Living"]),
        build_ceiling_beam(asset_collection, materials["Timber"]),
        build_pipe_bundle(asset_collection, materials["Utility"]),
        build_caged_lamp(asset_collection, materials["Utility"]),
    ]
    assets = [item[0] for item in assets_and_counts]
    exports = [export_asset(asset) for asset in assets]
    add_preview_stage(scene, assets)

    scene.render.filepath = os.path.join(CAPTURE_DIR, "safehouse_environment_blender_preview.png")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

    return {
        "blend_file": BLEND_PATH,
        "exports": exports,
        "textures": [os.path.join(TEXTURE_DIR, f"SH_POC_{name}_128.png") for name in palettes],
        "triangles": {asset.name: count for asset, count in assets_and_counts},
        "preview": scene.render.filepath,
    }


result = main()
