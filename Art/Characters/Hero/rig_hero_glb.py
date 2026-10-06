# Rigs the Tripo-generated hero (Assets/MainMenu/Models/Characters/anime+character+3d+model.glb) for Unity:
# scales to 1.8 m, decimates for weak PCs, adds a Mixamo-named skeleton fitted to the sculpted pose
# (right arm down, left hand on the belt) and skins it by distance to the bones.
# Run: blender -b --python rig_hero_glb.py -- <in.glb> <out.fbx> [target_faces]
import bpy, sys, math
from mathutils import Vector
from mathutils.geometry import intersect_point_line

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = args[0], args[1]
TARGET_FACES = int(args[2]) if len(args) > 2 else 30000
HEIGHT = 1.8

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
body = next(o for o in bpy.data.objects if o.type == "MESH")
for o in list(bpy.data.objects):
    if o is not body:
        bpy.data.objects.remove(o)
body.name = "Hero"
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

zmin = min(v.co.z for v in body.data.vertices)
zmax = max(v.co.z for v in body.data.vertices)
S = HEIGHT / (zmax - zmin)
for v in body.data.vertices:
    v.co.z -= zmin
    v.co *= S

faces = len(body.data.polygons)
if faces > TARGET_FACES:
    mod = body.modifiers.new("decimate", "DECIMATE")
    mod.ratio = TARGET_FACES / faces
    bpy.ops.object.modifier_apply(modifier=mod.name)
print("FACES", faces, "->", len(body.data.polygons), "VERTS", len(body.data.vertices))

# ------------------------------------------------------------ skeleton (unit-height coords, faces -Y, left = +X)
J = {  # name: (head, tail, parent)
    "Hips": ((0, 0.03, 0.53), (0, 0.03, 0.60), None),
    "Spine": ((0, 0.03, 0.60), (0, 0.03, 0.67), "Hips"),
    "Spine1": ((0, 0.03, 0.67), (0, 0.03, 0.74), "Spine"),
    "Spine2": ((0, 0.03, 0.74), (0, 0.03, 0.82), "Spine1"),
    "Neck": ((0, 0.03, 0.82), (0, 0.02, 0.87), "Spine2"),
    "Head": ((0, 0.02, 0.87), (0, 0.02, 0.99), "Neck"),
    "HeadTop_End": ((0, 0.02, 0.99), (0, 0.02, 1.04), "Head"),
    "LeftShoulder": ((0.02, 0.03, 0.80), (0.10, 0.03, 0.80), "Spine2"),
    "LeftArm": ((0.10, 0.03, 0.80), (0.125, 0.04, 0.645), "LeftShoulder"),
    "LeftForeArm": ((0.125, 0.04, 0.645), (0.045, -0.05, 0.625), "LeftArm"),
    "LeftHand": ((0.045, -0.05, 0.625), (0.0, -0.06, 0.62), "LeftForeArm"),
    "RightShoulder": ((-0.02, 0.03, 0.80), (-0.10, 0.03, 0.80), "Spine2"),
    "RightArm": ((-0.10, 0.03, 0.80), (-0.14, 0.03, 0.645), "RightShoulder"),
    "RightForeArm": ((-0.14, 0.03, 0.645), (-0.135, 0.02, 0.50), "RightArm"),
    "RightHand": ((-0.135, 0.02, 0.50), (-0.13, 0.0, 0.43), "RightForeArm"),
    "LeftUpLeg": ((0.055, 0.03, 0.52), (0.08, 0.03, 0.29), "Hips"),
    "LeftLeg": ((0.08, 0.03, 0.29), (0.115, 0.04, 0.07), "LeftUpLeg"),
    "LeftFoot": ((0.115, 0.04, 0.07), (0.125, -0.04, 0.02), "LeftLeg"),
    "LeftToeBase": ((0.125, -0.04, 0.02), (0.125, -0.11, 0.02), "LeftFoot"),
    "RightUpLeg": ((-0.05, 0.03, 0.52), (-0.06, 0.03, 0.29), "Hips"),
    "RightLeg": ((-0.06, 0.03, 0.29), (-0.09, 0.04, 0.07), "RightUpLeg"),
    "RightFoot": ((-0.09, 0.04, 0.07), (-0.10, -0.04, 0.02), "RightLeg"),
    "RightToeBase": ((-0.10, -0.04, 0.02), (-0.10, -0.11, 0.02), "RightFoot"),
}
P = lambda t: Vector(t) * HEIGHT

bpy.ops.object.select_all(action="DESELECT")
bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
arm = bpy.context.active_object
arm.name = "Armature"
eb = arm.data.edit_bones
eb.remove(eb[0])
for name, (h, t, parent) in J.items():
    b = eb.new("mixamorig:" + name)
    b.head, b.tail = P(h), P(t)
    if parent:
        b.parent = eb["mixamorig:" + parent]
bpy.ops.object.mode_set(mode="OBJECT")

# ------------------------------------------------------------ skinning: two nearest bones, inverse distance^4
# Each vertex may only follow bones of its own body region, so legs never pull the coat and the
# hanging arm never drags the hip. The left hand rests on the belly, so belly vertices stay on the spine.
SKIP = {"HeadTop_End", "LeftToeBase", "RightToeBase", "LeftShoulder", "RightShoulder"}
segs = {n: (P(h), P(t)) for n, (h, t, _) in J.items() if n not in SKIP}
LEG_TOP = 0.50 * HEIGHT

def candidates(co):
    names = []
    for n in segs:
        if ("UpLeg" in n or n.endswith("Leg") or "Foot" in n) and co.z > LEG_TOP + 0.04 * HEIGHT:
            continue
        if n in ("Head", "Neck") and co.z < 0.78 * HEIGHT:
            continue
        if n.startswith("Left") and ("Arm" in n or "Hand" in n) and co.x < 0.0:
            continue
        if n.startswith("Right") and ("Arm" in n or "Hand" in n) and co.x > -0.07 * HEIGHT:
            continue
        if n in ("LeftHand", "LeftForeArm") and co.y > -0.035 * HEIGHT and abs(co.x) < 0.09 * HEIGHT:
            continue  # the belly under the resting hand
        if n in ARM_REACH and dist(co, segs[n]) > ARM_REACH[n] * HEIGHT:
            continue  # the coat around the arm stays with the body
        names.append(n)
    return names

# Sleeves are thin: beyond this distance from the bone a vertex belongs to the coat, not the arm.
ARM_REACH = {"LeftArm": 0.055, "RightArm": 0.055, "LeftForeArm": 0.04, "RightForeArm": 0.05,
             "LeftHand": 0.045, "RightHand": 0.07}

def dist(co, seg):
    a, b = seg
    p, t = intersect_point_line(co, a, b)
    t = max(0.0, min(1.0, t))
    return (co - (a + (b - a) * t)).length

groups = {n: body.vertex_groups.new(name="mixamorig:" + n) for n in segs}
for v in body.data.vertices:
    ds = sorted((dist(v.co, segs[n]), n) for n in candidates(v.co))[:2]
    ws = [1.0 / max(d, 1e-4) ** 4 for d, _ in ds]
    total = sum(ws)
    for (d, n), w in zip(ds, ws):
        groups[n].add([v.index], w / total, "REPLACE")

body.parent = arm
body.modifiers.new("Armature", "ARMATURE").object = arm

# Textures go next to the FBX as PNGs; Unity builds a URP material from them (HeroImportSetup).
import os
tex_dir = os.path.abspath(os.path.join(os.path.dirname(OUT), "Textures"))
os.makedirs(tex_dir, exist_ok=True)
for img in bpy.data.images:
    if img.size[0] == 0 or img.name.endswith("_rm"):  # roughness/metal unused: smoothness is set on the material
        continue
    img.filepath_raw = os.path.join(tex_dir, img.name.replace("anime_character_3d_model", "Hero") + ".png")
    img.file_format = "PNG"
    img.save()
    print("TEX", img.filepath_raw)

bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={"ARMATURE", "MESH"},
                         add_leaf_bones=False, bake_anim=False, path_mode="STRIP",
                         apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
print("DONE", OUT)
