# Builds the slice NPCs: low-poly PS2-style humanoids (same recipe as Tomura/build_tomura.py --game),
# rigidly skinned to a Mixamo-named skeleton so the project's Mixamo clips play through Unity's Humanoid avatar.
# Run: blender -b --python build_npcs.py -- <fbx_out_dir> <preview_png> [Name ...]
# Every character is one FBX (<Name>.fbx). Materials are named after the shared PALETTE keys, so all FBXs
# share the same Unity materials (NpcModelsSetup remaps them by name).
import bpy, bmesh, math, sys, os, random
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:]
OUT, PREVIEW = ARGS[0], ARGS[1]
ONLY = set(ARGS[2:])
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ------------------------------------------------------------ shared palette (sRGB)
PALETTE = {
    "Skin": (0.95, 0.79, 0.69), "SkinTan": (0.86, 0.66, 0.52),
    "Eyes": (0.12, 0.1, 0.14), "EyeWhite": (0.97, 0.97, 0.97), "Mouth": (0.62, 0.27, 0.25),
    "HairBlack": (0.08, 0.08, 0.1), "HairBrown": (0.35, 0.22, 0.13), "HairBleach": (0.93, 0.82, 0.5),
    "HairChestnut": (0.55, 0.3, 0.18),
    "Gakuran": (0.1, 0.105, 0.14), "Gold": (0.83, 0.66, 0.23), "ShirtWhite": (0.95, 0.95, 0.94),
    "Sailor": (0.12, 0.15, 0.3), "Ribbon": (0.8, 0.12, 0.15), "Skirt": (0.13, 0.16, 0.3),
    "Socks": (0.13, 0.15, 0.25), "SocksWhite": (0.93, 0.93, 0.9),
    "Track": (0.72, 0.1, 0.1), "TrackStripe": (0.96, 0.96, 0.96), "TrackPants": (0.12, 0.14, 0.3),
    "GymShirt": (0.96, 0.96, 0.95), "GymShorts": (0.15, 0.2, 0.55), "Headband": (0.97, 0.97, 0.97),
    "Uwabaki": (0.95, 0.95, 0.92), "UwabakiTip": (0.25, 0.4, 0.75), "Sneakers": (0.9, 0.9, 0.88),
    "Shoes": (0.16, 0.13, 0.11), "Glasses": (0.12, 0.12, 0.12), "Mask": (0.96, 0.96, 0.96),
    "Clip": (0.98, 0.55, 0.7), "Bag": (0.25, 0.18, 0.12), "Books": (0.2, 0.45, 0.3),
}
MATS = {}
for name, rgb in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    lin = tuple(c ** 2.2 for c in rgb)
    bsdf.inputs["Base Color"].default_value = (*lin, 1)
    bsdf.inputs["Roughness"].default_value = 0.85
    if name == "Gold":
        bsdf.inputs["Metallic"].default_value = 0.8
        bsdf.inputs["Roughness"].default_value = 0.35
    m.diffuse_color = (*lin, 1)
    MATS[name] = m

# ------------------------------------------------------------ the cast
# scale: (x, y, z) applied to mesh and skeleton. top / bottom / hair / extras pick the parts below.
CAST = [
    dict(name="NPC_Toyoda", scale=(1.14, 1.12, 1.0), skin="SkinTan", top="track", bottom="trackpants",
         shoes="Sneakers", hair="buzz", hair_mat="HairBlack", jaw=0.18, brows="thick", mouth="whistle",
         extras={"belly"}),
    dict(name="NPC_Joker", scale=(0.95, 0.95, 0.95), skin="Skin", top="gakuran_open", bottom="pants",
         shoes="Uwabaki", hair="messy", hair_mat="HairChestnut", brows="up", mouth="grin", extras=set()),
    dict(name="NPC_Nerd", scale=(0.9, 0.9, 0.93), skin="Skin", top="gakuran", bottom="pants",
         shoes="Uwabaki", hair="neat", hair_mat="HairBlack", brows="worried", mouth="flat", extras={"glasses", "bag"}),
    dict(name="NPC_Jock", scale=(1.0, 1.0, 0.97), skin="SkinTan", top="gym", bottom="shorts",
         shoes="Sneakers", hair="buzz", hair_mat="HairBlack", brows="up", mouth="grin", extras={"headband"}),
    dict(name="NPC_Yankee", scale=(0.98, 0.98, 0.97), skin="Skin", top="gakuran_open", bottom="baggy",
         shoes="Shoes", hair="pompadour", hair_mat="HairBleach", brows="angry", mouth="flat", extras={"mask"}),
    dict(name="NPC_Gossip_A", scale=(0.86, 0.88, 0.9), skin="Skin", top="sailor", bottom="skirt",
         shoes="Uwabaki", hair="twintails", hair_mat="HairBrown", brows="up", mouth="smile", female=True,
         extras={"clips"}),
    dict(name="NPC_Gossip_B", scale=(0.86, 0.88, 0.89), skin="Skin", top="sailor", bottom="skirt",
         shoes="Uwabaki", hair="bob", hair_mat="HairBlack", brows="up", mouth="smile", female=True,
         extras={"clips"}),
]

# ------------------------------------------------------------ mesh helpers (canonical figure: ~1.85 m, faces -Y, left = +X)
parts = []
BONE = None

def finish(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(MATS[mat])
    for p in obj.data.polygons:
        p.use_smooth = False
    bpy.context.view_layer.update()
    for i, v in enumerate(obj.data.vertices):
        name = BONE(obj.matrix_world @ v.co) if callable(BONE) else BONE
        group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
        group.add([i], 1.0, "REPLACE")
    parts.append(obj)
    return obj

def active():
    return bpy.context.active_object

def side_name(s):
    return "Left" if s > 0 else "Right"

def torso_bone(co):
    if co.z < 1.0:
        return "mixamorig:Hips"
    if co.z < 1.17:
        return "mixamorig:Spine"
    if co.z < 1.32:
        return "mixamorig:Spine1"
    return "mixamorig:Spine2"

def cone(r1, r2, depth, loc, verts=8, mat="Gakuran", rot=(0, 0, 0), fill="NGON"):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot,
                                    end_fill_type=fill)
    return finish(active(), mat)

def limb(p1, p2, r1, r2, mat, verts=8):
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

def tube(r1, r2, depth, loc, mat, verts=12, open_front=0.0, thick=0.012):
    """Open-ended flared tube (jackets, skirts); open_front > 0 cuts a gap of that half-width at the front."""
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, end_fill_type="NOTHING")
    o = finish(active(), mat)
    if open_front:
        delete_faces(o, lambda f: f.calc_center_median().y < -0.15 and abs(f.calc_center_median().x) < open_front)
    o.modifiers.new("thick", "SOLIDIFY").thickness = thick
    return o

# ------------------------------------------------------------ body parts
def legs(c):
    bottom, shoes, skin = c["bottom"], c["shoes"], c["skin"]
    for s in (-1, 1):
        x = 0.1 * s
        up_mat, low_mat = {"pants": ("Gakuran", "Gakuran"), "baggy": ("Gakuran", "Gakuran"),
                           "trackpants": ("TrackPants", "TrackPants"), "shorts": (skin, skin),
                           "skirt": (skin, "Socks")}[bottom]
        wide = 1.25 if bottom == "baggy" else 1.0
        slim = 0.82 if c.get("female") else 1.0
        global BONE
        BONE = f"mixamorig:{side_name(s)}UpLeg"
        limb((x, 0, 0.45), (x * 0.95, 0, 0.9), 0.095 * wide * slim, 0.105 * slim, up_mat)
        if bottom == "shorts":
            limb((x, 0, 0.72), (x * 0.95, 0, 0.92), 0.11, 0.115, "GymShorts")
        BONE = f"mixamorig:{side_name(s)}Leg"
        limb((x, 0, 0.07), (x, 0, 0.5), 0.085 * wide * slim, 0.095 * slim * (1.15 if bottom == "baggy" else 1), low_mat)
        if bottom == "shorts":
            limb((x, 0, 0.07), (x, 0, 0.3), 0.075, 0.08, "SocksWhite")
        if bottom == "trackpants":
            box((0.012, 0.012, 0.42), (x * 1.0 + 0.09 * s, 0, 0.28), "TrackStripe")
        BONE = f"mixamorig:{side_name(s)}Foot"
        box((0.11, 0.25, 0.07), (x, -0.045, 0.035), shoes if shoes != "Uwabaki" else "Uwabaki", bevel=0.02)
        if shoes == "Uwabaki":
            box((0.112, 0.07, 0.072), (x, -0.15, 0.036), "UwabakiTip")
        if shoes == "Sneakers":
            box((0.114, 0.252, 0.025), (x, -0.045, 0.012), "Shoes")

def torso(c):
    global BONE
    BONE = torso_bone
    top, female = c["top"], c.get("female")
    shoulder = 0.42 if female else 0.5
    if c["bottom"] == "skirt":
        tube(0.17, 0.27, 0.36, (0, 0, 0.8), "Skirt", verts=14, thick=0.01)  # pleated feel from the low vert count
        cone(0.17, 0.165, 0.1, (0, 0, 0.95), verts=10, mat="Skirt")
    else:
        waist = {"trackpants": "TrackPants", "shorts": "GymShorts"}.get(c["bottom"], "Gakuran")
        cone(0.17, 0.165, 0.1, (0, 0, 0.92), verts=10, mat=waist)
    if "belly" in c["extras"]:
        blob((0.17, 0.12, 0.15), (0, -0.07, 1.08), "Track", seg=10, rings=6)

    if top == "gakuran":          # buttoned to the throat
        cone(0.19, 0.22, 0.6, (0, 0, 1.15), verts=10, mat="Gakuran")
        box((shoulder, 0.26, 0.07), (0, 0, 1.45), "Gakuran", bevel=0.03)
        bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.08, depth=0.1, location=(0, 0.01, 1.53))
        finish(active(), "Gakuran")
        for i in range(5):
            blob((0.012, 0.008, 0.012), (0, -0.215, 1.42 - i * 0.11), "Gold", seg=6, rings=4)
    elif top == "gakuran_open":   # white shirt under an open jacket
        cone(0.17, 0.21, 0.56, (0, 0, 1.17), verts=10, mat="ShirtWhite")
        tube(0.215, 0.235, 0.66, (0, 0, 1.13), "Gakuran", open_front=0.09)
        box((shoulder, 0.26, 0.07), (0, 0, 1.45), "Gakuran", bevel=0.03)
        for i in range(4):
            blob((0.012, 0.008, 0.012), (0.122, -0.205, 1.38 - i * 0.12), "Gold", seg=6, rings=4)
    elif top == "track":          # tracksuit jacket zipped up, white stripes on the shoulders
        cone(0.2, 0.23, 0.62, (0, 0, 1.15), verts=10, mat="Track")
        box((shoulder, 0.28, 0.08), (0, 0, 1.45), "Track", bevel=0.03)
        box((0.012, 0.01, 0.55), (0, -0.226, 1.16), "TrackStripe")          # zip
        bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.085, depth=0.08, location=(0, 0.01, 1.52))
        finish(active(), "Track")
    elif top == "gym":            # gym t-shirt, name tag on the chest
        cone(0.18, 0.21, 0.58, (0, 0, 1.16), verts=10, mat="GymShirt")
        box((shoulder, 0.26, 0.07), (0, 0, 1.45), "GymShirt", bevel=0.03)
        box((0.09, 0.01, 0.05), (0.08, -0.205, 1.32), "GymShorts")
    elif top == "sailor":         # sailor blouse with the big collar and a red ribbon
        cone(0.16, 0.19, 0.56, (0, 0, 1.17), verts=10, mat="ShirtWhite")
        box((shoulder, 0.24, 0.07), (0, 0, 1.45), "ShirtWhite", bevel=0.03)
        box((0.34, 0.03, 0.2), (0, 0.13, 1.38), "Sailor")                    # collar flap on the back
        box((0.35, 0.005, 0.02), (0, 0.147, 1.3), "ShirtWhite")              # collar stripe
        for s in (-1, 1):                                                     # collar V on the front
            box((0.03, 0.02, 0.2), (0.06 * s, -0.17, 1.37), "Sailor", rot=(0, math.radians(25 * s), 0))
        BONE = "mixamorig:Spine2"
        for s in (-1, 1):
            box((0.06, 0.03, 0.045), (0.035 * s, -0.2, 1.33), "Ribbon", rot=(0, math.radians(-15 * s), 0))
        box((0.03, 0.03, 0.03), (0, -0.205, 1.33), "Ribbon")
        box((0.035, 0.02, 0.09), (0, -0.2, 1.27), "Ribbon")
        BONE = torso_bone

    if "bag" in c["extras"]:      # heavy randoseru-like satchel
        box((0.28, 0.12, 0.32), (0, 0.24, 1.2), "Bag", bevel=0.02)

def arms(c):
    global BONE
    top = c["top"]
    sleeve = {"gakuran": "Gakuran", "gakuran_open": "Gakuran", "track": "Track", "gym": "GymShirt", "sailor": "ShirtWhite"}[top]
    lower = c["skin"] if top == "gym" else sleeve
    f = 0.85 if c.get("female") else 1.0
    for s in (-1, 1):
        S, E, W = (0.25 * s * f, 0, 1.42), (0.31 * s * f, 0.01, 1.16), (0.36 * s * f, -0.01, 0.92)
        BONE = f"mixamorig:{side_name(s)}Arm"
        limb(S, E, 0.068 * f, 0.058 * f, sleeve)
        BONE = f"mixamorig:{side_name(s)}ForeArm"
        limb(E, W, 0.058 * f, 0.048 * f, lower)
        if top == "track":
            limb((S[0] + 0.05 * s, 0.0, 1.4), (E[0] + 0.05 * s, 0.01, 1.16), 0.012, 0.012, "TrackStripe", verts=4)
        if top == "sailor":
            limb((W[0] - 0.006 * s, 0, 0.97), W, 0.05 * f, 0.05 * f, "Sailor")   # cuffs
        BONE = f"mixamorig:{side_name(s)}Hand"
        blob((0.042 * f, 0.03 * f, 0.06 * f), (0.365 * s * f, -0.015, 0.87), c["skin"], seg=6, rings=4)
    if "bag" in c["extras"]:      # books under the left arm
        BONE = "mixamorig:LeftForeArm"
        box((0.05, 0.2, 0.26), (0.38, -0.02, 1.02), "Books", rot=(0, math.radians(8), 0))

# ------------------------------------------------------------ head
HEAD = Vector((0, -0.01, 1.73)); A, B, C = 0.125, 0.135, 0.16

def head(c):
    global BONE
    jawk = c.get("jaw", 0.38)
    female = c.get("female")
    def jaw(z):
        return 1.0 - jawk * max(0.0, -z / C)
    def face_y(x, z, lift=0.004):
        lz = z - HEAD.z
        x0 = x / jaw(lz)
        k = 1 - (x0 / A) ** 2 - (lz / C) ** 2
        y = HEAD.y - B * math.sqrt(max(k, 0.0))
        if lz < 0:
            y -= 0.03 * (-lz / C)
        return y - lift

    BONE = "mixamorig:Neck"
    limb((0, 0, 1.47), (0, -0.005, 1.62), 0.062 * (1.25 if c["name"] == "NPC_Toyoda" else 1), 0.058, c["skin"])
    BONE = "mixamorig:Head"
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=9, radius=1, location=HEAD)
    h = active(); h.scale = (A, B, C); bpy.ops.object.transform_apply(scale=True)
    for v in h.data.vertices:
        lz = v.co.z - HEAD.z
        v.co.x *= jaw(lz)
        if lz < 0:
            v.co.y -= 0.03 * (-lz / C)
    finish(h, c["skin"])

    for s in (-1, 1):
        blob((0.022, 0.03, 0.04), (0.128 * s, HEAD.y + 0.01, 1.72), c["skin"], seg=6, rings=4)
        ex, ez = 0.048 * s, 1.735
        eh = 0.03 if female else 0.022                          # anime eyes: big and dark, a white glint
        box((0.034, 0.008, eh), (ex, face_y(ex, ez), ez), "Eyes")
        box((0.01, 0.006, 0.01), (ex + 0.008, face_y(ex + 0.008, ez + 0.006, 0.009), ez + 0.006), "EyeWhite")
        if female:
            box((0.042, 0.008, 0.006), (ex + 0.004 * s, face_y(ex, ez + 0.017, 0.006), ez + 0.017), "Eyes",
                rot=(0, math.radians(-8 * s), 0))           # lashes
        brow = {"thick": (0.05, 0.016, -10), "up": (0.04, 0.008, 8), "worried": (0.04, 0.008, 18),
                "angry": (0.045, 0.01, -22)}[c["brows"]]
        bz = ez + (0.034 if female else 0.03)
        box((brow[0], 0.008, brow[1]), (ex, face_y(ex, bz), bz), c["hair_mat"], rot=(0, math.radians(brow[2] * s), 0))
    box((0.012, 0.03, 0.025), (0, face_y(0, 1.7, 0.012), 1.7), c["skin"])          # nose

    m = c["mouth"]
    if m == "grin":
        box((0.05, 0.007, 0.012), (0, face_y(0, 1.655), 1.655), "Mouth")
        box((0.044, 0.006, 0.005), (0, face_y(0, 1.659, 0.007), 1.659), "EyeWhite")
    elif m == "smile":
        for s in (-1, 1):
            box((0.018, 0.007, 0.006), (0.009 * s, face_y(0.009 * s, 1.656), 1.656), "Mouth", rot=(0, math.radians(15 * s), 0))
    elif m == "flat":
        box((0.03, 0.007, 0.006), (0, face_y(0, 1.655), 1.655), "Mouth")
    elif m == "whistle":   # lips pursed round the (separate, Unity-side) whistle
        box((0.022, 0.01, 0.016), (0, face_y(0, 1.655), 1.655), "Mouth")

    ex = c["extras"]
    if "glasses" in ex:
        for s in (-1, 1):
            x = 0.048 * s
            y = face_y(x, 1.735, 0.016)
            box((0.05, 0.006, 0.006), (x, y, 1.757), "Glasses")
            box((0.05, 0.006, 0.006), (x, y, 1.713), "Glasses")
            box((0.006, 0.006, 0.05), (x + 0.025, y, 1.735), "Glasses")
            box((0.006, 0.006, 0.05), (x - 0.025, y, 1.735), "Glasses")
            limb((0.073 * s, y + 0.01, 1.745), (0.125 * s, HEAD.y + 0.02, 1.74), 0.004, 0.004, "Glasses", verts=4)
        box((0.02, 0.006, 0.006), (0, face_y(0, 1.745, 0.018), 1.745), "Glasses")
    if "mask" in ex:
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=1, location=(0, HEAD.y - 0.02, 1.665))
        mk = active(); mk.scale = (0.1, 0.13, 0.05); bpy.ops.object.transform_apply(scale=True)
        finish(mk, "Mask")
        delete_faces(mk, lambda f: f.calc_center_median().y > HEAD.y - 0.04)
        for s in (-1, 1):
            limb((0.085 * s, HEAD.y - 0.06, 1.68), (0.125 * s, HEAD.y + 0.01, 1.71), 0.003, 0.003, "Mask", verts=4)
    hair(c, face_y)

# ------------------------------------------------------------ hair
def hair(c, face_y):
    style, mat = c["hair"], c["hair_mat"]
    rnd = random.Random(hash(c["name"]) & 0xffff)
    capc = Vector((0, HEAD.y + 0.012, HEAD.z + 0.035))

    def cap(sx=1.1, sy=1.1, sz=0.95, front_cut=0.03, back_cut=-0.07, side_cut=None):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=1, location=capc)
        o = active(); o.scale = (A * sx, B * sy, C * sz); bpy.ops.object.transform_apply(scale=True)
        finish(o, mat)
        def cut(f):
            p = f.calc_center_median()
            lz = p.z - HEAD.z
            if p.y < HEAD.y - 0.06:
                return lz < front_cut
            if side_cut is not None and p.y < HEAD.y + 0.02:
                return lz < side_cut
            return lz < back_cut
        delete_faces(o, cut)
        return o

    def spike(az, el, ln, bias, r=0.045):
        a, e = math.radians(az), math.radians(el)
        n = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        base = capc + Vector((n.x * A, n.y * B, n.z * C)) * 0.9
        d = (n + Vector(bias)).normalized()
        limb(base, base + d * ln, r, 0.004, mat, verts=4)

    if style == "buzz":
        cap(1.04, 1.04, 0.92, front_cut=0.05, back_cut=-0.06)
    elif style == "messy":
        cap()
        for el in (25, 45, 65):
            for az in range(-150, 181, 30):
                if abs(az) < 40 and el == 25:
                    continue
                spike(az + rnd.uniform(-12, 12), el + rnd.uniform(-6, 6), rnd.uniform(0.06, 0.11), (0, 0.3, 0.5))
        for az in (-28, -8, 14, 34):
            spike(az, 30, rnd.uniform(0.06, 0.08), (0, -0.2, -0.75), r=0.035)
    elif style == "neat":       # flat, side-parted, the fringe combed to one side
        cap(1.08, 1.08, 0.95, front_cut=0.035, back_cut=-0.09)
        for i, az in enumerate((-40, -22, -5, 12)):
            spike(az, 34, 0.07, (0.5, -0.1, -0.7), r=0.04)
    elif style == "pompadour":  # bleached quiff sticking forward
        cap(1.06, 1.06, 0.93, front_cut=0.05, back_cut=-0.08)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=10, ring_count=6, radius=1,
                                             location=(0, HEAD.y - 0.11, HEAD.z + 0.13))
        q = active(); q.scale = (0.1, 0.12, 0.065); q.rotation_euler = (math.radians(-20), 0, 0)
        bpy.ops.object.transform_apply(scale=True, rotation=True)
        finish(q, mat)
    elif style in ("bob", "twintails"):
        cap(1.16, 1.14, 1.0, front_cut=0.04, back_cut=-0.16, side_cut=-0.12)
        for az in (-36, -18, 0, 18, 36):                       # straight fringe
            spike(az, 32, 0.07, (0, -0.25, -0.85), r=0.04)
        for s in (-1, 1):                                       # side locks framing the face
            limb((0.12 * s, HEAD.y - 0.04, 1.78), (0.125 * s, HEAD.y - 0.05, 1.6), 0.035, 0.02, mat, verts=5)
        if style == "twintails":
            for s in (-1, 1):
                root = Vector((0.13 * s, HEAD.y + 0.04, 1.8))
                blob((0.03, 0.03, 0.03), root, "Clip", seg=6, rings=4)
                limb(root, root + Vector((0.06 * s, 0.05, -0.3)), 0.055, 0.012, mat, verts=6)
        else:
            limb((0, HEAD.y + 0.1, 1.72), (0, HEAD.y + 0.13, 1.6), 0.1, 0.075, mat, verts=8)   # back of the bob
    if "clips" in c["extras"] and style != "twintails":
        box((0.035, 0.012, 0.012), (0.075, face_y(0.075, 1.81, 0.012), 1.81), "Clip", rot=(0, math.radians(-25), 0))
    if "headband" in c["extras"]:
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=1, depth=0.035, location=(0, HEAD.y, HEAD.z + 0.07),
                                            end_fill_type="NOTHING")
        hb = active(); hb.scale = (A * 1.08, B * 1.08, 1); bpy.ops.object.transform_apply(scale=True)
        finish(hb, "Headband")
        hb.modifiers.new("thick", "SOLIDIFY").thickness = 0.008
        for s in (-1, 1):                                        # knot tails at the back
            limb((0, HEAD.y + B, HEAD.z + 0.07), (0.05 * s, HEAD.y + B + 0.05, HEAD.z - 0.04), 0.015, 0.008, "Headband", verts=4)

# ------------------------------------------------------------ skeleton (Mixamo names, joints match the parts)
JOINTS = {
    "Hips": ((0, 0, 0.95), (0, 0, 1.05), None),
    "Spine": ((0, 0, 1.05), (0, 0, 1.2), "Hips"),
    "Spine1": ((0, 0, 1.2), (0, 0, 1.33), "Spine"),
    "Spine2": ((0, 0, 1.33), (0, 0, 1.47), "Spine1"),
    "Neck": ((0, 0, 1.47), (0, -0.005, 1.6), "Spine2"),
    "Head": ((0, -0.005, 1.6), (0, -0.01, 1.85), "Neck"),
    "HeadTop_End": ((0, -0.01, 1.85), (0, -0.01, 1.95), "Head"),
}

def joints(c):
    j = dict(JOINTS)
    f = 0.85 if c.get("female") else 1.0
    for s in (-1, 1):
        n = side_name(s)
        j[n + "Shoulder"] = ((0.06 * s * f, 0, 1.43), (0.25 * s * f, 0, 1.42), "Spine2")
        j[n + "Arm"] = ((0.25 * s * f, 0, 1.42), (0.31 * s * f, 0.01, 1.16), n + "Shoulder")
        j[n + "ForeArm"] = ((0.31 * s * f, 0.01, 1.16), (0.36 * s * f, -0.01, 0.92), n + "Arm")
        j[n + "Hand"] = ((0.36 * s * f, -0.01, 0.92), (0.37 * s * f, -0.015, 0.82), n + "ForeArm")
        j[n + "UpLeg"] = ((0.1 * s, 0, 0.92), (0.1 * s, 0, 0.48), "Hips")
        j[n + "Leg"] = ((0.1 * s, 0, 0.48), (0.1 * s, 0, 0.08), n + "UpLeg")
        j[n + "Foot"] = ((0.1 * s, 0, 0.08), (0.1 * s, -0.12, 0.03), n + "Leg")
        j[n + "ToeBase"] = ((0.1 * s, -0.12, 0.03), (0.1 * s, -0.18, 0.03), n + "Foot")
    return j

# ------------------------------------------------------------ build one character
def build(c, offset_x):
    global parts
    parts = []
    legs(c); torso(c); arms(c); head(c)

    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    body = active()
    body.name = c["name"]
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    sx, sy, sz = c["scale"]
    for v in body.data.vertices:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)

    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm = active()
    arm.name = c["name"] + "_Armature"
    arm.data.name = c["name"] + "_Skeleton"
    eb = arm.data.edit_bones
    eb.remove(eb[0])
    S = lambda p: Vector((p[0] * sx, p[1] * sy, p[2] * sz))
    for name, (h, t, parent) in joints(c).items():
        b = eb.new("mixamorig:" + name)
        b.head, b.tail = S(h), S(t)
        if parent:
            b.parent = eb["mixamorig:" + parent]
    bpy.ops.object.mode_set(mode="OBJECT")

    body.parent = arm
    body.modifiers.new("Armature", "ARMATURE").object = arm
    bad = [v.index for v in body.data.vertices if not v.groups]
    assert not bad, f"{c['name']}: {len(bad)} vertices have no bone"

    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, c["name"] + ".fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=False,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
    arm.location.x = offset_x
    print("BUILT", c["name"], "tris", tris)

for i, c in enumerate(CAST):
    if ONLY and c["name"] not in ONLY:
        continue
    build(c, (i - (len(CAST) - 1) / 2) * 0.95)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(os.path.dirname(os.path.abspath(__file__)), "NPC_Cast.blend"))

# ------------------------------------------------------------ lineup render
world = bpy.data.worlds.new("Sky"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75, 0.82, 0.9, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(-30)))
active().data.energy = 3.5
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(60), 0, math.radians(150)))
active().data.energy = 1.0
bpy.ops.mesh.primitive_plane_add(size=12)
fm = bpy.data.materials.new("Floor"); fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.45, 0.42, 0.4, 1)
active().data.materials.append(fm)
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = eng; break
    except TypeError:
        pass
scene.render.resolution_x, scene.render.resolution_y = 1600, 900
scene.view_settings.view_transform = "Standard"
bpy.ops.object.camera_add()
cam = active(); scene.camera = cam
cam.data.lens = 50
pos, target = Vector((0.6, -8.6, 1.7)), Vector((0, 0, 0.95))
cam.location = pos
cam.rotation_mode = "QUATERNION"
cam.rotation_quaternion = (target - pos).to_track_quat("-Z", "Y")
scene.render.filepath = PREVIEW
bpy.ops.render.render(write_still=True)
print("DONE")
