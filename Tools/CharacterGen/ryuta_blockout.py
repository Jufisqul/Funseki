# Grey blockout of Tamura Ryuta (silhouette and proportions only).
# Run: blender -b --python Tools/CharacterGen/ryuta_blockout.py -- <out_dir>
# Writes <out_dir>/Ryuta_Blockout.blend and <out_dir>/previews/blockout_*.png.
# Character faces -Y, Z up, feet at Z=0, 1 unit = 1 m, height 1.75 m (~7.25 heads).

import bpy, bmesh, math, os, sys
import numpy as np
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(argv[0] if argv else "Art/Characters/Ryuta")
PREV = os.path.join(OUT, "previews")
os.makedirs(PREV, exist_ok=True)

HEIGHT = 1.75
HEAD = HEIGHT / 7.25

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- skeleton
# Unity Humanoid names, no prefixes. Left side is +X.
BONES = [
    # name, head, tail, parent
    ("Hips", (0, 0.0, 0.95), (0, 0.0, 1.05), None),
    ("Spine", (0, 0.0, 1.05), (0, 0.005, 1.18), "Hips"),
    ("Chest", (0, 0.005, 1.18), (0, 0.01, 1.32), "Spine"),
    ("UpperChest", (0, 0.01, 1.32), (0, 0.01, 1.44), "Chest"),
    ("Neck", (0, 0.01, 1.45), (0, 0.0, 1.53), "UpperChest"),
    ("Head", (0, 0.0, 1.53), (0, 0.0, 1.745), "Neck"),
]
ARM_DOWN = math.radians(40)
for s, side in ((1, "Left"), (-1, "Right")):
    sh0 = Vector((0.025 * s, 0.0, 1.42))
    sh1 = Vector((0.165 * s, 0.01, 1.435))
    d = Vector((math.cos(ARM_DOWN) * s, 0, -math.sin(ARM_DOWN)))
    el = sh1 + d * 0.28
    wr = el + d * 0.25
    hd = wr + d * 0.09
    BONES += [
        (side + "Shoulder", sh0, sh1, "UpperChest"),
        (side + "UpperArm", sh1, el, side + "Shoulder"),
        (side + "LowerArm", el, wr, side + "UpperArm"),
        (side + "Hand", wr, hd, side + "LowerArm"),
        (side + "UpperLeg", (0.095 * s, 0.0, 0.92), (0.10 * s, 0.0, 0.50), "Hips"),
        (side + "LowerLeg", (0.10 * s, 0.0, 0.50), (0.10 * s, 0.025, 0.085), side + "UpperLeg"),
        (side + "Foot", (0.10 * s, 0.025, 0.085), (0.105 * s, -0.09, 0.025), side + "LowerLeg"),
        (side + "Toes", (0.105 * s, -0.09, 0.025), (0.11 * s, -0.17, 0.025), side + "Foot"),
    ]

arm_data = bpy.data.armatures.new("Ryuta_Rig")
rig = bpy.data.objects.new("Ryuta_Rig", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
for name, h, t, p in BONES:
    eb = arm_data.edit_bones.new(name)
    eb.head, eb.tail = Vector(h), Vector(t)
    if p:
        eb.parent = arm_data.edit_bones[p]
        eb.use_connect = (Vector(h) - arm_data.edit_bones[p].tail).length < 1e-4
bpy.ops.object.mode_set(mode="OBJECT")
B = {name: (Vector(h), Vector(t)) for name, h, t, p in BONES}

# ---------------------------------------------------------------- mesh helpers
parts = []  # (object, bone)


def add(obj, bone):
    for p in obj.data.polygons:
        p.use_smooth = True
    parts.append((obj, bone))
    return obj


def new_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    return ob


def ellipsoid(name, center, radii, bone, seg=16, rings=10, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
    from mathutils import Euler
    R = Euler(rot).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ R, verts=bm.verts)
    return add(new_obj(name, bm), bone)


def frustum(name, p0, p1, r0, r1, bone, seg=12, sx=1.0, sy=1.0, caps=True):
    """Tapered tube from p0 to p1, elliptical (sx, sy scale the cross-section)."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = p1 - p0
    q = axis.normalized().to_track_quat("Z", "Y")
    bm = bmesh.new()
    ring = []
    for k, (r, z) in enumerate(((r0, 0.0), (r1, axis.length))):
        vs = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            vs.append(bm.verts.new((math.cos(a) * r * sx, math.sin(a) * r * sy, z)))
        ring.append(vs)
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new((ring[0][i], ring[0][j], ring[1][j], ring[1][i]))
    if caps:
        bm.faces.new(list(reversed(ring[0])))
        bm.faces.new(ring[1])
    bmesh.ops.transform(bm, matrix=Matrix.Translation(p0) @ q.to_matrix().to_4x4(), verts=bm.verts)
    return add(new_obj(name, bm), bone)


def lofted(name, sections, bone, seg=16, open_front=0.0):
    """Loft of horizontal ellipses: sections = [(z, cx, cy, rx, ry), ...] bottom to top.
    open_front > 0 removes a front wedge (radians half-angle) to leave the jacket open."""
    bm = bmesh.new()
    rings = []
    for z, cx, cy, rx, ry in sections:
        vs = []
        for i in range(seg):
            a = -math.pi / 2 + 2 * math.pi * i / seg  # i=0 points to front (-Y)
            vs.append(bm.verts.new((cx + math.cos(a) * rx, cy + math.sin(a) * ry, z)))
        rings.append(vs)
    for k in range(len(rings) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            mid = -math.pi / 2 + 2 * math.pi * (i + 0.5) / seg
            if open_front and abs(math.atan2(math.sin(mid + math.pi / 2), math.cos(mid + math.pi / 2))) < open_front:
                continue
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    if not open_front:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    else:
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.012)
    return add(new_obj(name, bm), bone)


def box(name, center, size, bone, rot=(0, 0, 0), bevel=0.01):
    from mathutils import Euler
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    if bevel:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=2, affect="EDGES", clamp_overlap=True)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ Euler(rot).to_matrix().to_4x4(), verts=bm.verts)
    return add(new_obj(name, bm), bone)


def spike(name, base, tip, r, bone, flat=0.55):
    """Hair lock: flattened cone from base to tip."""
    return frustum(name, base, tip, r, 0.002, bone, seg=6, sx=1.0, sy=flat)


# ---------------------------------------------------------------- body
# Legs and baggy pants (pants are one tube per leg, bunching above the shoes).
for s, side in ((1, "Left"), (-1, "Right")):
    hip, knee = B[side + "UpperLeg"]
    _, ankle = B[side + "LowerLeg"]
    frustum(side + "Thigh", hip + Vector((0.005 * s, 0, 0.03)), knee, 0.082, 0.068, side + "UpperLeg")
    ellipsoid(side + "Knee", knee, (0.068, 0.07, 0.07), side + "LowerLeg")
    frustum(side + "Shin", knee, ankle + Vector((0, 0, 0.0)), 0.068, 0.074, side + "LowerLeg", sx=1.0, sy=1.05)
    ellipsoid(side + "Hem", ankle + Vector((0, -0.005, 0.03)), (0.078, 0.083, 0.045), side + "LowerLeg")
    # sneaker: low, rounded, slightly long
    f0, f1 = B[side + "Foot"]
    ellipsoid(side + "Shoe", Vector((f0.x, -0.04, 0.045)), (0.055, 0.135, 0.05), side + "Foot")
    ellipsoid(side + "ShoeToe", Vector((f1.x, -0.14, 0.035)), (0.052, 0.06, 0.038), side + "Toes")

# Pelvis / pants seat and waistband (the hand goes in here, so a clean belt line).
lofted("Pelvis", [
    (0.80, 0, 0.005, 0.15, 0.10),
    (0.90, 0, 0.0, 0.17, 0.115),
    (0.99, 0, 0.0, 0.165, 0.11),
    (1.03, 0, 0.0, 0.155, 0.105),
], "Hips")

# T-shirt torso (visible through the open jacket), slight slouch built into the volumes.
lofted("Torso", [
    (1.00, 0, 0.0, 0.15, 0.10),
    (1.10, 0, 0.0, 0.145, 0.10),
    (1.22, 0, 0.005, 0.155, 0.105),
    (1.34, 0, 0.01, 0.17, 0.11),
    (1.42, 0, 0.015, 0.165, 0.10),
    (1.46, 0, 0.02, 0.10, 0.075),
], "Chest")

# Open gakuran: longer than the shirt, hem to the hips, flared, front wedge removed.
lofted("Jacket", [
    (0.86, 0, 0.0, 0.185, 0.135),
    (0.98, 0, 0.0, 0.178, 0.128),
    (1.10, 0, 0.0, 0.168, 0.122),
    (1.22, 0, 0.005, 0.175, 0.122),
    (1.34, 0, 0.01, 0.19, 0.125),
    (1.42, 0, 0.015, 0.205, 0.118),
    (1.455, 0, 0.02, 0.15, 0.095),
], "Spine", seg=20, open_front=math.radians(28))
# Stand-up collar, unbuttoned: open ring around the neck.
lofted("Collar", [
    (1.43, 0, 0.02, 0.085, 0.075),
    (1.50, 0, 0.025, 0.08, 0.07),
], "UpperChest", seg=16, open_front=math.radians(40))

# Neck
frustum("NeckGeo", B["Neck"][0] + Vector((0, 0, -0.02)), B["Neck"][1] + Vector((0, 0, 0.02)), 0.05, 0.047, "Neck")

# Shoulders and arms in sleeves (sleeves slightly wide, cuffs hide the wrist).
for s, side in ((1, "Left"), (-1, "Right")):
    sh = B[side + "UpperArm"][0]
    el = B[side + "LowerArm"][0]
    wr, hd = B[side + "Hand"]
    ellipsoid(side + "Delt", sh + Vector((-0.01 * s, 0.0, -0.02)), (0.05, 0.058, 0.05), side + "UpperArm")
    frustum(side + "Sleeve", sh, el, 0.062, 0.052, side + "UpperArm")
    ellipsoid(side + "Elbow", el, (0.052, 0.052, 0.052), side + "LowerArm")
    frustum(side + "Forearm", el, wr, 0.052, 0.05, side + "LowerArm")
    # hand: palm block + mitten fingers + thumb, rotated along the arm direction
    d = (hd - wr).normalized()
    ang = math.atan2(d.z, d.x * s)
    rot = (0, s * -ang, 0) if s > 0 else (0, -ang + math.pi, 0)
    palm_c = wr + d * 0.05
    frustum(side + "Palm", wr - d * 0.005, wr + d * 0.09, 0.034, 0.038, side + "Hand", seg=8, sx=1.0, sy=0.5)
    frustum(side + "Fingers", wr + d * 0.09, wr + d * 0.175, 0.036, 0.026, side + "Hand", seg=8, sx=1.0, sy=0.45)
    thumb0 = wr + d * 0.035 + Vector((0, -0.025, 0))
    frustum(side + "Thumb", thumb0, thumb0 + d * 0.05 + Vector((0, -0.03, 0)), 0.014, 0.011, side + "Hand", seg=6)

# ---------------------------------------------------------------- head and hair
HC = Vector((0, -0.005, 1.625))  # head center
ellipsoid("Skull", HC + Vector((0, 0.01, 0.01)), (0.082, 0.098, 0.105), "Head")
ellipsoid("Jaw", HC + Vector((0, -0.025, -0.06)), (0.066, 0.072, 0.06), "Head")
ellipsoid("Chin", HC + Vector((0, -0.07, -0.095)), (0.028, 0.025, 0.02), "Head")
ellipsoid("Nose", HC + Vector((0, -0.098, -0.02)), (0.011, 0.014, 0.02), "Head", seg=8, rings=6)
for s in (1, -1):
    ellipsoid("Ear" + ("L" if s > 0 else "R"), HC + Vector((0.083 * s, 0.005, -0.005)), (0.012, 0.022, 0.03), "Head", seg=8, rings=6)

# Hair cap (covers top/back), then big sharp locks: messy, pointing out/down, fringe over the eyes.
ellipsoid("HairCap", HC + Vector((0, 0.015, 0.035)), (0.092, 0.108, 0.098), "Head")
locks = [
    # fringe: falls over the forehead, tips at eye level, uneven
    ((-0.045, -0.085, 0.07), (-0.06, -0.115, -0.005), 0.032),
    ((-0.012, -0.095, 0.075), (-0.005, -0.12, 0.0), 0.034),
    ((0.022, -0.09, 0.072), (0.035, -0.118, 0.012), 0.032),
    ((0.055, -0.075, 0.065), (0.078, -0.1, 0.0), 0.028),
    # sides over the ears
    ((0.08, -0.02, 0.05), (0.115, -0.03, -0.045), 0.035),
    ((-0.08, -0.02, 0.05), (-0.118, -0.025, -0.05), 0.035),
    ((0.085, 0.04, 0.05), (0.125, 0.06, -0.03), 0.035),
    ((-0.085, 0.04, 0.05), (-0.13, 0.055, -0.025), 0.035),
    # crown: messy, flicking up and back
    ((0.02, -0.04, 0.115), (0.075, -0.085, 0.15), 0.045),
    ((-0.03, -0.01, 0.12), (-0.1, -0.03, 0.155), 0.045),
    ((0.04, 0.04, 0.115), (0.115, 0.07, 0.14), 0.045),
    ((-0.035, 0.06, 0.105), (-0.1, 0.12, 0.125), 0.045),
    ((0.0, 0.07, 0.1), (0.01, 0.175, 0.095), 0.048),
    ((0.0, 0.0, 0.125), (0.02, 0.06, 0.165), 0.05),
    # back: falls to the nape
    ((0.04, 0.09, 0.0), (0.06, 0.13, -0.09), 0.036),
    ((-0.04, 0.09, 0.0), (-0.055, 0.13, -0.095), 0.036),
    ((0.0, 0.1, 0.02), (0.0, 0.145, -0.08), 0.038),
]
for i, (b0, b1, r) in enumerate(locks):
    spike("Lock%02d" % i, HC + Vector(b0), HC + Vector(b1), r, "Head")

# ---------------------------------------------------------------- one mesh, rigid weights
for ob, bone in parts:
    vg = ob.vertex_groups.new(name=bone)
    vg.add(list(range(len(ob.data.vertices))), 1.0, "REPLACE")
bpy.ops.object.select_all(action="DESELECT")
for ob, _ in parts:
    ob.select_set(True)
bpy.context.view_layer.objects.active = parts[0][0]
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Ryuta_Blockout"
mod = body.modifiers.new("Armature", "ARMATURE")
mod.object = rig
body.parent = rig
tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
print("[Blockout] tris:", tris, "height:", round(body.dimensions.z, 3))

# Head-height ruler (7.25 ticks) to the character's right.
for k in range(8):
    z = HEIGHT - k * HEAD
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(0.08 if k % 2 == 0 else 0.05, 0.005, 0.004), verts=bm.verts)
    bmesh.ops.translate(bm, vec=(-0.62, 0, z), verts=bm.verts)
    new_obj("Tick%d" % k, bm)
bm = bmesh.new()
bmesh.ops.create_cube(bm, size=1.0)
bmesh.ops.scale(bm, vec=(0.006, 0.005, HEIGHT), verts=bm.verts)
bmesh.ops.translate(bm, vec=(-0.62, 0, HEIGHT / 2), verts=bm.verts)
ruler = new_obj("Ruler", bm)
ruler_objs = [o for o in scene.objects if o.name.startswith(("Tick", "Ruler"))]

# ---------------------------------------------------------------- game pose (slouch, hand in waistband)
POSE_ROT = {
    "Hips": (-5, 0, 0), "Spine": (5, 0, 0), "Chest": (5, 0, 0), "UpperChest": (5, 0, 0),
    "Neck": (12, 0, 0), "Head": (-12, 0, 5),
    "LeftShoulder": (0, 0, 0), "RightShoulder": (0, 0, 0),
}


def set_pose(game):
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0, 0, 0)
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    if game:
        for n, r in POSE_ROT.items():
            pb = rig.pose.bones[n]
            pb.rotation_euler = [math.radians(a) for a in r]
        # IK targets: right hand into the front waistband, left arm hangs loose
        for side, tgt, pole in (("Right", (-0.085, -0.15, 1.0), (-0.5, 0.5, 1.2)),
                                ("Left", (0.25, 0.05, 0.86), (0.4, 0.5, 1.2))):
            for nm, loc in ((side + "_IK", tgt), (side + "_Pole", pole)):
                e = bpy.data.objects.get(nm) or bpy.data.objects.new(nm, None)
                if e.name not in scene.collection.objects:
                    scene.collection.objects.link(e)
                e.location = loc
            c = rig.pose.bones[side + "LowerArm"].constraints.new("IK")
            c.target = bpy.data.objects[side + "_IK"]
            c.pole_target = bpy.data.objects[side + "_Pole"]
            c.pole_angle = math.radians(-90 if side == "Right" else -90)
            c.chain_count = 2
        # right hand points down, fingers inside the front waistband
        e = bpy.data.objects.get("Right_HandAim") or bpy.data.objects.new("Right_HandAim", None)
        if e.name not in scene.collection.objects:
            scene.collection.objects.link(e)
        e.location = (-0.06, -0.11, 0.7)
        rig.pose.bones["RightHand"].constraints.new("DAMPED_TRACK").target = e
        # weight on one leg, knees soft
        rig.pose.bones["LeftUpperLeg"].rotation_euler = (math.radians(-6), 0, math.radians(-3))
        rig.pose.bones["LeftLowerLeg"].rotation_euler = (math.radians(10), 0, 0)
        rig.pose.bones["RightUpperLeg"].rotation_euler = (0, 0, math.radians(3))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- render
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x, scene.render.resolution_y = 560, 1000
scene.render.film_transparent = False
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "SINGLE"
scene.display.shading.single_color = (0.55, 0.55, 0.55)
scene.display.shading.show_object_outline = True
scene.display.shading.show_cavity = False
scene.display.shading.cavity_type = "WORLD"
scene.display.shading.background_type = "VIEWPORT"
scene.display.shading.background_color = (0.93, 0.93, 0.93)
scene.view_settings.view_transform = "Standard"

cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
scene.collection.objects.link(cam)
scene.camera = cam


def shoot(path, yaw_deg, ortho=True, dist=6.0, silhouette=False, zc=0.9, scale=2.05):
    yaw = math.radians(yaw_deg)
    # yaw 0 = front (camera on -Y)
    cam.location = (math.sin(yaw) * dist, -math.cos(yaw) * dist, zc)
    cam.rotation_euler = (math.radians(90), 0, yaw)
    cam.data.type = "ORTHO" if ortho else "PERSP"
    cam.data.ortho_scale = scale
    cam.data.lens = 85
    sh = scene.display.shading
    if silhouette:
        sh.light, sh.single_color, sh.show_cavity, sh.show_object_outline = "FLAT", (0.05, 0.05, 0.05), False, False
    else:
        sh.light, sh.single_color, sh.show_cavity, sh.show_object_outline = "STUDIO", (0.55, 0.55, 0.55), False, True
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def sheet(paths, out):
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p)
        a = np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4)
        imgs.append(a)
        bpy.data.images.remove(im)
    row = np.concatenate(imgs, axis=1)
    h, w = row.shape[:2]
    im = bpy.data.images.new("sheet", w, h, alpha=True)
    im.pixels = row.ravel()
    im.filepath_raw = out
    im.file_format = "PNG"
    im.save()


tmp = os.path.join(PREV, "_tmp")
os.makedirs(tmp, exist_ok=True)

set_pose(False)
for o in ruler_objs:
    o.hide_render = False
views = [("front", 0), ("side", 90), ("back", 180)]
a = [shoot(os.path.join(tmp, "a_%s.png" % n), y) for n, y in views]
a.append(shoot(os.path.join(tmp, "a_34.png"), -35, ortho=False, dist=7.0))
sheet(a, os.path.join(PREV, "blockout_apose.png"))

for o in ruler_objs:
    o.hide_render = True
set_pose(True)
g = [shoot(os.path.join(tmp, "g_%s.png" % n), y) for n, y in (("front", 0), ("side", 90))]
g.append(shoot(os.path.join(tmp, "g_34.png"), -35, ortho=False, dist=7.0))
g += [shoot(os.path.join(tmp, "s_%s.png" % n), y, silhouette=True) for n, y in (("front", 0), ("side", 90))]
sheet(g, os.path.join(PREV, "blockout_gamepose.png"))

# face-height close-up for the fringe / eye line
f = [shoot(os.path.join(tmp, "f_%s.png" % n), y, zc=1.6, scale=0.5) for n, y in (("front", 0), ("34", -35))]
sheet(f, os.path.join(PREV, "blockout_head.png"))

for p in os.listdir(tmp):
    os.remove(os.path.join(tmp, p))
os.rmdir(tmp)

set_pose(False)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Ryuta_Blockout.blend"))
print("[Blockout] done:", OUT)
