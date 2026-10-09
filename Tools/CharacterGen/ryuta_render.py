# Preview renders of the built Ryuta (run ryuta_build.py first): toon look close to the Unity shader
# (ramp, shadow tint, rim, inverted-hull outline), turnaround, face, expressions and the four weight-test poses.
# Run from the project root:
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b Art/Characters/Ryuta/Ryuta.blend --python Tools/CharacterGen/ryuta_render.py [-- only]
# `only` = comma list of sheets: turnaround, face, expressions, poses. Writes Art/Characters/Ryuta/previews/*.png.

import bpy, math, os, sys
import numpy as np
from mathutils import Vector as V, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Art", "Characters", "Ryuta", "previews")
TMP = os.path.join(OUT, "_tmp")
os.makedirs(TMP, exist_ok=True)
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ONLY = set(argv[0].split(",")) if argv else {"turnaround", "face", "expressions", "poses"}

scene = bpy.context.scene
ob = bpy.data.objects["Ryuta_Mesh"]
rig = bpy.data.objects["Ryuta_Rig"]
BG = np.array([0.86, 0.85, 0.83], np.float32)

# ------------------------------------------------------------ toon materials (preview approximation of ToonLit)
SHADOW = {"Ryuta_Body": (0.62, 0.60, 0.80), "Ryuta_Face": (0.93, 0.74, 0.74), "Ryuta_Hair": (0.55, 0.56, 0.78)}
SKIN_SHADOW = (0.93, 0.74, 0.74)   # body atlas alpha 0 = skin (neck, hands)


def toon(mat, shadow, clip):
    nt = mat.node_tree
    img = next(n.image for n in nt.nodes if n.type == "TEX_IMAGE")
    nt.nodes.clear()
    N = nt.nodes.new
    tex = N("ShaderNodeTexImage"); tex.image = img; tex.interpolation = "Linear"
    dif = N("ShaderNodeBsdfDiffuse")
    s2r = N("ShaderNodeShaderToRGB")
    ramp = N("ShaderNodeValToRGB"); ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].color = (0, 0, 0, 1)
    ramp.color_ramp.elements[1].position = 0.22
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    shade = N("ShaderNodeMix"); shade.data_type = "RGBA"; shade.blend_type = "MULTIPLY"
    shade.inputs[0].default_value = 1.0
    shade.inputs[7].default_value = (*shadow, 1)
    smix = N("ShaderNodeMix"); smix.data_type = "RGBA"
    smix.inputs[6].default_value = (*SKIN_SHADOW, 1)
    smix.inputs[7].default_value = (*shadow, 1)
    lit = N("ShaderNodeMix"); lit.data_type = "RGBA"
    lw = N("ShaderNodeLayerWeight"); lw.inputs[0].default_value = 0.35
    rimr = N("ShaderNodeValToRGB"); rimr.color_ramp.interpolation = "CONSTANT"
    rimr.color_ramp.elements[1].position = 0.7
    rimm = N("ShaderNodeMath"); rimm.operation = "MULTIPLY"; rimm.inputs[1].default_value = 0.12
    rim = N("ShaderNodeMix"); rim.data_type = "RGBA"; rim.blend_type = "ADD"
    rim.inputs[7].default_value = (0.75, 0.72, 1.0, 1)
    em = N("ShaderNodeEmission")
    outn = N("ShaderNodeOutputMaterial")
    L = nt.links.new
    L(dif.outputs[0], s2r.inputs[0]); L(s2r.outputs[0], ramp.inputs[0])
    L(tex.outputs["Color"], shade.inputs[6]); L(ramp.outputs[0], lit.inputs[0])
    L(shade.outputs[2], lit.inputs[6]); L(tex.outputs["Color"], lit.inputs[7])
    L(lw.outputs["Facing"], rimr.inputs[0]); L(rimr.outputs[0], rimm.inputs[0])
    L(rimm.outputs[0], rim.inputs[0])
    L(lit.outputs[2], rim.inputs[6]); L(rim.outputs[2], em.inputs[0])
    if not clip:
        L(tex.outputs["Alpha"], smix.inputs[0]); L(smix.outputs[2], shade.inputs[7])
    if clip:
        gt = N("ShaderNodeMath"); gt.operation = "GREATER_THAN"; gt.inputs[1].default_value = 0.5
        tr = N("ShaderNodeBsdfTransparent")
        mx = N("ShaderNodeMixShader")
        L(tex.outputs["Alpha"], gt.inputs[0]); L(gt.outputs[0], mx.inputs[0])
        L(tr.outputs[0], mx.inputs[1]); L(em.outputs[0], mx.inputs[2])
        L(mx.outputs[0], outn.inputs[0])
    else:
        L(em.outputs[0], outn.inputs[0])


for m in ob.data.materials:
    toon(m, SHADOW[m.name], m.name == "Ryuta_Face")
outline = bpy.data.materials.new("Ryuta_Outline_Preview")
outline.use_nodes = True
outline.use_backface_culling = True
nt = outline.node_tree
nt.nodes.clear()
e = nt.nodes.new("ShaderNodeEmission"); e.inputs[0].default_value = (0.04, 0.03, 0.05, 1)
o = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(e.outputs[0], o.inputs[0])
ob.data.materials.append(outline)
sol = ob.modifiers.new("Outline", "SOLIDIFY")
sol.thickness = 0.0032
sol.offset = 1.0
sol.use_flip_normals = True
sol.use_rim = False
sol.material_offset = 3
sol.vertex_group = "outline"
sol.thickness_vertex_group = 0.0

# ------------------------------------------------------------ light, render settings
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = eng
        break
    except TypeError:
        pass
scene.view_settings.view_transform = "Standard"
scene.render.film_transparent = True
world = bpy.data.worlds.new("W"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.6, 0.6, 0.65, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.25
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
scene.collection.objects.link(sun)
sun.data.energy = 3.2
sun.data.angle = math.radians(1.5)
sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
scene.collection.objects.link(cam)
scene.camera = cam


def look(pos, target, ortho=None, lens=85):
    cam.location = pos
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (V(target) - V(pos)).to_track_quat("-Z", "Y")
    cam.data.type = "ORTHO" if ortho else "PERSP"
    if ortho:
        cam.data.ortho_scale = ortho
    cam.data.lens = lens


def shot(name, w, h):
    scene.render.resolution_x, scene.render.resolution_y = w, h
    p = os.path.join(TMP, name + ".png")
    scene.render.filepath = p
    bpy.ops.render.render(write_still=True)
    return p


def sheet(paths, cols, out):
    imgs = []
    for p in paths:
        im = bpy.data.images.load(p)
        a = np.array(im.pixels[:], np.float32).reshape(im.size[1], im.size[0], 4)
        bpy.data.images.remove(im)
        a = a[:, :, :3] * a[:, :, 3:4] + BG * (1 - a[:, :, 3:4])
        imgs.append(a)
    rows = [np.concatenate(imgs[i:i + cols] + [np.ones_like(imgs[0]) * BG] * (cols - len(imgs[i:i + cols])), axis=1)
            for i in range(0, len(imgs), cols)]
    full = np.concatenate(rows[::-1], axis=0)
    h, w = full.shape[:2]
    im = bpy.data.images.new("sheet", w, h)
    rgba = np.concatenate([full, np.ones((h, w, 1), np.float32)], axis=2)
    im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw = os.path.join(OUT, out)
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)
    print("[RyutaRender] wrote", out)


# ------------------------------------------------------------ posing helpers
def reset_pose():
    for pb in rig.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
        pb.matrix_basis = Matrix()
    for k in ob.data.shape_keys.key_blocks:
        k.value = 0.0
    bpy.context.view_layer.update()


def rot(bone, axis, deg):
    """Rotate a bone around a world axis through its head."""
    pb = rig.pose.bones[bone]
    bpy.context.view_layer.update()
    M = pb.matrix.copy()
    piv = M.translation.copy()
    pb.matrix = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-piv) @ M
    bpy.context.view_layer.update()


def move(bone, d):
    pb = rig.pose.bones[bone]
    bpy.context.view_layer.update()
    pb.matrix = Matrix.Translation(V(d)) @ pb.matrix
    bpy.context.view_layer.update()


def point(bone, target):
    """Rotate a bone around its head so that it points at a world position."""
    pb = rig.pose.bones[bone]
    bpy.context.view_layer.update()
    cur, want = pb.tail - pb.head, V(target) - pb.head
    q = cur.rotation_difference(want)
    piv = pb.head.copy()
    pb.matrix = Matrix.Translation(piv) @ q.to_matrix().to_4x4() @ Matrix.Translation(-piv) @ pb.matrix
    bpy.context.view_layer.update()


def two_bone(upper, lower, end, pole):
    """Place upper + lower so the lower bone's tail reaches `end`, bending towards `pole` (analytic IK)."""
    bpy.context.view_layer.update()
    a = rig.pose.bones[upper].length
    b = rig.pose.bones[lower].length
    h = rig.pose.bones[upper].head.copy()
    end, pole = V(end), V(pole)
    d = end - h
    dist = min(d.length, (a + b) * 0.999)
    dn = d.normalized()
    x = (a * a - b * b + dist * dist) / (2 * dist)
    y = math.sqrt(max(a * a - x * x, 0.0))
    pn = pole - h
    pn = (pn - dn * pn.dot(dn)).normalized()
    knee = h + dn * x + pn * y
    point(upper, knee)
    point(lower, h + dn * dist)

X, Y, Z = V((1, 0, 0)), V((0, 1, 0)), V((0, 0, 1))


def pose_waistband():
    for b, d in (("Spine", 5), ("Chest", 4), ("UpperChest", 4), ("Neck", 8), ("Head", -9)):
        rot(b, X, d)
    rot("LeftUpperArm", Y, 44)
    rot("LeftLowerArm", X, -8)
    two_bone("RightUpperArm", "RightLowerArm", (-0.07, -0.118, 1.0), (-0.6, 0.35, 1.1))
    point("RightHand", (-0.05, -0.09, 0.82))
    rot("RightUpperLeg", Z, -4)


def pose_kick():
    for b, d in (("Spine", 10), ("Chest", 8), ("UpperChest", 4), ("Neck", -6), ("Head", -8)):
        rot(b, X, d)
    rot("RightUpperLeg", X, 42)
    rot("RightLowerLeg", X, 75)
    rot("RightFoot", X, 25)
    rot("LeftUpperLeg", X, -8)
    rot("LeftLowerLeg", X, 14)
    rot("LeftUpperArm", Y, 40)
    rot("LeftUpperArm", X, -30)
    rot("LeftLowerArm", X, -25)
    rot("RightUpperArm", Y, -40)
    rot("RightUpperArm", X, 25)


def pose_slouch():
    for b, d in (("Spine", 9), ("Chest", 8), ("UpperChest", 8), ("Neck", 14), ("Head", -18)):
        rot(b, X, d)
    rot("LeftShoulder", Z, -14)
    rot("RightShoulder", Z, 14)
    rot("LeftUpperArm", Y, 48)
    rot("RightUpperArm", Y, -48)
    for s in ("Left", "Right"):
        rot(s + "LowerArm", X, -10)
        rot(s + "UpperLeg", X, -6)
        rot(s + "LowerLeg", X, 12)


def pose_squat():
    move("Hips", (0, 0.10, -0.43))
    rot("Hips", X, 18)
    for b, d in (("Spine", 12), ("Chest", 10), ("UpperChest", 6), ("Neck", 4), ("Head", -32)):
        rot(b, X, d)
    for s, side in ((1, "Left"), (-1, "Right")):
        two_bone(side + "UpperLeg", side + "LowerLeg", (0.17 * s, 0.02, 0.085), (0.4 * s, -0.8, 0.9))
        point(side + "Foot", (0.21 * s, -0.09, 0.022))
        two_bone(side + "UpperArm", side + "LowerArm", (0.2 * s, -0.40, 0.56), (0.6 * s, 0.4, 1.0))
        point(side + "Hand", (0.18 * s, -0.46, 0.46))


POSES = [("waistband", pose_waistband), ("kick", pose_kick), ("slouch", pose_slouch), ("squat", pose_squat)]

# ------------------------------------------------------------ sheets
if "turnaround" in ONLY:
    reset_pose()
    paths = []
    for name, yaw in (("front", 0), ("side", 90), ("back", 180)):
        a = math.radians(yaw)
        look(V((math.sin(a) * 6, -math.cos(a) * 6, 0.9)), V((0, 0, 0.9)), ortho=1.95)
        paths.append(shot("t_" + name, 560, 1050))
    look(V((-3.4, -5.2, 1.6)), V((0, 0, 0.88)), lens=60)
    paths.append(shot("t_34", 560, 1050))
    sheet(paths, 4, "turnaround.png")

if "face" in ONLY:
    reset_pose()
    look(V((0, -1.2, 1.635)), V((0, 0, 1.635)), lens=85)
    p1 = shot("f_front", 700, 760)
    look(V((-0.75, -0.95, 1.66)), V((0, 0, 1.63)), lens=85)
    p2 = shot("f_34", 700, 760)
    look(V((-1.25, -0.02, 1.64)), V((0, 0, 1.63)), lens=85)
    p3 = shot("f_side", 700, 760)
    sheet([p1, p2, p3], 3, "face.png")

if "expressions" in ONLY:
    reset_pose()
    look(V((0, -0.9, 1.6)), V((0, 0, 1.6)), lens=110)
    paths = [shot("e_neutral", 380, 380)]
    for k in ob.data.shape_keys.key_blocks[1:]:
        k.value = 1.0
        paths.append(shot("e_" + k.name, 380, 380))
        k.value = 0.0
    sheet(paths, 6, "expressions.png")

if "poses" in ONLY:
    paths = []
    for name, fn in POSES:
        reset_pose()
        fn()
        look(V((-3.0, -4.6, 1.2)), V((0, 0, 0.8)), lens=55)
        paths.append(shot("p_%s_34" % name, 520, 820))
    for name, fn in POSES:
        reset_pose()
        fn()
        look(V((6, 0, 0.9)), V((0, 0, 0.9)), ortho=2.0)
        paths.append(shot("p_%s_side" % name, 520, 820))
    sheet(paths, 4, "poses.png")
    reset_pose()

for f in os.listdir(TMP):
    os.remove(os.path.join(TMP, f))
os.rmdir(TMP)
print("[RyutaRender] done")
