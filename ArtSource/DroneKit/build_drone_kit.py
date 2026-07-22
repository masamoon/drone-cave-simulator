import bpy
import math
import os
import random
from mathutils import Vector


PROJECT_ROOT = "/Users/andrelopes/Under Static"
SOURCE_DIR = os.path.join(PROJECT_ROOT, "ArtSource", "DroneKit")
MODEL_DIR = os.path.join(PROJECT_ROOT, "Assets", "UnderStatic", "Resources", "Art", "DroneKit", "Models")
TEXTURE_DIR = os.path.join(PROJECT_ROOT, "Assets", "UnderStatic", "Resources", "Art", "DroneKit", "Textures")
CAPTURE_DIR = os.path.join(PROJECT_ROOT, "Assets", "UnderStatic", "Captures")
BLEND_PATH = os.path.join(SOURCE_DIR, "Drone_Kit.blend")
PREVIEW_PATH = os.path.join(CAPTURE_DIR, "drone_kit_blender_preview.png")
SCENE_NAME = "UnderStatic_Drone_Kit"
TRIANGLE_LIMIT = 1000
TEXTURE_SIZE = 128
TILE_COUNT = 4


def ensure_directories():
    for path in (SOURCE_DIR, MODEL_DIR, TEXTURE_DIR, CAPTURE_DIR):
        os.makedirs(path, exist_ok=True)


def reset_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)
    scene = bpy.context.scene
    scene.name = SCENE_NAME
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    world = scene.world or bpy.data.worlds.new(f"{SCENE_NAME}_World")
    scene.world = world
    world.color = (0.018, 0.022, 0.021)
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
            tile_x, tile_y = x // tile_size, y // tile_size
            tile = tile_y * TILE_COUNT + tile_x
            lx, ly = x % tile_size, y % tile_size
            base = palette[tile % len(palette)]
            value = rng.randint(-8, 8)
            if (lx // 4 + ly // 4) % 2 == 0:
                value += 4
            else:
                value -= 3
            if lx < 2 or ly < 2 or lx >= tile_size - 2 or ly >= tile_size - 2:
                value -= 14
            if pattern == "frame":
                if (lx + ly + tile) % 7 < 2:
                    value += 12
                if (lx - ly) % 13 < 2:
                    value -= 9
                if (lx * 5 + ly * 7 + tile) % 47 == 0:
                    value += 34
            elif pattern == "components":
                if ly in (7, 8, 23, 24):
                    value += 11
                if (lx * 3 + ly * 5 + tile) % 37 == 0:
                    value -= 22
            elif pattern == "electronics":
                if lx % 8 == 0 or ly % 11 == 0:
                    value += 17
                if (lx * 7 + ly * 3 + tile) % 29 < 2:
                    value += 31
                if 9 < lx < 14 and 9 < ly < 14:
                    value -= 26
            elif pattern == "decals":
                if 6 < ly < 10 or 21 < ly < 24:
                    value -= 25
                if lx % 7 < 2 and ly < 17:
                    value += 19
                if (lx + 2 * ly + tile) % 31 == 0:
                    value -= 18
            offset = (y * TEXTURE_SIZE + x) * 4
            pixels[offset:offset + 4] = (
                clamp_byte(base[0] + value) / 255.0,
                clamp_byte(base[1] + value) / 255.0,
                clamp_byte(base[2] + value) / 255.0,
                1.0,
            )
    image.pixels.foreach_set(pixels)
    image.filepath_raw = os.path.join(TEXTURE_DIR, f"{name}.png")
    image.file_format = "PNG"
    image.save()
    image.pack()
    return image


def make_material(name, image, metallic=0.05, roughness=0.82):
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
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material


def move_to_collection(obj, collection):
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def apply_uv(obj, tile):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")
    layer = obj.data.uv_layers.active
    size = 1.0 / TILE_COUNT
    pad = 0.018
    tx, ty = tile % TILE_COUNT, tile // TILE_COUNT
    for loop in layer.data:
        loop.uv.x = tx * size + pad + max(0.0, min(1.0, loop.uv.x)) * (size - 2 * pad)
        loop.uv.y = ty * size + pad + max(0.0, min(1.0, loop.uv.y)) * (size - 2 * pad)
    obj.select_set(False)


def asset_root(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    root = bpy.data.objects.new(name, None)
    collection.objects.link(root)
    return root, collection, []


def finish_piece(obj, root, collection, pieces, material, tile):
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = False
    apply_uv(obj, tile)
    move_to_collection(obj, collection)
    obj.parent = root
    pieces.append(obj)
    return obj


def box(root, collection, pieces, name, location, dimensions, material, tile=0, bevel=0.0, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        modifier = obj.modifiers.new("SingleStepBevel", "BEVEL")
        modifier.width = min(bevel, min(dimensions) * 0.2)
        modifier.segments = 1
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return finish_piece(obj, root, collection, pieces, material, tile)


def cylinder(root, collection, pieces, name, location, radius, depth, material, tile=0, vertices=10, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, end_fill_type="NGON", location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_piece(obj, root, collection, pieces, material, tile)


def cone(root, collection, pieces, name, location, radius1, radius2, depth, material, tile=0, vertices=10, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2, depth=depth, end_fill_type="NGON", location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_piece(obj, root, collection, pieces, material, tile)


def beam(root, collection, pieces, name, start, end, radius, material, tile=0, vertices=6):
    start, end = Vector(start), Vector(end)
    direction = end - start
    rotation = direction.to_track_quat("Z", "Y").to_euler()
    return cylinder(root, collection, pieces, name, (start + end) * 0.5, radius, direction.length, material, tile, vertices, rotation)


def flat_beam(root, collection, pieces, name, start, end, width, height, material, tile=0):
    start, end = Vector(start), Vector(end)
    direction = end - start
    center = (start + end) * 0.5
    yaw = math.atan2(direction.y, direction.x)
    return box(root, collection, pieces, name, center, (direction.length, width, height), material, tile, 0.0, (0, 0, yaw))


def prop_blade(root, collection, pieces, name, angle, length, material, tile=8):
    verts = [
        (0.20, -0.12, -0.025), (length * 0.58, -0.22, -0.018), (length, -0.10, 0.0), (length * 0.6, 0.18, 0.018),
        (0.20, -0.12, 0.025), (length * 0.58, -0.22, 0.018), (length, -0.10, 0.035), (length * 0.6, 0.18, 0.055),
    ]
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.rotation_euler[2] = angle
    return finish_piece(obj, root, collection, pieces, material, tile)


def mark_and_check(root, pieces):
    triangles = 0
    for obj in pieces:
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    if triangles >= TRIANGLE_LIMIT:
        raise RuntimeError(f"{root.name} has {triangles} triangles; budget is < {TRIANGLE_LIMIT}")
    root["under_static_style"] = "PSX-inspired grounded low-poly"
    root["triangle_count"] = triangles
    root["triangle_budget"] = TRIANGLE_LIMIT
    root["texture_resolution"] = TEXTURE_SIZE
    return triangles


def export_asset(root, pieces):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for piece in pieces:
        piece.hide_render = False
        piece.hide_viewport = False
        piece.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODEL_DIR, f"{root.name}.fbx"),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True,
        axis_forward="Z",
        axis_up="Y",
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="RELATIVE",
        embed_textures=False,
    )


def build_frame(material):
    root, collection, p = asset_root("DR_ScoutFrame")
    center = Vector((0, 0.86, 1.155))
    # Long, narrow carbon sandwich: recognizable FPV proportions without hiding installed boards.
    box(root, collection, p, "FrameBottomPlate", (0, 0.86, 1.145), (.36, .58, .025), material, 0, .018)
    box(root, collection, p, "FrameTopPlate", (0, 0.86, 1.31), (.32, .5, .022), material, 1, .018)
    box(root, collection, p, "FrameNoseDeck", (0, .61, 1.327), (.29, .12, .012), material, 4)
    endpoints = [(-.48, .48, 1.155), (.48, .48, 1.155), (-.48, 1.24, 1.155), (.48, 1.24, 1.155)]
    for i, endpoint in enumerate(endpoints):
        direction = (Vector(endpoint) - center).normalized()
        flat_beam(root, collection, p, f"FrameArm.{i}", center + direction * .08, Vector(endpoint), .064, .032, material, 0)
        box(root, collection, p, f"MotorMount.{i}", (endpoint[0], endpoint[1], 1.176), (.125, .12, .032), material, 0)
        lateral = Vector((-direction.y, direction.x, 0))
        for lead in range(3):
            offset = lateral * ((lead - 1) * .012)
            beam(root, collection, p, f"MotorWire.{i}.{lead}", center + direction * .18 + offset + Vector((0, 0, .035)), Vector(endpoint) - direction * .08 + offset + Vector((0, 0, .035)), .0055, material, 8 + lead, 5)
    for i, pos in enumerate([(-.14, .68), (.14, .68), (-.14, .86), (.14, .86), (-.14, 1.04), (.14, 1.04)]):
        cylinder(root, collection, p, f"FrameStandoff.{i}", (pos[0], pos[1], 1.23), .018, .145, material, 3, 8)
        cylinder(root, collection, p, f"FrameScrew.{i}", (pos[0], pos[1], 1.327), .026, .012, material, 6, 8)
    box(root, collection, p, "CameraCage.Left", (-.112, .535, 1.205), (.028, .155, .135), material, 0)
    box(root, collection, p, "CameraCage.Right", (.112, .535, 1.205), (.028, .155, .135), material, 0)
    box(root, collection, p, "CameraCage.Crossbar", (0, .545, 1.265), (.25, .135, .022), material, 1)
    box(root, collection, p, "BatteryPad", (0, .89, 1.345), (.3, .37, .014), material, 8)
    box(root, collection, p, "ReceiverVtxShelf", (0, 1.07, 1.2), (.25, .14, .018), material, 4)
    beam(root, collection, p, "AntennaCableRun", (.12, 1.02, 1.215), (.2, 1.13, 1.255), .006, material, 8, 6)
    return root, p


def build_motor(material):
    root, c, p = asset_root("DR_Motor")
    cylinder(root, c, p, "MotorBase", (0, 0, -.06), .54, .09, material, 6, 8)
    cylinder(root, c, p, "MotorStator", (0, 0, .07), .48, .18, material, 4, 8)
    cylinder(root, c, p, "MotorBell", (0, 0, .32), .55, .36, material, 2, 8)
    cone(root, c, p, "MotorShoulder", (0, 0, .515), .5, .41, .11, material, 3, 8)
    cylinder(root, c, p, "MotorMarkingBand", (0, 0, .49), .558, .035, material, 9, 8)
    cylinder(root, c, p, "MotorCap", (0, 0, .595), .42, .1, material, 6, 8)
    cylinder(root, c, p, "MotorShaft", (0, 0, .8), .14, .34, material, 6, 8)
    for i in range(6):
        angle = i / 6 * math.tau
        box(root, c, p, f"MotorVent.{i}", (math.cos(angle) * .49, math.sin(angle) * .49, .31), (.11, .06, .22), material, 8, 0.0, (0, 0, angle))
    for x in (-1, 1):
        for y in (-1, 1):
            cylinder(
                root,
                c,
                p,
                f"MotorMountEar.{x}.{y}",
                (x * .4615, y * .4615, .015),
                .14,
                .158,
                material,
                6,
                6,
            )
    return root, p


def build_propeller(material):
    root, c, p = asset_root("DR_Propeller")
    cylinder(root, c, p, "PropellerCollet", (0, 0, -.32), .3, .75, material, 6, 8)
    cylinder(root, c, p, "PropellerHub", (0, 0, .08), .62, .28, material, 6, 12)
    for i in range(3):
        prop_blade(root, c, p, f"PropellerBlade.{i}", math.radians(i * 120), 3.05, material, 8)
    cylinder(root, c, p, "PropellerLockNut", (0, 0, .28), .22, .14, material, 7, 8)
    return root, p


def build_battery(material):
    root, c, p = asset_root("DR_Battery")
    box(root, c, p, "BatteryShrinkWrap", (0, 0, 0), (1, 1, .96), material, 12, .065)
    box(root, c, p, "BatteryLabel", (0, -.08, .515), (.76, .72, .035), material, 7, .025)
    box(root, c, p, "BatteryEndCap.Front", (0, -.505, 0), (1.035, .07, .97), material, 8)
    box(root, c, p, "BatteryEndCap.Rear", (0, .505, 0), (1.035, .07, .97), material, 8)
    for i in range(1, 4):
        box(root, c, p, f"BatteryCellCrease.{i}", (-.505 + i * .252, 0, -.495), (.015, .92, .018), material, 2)
    box(root, c, p, "BatteryXT60Connector", (.54, .77, .18), (.22, .2, .24), material, 7, .025)
    box(root, c, p, "BatteryBalanceConnector", (-.35, .64, -.035), (.155, .082, .115), material, 11)
    beam(root, c, p, "BatteryLead.Red.0", (.2, .49, .1), (.32, .62, .86), .035, material, 9, 6)
    beam(root, c, p, "BatteryLead.Red.1", (.32, .62, .86), (.54, .77, .23), .035, material, 9, 6)
    beam(root, c, p, "BatteryLead.Black.0", (.02, .49, .08), (.12, .65, .74), .035, material, 8, 6)
    beam(root, c, p, "BatteryLead.Black.1", (.12, .65, .74), (.48, .79, .13), .035, material, 8, 6)
    for i in range(5):
        beam(root, c, p, f"BatteryBalanceLead.{i}", (-.2 + i * .026, .49, -.1), (-.4 + i * .026, .63, -.035), .0055, material, 9 if i == 0 else 8, 5)
    return root, p


def build_camera(material):
    root, c, p = asset_root("DR_FpvCamera")
    box(root, c, p, "CameraHousing", (0, 0, 0), (1.02, .96, .92), material, 1, .075)
    cylinder(root, c, p, "CameraBezel", (0, -.62, 0), .48, .2, material, 2, 12, (math.pi / 2, 0, 0))
    cylinder(root, c, p, "CameraGlass", (0, -.78, 0), .32, .08, material, 10, 12, (math.pi / 2, 0, 0))
    cylinder(root, c, p, "CameraInnerLens", (0, -.825, 0), .18, .03, material, 10, 10, (math.pi / 2, 0, 0))
    box(root, c, p, "CameraBoard", (0, .53, 0), (.76, .08, .72), material, 4, .035)
    for side in (-1, 1):
        box(root, c, p, f"CameraSidePlate.{side}", (side * .57, -.04, 0), (.1, .92, 1.08), material, 0, .025)
        cylinder(root, c, p, f"CameraPivot.{side}", (side * .6, -.18, 0), .13, .13, material, 6, 8, (0, math.pi / 2, 0))
        box(
            root,
            c,
            p,
            f"CameraMountEar.{side}",
            (side * .5, -.66, .1),
            (.34, .34, .12),
            material,
            0,
            .025,
        )
        cylinder(
            root,
            c,
            p,
            f"CameraFastenerBoss.{side}",
            (side * .5, -.66, .17),
            .13,
            .06,
            material,
            6,
            6,
        )
    box(root, c, p, "CameraRibbonConnector", (0, .59, -.28), (.38, .11, .15), material, 7, .025)
    return root, p


def build_antenna(material):
    root, c, p = asset_root("DR_Antenna")
    cylinder(root, c, p, "AntennaBase", (0, 0, -.58), .76, .2, material, 6, 10)
    cylinder(root, c, p, "AntennaFerrule", (0, 0, -.35), .52, .18, material, 2, 10)
    cone(root, c, p, "AntennaWhip", (0, 0, .14), .34, .21, 1.45, material, 8, 8)
    cylinder(root, c, p, "AntennaTip", (0, 0, .83), .36, .12, material, 9, 8)
    return root, p


def build_esc(material):
    root, c, p = asset_root("DR_ESC")
    box(root, c, p, "EscBoard", (0, 0, 0), (1, .8, .12), material, 4)
    for i in range(8):
        row, col = i // 4, i % 4
        box(root, c, p, f"EscMosfet.{i}", (-.31 + col * .205, -.2 + row * .4, .12), (.13, .16, .12), material, 5)
    for side in (-1, 1):
        for pad in (-1, 0, 1):
            cylinder(root, c, p, f"EscMotorPad.{side}.{pad}", (side * .48, pad * .27, .16), .085, .035, material, 6, 6)
    for side_x in (-1, 1):
        for side_y in (-1, 1):
            cylinder(
                root,
                c,
                p,
                f"EscFastenerPost.{side_x}.{side_y}",
                (side_x * .2667, side_y * .3243, .88),
                .075,
                1.64,
                material,
                6,
                6,
            )
    box(root, c, p, "EscStackPort", (0, .4, .18), (.34, .12, .11), material, 7)
    return root, p


def build_flight_controller(material):
    root, c, p = asset_root("DR_FlightController")
    box(root, c, p, "FlightControllerBoard", (0, 0, 0), (.96, .78, .12), material, 4, .045)
    box(root, c, p, "FlightControllerGyro", (0, -.05, .2), (.24, .24, .13), material, 5, .025)
    box(root, c, p, "FlightControllerProcessor", (-.23, .2, .19), (.2, .2, .11), material, 5, .02)
    box(root, c, p, "FlightControllerUsbPort", (.49, -.2, .17), (.18, .22, .15), material, 6, .025)
    box(root, c, p, "FlightControllerStackPort", (0, .42, .19), (.36, .13, .13), material, 7, .018)
    for i, pos in enumerate([(-.39, -.3), (.39, -.3), (-.39, .3), (.39, .3)]):
        cylinder(root, c, p, f"FlightControllerSoftMount.{i}", (pos[0], pos[1], -.08), .07, .12, material, 8, 8)
    return root, p


def build_receiver_vtx(material):
    root, c, p = asset_root("DR_ReceiverVTX")
    box(root, c, p, "ReceiverBoard", (-.15, 0, 0), (.28, .42, .08), material, 4, .025)
    box(root, c, p, "VtxBoard", (.17, 0, 0), (.3, .42, .08), material, 4, .025)
    box(root, c, p, "VtxShield", (.17, -.03, .085), (.22, .24, .08), material, 6, .018)
    box(root, c, p, "ReceiverChip", (-.15, 0, .075), (.15, .18, .07), material, 5, .015)
    box(root, c, p, "UflConnector", (.32, .14, .075), (.08, .08, .05), material, 7, .01)
    beam(root, c, p, "ReceiverAntennaLead", (-.26, .12, .06), (-.46, .34, .08), .012, material, 8, 6)
    return root, p


def build_strike_rack(material):
    root, c, p = asset_root("DR_StrikeRack")
    for side in (-1, 1):
        box(root, c, p, f"RackRail.{side}", (side * .55, 0, .08), (.14, 3.75, .14), material, 2, .025)
        box(root, c, p, f"RackRailLip.{side}", (side * .68, 0, -.02), (.1, 3.25, .18), material, 0, .02)
    for end, y in (("Front", -1.58), ("Rear", 1.58)):
        box(root, c, p, f"RackCrossmember.{end}", (0, y, .11), (1.45, .2, .18), material, 2, .025)
    for saddle, y in (("Forward", -.78), ("Rear", .78)):
        box(root, c, p, f"RackSaddle.{saddle}", (0, y, -.11), (1.16, .32, .18), material, 6, .035)
        for side in (-1, 1):
            box(root, c, p, f"RackStrapAnchor.{saddle}.{side}", (side * .68, y, .02), (.16, .22, .24), material, 7, .025)
    for bridge, y in (("Front", -.34), ("Rear", .34)):
        box(root, c, p, f"RackAirframeMountingBridge.{bridge}", (0, y, .64), (1.42, .28, .18), material, 1, .035)
    box(root, c, p, "RackInterfaceBlock", (0, 1.34, .39), (.42, .32, .28), material, 7, .035)
    box(root, c, p, "RackClearanceChannel", (0, 0, -.24), (.58, 2.88, .1), material, 4, .02)
    for side_x in (-1, 1):
        for side_y in (-1, 1):
            cylinder(
                root,
                c,
                p,
                f"RackFastenerTower.{side_x}.{side_y}",
                (side_x * .5, side_y * .3333, .266),
                .105,
                .412,
                material,
                6,
                6,
            )
    return root, p


def build_sealed_payload(material):
    """Fictional sealed game prop: recognizable carrier silhouette, no functional arming detail."""
    root, c, p = asset_root("DR_SealedPayload")
    cylinder(root, c, p, "PayloadFacetedBody", (0, .12, 0), .5, 2.45, material, 1, 10, (math.pi / 2, 0, 0))
    cone(root, c, p, "PayloadBluntNose", (0, -1.37, 0), .5, .14, .72, material, 1, 10, (math.pi / 2, 0, 0))
    cylinder(root, c, p, "PayloadNoseCap", (0, -1.75, 0), .15, .1, material, 6, 10, (math.pi / 2, 0, 0))
    cylinder(root, c, p, "PayloadRearClosure", (0, 1.39, 0), .53, .18, material, 6, 10, (math.pi / 2, 0, 0))
    cylinder(root, c, p, "PayloadIdentificationBand", (0, -.68, 0), .515, .2, material, 10, 10, (math.pi / 2, 0, 0))
    cylinder(root, c, p, "PayloadWarningBand", (0, .86, 0), .512, .1, material, 9, 10, (math.pi / 2, 0, 0))
    for pad, y in ((-1, -.7), (1, .7)):
        box(root, c, p, f"PayloadRetentionContact.{pad}", (0, y, .43), (.74, .25, .1), material, 8, .025)
    box(root, c, p, "PayloadHarnessPortFlange", (-.43, 1.08, .08), (.14, .42, .3), material, 1, .035)
    box(root, c, p, "PayloadHarnessPort", (-.53, 1.08, .08), (.12, .24, .18), material, 8, .025)
    box(root, c, p, "PayloadInertLabel", (0, .12, -.505), (.58, .5, .025), material, 10, .018)
    return root, p


def build_complete(frame_material, component_material, electronic_material):
    root, c, p = asset_root("DR_ScoutComplete")
    center = Vector((0, 0, .22))
    box(root, c, p, "Complete.BottomPlate", (0, 0, .2), (.54, .42, .03), frame_material, 0, .025)
    box(root, c, p, "Complete.TopPlate", (0, 0, .34), (.54, .42, .025), frame_material, 1, .025)
    endpoints = [(-.48, -.38, .22), (.48, -.38, .22), (-.48, .38, .22), (.48, .38, .22)]
    for i, endpoint in enumerate(endpoints):
        flat_beam(root, c, p, f"Complete.Arm.{i}", center, endpoint, .08, .032, frame_material, 0)
        cylinder(root, c, p, f"Complete.Motor.{i}", (endpoint[0], endpoint[1], .3), .075, .105, component_material, 2, 8)
        cylinder(root, c, p, f"Complete.PropHub.{i}", (endpoint[0], endpoint[1], .37), .055, .035, component_material, 6, 8)
        for blade in range(3):
            angle = math.radians(blade * 120)
            start = Vector((endpoint[0], endpoint[1], .385))
            end = start + Vector((math.cos(angle), math.sin(angle), 0)) * .2
            flat_beam(root, c, p, f"Complete.Prop.{i}.{blade}", start, end, .027, .012, component_material, 8)
    box(root, c, p, "Complete.Battery", (0, .03, .45), (.24, .42, .11), component_material, 12, .035)
    box(root, c, p, "Complete.BatteryStrap", (0, .03, .515), (.055, .46, .018), component_material, 9, .006)
    box(root, c, p, "Complete.CameraHousing", (0, -.27, .27), (.16, .11, .13), component_material, 1, .025)
    cylinder(root, c, p, "Complete.CameraLens", (0, -.335, .27), .045, .035, component_material, 10, 10, (math.pi / 2, 0, 0))
    box(root, c, p, "Complete.Electronics", (0, .05, .285), (.25, .2, .045), electronic_material, 4, .018)
    beam(root, c, p, "Complete.Antenna", (.16, .15, .36), (.21, .3, .56), .012, component_material, 8, 7)
    return root, p


def build_fpv_civilian_details(root, collection, pieces, label, material, long_range=False, reinforced=False):
    rail_length = .54 if long_range else .46
    for side in (-1, 1):
        box(
            root,
            collection,
            pieces,
            f"Fpv.StackRail.{side}.{label}",
            (side * .17, .87, 1.295),
            (.025, rail_length, .055),
            material,
            0,
            .008,
        )
    # Frame-side half of the battery connection. The battery's own short lead meets this plug.
    box(root, collection, pieces, f"Fpv.PowerSocket.{label}", (.105, 1.105, 1.397), (.11, .12, .075), material, 7, .018)
    beam(root, collection, pieces, f"Fpv.PowerLead.Red.{label}", (.08, 1.055, 1.37), (.115, .98, 1.275), .009, material, 9, 6)
    beam(root, collection, pieces, f"Fpv.PowerLead.Black.{label}", (.13, 1.06, 1.365), (.15, .985, 1.27), .009, material, 8, 6)
    box(root, collection, pieces, f"Fpv.ReceiverTray.{label}", (0, 1.07, 1.235), (.24, .13, .022), material, 4, .008)
    if long_range:
        beam(root, collection, pieces, f"Fpv.AntennaBrace.Left.{label}", (-.12, 1.08, 1.28), (-.23, 1.23, 1.43), .01, material, 8, 6)
        beam(root, collection, pieces, f"Fpv.AntennaBrace.Right.{label}", (.12, 1.08, 1.28), (.23, 1.23, 1.43), .01, material, 8, 6)
    if reinforced:
        box(root, collection, pieces, f"Fpv.RearBrace.{label}", (0, 1.16, 1.2), (.38, .045, .055), material, 2, .012)


def build_civilian_aster(material, detail_material):
    root, c, p = asset_root("DR_CivilianAsterCX4")
    build_fpv_civilian_details(root, c, p, "Aster", material)
    # Small retail guards, not an enclosing fuselage. Removing them leaves the same functional FPV.
    box(root, c, p, "Shell.Panel.0.Aster", (0, .49, 1.265), (.29, .075, .06), detail_material, 3, .018)
    flat_beam(root, c, p, "Shell.Panel.1.Aster", (-.07, .75, 1.205), (-.4, .5, 1.205), .052, .018, detail_material, 2)
    flat_beam(root, c, p, "Shell.Panel.2.Aster", (.07, .75, 1.205), (.4, .5, 1.205), .052, .018, detail_material, 2)
    box(root, c, p, "Fpv.CameraBumper.Left.Aster", (-.13, .525, 1.22), (.025, .13, .13), material, 0, .008)
    box(root, c, p, "Fpv.CameraBumper.Right.Aster", (.13, .525, 1.22), (.025, .13, .13), material, 0, .008)
    return root, p


def build_civilian_horizon(material, detail_material):
    root, c, p = asset_root("DR_CivilianHorizonSurvey6")
    build_fpv_civilian_details(root, c, p, "Horizon", material, long_range=True)
    box(root, c, p, "Shell.Panel.0.Horizon", (0, 1.075, 1.305), (.27, .17, .07), detail_material, 4, .02)
    flat_beam(root, c, p, "Shell.Panel.1.Horizon", (-.06, .79, 1.21), (-.43, .5, 1.21), .058, .02, detail_material, 2)
    flat_beam(root, c, p, "Shell.Panel.2.Horizon", (.06, .79, 1.21), (.43, .5, 1.21), .058, .02, detail_material, 2)
    box(root, c, p, "Fpv.GpsShelf.Horizon", (0, .72, 1.34), (.2, .14, .025), material, 11, .008)
    return root, p


def build_civilian_atlas(material, detail_material):
    root, c, p = asset_root("DR_CivilianAtlasCargo8")
    build_fpv_civilian_details(root, c, p, "Atlas", material, reinforced=True)
    box(root, c, p, "Shell.Panel.0.Atlas", (0, .86, 1.105), (.42, .44, .035), detail_material, 6, .012)
    box(root, c, p, "Shell.Panel.1.Atlas", (-.205, .86, 1.225), (.035, .48, .075), detail_material, 2, .012)
    box(root, c, p, "Shell.Panel.2.Atlas", (.205, .86, 1.225), (.035, .48, .075), detail_material, 2, .012)
    for side in (-1, 1):
        beam(root, c, p, f"Fpv.Reinforcement.{side}.Atlas", (side * .16, .7, 1.18), (side * .39, .5, 1.18), .015, material, 0, 6)
    return root, p


def add_preview(scene, assets, materials):
    preview = bpy.data.collections.new("DR_Preview")
    scene.collection.children.link(preview)
    bpy.ops.mesh.primitive_plane_add(size=9, location=(0, 0, 0))
    ground = bpy.context.object
    ground.name = "Preview.Ground"
    ground.data.materials.append(materials["frame"])
    move_to_collection(ground, preview)
    layout = {
        "DR_ScoutComplete": ((0, .35, .22), (1.45, 1.45, 1.45)),
        "DR_ScoutFrame": ((-2.1, .45, -.75), (.9, .9, .9)),
        "DR_Motor": ((-3.0, -1.55, .12), (.32, .32, .32)),
        "DR_Propeller": ((-2.1, -1.55, .12), (.18, .18, .18)),
        "DR_Battery": ((-1.15, -1.55, .2), (.35, .35, .35)),
        "DR_FpvCamera": ((-.25, -1.55, .2), (.3, .3, .3)),
        "DR_Antenna": ((.55, -1.55, .2), (.25, .25, .25)),
        "DR_ESC": ((1.35, -1.55, .14), (.35, .35, .35)),
        "DR_FlightController": ((2.15, -1.55, .14), (.35, .35, .35)),
        "DR_ReceiverVTX": ((2.9, -1.55, .14), (.55, .55, .55)),
        "DR_StrikeRack": ((2.15, .55, .15), (.42, .42, .42)),
        "DR_SealedPayload": ((3.05, .55, .15), (.42, .42, .42)),
    }
    for name, (location, scale) in layout.items():
        source_root, pieces = assets[name]
        clone_root = source_root.copy()
        clone_root.data = None
        clone_root.name = f"Preview.{name}"
        clone_root.location = location
        clone_root.scale = scale
        clone_root.hide_render = False
        clone_root.hide_viewport = False
        preview.objects.link(clone_root)
        for piece in pieces:
            clone = piece.copy()
            clone.data = piece.data.copy()
            clone.parent = clone_root
            clone.matrix_parent_inverse = piece.matrix_parent_inverse.copy()
            clone.hide_render = False
            clone.hide_viewport = False
            preview.objects.link(clone)
    for name, (location, scale) in layout.items():
        bpy.ops.mesh.primitive_cube_add(size=1, location=(location[0], location[1], .035))
        plinth = bpy.context.object
        plinth.name = f"Preview.Plinth.{name}"
        plinth.dimensions = (.75 if name not in ("DR_ScoutComplete", "DR_ScoutFrame") else 1.65, .68, .07)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        plinth.data.materials.append(materials["components"])
        move_to_collection(plinth, preview)

    bpy.ops.object.light_add(type="AREA", location=(-3.5, -4.0, 6.0))
    key = bpy.context.object
    key.name = "Preview.Key"
    key.data.energy = 1050
    key.data.shape = "DISK"
    key.data.size = 4.0
    key.rotation_euler = (math.radians(28), 0, math.radians(-35))
    bpy.ops.object.light_add(type="AREA", location=(4.0, 1.0, 3.2))
    fill = bpy.context.object
    fill.name = "Preview.Fill"
    fill.data.energy = 700
    fill.data.color = (.45, .62, 1.0)
    fill.data.size = 3.0
    bpy.ops.object.camera_add(location=(6.7, -8.3, 5.35))
    camera = bpy.context.object
    camera.name = "Preview.Camera"
    direction = Vector((0, -.35, .45)) - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 52
    scene.camera = camera
    scene.render.filepath = PREVIEW_PATH


def build():
    ensure_directories()
    scene = reset_scene()
    frame_image = make_texture("DR_Frame_128", [(18, 26, 24), (27, 35, 32), (48, 58, 48), (77, 81, 75), (24, 31, 29), (34, 43, 37), (109, 112, 103), (172, 136, 50), (16, 19, 18), (152, 47, 26), (21, 70, 83), (181, 180, 156), (42, 48, 38), (62, 67, 57), (96, 93, 76), (21, 25, 23)], 1701, "frame")
    component_image = make_texture("DR_Components_128", [(40, 48, 44), (58, 67, 60), (94, 100, 92), (127, 130, 121), (32, 79, 62), (18, 23, 22), (149, 139, 111), (211, 161, 42), (18, 21, 20), (169, 53, 30), (20, 82, 104), (186, 184, 163), (66, 73, 48), (84, 74, 48), (100, 91, 67), (35, 39, 34)], 1702, "components")
    electronic_image = make_texture("DR_Electronics_128", [(22, 66, 51), (29, 87, 64), (39, 102, 71), (51, 117, 78), (18, 71, 54), (13, 18, 17), (158, 112, 46), (205, 166, 62), (18, 21, 20), (148, 43, 27), (34, 78, 94), (176, 177, 158), (31, 55, 44), (50, 71, 54), (91, 86, 64), (19, 28, 24)], 1703, "electronics")
    civilian_image = make_texture("DR_Civilian_128", [(34, 42, 44), (53, 63, 63), (77, 86, 82), (119, 125, 116), (29, 57, 62), (18, 23, 24), (148, 143, 126), (210, 168, 54), (21, 25, 26), (161, 57, 39), (31, 91, 106), (190, 188, 169), (55, 66, 62), (73, 79, 71), (103, 101, 87), (27, 32, 32)], 1705, "frame")
    decal_image = make_texture("DR_Decals_128", [(205, 174, 87), (177, 141, 65), (229, 216, 166), (88, 96, 83), (52, 94, 67), (18, 23, 22), (143, 135, 110), (224, 172, 41), (25, 29, 27), (175, 48, 26), (30, 103, 122), (193, 190, 169), (75, 82, 54), (91, 72, 44), (121, 102, 67), (35, 41, 37)], 1704, "decals")
    materials = {
        "frame": make_material("DR_FrameMaterial", frame_image, .15, .84),
        "components": make_material("DR_ComponentsMaterial", component_image, .22, .76),
        "electronics": make_material("DR_ElectronicsMaterial", electronic_image, .06, .83),
        "decals": make_material("DR_DecalsMaterial", decal_image, .02, .88),
        "civilian": make_material("DR_CivilianMaterial", civilian_image, .08, .87),
    }
    assets = {}
    builders = [
        (build_frame, (materials["frame"],)),
        (build_motor, (materials["components"],)),
        (build_propeller, (materials["components"],)),
        (build_battery, (materials["components"],)),
        (build_camera, (materials["components"],)),
        (build_antenna, (materials["components"],)),
        (build_esc, (materials["electronics"],)),
        (build_flight_controller, (materials["electronics"],)),
        (build_receiver_vtx, (materials["electronics"],)),
        (build_strike_rack, (materials["components"],)),
        (build_sealed_payload, (materials["components"],)),
        (build_complete, (materials["frame"], materials["components"], materials["electronics"])),
        (build_civilian_aster, (materials["civilian"], materials["components"])),
        (build_civilian_horizon, (materials["civilian"], materials["components"])),
        (build_civilian_atlas, (materials["civilian"], materials["components"])),
    ]
    triangle_report = {}
    for builder, args in builders:
        root, pieces = builder(*args)
        triangles = mark_and_check(root, pieces)
        export_asset(root, pieces)
        assets[root.name] = (root, pieces)
        triangle_report[root.name] = triangles
        root.hide_render = True
        root.hide_viewport = True
        for piece in pieces:
            piece.hide_render = True
            piece.hide_viewport = True
    add_preview(scene, assets, materials)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    bpy.context.scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    return triangle_report


TRIANGLE_REPORT = build()
