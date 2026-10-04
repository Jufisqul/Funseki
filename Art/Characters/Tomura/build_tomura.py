# Builds Tomura Ryuta: low-poly PS2/Persona-4-style character. Run: blender -b --python tomura.py -- <out_dir> [--game]
# Default: the posed statue for the main menu (magazine, hand on the belt), no skeleton.
# --game: the playable hero -- arms down, legs split at the knee, rigidly skinned to a Mixamo-named
#         skeleton so Mixamo clips retarget through Unity's Humanoid avatar. Exports Tomura_Ryuta_Game.fbx.
import bpy, bmesh, math, sys, os
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
GAME = "--game" in sys.argv
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ------------------------------------------------------------ materials
PALETTE = {
    "Jacket": (0.11, 0.115, 0.15), "Pants": (0.13, 0.135, 0.18), "Shirt": (0.95, 0.95, 0.94),
    "Skin": (0.95, 0.79, 0.69), "Hair": (0.9, 0.74, 0.36), "Gold": (0.83, 0.66, 0.23),
    "Shoes": (0.16, 0.13, 0.11), "Magazine": (0.75, 0.22, 0.18), "Eyes": (0.17, 0.13, 0.2),
    "Brows": (0.6, 0.46, 0.22), "Mouth": (0.62, 0.27, 0.25), "Cig": (0.97, 0.97, 0.96), "Ember": (1.0, 0.42, 0.16),
}
MATS = {}
for name, rgb in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    lin = tuple(c ** 2.2 for c in rgb)  # palette is sRGB
    bsdf.inputs["Base Color"].default_value = (*lin, 1)
    bsdf.inputs["Roughness"].default_value = 0.85
    if name == "Gold":
        bsdf.inputs["Metallic"].default_value = 0.8
        bsdf.inputs["Roughness"].default_value = 0.35
    if name == "Ember":
        bsdf.inputs["Emission Color"].default_value = (*lin, 1)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    m.diffuse_color = (*lin, 1)
    MATS[name] = m

parts = []

# Game build: the bone that owns the next parts -- a name, or fn(world_co) -> name. Low-poly PS2 style,
# so every vertex follows exactly one bone (rigid skinning), like the segmented models of that era.
BONE = None

def finish(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(MATS[mat])
    for p in obj.data.polygons:
        p.use_smooth = False
    if GAME:
        assert BONE, "game build: set BONE before adding a part"
        bpy.context.view_layer.update()
        for i, v in enumerate(obj.data.vertices):
            name = BONE(obj.matrix_world @ v.co) if callable(BONE) else BONE
            group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
            group.add([i], 1.0, "REPLACE")
    parts.append(obj)
    return obj

def torso_bone(co):
    if co.z < 1.0:
        return "mixamorig:Hips"
    if co.z < 1.17:
        return "mixamorig:Spine"
    if co.z < 1.32:
        return "mixamorig:Spine1"
    return "mixamorig:Spine2"

def side_name(s):  # the hero faces -Y, so his left is +X
    return "Left" if s > 0 else "Right"

def active():
    return bpy.context.active_object

def cone(r1, r2, depth, loc, verts=8, mat="Jacket", rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    return finish(active(), mat)

def limb(p1, p2, r1, r2, mat, verts=8):
    """Tapered cylinder from p1 (radius r1) to p2 (radius r2)."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=d.length, location=(p1 + p2) / 2)
    o = active()
    o.rotation_mode = "QUATERNION"
    o.rotation_quaternion = d.to_track_quat("Z", "Y")
    return finish(o, mat)

def box(size, loc, mat, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = active()
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        mod = o.modifiers.new("bevel", "BEVEL"); mod.width = bevel; mod.segments = 1
    return finish(o, mat)

def blob(scale, loc, mat, seg=8, rings=6, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, radius=1, location=loc, rotation=rot)
    o = active()
    o.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, mat)

def delete_faces(obj, test):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if test(f)], context="FACES")
    bm.to_mesh(obj.data); bm.free()

# ------------------------------------------------------------ body (faces -Y)
# Legs: wide yankii trousers, slightly apart.
for s in (-1, 1):
    if GAME:  # thigh and shin overlap at the knee so a bent leg shows no gap
        BONE = f"mixamorig:{side_name(s)}UpLeg"
        limb((0.1 * s, 0, 0.45), (0.095 * s, 0, 0.9), 0.095, 0.105, "Pants")
        BONE = f"mixamorig:{side_name(s)}Leg"
        limb((0.1 * s, 0, 0.07), (0.1 * s, 0, 0.5), 0.085, 0.095, "Pants")
        BONE = f"mixamorig:{side_name(s)}Foot"
    else:
        limb((0.1 * s, 0, 0.07), (0.095 * s, 0, 0.9), 0.085, 0.105, "Pants")
    box((0.12, 0.27, 0.07), (0.1 * s, -0.045, 0.035), "Shoes", bevel=0.02)
BONE = torso_bone
cone(0.17, 0.165, 0.1, (0, 0, 0.92), verts=10, mat="Pants")           # waist
cone(0.17, 0.21, 0.56, (0, 0, 1.17), verts=10, mat="Shirt")           # t-shirt torso

# Open gakuran: a flared tube with the front faces removed, solidified.
bpy.ops.mesh.primitive_cone_add(vertices=12, radius1=0.215, radius2=0.235, depth=0.68, location=(0, 0, 1.12), end_fill_type="NOTHING")
jacket = finish(active(), "Jacket")
delete_faces(jacket, lambda f: f.calc_center_median().y < -0.15 and abs(f.calc_center_median().x) < 0.09)
jacket.modifiers.new("thick", "SOLIDIFY").thickness = 0.012
box((0.5, 0.26, 0.07), (0, 0.0, 1.45), "Jacket", bevel=0.03)          # shoulders
# Standing collar, unbuttoned at the front.
bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.085, depth=0.1, location=(0, 0.01, 1.53), end_fill_type="NOTHING")
collar = finish(active(), "Jacket")
delete_faces(collar, lambda f: f.calc_center_median().y < -0.06)
collar.modifiers.new("thick", "SOLIDIFY").thickness = 0.012
for i in range(5):                                                      # gold buttons on one placket
    blob((0.012, 0.008, 0.012), (0.122, -0.205, 1.4 - i * 0.11), "Gold", seg=6, rings=4)

if GAME:
    # Arms hang loose (A-pose) so walk and run clips swing them naturally; joints match the skeleton.
    for s in (-1, 1):
        S, E, W = (0.25 * s, 0, 1.42), (0.31 * s, 0.01, 1.16), (0.36 * s, -0.01, 0.92)
        BONE = f"mixamorig:{side_name(s)}Arm"
        limb(S, E, 0.068, 0.058, "Jacket")
        BONE = f"mixamorig:{side_name(s)}ForeArm"
        limb(E, W, 0.058, 0.048, "Jacket")
        BONE = f"mixamorig:{side_name(s)}Hand"
        blob((0.042, 0.03, 0.06), (0.365 * s, -0.015, 0.87), "Skin", seg=6, rings=4)
else:
    # Arms: screen-left arm hangs with a magazine, screen-right hand on the belt.
    SL, EL, WL = (-0.25, 0, 1.42), (-0.29, 0.01, 1.13), (-0.3, -0.01, 0.87)
    SR, ER, WR = (0.25, 0, 1.42), (0.34, 0.05, 1.15), (0.17, -0.15, 0.96)
    for a, b, r1, r2 in ((SL, EL, 0.068, 0.058), (EL, WL, 0.058, 0.048), (SR, ER, 0.068, 0.058), (ER, WR, 0.058, 0.048)):
        limb(a, b, r1, r2, "Jacket")
    blob((0.042, 0.03, 0.06), (-0.3, -0.015, 0.82), "Skin", seg=6, rings=4)   # hand L
    blob((0.045, 0.035, 0.05), (0.14, -0.17, 0.93), "Skin", seg=6, rings=4)   # hand R (thumb in belt)
    box((0.025, 0.17, 0.23), (-0.33, -0.02, 0.8), "Magazine", rot=(math.radians(6), 0, math.radians(-4)))

# ------------------------------------------------------------ head
HEAD = Vector((0, -0.01, 1.73)); A, B, C = 0.125, 0.135, 0.16   # ellipsoid half-axes

def jaw(z):  # narrowing of the lower face -> anime V jaw
    return 1.0 - 0.38 * max(0.0, -z / C)

BONE = "mixamorig:Neck"
limb((0, 0, 1.47), (0, -0.005, 1.62), 0.062, 0.058, "Skin")            # neck
BONE = "mixamorig:Head"
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=9, radius=1, location=HEAD)
head = active(); head.scale = (A, B, C); bpy.ops.object.transform_apply(scale=True)
for v in head.data.vertices:
    lz = v.co.z - HEAD.z
    v.co.x *= jaw(lz)
    if lz < 0:
        v.co.y -= 0.03 * (-lz / C)  # chin forward
finish(head, "Skin")

def face_y(x, z, lift=0.004):
    """Point on the front of the deformed head at (x, z)."""
    lz = z - HEAD.z
    x0 = x / jaw(lz)
    k = 1 - (x0 / A) ** 2 - (lz / C) ** 2
    y = HEAD.y - B * math.sqrt(max(k, 0.0))
    if lz < 0:
        y -= 0.03 * (-lz / C)
    return y - lift

for s in (-1, 1):
    blob((0.022, 0.03, 0.04), (0.128 * s, HEAD.y + 0.01, 1.72), "Skin", seg=6, rings=4)   # ears
    ex, ez = 0.047 * s, 1.735
    box((0.034, 0.008, 0.011), (ex, face_y(ex, ez), ez), "Eyes", rot=(0, math.radians(-6 * s), 0))          # half-lidded eye
    box((0.04, 0.008, 0.008), (ex, face_y(ex, ez + 0.03), ez + 0.022), "Brows", rot=(0, math.radians(-14 * s), 0))  # low, cocky brows
box((0.012, 0.03, 0.025), (0, face_y(0, 1.7, 0.012), 1.7), "Skin")                     # nose
box((0.026, 0.007, 0.007), (-0.004, face_y(-0.004, 1.652), 1.652), "Mouth", rot=(0, math.radians(-4), 0))   # crooked smirk:
box((0.018, 0.007, 0.007), (0.019, face_y(0.019, 1.659), 1.659), "Mouth", rot=(0, math.radians(-28), 0))   # one corner up

# Cigarette hanging from the smirk corner, ember at the tip.
mx, mz = 0.02, 1.657
tip = Vector((mx + 0.05, face_y(mx, mz) - 0.05, mz - 0.022))
limb((mx, face_y(mx, mz) + 0.004, mz), tip, 0.0055, 0.0055, "Cig", verts=6)
limb(tip, tip + (tip - Vector((mx, face_y(mx, mz), mz))).normalized() * 0.008, 0.0058, 0.0058, "Ember", verts=6)

# Hair: blond cap plus spikes sticking up and out.
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=(0, HEAD.y + 0.012, HEAD.z + 0.035))
cap = active(); cap.scale = (A * 1.1, B * 1.1, C * 0.95); bpy.ops.object.transform_apply(scale=True)
finish(cap, "Hair")
delete_faces(cap, lambda f: (f.calc_center_median().z - HEAD.z) < (0.03 if f.calc_center_median().y < HEAD.y - 0.02 else -0.07))
import random
rnd = random.Random(3)
CAPC = Vector((0, HEAD.y + 0.012, HEAD.z + 0.035))

def spike(az, el, ln, bias, r=0.045):
    a, e = math.radians(az), math.radians(el)
    n = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    base = CAPC + Vector((n.x * A, n.y * B, n.z * C)) * 0.9
    d = (n + Vector(bias)).normalized()
    limb(base, base + d * ln, r, 0.004, "Hair", verts=4)

# Messy hair swept up and back, a few strands falling over the forehead.
for el in (25, 45, 65):
    for az in range(-150, 181, 30):
        if abs(az) < 40 and el == 25:
            continue  # leave the forehead to the fringe
        spike(az + rnd.uniform(-12, 12), el + rnd.uniform(-6, 6), rnd.uniform(0.07, 0.13), (0, 0.45, 0.6))
spike(0, 85, 0.12, (0, 0.3, 0.8), r=0.05)
for az in (-30, -10, 12, 32):  # fringe
    spike(az, 30, rnd.uniform(0.06, 0.085), (0, -0.2, -0.75), r=0.035)
for s in (-1, 1):  # sideburns
    spike(98 * s, 5, 0.06, (0, 0.1, -0.9), r=0.03)

# ------------------------------------------------------------ assemble, render, export
for o in parts:
    o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.convert(target="MESH")          # apply solidify/bevel
bpy.ops.object.join()
body = bpy.context.active_object
body.name = "Tomura_Ryuta"
bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
print("TRIS", sum(len(p.vertices) - 2 for p in body.data.polygons))

if GAME:
    # Mixamo bone names, so Unity's Humanoid auto-mapping finds every bone. Joints match the parts above.
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = active()
    arm.name = "Armature"
    eb = arm.data.edit_bones
    eb.remove(eb[0])

    def bone(name, head, tail, parent=None):
        b = eb.new("mixamorig:" + name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = eb["mixamorig:" + parent]

    bone("Hips", (0, 0, 0.95), (0, 0, 1.05))
    bone("Spine", (0, 0, 1.05), (0, 0, 1.2), "Hips")
    bone("Spine1", (0, 0, 1.2), (0, 0, 1.33), "Spine")
    bone("Spine2", (0, 0, 1.33), (0, 0, 1.47), "Spine1")
    bone("Neck", (0, 0, 1.47), (0, -0.005, 1.6), "Spine2")
    bone("Head", (0, -0.005, 1.6), (0, -0.01, 1.85), "Neck")
    bone("HeadTop_End", (0, -0.01, 1.85), (0, -0.01, 1.95), "Head")
    for s in (-1, 1):
        n = side_name(s)
        bone(n + "Shoulder", (0.06 * s, 0, 1.43), (0.25 * s, 0, 1.42), "Spine2")
        bone(n + "Arm", (0.25 * s, 0, 1.42), (0.31 * s, 0.01, 1.16), n + "Shoulder")
        bone(n + "ForeArm", (0.31 * s, 0.01, 1.16), (0.36 * s, -0.01, 0.92), n + "Arm")
        bone(n + "Hand", (0.36 * s, -0.01, 0.92), (0.37 * s, -0.015, 0.82), n + "ForeArm")
        bone(n + "UpLeg", (0.1 * s, 0, 0.92), (0.1 * s, 0, 0.48), "Hips")
        bone(n + "Leg", (0.1 * s, 0, 0.48), (0.1 * s, 0, 0.08), n + "UpLeg")
        bone(n + "Foot", (0.1 * s, 0, 0.08), (0.1 * s, -0.12, 0.03), n + "Leg")
        bone(n + "ToeBase", (0.1 * s, -0.12, 0.03), (0.1 * s, -0.18, 0.03), n + "Foot")
    bpy.ops.object.mode_set(mode="OBJECT")

    body.parent = arm
    body.modifiers.new("Armature", "ARMATURE").object = arm
    unweighted = [v.index for v in body.data.vertices if not v.groups]
    assert not unweighted, f"{len(unweighted)} vertices have no bone"

    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Tomura_Ryuta_Game.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    arm.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Tomura_Ryuta_Game.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=False,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
    print("DONE")
    sys.exit(0)

world = bpy.data.worlds.new("Sunset"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.95, 0.55, 0.3, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(55), 0, math.radians(-35)))
active().data.energy = 3.5; active().data.color = (1.0, 0.82, 0.62)
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(60), 0, math.radians(150)))
active().data.energy = 1.2; active().data.color = (0.75, 0.8, 1.0)
bpy.ops.mesh.primitive_plane_add(size=6)
floor = active()
fm = bpy.data.materials.new("Floor"); fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.45, 0.38, 0.33, 1)
floor.data.materials.append(fm)

for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = eng; break
    except TypeError:
        pass
scene.render.resolution_x, scene.render.resolution_y = 800, 1100
scene.view_settings.view_transform = "Standard"

bpy.ops.object.camera_add()
cam = active(); scene.camera = cam
cam.data.lens = 60
def shot(name, pos, target=(0, 0, 0.95)):
    cam.location = pos
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (Vector(target) - Vector(pos)).to_track_quat("-Z", "Y")
    scene.render.filepath = os.path.join(OUT, name)
    bpy.ops.render.render(write_still=True)

shot("tomura_34.png", (1.6, -3.4, 1.25))
shot("tomura_face.png", (0.35, -0.95, 1.72), target=(0, 0, 1.7))

bpy.data.objects.remove(floor)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Tomura_Ryuta.blend"))
bpy.ops.object.select_all(action="DESELECT"); body.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Tomura_Ryuta.fbx"), use_selection=True,
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
print("DONE")
