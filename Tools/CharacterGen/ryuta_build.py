# Tamura Ryuta (playable hero): game mesh, UVs, painted textures, Humanoid skeleton, skin weights,
# face blend shapes and the FBX for Unity. Everything is rebuilt from scratch on every run.
#
# Run from the project root:
#   "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --python Tools/CharacterGen/ryuta_build.py
# Writes:
#   Assets/_Project/Art/Characters/Ryuta/Ryuta.fbx
#   Assets/_Project/Art/Characters/Ryuta/Textures/Ryuta_Body.png (1024, body + hair), Ryuta_Face.png (512), Ryuta_Ramp.png
#   Art/Characters/Ryuta/Ryuta.blend, Art/Characters/Ryuta/build_report.txt
# Previews: Tools/CharacterGen/ryuta_render.py. Unity side: Tools > Funseki > Characters > Setup Ryuta Model.
#
# Conventions: Z up, the character faces -Y, his left is +X, feet on Z = 0, 1 unit = 1 m, 1.75 m tall (7.25 heads).
# Arms are modelled in T-pose along +X ("arm space", left side) and rotated down into a 40 degree A-pose.
# Texture colours are baked from 3D: every island is painted by a function of its vertices' paint coordinates
# (the surface position, or a local frame for the face cards), so seams never show and the UV layout is free.

import bpy, math, os, time
import numpy as np
from mathutils import Vector as V, Matrix
from mathutils.bvhtree import BVHTree

T0 = time.time()
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
ART = os.path.join(ROOT, "Art", "Characters", "Ryuta")
UNITY = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters", "Ryuta")
TEX = os.path.join(UNITY, "Textures")
for d in (ART, UNITY, TEX):
    os.makedirs(d, exist_ok=True)

ATLAS = {"body": 1024, "face": 512}
MAT_BODY, MAT_FACE, MAT_HAIR = 0, 1, 2
MAT_NAMES = ["Ryuta_Body", "Ryuta_Face", "Ryuta_Hair"]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


# ============================================================ math helpers
def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def table_at(rows, x):
    """Linear interpolation in a table sorted by its first column; returns the other columns."""
    if x <= rows[0][0]:
        return rows[0][1:]
    if x >= rows[-1][0]:
        return rows[-1][1:]
    for r0, r1 in zip(rows, rows[1:]):
        if r0[0] <= x <= r1[0]:
            t = (x - r0[0]) / (r1[0] - r0[0])
            return tuple(a + (b - a) * t for a, b in zip(r0[1:], r1[1:]))


def qe(a, rs, rf, rb, cy, p=1.0):
    """Horizontal 'quad ellipse': angle a from the front (-Y) towards +X; separate side / front / back radii.
    p > 1 narrows the front into a point (the chin)."""
    sa, ca = math.sin(a), math.cos(a)
    if ca > 0:
        return math.copysign(abs(sa) ** p, sa) * rs, cy - rf * ca
    return rs * sa, cy - rb * ca


def hring(z, rs, rf, rb, cy, n, p=1.0, cx=0.0, arc=None):
    """Horizontal ring, counter-clockwise seen from above. Closed rings start at the back (UV seam);
    arc=(a0, a1) makes an open arc, e.g. the open front of the jacket."""
    pts = []
    for i in range(n):
        a = math.pi + 2 * math.pi * i / n if arc is None else arc[0] + (arc[1] - arc[0]) * i / (n - 1)
        x, y = qe(a, rs, rf, rb, cy, p)
        pts.append(V((cx + x, y, z)))
    return pts


def tring(c, axis, ref, r1, r2, n, phase=0.0):
    """Ring around an axis, counter-clockwise about it. r1 along `ref`, r2 along axis x ref."""
    axis = axis.normalized()
    X = (ref - axis * ref.dot(axis)).normalized()
    Y = axis.cross(X)
    return [c + X * (r1 * math.cos(phase + 2 * math.pi * i / n)) + Y * (r2 * math.sin(phase + 2 * math.pi * i / n))
            for i in range(n)]


# ============================================================ skin weights
def chain(s, bones, joints, blends):
    """Weights along a chain of bones parametrised by s; smooth hand-over of +-blend around each joint."""
    w, prev = {}, 1.0
    for i, b in enumerate(bones):
        nxt = smoothstep(joints[i] - blends[i], joints[i] + blends[i], s) if i < len(joints) else 0.0
        if prev - nxt > 1e-4:
            w[b] = w.get(b, 0.0) + prev - nxt
        prev = nxt
    return w


def mix(w, bone, amount):
    for k in w:
        w[k] *= 1 - amount
    w[bone] = w.get(bone, 0.0) + amount
    return w


def torso_w(p, shoulders=True, legs=True, neck=True):
    w = chain(p.z, ["Hips", "Spine", "Chest", "UpperChest"], [1.03, 1.17, 1.30], [0.04, 0.05, 0.05])
    ax, side = abs(p.x), ("Left" if p.x > 0 else "Right")
    if neck:
        n = smoothstep(1.44, 1.50, p.z) * (1 - smoothstep(0.08, 0.12, ax))
        if n > 0.01:
            mix(w, "Neck", n * 0.6)
    if shoulders:
        a = smoothstep(0.08, 0.17, ax) * smoothstep(1.30, 1.40, p.z)
        if a > 0.01:
            u = smoothstep(0.15, 0.215, ax) * 0.6
            mix(w, side + "Shoulder", a * (1 - u))
            mix(w, side + "UpperArm", a * u)
    if legs:
        l = smoothstep(0.96, 0.80, p.z) * smoothstep(0.01, 0.09, ax) * 0.55
        if l > 0.01:
            mix(w, side + "UpperLeg", l)
    return w


def finalize_w(w):
    items = sorted(((b, v) for b, v in w.items() if v > 0.01), key=lambda kv: -kv[1])[:4]
    tot = sum(v for _, v in items)
    return {b: v / tot for b, v in items}


# ============================================================ mesh builder
class Builder:
    def __init__(self):
        self.co, self.w, self.ol, self.pc = [], [], [], []
        self.faces, self.fuv, self.fmat, self.fisl = [], [], [], []
        self.islands = []

    def vert(self, co, w, ol=1.0, pc=None):
        self.co.append(V(co))
        self.w.append(dict(w))
        self.ol.append(ol)
        self.pc.append(V(pc) if pc is not None else V(co))
        return len(self.co) - 1

    def island(self, part, atlas, W, H, mult=1.0):
        self.islands.append(dict(part=part, atlas=atlas, W=max(W, 0.004), H=max(H, 0.004), mult=mult))
        return len(self.islands) - 1

    def face(self, vs, uvs, isl, mat, flip=False):
        vs, uvs = list(vs), [tuple(u) for u in uvs]
        if flip:
            vs, uvs = vs[::-1], uvs[::-1]
        self.faces.append(vs)
        self.fuv.append(uvs)
        self.fmat.append(mat)
        self.fisl.append(isl)

    def face_oriented(self, vs, uvs, isl, mat, expect):
        n = V()
        pts = [self.co[i] for i in vs]
        for a, b in zip(pts, pts[1:] + pts[:1]):
            n += V(((a.y - b.y) * (a.z + b.z), (a.z - b.z) * (a.x + b.x), (a.x - b.x) * (a.y + b.y)))
        self.face(vs, uvs, isl, mat, flip=n.dot(expect) < 0)


MB = Builder()


def loft(rings, part, atlas, mat, wfn, olfn=None, closed=True, flip=False, pcfn=None, cap0=None, cap1=None,
         uparams=None, vparams=None, mult=1.0, xf=None):
    """Quad strip between rings (same point count). Rings counter-clockwise about the loft direction give
    outward normals. wfn/olfn/pcfn get the local point; xf maps local -> world (arms are built in arm space)."""
    K, n = len(rings), len(rings[0])
    segs = n if closed else n - 1
    cents = [sum(r, V()) / n for r in rings]
    W = sum(sum((r[(i + 1) % n] - r[i]).length for i in range(segs)) for r in rings) / K
    acc = [0.0]
    for k in range(K - 1):
        acc.append(acc[-1] + (cents[k + 1] - cents[k]).length)
    isl = MB.island(part, atlas, W, acc[-1], mult)
    if vparams is None:
        vparams = [a / max(acc[-1], 1e-6) for a in acc]
    if uparams is None:
        uparams = [i / segs for i in range(segs + 1)]
    olfn = olfn or (lambda p, k, i: 1.0)
    X = xf or (lambda p: p)

    def mk(p, k, i):
        return MB.vert(X(p), finalize_w(wfn(p, k, i)), olfn(p, k, i),
                       pcfn(p, k, i) if pcfn else (p if xf else None))

    idx = [[mk(p, k, i) for i, p in enumerate(r)] for k, r in enumerate(rings)]
    for k in range(K - 1):
        for i in range(segs):
            j = (i + 1) % n
            MB.face([idx[k][i], idx[k][j], idx[k + 1][j], idx[k + 1][i]],
                    [(uparams[i], vparams[k]), (uparams[i + 1], vparams[k]),
                     (uparams[i + 1], vparams[k + 1]), (uparams[i], vparams[k + 1])], isl, mat, flip)
    for cap, k, start in ((cap0, 0, True), (cap1, K - 1, False)):
        if cap is None:
            continue
        c = cents[k] if cap is True else cap
        ci = mk(c, k, -1)
        v = vparams[k]
        for i in range(segs):
            j = (i + 1) % n
            if start:
                MB.face([ci, idx[k][j], idx[k][i]], [(0.5, v), (uparams[i + 1], v), (uparams[i], v)], isl, mat, flip)
            else:
                MB.face([ci, idx[k][i], idx[k][j]], [(0.5, v), (uparams[i], v), (uparams[i + 1], v)], isl, mat, flip)
    return idx, isl


def lip(ring, axis_in, thick, depth, part, mat, mult=0.4):
    """Folded-in wall at an open tube end (cuffs, hems, neckline) so the opening shows a dark inside, not a hole."""
    n = len(ring)
    P = [MB.co[i] for i in ring]
    c = sum(P, V()) / n
    r1 = [c + (p - c) * max(1 - thick / max((p - c).length, 1e-6), 0.2) for p in P]
    r2 = [p + axis_in * depth for p in r1]
    i1 = [MB.vert(p, MB.w[ring[i]], 0.0, MB.pc[ring[i]]) for i, p in enumerate(r1)]
    i2 = [MB.vert(p, MB.w[ring[i]], 0.0, MB.pc[ring[i]]) for i, p in enumerate(r2)]
    ci = MB.vert(sum(r2, V()) / n, MB.w[ring[0]], 0.0, MB.pc[ring[0]])
    per = sum((P[(i + 1) % n] - P[i]).length for i in range(n))
    isl = MB.island(part, "body", per, thick + depth, mult)
    for i in range(n):
        j = (i + 1) % n
        u0, u1 = i / n, (i + 1) / n
        MB.face_oriented([ring[i], ring[j], i1[j], i1[i]], [(u0, 0), (u1, 0), (u1, .3), (u0, .3)], isl, mat, -axis_in)
        mid = (r1[i] + r1[j] + r2[i] + r2[j]) / 4
        MB.face_oriented([i1[i], i1[j], i2[j], i2[i]], [(u0, .3), (u1, .3), (u1, 1), (u0, 1)], isl, mat,
                         (c + axis_in * depth * 0.5) - mid)
        MB.face_oriented([ci, i2[i], i2[j]], [(0.5, 1), (u0, 1), (u1, 1)], isl, mat, -axis_in)


def shell(out_rings, in_rings, part_out, part_in, mat, wfn, ol_out, mult_out=1.0, mult_in=0.6):
    """Cloth with thickness from open arcs: outer surface, inner surface facing the body, and rims."""
    io, _ = loft(out_rings, part_out, "body", mat, wfn, ol_out, closed=False, mult=mult_out)
    ii, _ = loft(in_rings, part_in, "body", mat, wfn, lambda p, k, i: 0.0, closed=False, flip=True, mult=mult_in)
    K, n = len(io), len(io[0])
    strips = [([io[k][0] for k in range(K)], [ii[k][0] for k in range(K)], [io[k][1] for k in range(K)]),
              ([io[k][n - 1] for k in range(K)], [ii[k][n - 1] for k in range(K)], [io[k][n - 2] for k in range(K)]),
              (io[0], ii[0], io[1]),
              (io[K - 1], ii[K - 1], io[K - 2])]
    for a, b, nb in strips:
        L = sum((MB.co[a[m + 1]] - MB.co[a[m]]).length for m in range(len(a) - 1))
        isl = MB.island(part_out, "body", L, 0.01, 0.6)
        for m in range(len(a) - 1):
            u0, u1 = m / (len(a) - 1), (m + 1) / (len(a) - 1)
            MB.face_oriented([a[m], a[m + 1], b[m + 1], b[m]], [(u0, 0), (u1, 0), (u1, 1), (u0, 1)], isl, mat,
                             MB.co[a[m]] - MB.co[nb[m]])
        for v in a + b:
            MB.ol[v] = min(MB.ol[v], 0.6)
    return io, ii


# ============================================================ skeleton
A_POSE = math.radians(40)
ARM_PIVOT = V((0.165, 0.015, 1.428))          # shoulder joint, arm space
SHOULDER_TAIL = V((0.162, 0.014, 1.429))
ELBOW, WRIST = V((0.445, 0.022, 1.428)), V((0.695, 0.018, 1.428))
CURL = math.radians(7)                         # relaxed finger curl per joint, towards the palm (-Z)
FINGERS = {  # knuckle (arm space), yaw (deg, + towards the back), phalanx lengths, radius
    "Index": (V((0.777, -0.025, 1.4285)), -6, (0.040, 0.025, 0.021), 0.0098),
    "Middle": (V((0.781, -0.007, 1.429)), -1, (0.044, 0.028, 0.022), 0.0101),
    "Ring": (V((0.778, 0.011, 1.4285)), 5, (0.041, 0.026, 0.021), 0.0094),
    "Little": (V((0.770, 0.027, 1.4275)), 11, (0.032, 0.021, 0.019), 0.0083),
}
THUMB = (V((0.708, -0.020, 1.419)), V((0.55, -0.78, -0.30)), (0.036, 0.031, 0.026), 0.0125)


def finger_pts(knuckle, d0, lens):
    pts, d = [V(knuckle)], d0.normalized()
    for k, L in enumerate(lens):
        ax = V((0, 0, 1)).cross(d).normalized()
        d = (Matrix.Rotation(CURL * (0.5 if k == 0 else 1.0), 3, ax) @ d).normalized()
        pts.append(pts[-1] + d * L)
    return pts


FINGER_PTS = {name: finger_pts(k, V((math.cos(math.radians(yaw)), math.sin(math.radians(yaw)), 0)), lens)
              for name, (k, yaw, lens, r) in FINGERS.items()}
FINGER_PTS["Thumb"] = finger_pts(THUMB[0], THUMB[1], THUMB[2])
HAND_TAIL = FINGER_PTS["Middle"][0]


def arm_matrix(s):
    """Arm space (left T-pose along +X) -> world A-pose for side s (+1 left, -1 right)."""
    piv = V((ARM_PIVOT.x * s, ARM_PIVOT.y, ARM_PIVOT.z))
    return (Matrix.Translation(piv) @ Matrix.Rotation(A_POSE * s, 4, "Y") @ Matrix.Translation(-piv)
            @ Matrix.Diagonal((s, 1, 1, 1)))


def leg_center(z, s=1):
    if z >= 0.49:
        t = (0.905 - z) / (0.905 - 0.49)
        x, y = lerp(0.092, 0.098, t), lerp(0.0, -0.008, t)
    elif z >= 0.085:
        t = (0.49 - z) / (0.49 - 0.085)
        x, y = lerp(0.098, 0.102, t), lerp(-0.008, 0.018, t)
    else:
        x, y = 0.102, 0.018
    y = lerp(y, -0.004, smoothstep(0.13, 0.06, z))
    return s * x, y


def skeleton():
    """name -> (head, tail, parent, roll_up); every bone is a Unity Humanoid bone named after HumanBodyBones."""
    B = {}
    fwd = V((0, -1, 0))

    def add(n, h, t, p, up=fwd):
        B[n] = (V(h), V(t), p, V(up))

    add("Hips", (0, 0.005, 0.955), (0, 0.005, 1.04), None)
    add("Spine", (0, 0.005, 1.04), (0, 0.0, 1.17), "Hips")
    add("Chest", (0, 0.0, 1.17), (0, 0.005, 1.30), "Spine")
    add("UpperChest", (0, 0.005, 1.30), (0, 0.012, 1.425), "Chest")
    add("Neck", (0, 0.012, 1.445), (0, 0.004, 1.545), "UpperChest")
    add("Head", (0, 0.004, 1.545), (0, 0.004, 1.75), "Neck")
    for s, side in ((1, "Left"), (-1, "Right")):
        M = arm_matrix(s)
        up = (M.to_3x3() @ V((0, 0, 1))).normalized()
        add(side + "Shoulder", (0.02 * s, 0.002, 1.418), M @ SHOULDER_TAIL, "UpperChest")
        add(side + "UpperArm", M @ ARM_PIVOT, M @ ELBOW, side + "Shoulder")
        add(side + "LowerArm", M @ ELBOW, M @ WRIST, side + "UpperArm")
        add(side + "Hand", M @ WRIST, M @ HAND_TAIL, side + "LowerArm")
        for f, pts in FINGER_PTS.items():
            parent = side + "Hand"
            for seg, part in enumerate(("Proximal", "Intermediate", "Distal")):
                name = side + f + part
                add(name, M @ pts[seg], M @ pts[seg + 1], parent, up)
                parent = name
        hx, hy = leg_center(0.905, s)
        kx, ky = leg_center(0.49, s)
        ax_, ay = leg_center(0.085, s)
        add(side + "UpperLeg", (hx, hy, 0.905), (kx, ky, 0.49), "Hips")
        add(side + "LowerLeg", (kx, ky, 0.49), (ax_, 0.018, 0.085), side + "UpperLeg")
        add(side + "Foot", (ax_, 0.018, 0.085), (0.108 * s, -0.085, 0.022), side + "LowerLeg", V((0, 0, 1)))
        add(side + "Toes", (0.108 * s, -0.085, 0.022), (0.112 * s, -0.152, 0.022), side + "Foot", V((0, 0, 1)))
    return B


BONES = skeleton()


# ============================================================ head (face material)
HEAD = [  # z, side radius, front radius, back radius, centre y, chin pointiness
    (1.506, 0.004, 0.004, 0.004, -0.066, 1.0),
    (1.514, 0.016, 0.016, 0.022, -0.061, 1.5),
    (1.528, 0.032, 0.030, 0.044, -0.051, 1.4),
    (1.546, 0.048, 0.045, 0.064, -0.040, 1.25),
    (1.562, 0.058, 0.056, 0.076, -0.031, 1.15),
    (1.578, 0.066, 0.065, 0.086, -0.024, 1.08),
    (1.596, 0.072, 0.072, 0.093, -0.017, 1.0),
    (1.614, 0.077, 0.077, 0.098, -0.012, 0.95),
    (1.632, 0.080, 0.080, 0.101, -0.009, 0.92),
    (1.652, 0.081, 0.081, 0.102, -0.007, 0.92),
    (1.674, 0.080, 0.079, 0.100, -0.005, 0.92),
    (1.696, 0.075, 0.073, 0.093, -0.004, 0.92),
    (1.716, 0.066, 0.063, 0.081, -0.002, 0.92),
    (1.732, 0.052, 0.049, 0.064, 0.0, 0.92),
    (1.744, 0.033, 0.031, 0.041, 0.0, 0.92),
    (1.751, 0.012, 0.011, 0.015, 0.0, 0.92),
]
HEAD_N = 28
CHIN_TIP, CROWN = V((0, -0.066, 1.503)), V((0, 0.0, 1.754))
HC, HR = V((0.0, -0.008, 1.628)), V((0.082, 0.097, 0.125))   # ellipsoid whose normals light the face


def head_pt(a, z, extra=0.0):
    z = min(max(z, HEAD[0][0]), HEAD[-1][0])
    rs, rf, rb, cy, p = table_at(HEAD, z)
    x, y = qe(a, rs + extra, rf + extra, rb + extra, cy, p)
    return V((x, y, z))


def head_nrm(p):
    d = p - HC
    return V((d.x / HR.x ** 2, d.y / HR.y ** 2, d.z / HR.z ** 2)).normalized()


def head_u(u):
    """More texels for the face: the front (u = 0.5) gets ~1.3x density, the back of the head less."""
    d = (u - 0.5) * 2
    return 0.5 + 0.5 * math.copysign(0.45 * abs(d) + 0.55 * math.sin(abs(d) * math.pi / 2), d)


def head_v(z):
    return 0.85 * (z - 1.503) / (1.70 - 1.503) if z < 1.70 else 0.85 + 0.15 * (z - 1.70) / (1.754 - 1.70)


def head_w(p):
    n = smoothstep(1.585, 1.54, p.z) * smoothstep(-0.035, 0.015, p.y) * 0.7
    return {"Head": 1 - n, "Neck": n} if n > 0.01 else {"Head": 1.0}


def build_head():
    rings = []
    for z, rs, rf, rb, cy, p in HEAD:
        r = hring(z, rs, rf, rb, cy, HEAD_N, p)
        f = HEAD_N // 2                                    # front column
        nose = {1.578: (0.011, 0.004), 1.596: (0.005, 0.0015), 1.562: (0.002, 0.0)}.get(z)
        if nose:
            r[f].y -= nose[0]
            r[f - 1].y -= nose[1]
            r[f + 1].y -= nose[1]
        rings.append(r)
    start_v, start_f = len(MB.co), len(MB.faces)
    loft(rings, "head", "face", MAT_FACE, lambda p, k, i: head_w(p), cap0=CHIN_TIP, cap1=CROWN,
         uparams=[head_u(i / HEAD_N) for i in range(HEAD_N + 1)], vparams=[head_v(r[0]) for r in HEAD])
    return range(start_v, len(MB.co)), range(start_f, len(MB.faces))


EAR = [  # z, half width (y), half thickness (x), centre y
    (1.586, 0.006, 0.004, 0.010), (1.596, 0.012, 0.0065, 0.008), (1.612, 0.0145, 0.0075, 0.008),
    (1.628, 0.0135, 0.007, 0.010), (1.638, 0.0085, 0.005, 0.013)]


def build_ears():
    for s in (1, -1):
        rings = []
        for z, hw, ht, yc in EAR:
            rs = table_at(HEAD, z)[0]
            cx = s * (rs + 0.002 + 0.004 * (z - 1.586) / 0.052)
            rings.append(hring(z, ht, hw, hw, yc, 8, cx=cx))
        loft(rings, "ear", "face", MAT_FACE, lambda p, k, i: {"Head": 1.0}, lambda p, k, i: 0.7,
             pcfn=lambda p, k, i, s=s: V(((p.x - s * 0.08) * s, p.y, p.z)), cap0=True, cap1=True)


# ============================================================ face cards (eyes, lids, brows, mouth)
EX, EZ, EW, EH, ETILT = 0.034, 1.611, 0.035, 0.025, -0.0012   # eye centre, size, outer-corner droop
LASH, LID_TOP = 0.15, 1.25          # resting upper lid in eye half-heights: heavy, bored lids
MZ, MW = 1.542, 0.022
EYE_OFF, LID_OFF, BROW_OFF, MOUTH_OFF, MOUTH_HIDE = 0.0012, 0.0024, 0.0034, 0.0012, -0.003
LID_ROWS = (0.0, 0.22, 0.6, 1.0)
FACE_CARDS = []                     # (vertex, kind, side, a, b)
BVH = None


def face_pt(x, z, off):
    hit = BVH.ray_cast(V((x, -0.4, z)), V((0, 1, 0)))
    loc = hit[0] if hit[0] is not None else head_pt(math.atan2(x, 0.08), z)
    return loc + head_nrm(loc) * off


def eye_xz(side, s, t):
    return side * (EX + s * EW / 2), EZ + t * EH / 2 + ETILT * s


def brow_xz(side, sa, tb, dx=0.0, dz=0.0):
    x = 0.013 + 0.044 * sa - dx
    z = 1.6395 + 0.004 * sa + 0.0025 * math.sin(math.pi * sa) + dz
    return side * x, z + tb * lerp(0.0032, 0.0017, sa)


def mouth_xz(s, t, m):
    colf = math.sqrt(max(1 - 0.9 * s * s, 0.0)) * m.get("hfn", lambda s: 1.0)(s)
    x = s * m["w"] / 2 + m.get("shift", 0.0) * max(s, 0.0)
    z = MZ + t * m["h"] / 2 * colf + m.get("corner", 0.0) * s * s + m.get("lift", 0.0) * max(s, 0.0) ** 1.5
    if t < 0:
        z -= m.get("jaw", 0.0) * 0.5 * (-t) * colf
    return x, z


MOUTH_REST = dict(w=MW, h=0.0004)


def card(kind, side, cols, rows, xzfn, off, pcfn, mult, ol=0.0):
    grid = [[None] * len(cols) for _ in rows]
    for j, b in enumerate(rows):
        for i, a in enumerate(cols):
            x, z = xzfn(a, b)
            grid[j][i] = MB.vert(face_pt(x, z, off), {"Head": 1.0}, ol, pcfn(a, b))
            FACE_CARDS.append((grid[j][i], kind, side, a, b))
    W = (MB.co[grid[0][-1]] - MB.co[grid[0][0]]).length
    H = (MB.co[grid[-1][0]] - MB.co[grid[0][0]]).length
    isl = MB.island(kind, "face", W, max(H, 0.006), mult)
    for j in range(len(rows) - 1):
        for i in range(len(cols) - 1):
            u0, u1 = i / (len(cols) - 1), (i + 1) / (len(cols) - 1)
            v0, v1 = j / (len(rows) - 1), (j + 1) / (len(rows) - 1)
            MB.face_oriented([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]],
                             [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], isl, MAT_FACE, V((0, -1, 0)))


def lid_t(tl):
    return LASH + tl * (LID_TOP - LASH)


def build_face_cards():
    lin = lambda a, b, n: [a + (b - a) * i / (n - 1) for i in range(n)]
    for side in (1, -1):
        card("eye", side, lin(-1, 1, 5), lin(-1, 1, 4), lambda s, t, side=side: eye_xz(side, s, t), EYE_OFF,
             lambda s, t, side=side: (s, t, side), 4.0)
        card("lid", side, lin(-1.12, 1.12, 5), LID_ROWS, lambda s, tl, side=side: eye_xz(side, s, lid_t(tl)),
             LID_OFF, lambda s, tl, side=side: (s, tl, side), 2.5)
        card("brow", side, lin(0, 1, 5), (-1.0, 1.0), lambda sa, tb, side=side: brow_xz(side, sa, tb), BROW_OFF,
             lambda sa, tb, side=side: (sa, tb, side), 2.5)
    card("mouth", 0, lin(-1, 1, 5), lin(-1, 1, 3), lambda s, t: mouth_xz(s, t, MOUTH_REST), MOUTH_HIDE,
         lambda s, t: (s, t, 0), 4.0)


# ============================================================ neck, shirt, jacket, collar
def build_neck():
    zs = [1.418, 1.45, 1.48, 1.51, 1.54, 1.565, 1.59]
    rings = [hring(z, lerp(0.046, 0.044, i / 6), lerp(0.044, 0.042, i / 6), 0.046, lerp(0.014, 0.004, i / 6), 12)
             for i, z in enumerate(zs)]
    loft(rings, "neck", "body", MAT_BODY,
         lambda p, k, i: chain(p.z, ["UpperChest", "Neck", "Head"], [1.45, 1.552], [0.02, 0.02]),
         lambda p, k, i: 1.0 if 1.47 < p.z < 1.56 else 0.0)


SHIRT = [  # z, rs, rf, rb, cy
    (0.950, 0.142, 0.096, 0.100, 0.0), (1.000, 0.146, 0.099, 0.102, 0.0), (1.06, 0.150, 0.103, 0.106, 0.0),
    (1.15, 0.153, 0.105, 0.107, 0.0), (1.24, 0.162, 0.111, 0.107, 0.005), (1.32, 0.172, 0.112, 0.104, 0.01),
    (1.38, 0.180, 0.102, 0.098, 0.012), (1.42, 0.170, 0.088, 0.088, 0.014), (1.445, 0.120, 0.075, 0.078, 0.015),
    (1.458, 0.068, 0.062, 0.064, 0.015)]


def build_shirt():
    rings = [hring(z, rs, rf, rb, cy, 24) for z, rs, rf, rb, cy in SHIRT]
    idx, _ = loft(rings, "shirt", "body", MAT_BODY, lambda p, k, i: torso_w(p, legs=False),
                  lambda p, k, i: 0.0, mult=0.7)
    lip(idx[-1], V((0, 0, -1)), 0.004, 0.012, "shirt", MAT_BODY)


JACKET = [  # z, rs, rf, rb, cy, half-angle of the open front (deg)
    (0.835, 0.207, 0.146, 0.150, 0.0, 34), (0.90, 0.199, 0.138, 0.142, 0.0, 31), (0.98, 0.188, 0.131, 0.133, 0.0, 28),
    (1.06, 0.178, 0.125, 0.125, 0.0, 24), (1.15, 0.178, 0.125, 0.125, 0.0, 22), (1.24, 0.187, 0.131, 0.125, 0.005, 20),
    (1.32, 0.195, 0.131, 0.123, 0.010, 20), (1.38, 0.196, 0.121, 0.115, 0.012, 22), (1.42, 0.190, 0.106, 0.105, 0.014, 26),
    (1.448, 0.170, 0.091, 0.093, 0.016, 34), (1.468, 0.118, 0.079, 0.081, 0.016, 44)]
JACKET_T = 0.008
BUTTONS_Z = (1.355, 1.245, 1.135, 1.025, 0.915)


def build_jacket():
    def rings(dt):
        out = []
        for z, rs, rf, rb, cy, gap in JACKET:
            g = math.radians(gap)
            out.append(hring(z, rs - dt, rf - dt, rb - dt, cy, 24, arc=(g, 2 * math.pi - g)))
        return out
    shell(rings(0.0), rings(JACKET_T), "jacket", "lining_jacket", MAT_BODY, lambda p, k, i: torso_w(p),
          lambda p, k, i: 1.0)


COLLAR = [(1.452, 0.082, 0.075, 0.077, 0.018), (1.475, 0.079, 0.072, 0.074, 0.018), (1.512, 0.074, 0.066, 0.069, 0.02)]


def build_collar():
    g = math.radians(40)

    def rings(dt):
        return [hring(z, rs - dt, rf - dt, rb - dt, cy, 16, arc=(g, 2 * math.pi - g)) for z, rs, rf, rb, cy in COLLAR]
    shell(rings(0.0), rings(0.005), "collar", "collar_in", MAT_BODY,
          lambda p, k, i: chain(p.z, ["UpperChest", "Neck"], [1.49], [0.04]), lambda p, k, i: 1.0, 1.0, 1.0)


# ============================================================ arms: sleeves, hands, fingers (arm space)
SLEEVE = [  # x, radius up/down, radius front/back, centre drop
    (0.140, 0.050, 0.058, -0.020), (0.185, 0.056, 0.062, -0.012), (0.24, 0.060, 0.062, -0.005), (0.31, 0.057, 0.058, 0.0),
    (0.38, 0.054, 0.055, 0.0), (0.425, 0.052, 0.053, 0.0), (0.445, 0.053, 0.054, 0.0), (0.465, 0.052, 0.053, 0.0),
    (0.52, 0.050, 0.052, 0.0), (0.59, 0.048, 0.050, 0.0), (0.65, 0.046, 0.048, 0.0), (0.685, 0.047, 0.049, 0.0),
    (0.705, 0.048, 0.050, 0.0)]
PALM = [  # x, half width (y), half thickness (z), centre y, centre z
    (0.672, 0.025, 0.020, 0.018, 1.428), (0.695, 0.027, 0.0185, 0.016, 1.428), (0.712, 0.033, 0.017, 0.010, 1.427),
    (0.735, 0.037, 0.0155, 0.004, 1.4275), (0.758, 0.039, 0.0145, 0.001, 1.428), (0.775, 0.038, 0.0125, 0.0, 1.4285),
    (0.788, 0.034, 0.009, 0.0, 1.4285)]


def arm_line_y(x):
    return table_at([(0.0, 0.015), (0.165, 0.015), (0.445, 0.022), (0.695, 0.018), (1.0, 0.018)], x)[0]


def build_arm(s):
    side = "Left" if s > 0 else "Right"
    M = arm_matrix(s)
    xf = lambda p: M @ p
    flip = s < 0
    pre = lambda w: {side + b: v for b, v in w.items()}
    X, DOWN = V((1, 0, 0)), V((0, 0, -1))

    def arm_w(x):
        return pre(chain(x, ["Shoulder", "UpperArm", "LowerArm", "Hand"], [0.178, 0.445, 0.712], [0.035, 0.05, 0.015]))

    rings = [tring(V((x, arm_line_y(x), 1.428 + dz)), X, DOWN, rz, ry, 14) for x, rz, ry, dz in SLEEVE]
    idx, _ = loft(rings, "sleeve", "body", MAT_BODY, lambda p, k, i: arm_w(p.x),
                  lambda p, k, i: 0.0 if p.x < 0.17 else 1.0, flip=flip, xf=xf)
    lip(idx[-1], (M.to_3x3() @ V((-1, 0, 0))).normalized(), 0.005, 0.03, "lining", MAT_BODY)

    rings = [tring(V((x, cy, cz)), X, V((0, 0, 1)), rz, ry, 10) for x, ry, rz, cy, cz in PALM]
    loft(rings, "hand", "body", MAT_BODY, lambda p, k, i: pre(chain(p.x, ["LowerArm", "Hand"], [0.695], [0.012])),
         lambda p, k, i: 0.0 if k == 0 else 1.0, flip=flip, xf=xf, cap1=True, mult=1.2)

    for f, pts in FINGER_PTS.items():
        r = THUMB[3] if f == "Thumb" else FINGERS[f][3]
        L = [(pts[i + 1] - pts[i]).length for i in range(3)]
        S1, S2, S3 = L[0], L[0] + L[1], sum(L)
        st = [(-0.008, 1.05), (0.45 * L[0], 1.0), (S1 - 0.003, 0.93), (S1 + 0.003, 0.92), (S1 + 0.5 * L[1], 0.88),
              (S2, 0.83), (S2 + 0.45 * L[2], 0.8), (S3 - 0.004, 0.68)]

        def at(sv):
            seg = 0 if sv < S1 else (1 if sv < S2 else 2)
            base = (0.0, S1, S2)[seg]
            d = (pts[seg + 1] - pts[seg]).normalized()
            return pts[seg] + d * (sv - base), d

        rings, svals = [], []
        for sv, fac in st:
            c, d = at(sv)
            rings.append(tring(c, d, V((0, 0, 1)), r * fac * 0.88, r * fac, 6))
            svals.append(sv)
        bones = ["Hand", f + "Proximal", f + "Intermediate", f + "Distal"]
        tipc, tipd = at(S3)
        loft(rings, "hand", "body", MAT_BODY,
             lambda p, k, i, sv=svals, b=bones: pre(chain(sv[k] if k >= 0 else S3, b, [0.0, S1, S2], [0.006, 0.005, 0.004])),
             lambda p, k, i: 0.0 if k == 0 else 1.0, flip=flip, xf=xf, cap1=tipc + tipd * 0.002, mult=1.2)


# ============================================================ pants, shoes
PELVIS = [  # z, rs, rf, rb, cy
    (0.792, 0.030, 0.028, 0.030, 0.012), (0.805, 0.085, 0.066, 0.072, 0.010), (0.835, 0.140, 0.095, 0.108, 0.008),
    (0.875, 0.166, 0.112, 0.124, 0.005), (0.920, 0.170, 0.116, 0.122, 0.002), (0.955, 0.166, 0.115, 0.116, 0.0),
    (0.990, 0.165, 0.114, 0.114, 0.0), (1.005, 0.161, 0.111, 0.111, 0.0), (1.008, 0.152, 0.104, 0.106, 0.0)]
LEG = [  # z, side radius, front/back radius
    (0.055, 0.082, 0.086), (0.075, 0.083, 0.087), (0.115, 0.081, 0.085), (0.17, 0.078, 0.081), (0.27, 0.075, 0.078),
    (0.38, 0.075, 0.078), (0.46, 0.076, 0.080), (0.495, 0.077, 0.081), (0.53, 0.078, 0.082), (0.60, 0.082, 0.086),
    (0.70, 0.087, 0.091), (0.80, 0.091, 0.095), (0.88, 0.094, 0.099), (0.935, 0.095, 0.100)]
SHOE = [  # y, half width, height, toe-out
    (-0.171, 0.006, 0.026, 0.004), (-0.167, 0.020, 0.036, 0.005), (-0.157, 0.036, 0.045, 0.006),
    (-0.140, 0.046, 0.051, 0.007), (-0.115, 0.051, 0.057, 0.007), (-0.085, 0.052, 0.064, 0.006),
    (-0.050, 0.049, 0.078, 0.004), (-0.010, 0.046, 0.092, 0.002), (0.025, 0.044, 0.100, 0.0),
    (0.052, 0.041, 0.102, 0.0), (0.070, 0.034, 0.088, 0.0), (0.078, 0.012, 0.050, 0.0)]


def build_pants():
    rings = [hring(z, rs, rf, rb, cy, 24) for z, rs, rf, rb, cy in PELVIS]
    idx, _ = loft(rings, "pants", "body", MAT_BODY, lambda p, k, i: torso_w(p, shoulders=False, neck=False),
                  lambda p, k, i: 1.0 if p.z < 0.995 else 0.0, cap0=V((0, 0.012, 0.788)))
    lip(idx[-1], V((0, 0, -1)), 0.004, 0.03, "lining", MAT_BODY)
    for s in (1, -1):
        side = "Left" if s > 0 else "Right"
        rings = []
        for z, rx, ry in LEG:
            cx, cy = leg_center(z, s)
            rings.append(hring(z, rx, ry, ry, cy, 16, cx=cx))
        idx, _ = loft(rings, "pants", "body", MAT_BODY,
                      lambda p, k, i, side=side: {side + b: v for b, v in
                                                  chain(-p.z, ["UpperLeg", "LowerLeg"], [-0.49], [0.045]).items()}
                      if p.z < 0.90 else mix(dict(Hips=1.0), side + "UpperLeg", smoothstep(0.95, 0.90, p.z)),
                      lambda p, k, i: 0.0 if p.z > 0.86 else 1.0)
        lip(idx[0], V((0, 0, 1)), 0.004, 0.03, "lining", MAT_BODY)


def shoe_ring(y, hw, h, xs, s, n=14, e=2.8):
    cx = s * (0.104 + xs)
    lift = 0.006 * smoothstep(-0.13, -0.171, y)
    pts = []
    for i in range(n):
        ph = -math.pi / 2 + 2 * math.pi * i / n
        c, sn = math.cos(ph), math.sin(ph)
        x = cx + s * hw * math.copysign(abs(c) ** (2 / e), c)
        z = lift + h / 2 + h / 2 * math.copysign(abs(sn) ** (2 / e), sn)
        pts.append(V((x, y, z)))
    return pts


def build_shoes():
    for s in (1, -1):
        side = "Left" if s > 0 else "Right"
        rings = [shoe_ring(y, hw, h, xs, s) for y, hw, h, xs in reversed(SHOE)]
        loft(rings, "shoe", "body", MAT_BODY,
             lambda p, k, i, side=side: {side + b: v for b, v in chain(-p.y, ["Foot", "Toes"], [0.085], [0.02]).items()},
             pcfn=lambda p, k, i, s=s: V(((p.x - s * 0.104) * s, p.y, p.z)), flip=s < 0, cap0=True, cap1=True)


# ============================================================ hair (hair material)
HAIRLINE = [(0, 1.700), (30, 1.695), (55, 1.672), (70, 1.615), (80, 1.612), (88, 1.645), (110, 1.625), (125, 1.585),
            (150, 1.565), (180, 1.560)]
HAIR_C = V((0.0, 0.0, 1.63))


def hairline(a):
    d = math.degrees(abs((a + math.pi) % (2 * math.pi) - math.pi))
    return table_at(HAIRLINE, d)[0]


def hair_out(p):
    return (p - HAIR_C).normalized()


def push_out(p, margin):
    if not (HEAD[0][0] <= p.z <= HEAD[-1][0]):
        return p
    cy = table_at(HEAD, p.z)[3]
    a = math.atan2(p.x, -(p.y - cy))
    s = head_pt(a, p.z, margin)
    rs, rp = math.hypot(s.x, s.y - cy), math.hypot(p.x, p.y - cy)
    if rp < rs and rp > 1e-6:
        k = rs / rp
        return V((p.x * k, cy + (p.y - cy) * k, p.z))
    return p


def build_hair_cap():
    N, ts = 24, [0.0, 0.15, 0.32, 0.5, 0.66, 0.8, 0.91]
    rings = []
    for t in ts:
        r = []
        for i in range(N):
            a = math.pi + 2 * math.pi * i / N
            z = lerp(hairline(a), 1.751, t ** 0.85)
            base = head_pt(a, z)
            r.append(base + head_nrm(base) * lerp(0.004, 0.02, t))
        rings.append(r)
    loft(rings, "hair", "body", MAT_HAIR, lambda p, k, i: {"Head": 1.0},
         lambda p, k, i: 0.0 if k == 0 else 1.0, cap1=V((0, -0.002, 1.774)), mult=1.2)


def build_lock(path, w0, th0, n=6, stations=(0.0, 0.12, 0.3, 0.5, 0.7, 0.86)):
    rings = []
    for t in stations:
        p = path(t)
        d = (path(min(t + 0.02, 1.0)) - path(max(t - 0.02, 0.0))).normalized()
        R = hair_out(p)
        R = (R - d * R.dot(d)).normalized()
        Wd = R.cross(d)
        f = (1 - t) ** 0.7
        rings.append([p + Wd * (w0 * f * math.cos(2 * math.pi * i / n)) + R * (th0 * f * math.sin(2 * math.pi * i / n))
                      for i in range(n)])
    loft(rings, "hair", "body", MAT_HAIR, lambda p, k, i: {"Head": 1.0},
         lambda p, k, i: 0.0 if k == 0 else 1.0, cap1=path(1.0), mult=1.4)


def surf_lock(a0, z0, o0, a1, z1, o1, bulge, w0, th0):
    def P(t):
        base = head_pt(math.radians(lerp(a0, a1, t)), lerp(z0, z1, t))
        return base + head_nrm(base) * (lerp(o0, o1, t) + bulge * math.sin(math.pi * t))
    build_lock(P, w0, th0)


def free_lock(a, z0, elev, daz, L, w0, th0, droop):
    base = head_pt(math.radians(a), z0)
    root = base + head_nrm(base) * 0.012
    az, e = math.radians(a + daz), math.radians(elev)
    d = V((math.sin(az) * math.cos(e), -math.cos(az) * math.cos(e), math.sin(e)))

    def P(t):
        return push_out(root + d * (L * t) + V((0, 0, -droop * t * t)), 0.024)
    build_lock(P, w0, th0)


SURF_LOCKS = [  # a0, z0, out0, a1, z1, out1, bulge, width, thickness
    # fringe over the forehead, partly over the eyes
    (-40, 1.712, 0.012, -46, 1.628, 0.016, 0.010, 0.030, 0.011),
    (-20, 1.716, 0.012, -22, 1.610, 0.015, 0.012, 0.033, 0.012),
    (0, 1.718, 0.012, 3, 1.604, 0.015, 0.012, 0.034, 0.012),
    (19, 1.716, 0.012, 24, 1.616, 0.015, 0.012, 0.033, 0.012),
    (38, 1.712, 0.012, 46, 1.630, 0.016, 0.010, 0.030, 0.011),
    # temples and over the ears
    (-62, 1.705, 0.012, -70, 1.590, 0.016, 0.010, 0.034, 0.012),
    (62, 1.705, 0.012, 70, 1.592, 0.016, 0.010, 0.034, 0.012),
    (-95, 1.712, 0.020, -102, 1.596, 0.034, 0.012, 0.040, 0.013),
    (95, 1.712, 0.020, 102, 1.598, 0.034, 0.012, 0.040, 0.013),
    # back, down to the nape
    (-125, 1.716, 0.014, -132, 1.578, 0.026, 0.012, 0.042, 0.014),
    (125, 1.716, 0.014, 132, 1.578, 0.026, 0.012, 0.042, 0.014),
    (-152, 1.720, 0.014, -158, 1.566, 0.024, 0.012, 0.044, 0.014),
    (152, 1.720, 0.014, 158, 1.566, 0.024, 0.012, 0.044, 0.014),
    (180, 1.722, 0.014, 180, 1.560, 0.022, 0.012, 0.046, 0.014),
]
FREE_LOCKS = [  # azimuth, z, elevation, azimuth drift, length, width, thickness, droop: the messy crown
    (-150, 1.742, 2, -10, 0.080, 0.042, 0.014, 0.040), (-105, 1.742, 4, 0, 0.075, 0.042, 0.014, 0.038),
    (-55, 1.745, 10, 5, 0.070, 0.040, 0.013, 0.030), (-15, 1.748, 20, 0, 0.066, 0.038, 0.013, 0.020),
    (25, 1.747, 14, 0, 0.068, 0.038, 0.013, 0.025), (70, 1.744, 4, -5, 0.074, 0.042, 0.014, 0.038),
    (115, 1.742, 2, 0, 0.078, 0.042, 0.014, 0.040), (160, 1.742, 0, 8, 0.084, 0.044, 0.014, 0.042),
    (170, 1.750, 28, 10, 0.070, 0.040, 0.013, 0.012),
]


def build_hair():
    build_hair_cap()
    for l in SURF_LOCKS:
        surf_lock(*l)
    for l in FREE_LOCKS:
        free_lock(*l)


# ============================================================ build the geometry
head_vs, head_fs = build_head()
BVH = BVHTree.FromPolygons([MB.co[i] for i in head_vs],
                           [[v - head_vs.start for v in MB.faces[f]] for f in head_fs])
build_ears()
build_face_cards()
build_neck()
build_shirt()
build_jacket()
build_collar()
build_arm(1)
build_arm(-1)
build_pants()
build_shoes()
build_hair()
bad = [i for i, w in enumerate(MB.w) if not w]
assert not bad, f"{len(bad)} vertices without weights"
print(f"[Ryuta] geometry: {len(MB.co)} verts, {sum(len(f) - 2 for f in MB.faces)} tris, "
      f"{len(MB.islands)} islands ({time.time() - T0:.1f}s)")


# ============================================================ UV atlas packing (shelf packer, max texel density)
def pack(atlas):
    S, pad = ATLAS[atlas], 4
    ids = [i for i, isl in enumerate(MB.islands) if isl["atlas"] == atlas]

    def size(i, d):
        isl = MB.islands[i]
        return max(isl["W"] * d * isl["mult"], 3), max(isl["H"] * d * isl["mult"], 3)

    def attempt(d):
        order = sorted(ids, key=lambda i: -size(i, d)[1])
        x = y = shelf = 0.0
        out = {}
        for i in order:
            w, h = size(i, d)
            w2, h2 = w + 2 * pad, h + 2 * pad
            if w2 > S:
                return None
            if x + w2 > S:
                x, y, shelf = 0.0, y + shelf, 0.0
            if y + h2 > S:
                return None
            out[i] = (x + pad, y + pad, w, h)
            x += w2
            shelf = max(shelf, h2)
        return out

    lo, hi = 10.0, 50000.0
    for _ in range(40):
        mid = (lo + hi) / 2
        if attempt(mid):
            lo = mid
        else:
            hi = mid
    rects = attempt(lo)
    for i, r in rects.items():
        MB.islands[i]["rect"] = r
    return lo


DENSITY = {a: pack(a) for a in ATLAS}
FUV = []
for f, uvs in enumerate(MB.fuv):
    isl = MB.islands[MB.fisl[f]]
    x, y, w, h = isl["rect"]
    S = ATLAS[isl["atlas"]]
    FUV.append([((x + u * w) / S, (y + v * h) / S) for u, v in uvs])
print(f"[Ryuta] texel density: body {DENSITY['body']:.0f} px/m, face {DENSITY['face']:.0f} px/m")


# ============================================================ painting (all colours sRGB)
def C(*rgb):
    return np.array(rgb, dtype=np.float32)


SKIN, SKIN_SH, BLUSH = C(0.98, 0.85, 0.75), C(0.90, 0.69, 0.63), C(0.93, 0.62, 0.58)
BANDAID, BANDAID_PAD, BANDAID_EDGE = C(0.94, 0.80, 0.64), C(0.98, 0.92, 0.84), C(0.80, 0.62, 0.48)
JACKET_C, JLINE, STITCH = C(0.10, 0.105, 0.15), C(0.05, 0.05, 0.08), C(0.17, 0.18, 0.25)
LINING = C(0.33, 0.23, 0.48)
GOLD, GOLD_DK, GOLD_HI = C(0.88, 0.70, 0.30), C(0.52, 0.36, 0.13), C(1.0, 0.94, 0.72)
SHIRT_C, SHIRT_RIB, PRINT = C(0.93, 0.92, 0.90), C(0.83, 0.82, 0.83), C(0.56, 0.50, 0.90)
PANTS_C, PLINE, PHI = C(0.12, 0.12, 0.16), C(0.06, 0.06, 0.09), C(0.20, 0.20, 0.27)
BELT, BUCKLE, BUCKLE_DK = C(0.21, 0.15, 0.12), C(0.80, 0.81, 0.84), C(0.36, 0.37, 0.42)
SHOE_C, SOLE, SOLE_LINE, LACE, ACCENT = C(0.93, 0.91, 0.86), C(0.99, 0.99, 0.97), C(0.26, 0.26, 0.32), \
    C(0.78, 0.78, 0.82), C(0.55, 0.48, 0.92)
HAIR_C_, HAIR_HI, HAIR_DK = C(0.11, 0.11, 0.17), C(0.30, 0.33, 0.50), C(0.07, 0.07, 0.11)
EYE_W, EYE_SH, IRIS_DK, IRIS_LO, PUPIL, LASH_C = C(0.98, 0.98, 1.0), C(0.80, 0.80, 0.90), C(0.22, 0.19, 0.33), \
    C(0.52, 0.46, 0.74), C(0.08, 0.06, 0.12), C(0.10, 0.07, 0.10)
BROW_C = C(0.13, 0.12, 0.18)
MOUTH_IN, TONGUE, TEETH, MOUTH_LINE = C(0.33, 0.10, 0.13), C(0.86, 0.47, 0.48), C(0.98, 0.98, 0.98), C(0.20, 0.07, 0.09)


def base(n, c, a=1.0):
    out = np.empty((n, 4), np.float32)
    out[:, :3] = c
    out[:, 3] = a
    return out


def tab(rows, col, x):
    return np.interp(x, [r[0] for r in rows], [r[col] for r in rows])


def qe_np(a, rs, rf, rb, cy):
    sa, ca = np.sin(a), np.cos(a)
    return rs * sa, np.where(ca > 0, cy - rf * ca, cy - rb * ca)


def p_jacket(P, lining=False):
    x, y, z = P.T
    out = base(len(P), LINING if lining else JACKET_C)
    cy, gap = tab(JACKET, 4, z), np.radians(tab(JACKET, 5, z))
    a = np.arctan2(x, -(y - cy))
    edge = np.abs(a) - gap
    if lining:
        out[edge < 0.2, :3] = JACKET_C                        # front facing in jacket cloth
        out[np.abs(edge - 0.2) < 0.012, :3] = STITCH
        return out
    out[(edge >= -0.01) & (edge < 0.022), :3] = STITCH         # edge piping
    out[(np.abs(x) < 0.0016) & (y > 0.04), :3] = JLINE         # back seam
    out[np.abs(z - 0.851) < 0.0011, :3] = STITCH               # hem stitch
    out[(np.abs(z - 0.952) < 0.0012) & (y < 0) & (np.abs(x) > 0.085) & (np.abs(x) < 0.155), :3] = JLINE  # pockets
    for zb in BUTTONS_Z:
        rs, rf, rb, cyb, g = table_at(JACKET, zb)
        g = math.radians(g)
        bx, by = qe(-(g + 0.15), rs, rf, rb, cyb)              # buttons on his right panel
        d = np.sqrt((x - bx) ** 2 + (y - by) ** 2 + (z - zb) ** 2)
        out[d < 0.0095, :3] = GOLD_DK
        out[d < 0.0078, :3] = GOLD
        out[np.sqrt((x - bx + 0.002) ** 2 + (y - by) ** 2 + (z - zb - 0.003) ** 2) < 0.0022, :3] = GOLD_HI
        ah = g + 0.10                                          # buttonholes on the left panel
        out[(np.abs(z - zb) < 0.0011) & (np.abs(a - ah) * rf < 0.0085), :3] = JLINE
    return out


def p_collar(P, inner=False):
    x, y, z = P.T
    out = base(len(P), JACKET_C)
    if inner:
        out[z > 1.500, :3] = SHIRT_C                            # white celluloid liner
        return out
    rs, rf, rb, cy = table_at([(c[0],) + c[1:] for c in COLLAR], 1.49)
    bx, by = qe(math.radians(70), rs, rf, rb, cy)
    d = np.sqrt((x - bx) ** 2 + (y - by) ** 2 + (z - 1.49) ** 2)
    out[d < 0.0068, :3] = GOLD_DK                               # school badge on his left collar
    out[d < 0.0055, :3] = GOLD
    out[d < 0.0022, :3] = GOLD_DK
    return out


def p_shirt(P):
    x, y, z = P.T
    out = base(len(P), SHIRT_C)
    out[z > 1.447, :3] = SHIRT_RIB
    front = y < -0.04
    c1 = np.hypot(x, z - 1.285) < 0.040
    c2 = np.hypot(x - 0.015, z - 1.293) < 0.035
    out[front & c1 & ~c2, :3] = PRINT                           # sleepy crescent moon print
    for sx, sz, r in ((-0.035, 1.322, 0.0045), (0.028, 1.332, 0.0035), (0.045, 1.25, 0.003)):
        out[front & (np.hypot(x - sx, z - sz) < r), :3] = PRINT
    return out


def p_sleeve(P):
    x, y, z = P.T
    out = base(len(P), JACKET_C)
    out[np.abs(x - 0.666) < 0.0011, :3] = STITCH                # cuff
    out[(z < 1.428 - 0.044) & (np.abs(y - 0.018) < 0.0014), :3] = JLINE   # under-arm seam
    back = y > 0.04
    for bx in (0.676, 0.692):
        d = np.hypot(x - bx, z - 1.418)
        out[back & (d < 0.0048), :3] = GOLD_DK
        out[back & (d < 0.0036), :3] = GOLD
    front = y < -0.03
    for ex in (0.437, 0.452):                                   # elbow creases
        out[front & (np.abs((x - ex) - 2.5 * (z - 1.428) ** 2 * 10) < 0.0012) & (np.abs(z - 1.428) < 0.018), :3] = JLINE
    return out


def p_pants(P):
    x, y, z = P.T
    out = base(len(P), PANTS_C)
    ax = np.abs(x)
    belt = (z > 0.957) & (z < 0.989)
    out[belt, :3] = BELT
    out[belt & ((np.abs(z - 0.958) < 0.0012) | (np.abs(z - 0.988) < 0.0012)), :3] = PLINE
    a = np.degrees(np.arctan2(x, -y))
    for la in (-130, -55, 55, 130, 180):                        # belt loops
        da = np.abs((a - la + 180) % 360 - 180)
        out[belt & (da < 2.0), :3] = PANTS_C
    front = y < 0
    buck = front & (ax < 0.022) & (z > 0.954) & (z < 0.992)
    out[buck, :3] = BUCKLE
    out[buck & (ax < 0.013) & (np.abs(z - 0.973) < 0.0085), :3] = BUCKLE_DK
    out[buck & (np.abs(x - 0.002) < 0.0012) & (np.abs(z - 0.973) < 0.009), :3] = BUCKLE
    out[(z > 0.995) & (z < 1.01), :3] = PLINE
    out[front & (np.abs(x - 0.007) < 0.0011) & (z > 0.84) & (z < 0.955), :3] = PLINE      # fly
    for sgn in (1, -1):                                         # slanted front pockets
        t = np.clip((z - 0.955) / (0.89 - 0.955), 0, 1)
        px = sgn * (0.11 + 0.04 * t)
        out[front & (z < 0.955) & (z > 0.89) & (np.abs(x - px) < 0.0013), :3] = PLINE
    # legs: pressed crease, side seam, knee wrinkles, hem
    keys = [0.0, 0.085, 0.49, 0.905, 1.2]
    cxs = [0.102, 0.102, 0.098, 0.092, 0.090]
    cys = [-0.004, 0.018, -0.008, 0.0, 0.0]
    lcx, lcy = np.interp(z, keys, cxs), np.interp(z, keys, cys)
    lx = ax - lcx
    leg = z < 0.86
    out[leg & (np.abs(lx) < 0.0012) & (y < lcy) & (z > 0.12), :3] = PHI
    out[leg & (np.abs(y - lcy) < 0.0013) & (lx > 0.04), :3] = PLINE
    for kz in (0.475, 0.49, 0.508):
        out[leg & (np.abs(z - kz - 0.15 * lx ** 2 * 30) < 0.0009) & (y < lcy) & (np.abs(lx) > 0.012) & (np.abs(lx) < 0.05), :3] = PLINE
    out[leg & (z < 0.072), :3] = PLINE * 0.6 + PANTS_C * 0.4
    return out


def p_lining(P):
    return base(len(P), LINING * 0.7)


def p_shoe(P):
    xo, y, z = P.T
    out = base(len(P), SHOE_C)
    h = np.interp(y, [r[0] for r in SHOE], [r[2] for r in SHOE])
    out[z < 0.017, :3] = SOLE
    out[np.abs(z - 0.017) < 0.0014, :3] = SOLE_LINE
    out[z < 0.004, :3] = SOLE_LINE
    toe = (y < -0.132) & (z < 0.042)
    out[toe & (z > 0.017), :3] = SOLE
    out[(np.abs(y + 0.132) < 0.0012) & (z < 0.042) & (z > 0.017), :3] = SOLE_LINE
    top = (np.abs(xo) < 0.02) & (y > -0.105) & (y < -0.01) & (z > h - 0.014)
    out[top, :3] = SHOE_C * 0.9
    out[top & (np.mod((y + 0.105) / 0.013, 1.0) < 0.38), :3] = LACE
    for ex in (-0.017, 0.017):
        for ey in np.arange(-0.098, -0.012, 0.013):
            out[(np.hypot(xo - ex, y - ey) < 0.0022) & (z > h - 0.02), :3] = SOLE_LINE
    out[(y > 0.06) & (z > 0.04) & (np.abs(xo) < 0.012), :3] = ACCENT        # heel tab
    out[(xo > 0.03) & (np.abs(z - 0.04 - 0.08 * (y + 0.02)) < 0.0045) & (y > -0.07) & (y < 0.04), :3] = ACCENT
    return out


def p_skin(P):
    return base(len(P), SKIN, 0.0)


def p_neck(P):
    x, y, z = P.T
    out = base(len(P), SKIN, 0.0)
    out[(z > 1.526 + 0.25 * np.abs(x)) & (y < -0.012) & (np.abs(x) < 0.036), :3] = SKIN_SH          # shadow under the jaw
    return out


def p_head(P):
    x, y, z = P.T
    out = base(len(P), SKIN)
    face = y < -0.05
    curve = MZ - 0.0012 * (x / 0.0105) ** 2
    out[face & (np.abs(z - curve) < 0.00065) & (np.abs(x) < 0.0105), :3] = MOUTH_LINE
    out[face & (np.abs(x - 0.004) < 0.002 - (z - 1.572) * 0.25) & (z > 1.572) & (z < 1.58) & (y < -0.085), :3] = SKIN_SH
    # band-aid on his left cheek
    dx, dz = x - 0.047, z - 1.585
    ca, sa = math.cos(math.radians(25)), math.sin(math.radians(25))
    u, v = dx * ca + dz * sa, -dx * sa + dz * ca
    ba = face & (np.abs(u) < 0.012) & (np.abs(v) < 0.0045) & (x > 0)
    out[ba, :3] = BANDAID
    out[ba & ((np.abs(u) > 0.011) | (np.abs(v) > 0.0038)), :3] = BANDAID_EDGE
    out[ba & (np.abs(u) < 0.0042) & (np.abs(v) < 0.0034), :3] = BANDAID_PAD
    return out


def p_ear(P):
    xo, y, z = P.T
    out = base(len(P), SKIN)
    out[(((y - 0.009) / 0.0075) ** 2 + ((z - 1.613) / 0.017) ** 2 < 1) & (xo > 0.004), :3] = SKIN_SH
    return out


def almond(X, Y):
    """Eye outline in metres from the eye centre (X towards the outer corner)."""
    hw = EW / 2
    q = np.clip(1 - (X / hw) ** 2, 0, 1)
    top = EH / 2 * 0.86 * q ** 0.55
    bot = -EH / 2 * 0.72 * q ** 0.75
    return top, bot, (np.abs(X) < hw * 0.985) & (Y < top) & (Y > bot)


def p_eye(P):
    s, t, side = P.T
    X, Y = s * EW / 2, t * EH / 2
    top, bot, inside = almond(X, Y)
    out = base(len(P), EYE_W, 0.0)
    out[inside, 3] = 1.0
    out[inside & (Y > top - 0.0022), :3] = EYE_SH
    Xw = X * side                                                # world-consistent iris and highlight
    ix, iy = -0.0012 * side, -0.0006
    d = np.sqrt(((Xw - ix) / 0.0088) ** 2 + ((Y - iy) / 0.0098) ** 2)
    iris = inside & (d < 1)
    k = np.clip((Y - iy) / 0.0098 * 0.5 + 0.5, 0, 1)[:, None]
    out[iris, :3] = (IRIS_LO * (1 - k) + IRIS_DK * k)[iris]
    out[iris & (d < 0.42), :3] = PUPIL
    out[iris & (d > 0.9), :3] = IRIS_DK * 0.7
    out[inside & (np.hypot(Xw - ix + 0.0032, Y - iy - 0.0034) < 0.0017), :3] = EYE_W
    out[inside & (np.hypot(Xw - ix - 0.0028, Y - iy + 0.0036) < 0.0008), :3] = EYE_W
    out[inside & (Y < bot + 0.0007 + 0.0005 * np.clip(X / (EW / 2), 0, 1)), :3] = LASH_C
    return out


def p_lid(P):
    s, tl, side = P.T
    X = s * EW / 2
    Yl = tl * (LID_TOP - LASH) * EH / 2
    hw = EW / 2
    out = base(len(P), SKIN, 0.0)
    out[np.abs(X) < hw * 1.06, 3] = 1.0
    th = 0.0016 + 0.0011 * np.clip(s, 0, 1)
    lash = (Yl < th) & (np.abs(X) < hw * 1.06)
    wing = (X > hw * 0.8) & (X < hw * 1.12) & (Yl < th + (X - hw * 0.8) * 0.45) & (Yl > (X - hw * 0.8) * 0.3)
    out[lash | wing, :3] = LASH_C
    out[lash | wing, 3] = 1.0
    crease = (np.abs(Yl - 0.0056 - 0.001 * (1 - (X / hw) ** 2)) < 0.00035) & (np.abs(X) < hw * 0.8)
    out[crease, :3] = SKIN_SH
    return out


def p_brow(P):
    sa, tb, side = P.T
    out = base(len(P), BROW_C, 0.0)
    ends = np.minimum(sa / 0.08, (1 - sa) / 0.12)
    out[(np.abs(tb) < 0.82 * np.clip(ends, 0, 1) ** 0.5), 3] = 1.0
    return out


def p_mouth(P):
    s, t, _ = P.T
    lens = (1 - s * s) ** 0.6
    inside = np.abs(t) < lens
    out = base(len(P), MOUTH_IN, 0.0)
    out[inside, 3] = 1.0
    out[inside & (t > 0.45 * lens), :3] = TEETH
    out[inside & (t < -0.45 * lens) & (np.abs(s) < 0.65), :3] = TONGUE
    out[inside & (np.abs(t) > lens - 0.14), :3] = MOUTH_LINE
    return out


def p_hair(P):
    d = P - np.array(HAIR_C, dtype=np.float32)
    r = np.linalg.norm(d, axis=1) + 1e-6
    elev = np.degrees(np.arcsin(np.clip(d[:, 2] / r, -1, 1)))
    az = np.arctan2(d[:, 0], -d[:, 1])
    out = base(len(P), HAIR_C_)
    out[elev < -5, :3] = HAIR_DK * 0.5 + HAIR_C_ * 0.5
    band = np.abs(elev - (38 + 3.5 * np.sin(az * 7))) < 2.2 + 1.0 * np.sin(az * 23)
    out[band, :3] = HAIR_HI
    return out


PAINT = {"jacket": p_jacket, "lining": p_lining, "collar": p_collar,
         "collar_in": lambda P: p_collar(P, inner=True), "shirt": p_shirt, "sleeve": p_sleeve,
         "pants": p_pants, "shoe": p_shoe, "hand": p_skin, "neck": p_neck, "head": p_head, "ear": p_ear,
         "eye": p_eye, "lid": p_lid, "brow": p_brow, "mouth": p_mouth, "hair": p_hair}
# the jacket's inner surface uses the jacket function with lining=True
PAINT["lining_jacket"] = lambda P: p_jacket(P, lining=True)


def bake(atlas):
    S = ATLAS[atlas]
    img = np.zeros((S, S, 4), np.float32)
    filled = np.zeros((S, S), bool)
    buckets = {}
    for f, vs in enumerate(MB.faces):
        isl = MB.islands[MB.fisl[f]]
        if isl["atlas"] != atlas:
            continue
        uv = np.array(FUV[f], np.float64) * S
        pc = np.array([MB.pc[v] for v in vs], np.float64)
        for t in range(1, len(vs) - 1):
            tri = uv[[0, t, t + 1]]
            tp = pc[[0, t, t + 1]]
            x0, y0 = np.floor(tri.min(0)).astype(int)
            x1, y1 = np.ceil(tri.max(0)).astype(int)
            x0, y0, x1, y1 = max(x0, 0), max(y0, 0), min(x1, S - 1), min(y1, S - 1)
            if x1 < x0 or y1 < y0:
                continue
            gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
            (ax, ay), (bx, by), (cx, cy) = tri
            den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
            if abs(den) < 1e-9:
                continue
            l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
            l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
            l3 = 1 - l1 - l2
            m = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
            if not m.any():
                continue
            P = l1[m, None] * tp[0] + l2[m, None] * tp[1] + l3[m, None] * tp[2]
            b = buckets.setdefault(isl["part"], ([], [], []))
            b[0].append(gy[m].astype(int))
            b[1].append(gx[m].astype(int))
            b[2].append(P)
    for part, (ys, xs, Ps) in buckets.items():
        ys, xs, P = np.concatenate(ys), np.concatenate(xs), np.concatenate(Ps).astype(np.float32)
        img[ys, xs] = PAINT[part](P)
        filled[ys, xs] = True
    for _ in range(12):                                          # dilate islands into the padding
        empty = ~filled
        if not empty.any():
            break
        acc = np.zeros_like(img)
        cnt = np.zeros((S, S), np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            sh = np.roll(np.roll(img, dy, 0), dx, 1)
            sf = np.roll(np.roll(filled, dy, 0), dx, 1)
            acc[sf] += sh[sf]
            cnt[sf] += 1
        new = empty & (cnt > 0)
        img[new] = acc[new] / cnt[new][:, None]
        filled |= new
    if atlas == "body":
        img[~filled] = np.append(JACKET_C, 1.0)
    return img


def save_png(arr, name):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(name, w, h, alpha=True)
    im.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    path = os.path.join(TEX, name + ".png")
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    return im, path


body_img = bake("body")
face_img = bake("face")
IM_BODY, P_BODY = save_png(body_img, "Ryuta_Body")
IM_FACE, P_FACE = save_png(face_img, "Ryuta_Face")
ramp = np.zeros((4, 256, 4), np.float32)
xs = np.arange(256) / 255.0
ramp[:, :, :3] = np.where(xs < 0.47, 0.0, np.where(xs < 0.53, 0.55, 1.0))[None, :, None]
ramp[:, :, 3] = 1.0
IM_RAMP, P_RAMP = save_png(ramp, "Ryuta_Ramp")
print(f"[Ryuta] textures baked ({time.time() - T0:.1f}s)")


# ============================================================ Blender mesh, weights, normals, colours
me = bpy.data.meshes.new("Ryuta_Mesh")
me.from_pydata([tuple(c) for c in MB.co], [], MB.faces)
me.update()
me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
me.polygons.foreach_set("material_index", MB.fmat)
uvl = me.uv_layers.new(name="UVMap")
uvl.data.foreach_set("uv", [c for f in FUV for uv in f for c in uv])
col = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")    # R = outline width
col.data.foreach_set("color", [c for o in MB.ol for c in (o, 1.0, 1.0, 1.0)])
ob = bpy.data.objects.new("Ryuta_Mesh", me)
scene.collection.objects.link(ob)

for name, img in zip(MAT_NAMES, (IM_BODY, IM_FACE, IM_BODY)):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if name == "Ryuta_Face":
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    me.materials.append(m)

groups = {}
for i, w in enumerate(MB.w):
    for b, v in w.items():
        g = groups.get(b) or ob.vertex_groups.new(name=b)
        groups[b] = g
        g.add([i], v, "REPLACE")
missing = set(groups) - set(BONES)
assert not missing, f"weights on unknown bones: {missing}"

# Face, face cards and hair cap get normals from smooth ellipsoids: clean anime shading, no dirty shadows.
me.update()
nrm = [v.normal.copy() for v in me.vertices]
face_set = set(head_vs) | {c[0] for c in FACE_CARDS}
for i in face_set:
    nrm[i] = head_nrm(MB.co[i])
try:
    me.normals_split_custom_set_from_vertices(nrm)
except AttributeError:
    me.normals_split_custom_set([nrm[l.vertex_index] for l in me.loops])


# ============================================================ face blend shapes
def lid_delta(name, side, s):
    sc = max(min(s, 1.0), -1.0)
    if name in ("blink_L", "blink_R"):
        if (name == "blink_L") != (side > 0):
            return 0.0
        return (-0.72 * (1 - sc * sc) ** 0.75 + 0.06) - LASH
    return {"bored": -0.24, "angry": -0.16 - 0.36 * (1 - sc) / 2, "surprised": 0.55,
            "smirk": -0.16 if side > 0 else 0.0}.get(name, 0.0)


def lid_row_k(name, tl):
    if tl <= 0.22:
        return 1.0
    if name == "surprised":
        return 0.55 if tl < 0.9 else 0.15
    return 0.45 if tl < 0.9 else 0.0


def brow_delta(name, side, sa):
    if name == "angry":
        return 0.0015 * (1 - sa), -0.0045 * (1 - sa) + 0.0012 * sa
    if name == "surprised":
        return 0.0, 0.0055 + 0.001 * sa
    if name == "bored":
        return 0.0, -0.0012
    if name == "smirk" and side < 0:
        return 0.0, 0.0028 * (0.6 + 0.4 * sa)
    return 0.0, 0.0


MOUTH = {
    "mouth_A": dict(w=0.022, h=0.019, jaw=0.0075), "mouth_I": dict(w=0.029, h=0.0055, jaw=0.001, corner=0.0008),
    "mouth_U": dict(w=0.012, h=0.009, jaw=0.0025), "mouth_E": dict(w=0.027, h=0.0095, jaw=0.003),
    "mouth_O": dict(w=0.016, h=0.016, jaw=0.005), "angry": dict(w=0.022, h=0.0055, jaw=0.0005, corner=-0.0018),
    "smirk": dict(w=0.022, h=0.0045, jaw=0.0, lift=0.0035, shift=0.0015, hfn=lambda s: smoothstep(-0.4, 0.5, s)),
    "surprised": dict(w=0.012, h=0.008, jaw=0.003),
}
SHAPES = ["blink_L", "blink_R", "angry", "bored", "smirk", "surprised",
          "mouth_A", "mouth_I", "mouth_U", "mouth_E", "mouth_O"]


def shape_positions(name):
    pos = {}
    m = MOUTH.get(name)
    for v, kind, side, a, b in FACE_CARDS:
        if kind == "eye":
            sc = 1.06 if name == "surprised" else 1.0
            x, z = eye_xz(side, a, b * sc)
            pos[v] = face_pt(x, z, EYE_OFF)
        elif kind == "lid":
            x, z = eye_xz(side, a, lid_t(b) + lid_delta(name, side, a) * lid_row_k(name, b))
            pos[v] = face_pt(x, z, LID_OFF)
        elif kind == "brow":
            dx, dz = brow_delta(name, side, a)
            x, z = brow_xz(side, a, b, dx, dz)
            pos[v] = face_pt(x, z, BROW_OFF)
        elif kind == "mouth":
            x, z = mouth_xz(a, b, m or MOUTH_REST)
            pos[v] = face_pt(x, z, MOUTH_OFF if m else MOUTH_HIDE)
    jaw = m.get("jaw", 0.0) if m else 0.0
    if jaw:
        for i in head_vs:
            p = MB.co[i]
            f = smoothstep(MZ + 0.003, MZ - 0.014, p.z) * smoothstep(-0.02, -0.06, p.y)
            if f > 0:
                pos[i] = p + V((0, 0.25 * jaw * f, -jaw * f))
    return pos


ob.shape_key_add(name="Basis", from_mix=False)
for name in SHAPES:
    kb = ob.shape_key_add(name=name, from_mix=False)
    kb.value = 0.0                                   # new keys start at 1 in Blender 5; the FBX carries the value
    for i, p in shape_positions(name).items():
        kb.data[i].co = p


# ============================================================ armature
arm_data = bpy.data.armatures.new("Ryuta_Skeleton")
rig = bpy.data.objects.new("Ryuta_Rig", arm_data)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode="EDIT")
for name, (h, t, parent, up) in BONES.items():
    eb = arm_data.edit_bones.new(name)
    eb.head, eb.tail = h, t
    eb.align_roll(up)
    if parent:
        eb.parent = arm_data.edit_bones[parent]
        eb.use_connect = (h - arm_data.edit_bones[parent].tail).length < 1e-5
bpy.ops.object.mode_set(mode="OBJECT")
ob.parent = rig
ob.modifiers.new("Armature", "ARMATURE").object = rig

# ============================================================ export
fbx_path = os.path.join(UNITY, "Ryuta.fbx")
bpy.ops.object.select_all(action="DESELECT")
ob.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"ARMATURE", "MESH"},
                         use_mesh_modifiers=False, mesh_smooth_type="OFF", add_leaf_bones=False,
                         use_armature_deform_only=True, bake_anim=False, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", colors_type="LINEAR", path_mode="STRIP",
                         use_tspace=False, use_custom_props=False, bake_space_transform=True)

# Preview-only data after the export: outline thickness as a vertex group (Solidify reads it in ryuta_render.py).
og = ob.vertex_groups.new(name="outline")
for i, o in enumerate(MB.ol):
    if o > 0:
        og.add([i], o, "REPLACE")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ART, "Ryuta.blend"))

tris = sum(len(f) - 2 for f in MB.faces)
per_mat = [sum(len(f) - 2 for f, m in zip(MB.faces, MB.fmat) if m == k) for k in range(3)]
report = [
    f"Ryuta build {time.strftime('%Y-%m-%d %H:%M')}",
    f"triangles: {tris} (body {per_mat[0]}, face {per_mat[1]}, hair {per_mat[2]}); vertices (Blender): {len(MB.co)}",
    f"textures: Ryuta_Body 1024x1024 (body + hair), Ryuta_Face 512x512 (alpha clip), Ryuta_Ramp 256x4",
    f"texel density: body {DENSITY['body']:.0f} px/m, face {DENSITY['face']:.0f} px/m",
    f"bones ({len(BONES)}): " + ", ".join(BONES),
    f"blend shapes ({len(SHAPES)}): " + ", ".join(SHAPES),
    f"height: {max(c.z for c in MB.co):.3f} m with hair, head top {CROWN.z:.3f} m",
]
with open(os.path.join(ART, "build_report.txt"), "w", encoding="utf-8") as fh:
    fh.write("\n".join(report) + "\n")
print("\n".join("[Ryuta] " + r for r in report))
print(f"[Ryuta] done in {time.time() - T0:.1f}s -> {fbx_path}")
