# Main-menu environment for "One Funseki, Seven Days".
# Low-poly, flat-colour PS2 style (see the reference board). Every asset is built with bmesh,
# exported as its own FBX (front faces -Y in Blender) and rendered on a preview sheet.
# Run: blender -b --factory-startup --python build_menu_env.py -- <fbx_out_dir> <preview_dir>
import bpy, bmesh, math, os, random, sys
from mathutils import Matrix, Vector, Quaternion

ARGS = sys.argv[sys.argv.index("--") + 1:]
FBX_DIR, PREVIEW_DIR = ARGS[0], ARGS[1]
os.makedirs(FBX_DIR, exist_ok=True)
os.makedirs(PREVIEW_DIR, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
rng = random.Random(7)

# ------------------------------------------------------------------ palette (names are mapped in Unity)
PALETTE = {
    "Wall": "#DEDAD3", "WallLight": "#ECE9E3", "WallShade": "#B4B0A9", "Trim": "#F2F0EB", "Concrete": "#BDB8AE",
    "Roof": "#8F8C88", "Interior": "#3B4A63", "Glass": "#9FD3EE", "GlassDark": "#4A6A82", "Frosted": "#E3ECEF",
    "Frame": "#8E9AA0", "Curtain": "#F4EAD2", "Plywood": "#C9A26B", "Metal": "#7A8288", "Green": "#4F7F5A",
    "ChainLink": "#5F7F68", "Tile": "#7FB7B0", "DoorRed": "#C8553D", "DoorBlue": "#3E6FA8", "Black": "#1C1B22",
    "White": "#F4F4F4", "Red": "#E23C3C", "Blue": "#2F5D9E", "Yellow": "#FFD23F", "Orange": "#FF8A2B",
    "Gold": "#D4A93A", "Wood": "#8C6239", "WoodDark": "#5B3E28", "Cork": "#B98A5A", "TankWhite": "#DADBD5",
    "ACWhite": "#E6E6E1", "VendingRed": "#D93A3A", "VendingLight": "#FFF4D6", "TrashBag": "#2B2F3A",
    "TrashBlue": "#3E6FA8", "Cardboard": "#B58A5A", "BikeFrame": "#C0392B", "Tire": "#26262B",
    "Pink": "#F6A9C6", "PinkLight": "#FBC9DA", "PinkDeep": "#E58DB0", "Bark": "#5A3B32",
    "Leaf": "#7DBB5A", "LeafDark": "#5E9E47", "HouseCream": "#EFE6D2", "HouseGrey": "#C9CCCF",
    "HouseBlue": "#B8C7D3", "RoofBlue": "#4F6476", "RoofBrown": "#6B4A3A", "RoofDark": "#3C3F48",
    "Awning": "#3E8E7E", "LampGlow": "#FFE9A8", "Beak": "#3A3A40",
}

def srgb(h):
    h = h.lstrip("#")
    return tuple((int(h[i:i + 2], 16) / 255) ** 2.2 for i in (0, 2, 4))

MAT = {}
for name, hx in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*srgb(hx), 1)
    b.inputs["Roughness"].default_value = 0.9
    if name in ("VendingLight", "LampGlow"):
        b.inputs["Emission Color"].default_value = (*srgb(hx), 1)
        b.inputs["Emission Strength"].default_value = 1.5
    m.diffuse_color = (*srgb(hx), 1)
    MAT[name] = m

# ------------------------------------------------------------------ mesh builder

class Mesh:
    def __init__(self, name):
        self.name, self.bm, self.mats = name, bmesh.new(), []

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _tag(self, verts, mat):
        idx = self.mi(mat)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = idx

    def box(self, c, size, mat, rot=None):
        M = Matrix.Translation(Vector(c)) @ (rot.to_matrix().to_4x4() if rot else Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1))
        self._tag(bmesh.ops.create_cube(self.bm, size=1, matrix=M)["verts"], mat)

    def boxr(self, x0, x1, y0, y1, z0, z1, mat):
        self.box(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), (x1 - x0, y1 - y0, z1 - z0), mat)

    def cyl(self, p1, p2, r1, r2, seg, mat, cap=True):
        p1, p2 = Vector(p1), Vector(p2)
        d = p2 - p1
        M = Matrix.Translation((p1 + p2) / 2) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        r = bmesh.ops.create_cone(self.bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=d.length, matrix=M)
        self._tag(r["verts"], mat)

    def ico(self, c, scale, mat, sub=1, rot=None, jitter=0.0):
        M = Matrix.Translation(Vector(c)) @ (rot.to_matrix().to_4x4() if rot else Matrix.Identity(4)) @ Matrix.Diagonal((*scale, 1))
        verts = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=1, matrix=M)["verts"]
        if jitter:
            for v in verts:
                v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * jitter
        self._tag(verts, mat)

    def poly(self, pts, mat, double=False):
        vs = [self.bm.verts.new(p) for p in pts]
        self.bm.faces.new(vs).material_index = self.mi(mat)
        if double:
            vs = [self.bm.verts.new(p) for p in reversed(pts)]
            self.bm.faces.new(vs).material_index = self.mi(mat)

    def front_quad(self, x0, x1, z0, z1, y, mat, double=False):
        """Quad facing -Y."""
        self.poly([(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)], mat, double)

    def torus(self, c, R, r, seg, tseg, mat, axis="X"):
        c = Vector(c)
        rows = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring = []
            for j in range(tseg):
                b = 2 * math.pi * j / tseg
                rr = R + r * math.cos(b)
                p = Vector((r * math.sin(b), rr * math.cos(a), rr * math.sin(a))) if axis == "X" else \
                    Vector((rr * math.cos(a), r * math.sin(b), rr * math.sin(a)))
                ring.append(self.bm.verts.new(c + p))
            rows.append(ring)
        idx = self.mi(mat)
        for i in range(seg):
            for j in range(tseg):
                a, b = rows[i], rows[(i + 1) % seg]
                f = self.bm.faces.new((a[j], b[j], b[(j + 1) % tseg], a[(j + 1) % tseg]))
                f.material_index = idx

    def build(self):
        me = bpy.data.meshes.new(self.name)
        # World-space box projection in metres, so tiling textures (chain-link, dirt) line up everywhere.
        self.bm.normal_update()
        uv = self.bm.loops.layers.uv.new("UVMap")
        for f in self.bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in f.loops:
                p = loop.vert.co
                loop[uv].uv = (p.x, p.y) if ax == 2 else (p.y, p.z) if ax == 0 else (p.x, p.z)
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(MAT[m])
        for p in me.polygons:
            p.use_smooth = False
        o = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(o)
        return o

ASSETS = []
def asset(fn):
    ASSETS.append(fn)
    return fn

def rot_y(deg):
    return Quaternion((0, 1, 0), math.radians(deg))

def rot_z(deg):
    return Quaternion((0, 0, 1), math.radians(deg))

def rot_x(deg):
    return Quaternion((1, 0, 0), math.radians(deg))

# ------------------------------------------------------------------ shared pieces

def chain_fence(m, x0, x1, y, z0, height, post_step=2.5):
    n = max(1, round((x1 - x0) / post_step))
    step = (x1 - x0) / n
    for i in range(n + 1):
        x = x0 + i * step
        m.cyl((x, y, z0), (x, y, z0 + height), 0.035, 0.035, 6, "Green")
    m.cyl((x0, y, z0 + height), (x1, y, z0 + height), 0.03, 0.03, 6, "Green")
    m.cyl((x0, y, z0 + 0.08), (x1, y, z0 + 0.08), 0.025, 0.025, 6, "Green")
    m.front_quad(x0, x1, z0 + 0.05, z0 + height - 0.02, y, "ChainLink", double=True)

def ac_unit(m, c):
    x, y, z = c
    m.boxr(x - 0.4, x + 0.4, y - 0.15, y + 0.15, z - 0.28, z + 0.28, "ACWhite")
    m.cyl((x - 0.1, y - 0.15, z), (x - 0.1, y - 0.17, z), 0.2, 0.2, 10, "Metal")
    m.boxr(x - 0.35, x + 0.35, y - 0.05, y + 0.2, z - 0.36, z - 0.28, "Metal")  # bracket

def window(m, wx0, wx1, z0, z1, depth_y=0.24):
    """Window in a facade opening (facade at y=0, opening depth 0.4). Returns state for logging."""
    w = wx1 - wx0
    tr = z0 + (z1 - z0) * 0.72
    r = rng.random()
    state = "glass" if r < 0.62 else "broken" if r < 0.8 else "missing" if r < 0.88 else "boarded"
    t, d = 0.05, 0.07
    fy0, fy1 = depth_y - d / 2, depth_y + d / 2
    m.boxr(wx0, wx1, fy0, fy1, z0, z0 + t, "Frame")
    m.boxr(wx0, wx1, fy0, fy1, z1 - t, z1, "Frame")
    m.boxr(wx0, wx0 + t, fy0, fy1, z0, z1, "Frame")
    m.boxr(wx1 - t, wx1, fy0, fy1, z0, z1, "Frame")
    m.boxr((wx0 + wx1) / 2 - t / 2, (wx0 + wx1) / 2 + t / 2, fy0, fy1, z0, z1, "Frame")
    m.boxr(wx0, wx1, fy0, fy1, tr - t / 2, tr + t / 2, "Frame")
    m.boxr(wx0 - 0.06, wx1 + 0.06, -0.12, 0.26, z0 - 0.06, z0, "Trim")  # sill
    for s in range(2):
        sx0 = wx0 + t + s * (w / 2)
        sx1 = sx0 + w / 2 - 1.5 * t
        for (gz0, gz1) in ((z0 + t, tr - t / 2), (tr + t / 2, z1 - t)):
            if state == "glass" or (state == "broken" and s == 0):
                m.front_quad(sx0, sx1, gz0, gz1, depth_y, "Glass")
            elif state == "broken":
                # Jagged shards left in the corners of the frame.
                gw, gh = sx1 - sx0, gz1 - gz0
                m.poly([(sx0, depth_y, gz1), (sx0 + gw * rng.uniform(0.3, 0.6), depth_y, gz1), (sx0, depth_y, gz1 - gh * rng.uniform(0.4, 0.8))], "Glass")
                m.poly([(sx1, depth_y, gz0), (sx1, depth_y, gz0 + gh * rng.uniform(0.3, 0.7)), (sx1 - gw * rng.uniform(0.3, 0.7), depth_y, gz0)], "Glass")
                m.poly([(sx0, depth_y, gz0), (sx0 + gw * 0.25, depth_y, gz0), (sx0, depth_y, gz0 + gh * 0.3)], "Glass")
        if state == "glass" and rng.random() < 0.4:  # curtain bunched at one side
            cx0 = sx0 if rng.random() < 0.5 else sx1 - (sx1 - sx0) * 0.35
            m.front_quad(cx0, cx0 + (sx1 - sx0) * 0.35, z0 + 0.1, z1 - 0.08, depth_y + 0.08, "Curtain")
    if state == "boarded":
        for k in range(3):
            zc = z0 + (z1 - z0) * (0.22 + 0.28 * k)
            m.box(((wx0 + wx1) / 2, -0.04, zc), (w * 1.02, 0.03, 0.26), "Plywood", rot=rot_y(rng.uniform(-8, 8)))
    return state

# ------------------------------------------------------------------ school

FH, FLOORS = 3.4, 3
H = FH * FLOORS

@asset
def school_main():
    m = Mesh("School_Main")
    bay, bays, D = 3.4, 10, 11.0
    x0 = -bay * bays / 2
    x1 = -x0
    m.boxr(x0, x1, 0.4, D, 0, H, "Wall")
    m.front_quad(x0, x1, 0, H, 0.38, "Interior")
    for f in range(FLOORS):
        z0 = f * FH
        for b in range(bays):
            bx0 = x0 + b * bay
            if b in (4, 5):
                continue  # central tower
            m.boxr(bx0, bx0 + bay, 0, 0.4, z0, z0 + 0.95, "WallShade" if f == 0 else "Wall")
            m.boxr(bx0, bx0 + bay, 0, 0.4, z0 + 2.55, z0 + FH, "Wall")
            m.boxr(bx0, bx0 + 0.35, 0, 0.4, z0 + 0.95, z0 + 2.55, "Wall")
            window(m, bx0 + 0.35, bx0 + bay, z0 + 0.95, z0 + 2.55)
        m.boxr(x1 - 0.35, x1, 0, 0.4, z0 + 0.95, z0 + 2.55, "Wall")
        if f < FLOORS - 1:
            m.boxr(x0 - 0.1, x1 + 0.1, -0.35, 0.4, z0 + FH - 0.12, z0 + FH + 0.03, "Trim")
    m.boxr(x0 - 0.05, x1 + 0.05, -0.08, 0.4, 0, 0.45, "WallShade")             # plinth
    # Parapet, roof, rooftop fence, water tank.
    m.boxr(x0 - 0.1, x1 + 0.1, -0.1, 0.3, H, H + 0.9, "Trim")
    m.boxr(x0 - 0.1, x1 + 0.1, D - 0.3, D + 0.1, H, H + 0.9, "Trim")
    m.boxr(x0 - 0.1, x0 + 0.2, 0, D, H, H + 0.9, "Trim")
    m.boxr(x1 - 0.2, x1 + 0.1, 0, D, H, H + 0.9, "Trim")
    m.boxr(x0, x1, 0.3, D - 0.3, H, H + 0.05, "Roof")
    chain_fence(m, x0, -3.6, 0.1, H + 0.9, 1.6)
    chain_fence(m, 3.6, x1, 0.1, H + 0.9, 1.6)
    m.cyl((-10, D - 3, H + 1.3), (-10, D - 3, H + 2.9), 1.2, 1.2, 12, "TankWhite")
    m.cyl((-10, D - 3, H + 2.9), (-10, D - 3, H + 3.2), 1.2, 0.3, 12, "TankWhite")
    for dx, dy in ((-0.8, -0.8), (0.8, -0.8), (-0.8, 0.8), (0.8, 0.8)):
        m.cyl((-10 + dx, D - 3 + dy, H), (-10 + dx, D - 3 + dy, H + 1.3), 0.06, 0.06, 6, "Metal")
    # Central tower with crest, slit windows and the entrance.
    tx = 3.4
    m.boxr(-tx, tx, -0.8, 0.4, 0, H + 1.5, "WallLight")
    m.boxr(-tx - 0.2, tx + 0.2, -1.0, 0.6, H + 1.5, H + 1.75, "Trim")
    for f in (1, 2):
        for sx in (-2.4, -1.4, 1.4, 2.4):
            z0 = f * FH + 0.6
            m.boxr(sx - 0.34, sx + 0.34, -0.86, -0.78, z0 - 0.05, z0 + 2.05, "Frame")
            m.front_quad(sx - 0.28, sx + 0.28, z0, z0 + 2.0, -0.865, "Glass")
    cz = 2 * FH + 1.7
    m.cyl((0, -0.8, cz), (0, -0.9, cz), 0.8, 0.8, 16, "Gold")
    m.cyl((0, -0.9, cz), (0, -0.95, cz), 0.62, 0.62, 16, "Blue")
    for k in range(5):  # five-petal emblem (sakura)
        a = math.radians(90 + k * 72)
        m.cyl((0.28 * math.cos(a), -0.95, cz + 0.28 * math.sin(a)), (0.28 * math.cos(a), -0.98, cz + 0.28 * math.sin(a)), 0.17, 0.17, 8, "Gold")
    # Entrance: glass doors, canopy, steps.
    m.boxr(-2.5, 2.5, -0.86, -0.78, 0, 2.7, "Frame")
    for k in range(4):
        dx0 = -2.4 + k * 1.2
        m.front_quad(dx0 + 0.05, dx0 + 1.15, 0.05, 2.6, -0.87, "GlassDark")
    m.boxr(-3.9, 3.9, -3.8, -0.8, 2.9, 3.15, "Trim")
    for sx in (-3.5, 3.5):
        m.cyl((sx, -3.5, 0), (sx, -3.5, 2.9), 0.15, 0.15, 8, "Trim")
    m.boxr(-3.9, 3.9, -3.8, -0.8, 0, 0.13, "Concrete")
    m.boxr(-3.5, 3.5, -2.6, -0.8, 0.13, 0.26, "Concrete")
    # AC units and drainpipes.
    for (b, f) in ((1, 1), (2, 2), (7, 1), (8, 2), (9, 1)):
        ac_unit(m, (x0 + b * bay + bay / 2 + 0.6, -0.3, f * FH + 0.55))
    for x in (x0 + 0.2, -tx - 0.2, tx + 0.2, x1 - 0.2):
        m.cyl((x, -0.15, 0), (x, -0.15, H + 0.9), 0.07, 0.07, 8, "Metal")
    return m.build()

@asset
def school_wing():
    """Blank side wall ('сплошная стена') with a fire-escape stair. Front (blank) faces -Y."""
    m = Mesh("School_Wing")
    L, D = 24.0, 9.0
    x0, x1 = -L / 2, L / 2
    m.boxr(x0, x1, 0, D, 0, H, "Wall")
    m.boxr(x0 - 0.05, x1 + 0.05, -0.08, D, 0, 0.45, "WallShade")
    for f in range(1, FLOORS):
        m.boxr(x0 - 0.1, x1 + 0.1, -0.3, 0.05, f * FH - 0.12, f * FH + 0.03, "Trim")
    m.boxr(x0 - 0.1, x1 + 0.1, -0.1, 0.3, H, H + 0.9, "Trim")
    m.boxr(x0, x1, 0.3, D, H, H + 0.05, "Roof")
    chain_fence(m, x0, x1, 0.1, H + 0.9, 1.6)
    # Small frosted vents high up, louvres, drainpipes.
    for x in (2.5, 5.0, 7.5):
        m.boxr(x - 0.45, x + 0.45, -0.06, 0.02, 2 * FH + 1.6, 2 * FH + 2.3, "Frame")
        m.front_quad(x - 0.4, x + 0.4, 2 * FH + 1.65, 2 * FH + 2.25, -0.065, "Frosted")
    for x, z in ((-6.0, 2.6), (-1.0, FH + 2.6), (4.0, 2.6)):
        m.boxr(x - 0.3, x + 0.3, -0.08, 0.0, z, z + 0.4, "Metal")
        for k in range(4):
            m.boxr(x - 0.28, x + 0.28, -0.12, -0.08, z + 0.05 + k * 0.09, z + 0.08 + k * 0.09, "Frame")
    for x in (x0 + 0.25, -3.0, x1 - 3.4):
        m.cyl((x, -0.15, 0), (x, -0.15, H + 0.9), 0.07, 0.07, 8, "Metal")
    ac_unit(m, (1.5, -0.3, FH + 0.6))
    # Cork notice board on the left part of the wall (settings UI frames it).
    m.boxr(-10.6, -3.4, -0.1, 0.0, 0.9, 3.2, "WoodDark")
    m.boxr(-10.45, -3.55, -0.13, -0.1, 1.05, 3.05, "Cork")
    for k in range(9):  # old pinned papers
        px, pz = rng.uniform(-10.1, -4.2), rng.uniform(1.3, 2.7)
        m.box((px, -0.135, pz), (rng.uniform(0.3, 0.5), 0.005, rng.uniform(0.35, 0.5)), "White", rot=rot_y(rng.uniform(-10, 10)))
    # Fire-escape stair at the right end.
    sx0, sx1 = x1 - 3.0, x1 - 0.2
    for f in range(1, FLOORS + 1):
        z = f * FH if f < FLOORS else H + 0.05
        m.boxr(sx0 - 0.1, sx1, -1.4, 0.0, z - 0.1, z, "Green")
        m.cyl((sx0 - 0.1, -1.4, z), (sx1, -1.4, z), 0.03, 0.03, 6, "Green")
        m.cyl((sx0 - 0.1, -1.4, z + 1.0), (sx1, -1.4, z + 1.0), 0.03, 0.03, 6, "Green")
        for x in (sx0 - 0.1, sx1):
            m.cyl((x, -1.4, z), (x, -1.4, z + 1.0), 0.03, 0.03, 6, "Green")
            m.cyl((x, -1.4, 0), (x, -1.4, z), 0.06, 0.06, 6, "Green")
        zb = (f - 1) * FH
        run = Vector((sx1 - sx0 - 0.4, 0, z - zb))
        mid = Vector((sx0 + 0.2, -0.7, zb)) + run / 2
        ang = math.degrees(math.atan2(run.z, run.x))
        m.box(mid, (run.length, 1.1, 0.08), "Green", rot=rot_y(-ang))
        m.cyl((sx0 + 0.2, -1.3, zb + 1.0), (sx1 - 0.2, -1.3, z + 1.0), 0.025, 0.025, 6, "Green")
    return m.build()

# ------------------------------------------------------------------ toilet & bikes

@asset
def toilet_block():
    m = Mesh("ToiletBlock")
    W, D, Hh = 7.0, 4.5, 3.2
    m.boxr(-W / 2, W / 2, 0, D, 0, Hh, "Wall")
    m.boxr(-W / 2 - 0.02, W / 2 + 0.02, -0.03, D + 0.02, 0, 1.2, "Tile")
    m.boxr(-W / 2 - 0.4, W / 2 + 0.4, -0.6, D + 0.3, Hh, Hh + 0.22, "Trim")
    for sx, door, sign in ((-1.6, "DoorBlue", "Blue"), (1.6, "DoorRed", "Red")):
        m.boxr(sx - 0.62, sx + 0.62, -0.08, 0.0, 0, 2.28, "Trim")
        m.front_quad(sx - 0.5, sx + 0.5, 0.0, 2.15, -0.085, "Interior")
        m.boxr(sx - 0.3, sx + 0.3, -0.1, -0.04, 2.4, 3.0, "White")
        m.cyl((sx, -0.11, 2.83), (sx, -0.12, 2.83), 0.07, 0.07, 10, sign)
        if sign == "Blue":
            m.boxr(sx - 0.08, sx + 0.08, -0.12, -0.105, 2.5, 2.74, sign)
        else:
            m.poly([(sx - 0.12, -0.115, 2.5), (sx + 0.12, -0.115, 2.5), (sx, -0.115, 2.75)], sign)
    m.boxr(-0.7, 0.7, -0.07, 0.0, 2.2, 2.9, "Frame")
    m.front_quad(-0.64, 0.64, 2.25, 2.85, -0.075, "Frosted")
    for k in range(5):
        x = -0.55 + k * 0.275
        m.cyl((x, -0.1, 2.25), (x, -0.1, 2.85), 0.015, 0.015, 5, "Metal")
    m.cyl((2.6, 3.4, Hh), (2.6, 3.4, Hh + 1.2), 0.08, 0.08, 8, "Metal")
    m.cyl((2.6, 3.4, Hh + 1.2), (2.6, 3.4, Hh + 1.35), 0.16, 0.05, 8, "Metal")
    m.boxr(-0.2, 0.2, -0.25, -0.05, 2.0, 2.15, "Metal")                     # wall lamp
    m.boxr(-0.16, 0.16, -0.24, -0.06, 1.9, 2.0, "LampGlow")
    # Outdoor wash trough with taps along the side wall.
    m.boxr(W / 2, W / 2 + 0.6, 0.6, 3.8, 0, 0.8, "Concrete")
    m.boxr(W / 2 + 0.05, W / 2 + 0.55, 0.7, 3.7, 0.65, 0.8, "Interior")
    for k in range(4):
        y = 1.0 + k * 0.85
        m.cyl((W / 2 + 0.05, y, 1.15), (W / 2 + 0.3, y, 1.15), 0.025, 0.025, 6, "Metal")
        m.cyl((W / 2 + 0.3, y, 1.15), (W / 2 + 0.3, y, 1.0), 0.02, 0.02, 6, "Metal")
    return m.build()

@asset
def toilet_door():
    m = Mesh("ToiletDoor")  # pivot on the hinge (left edge)
    m.boxr(0, 1.0, -0.03, 0.03, 0, 2.12, "DoorRed")
    m.boxr(0.8, 0.88, -0.07, -0.03, 1.0, 1.1, "Metal")
    m.boxr(0.25, 0.75, -0.035, -0.03, 1.35, 1.75, "Frosted")
    return m.build()

@asset
def bicycle():
    m = Mesh("Bicycle")  # along Y, wheels on the ground
    R = 0.33
    for y in (0.52, -0.52):
        m.torus((0, y, R), R, 0.025, 16, 5, "Tire")
        m.cyl((-0.04, y, R), (0.04, y, R), 0.04, 0.04, 6, "Metal")
        for k in range(4):
            a = math.radians(k * 45)
            p = Vector((0, math.cos(a), math.sin(a))) * (R - 0.02)
            m.cyl((0, y - p.y, R - p.z), (0, y + p.y, R + p.z), 0.005, 0.005, 3, "Metal")
    rear, crank, seat, head, front = (0, 0.52, R), (0, 0.05, 0.3), (0, 0.18, 0.86), (0, -0.4, 0.9), (0, -0.52, R)
    for a, b in ((rear, crank), (rear, seat), (seat, crank), (crank, head), (seat, head), (head, front)):
        m.cyl(a, b, 0.022, 0.022, 6, "BikeFrame")
    m.cyl((0, 0.2, 0.86), (0, 0.22, 0.98), 0.015, 0.015, 5, "Metal")
    m.box((0, 0.24, 1.0), (0.14, 0.24, 0.06), "Black")
    m.cyl((0, -0.4, 0.9), (0, -0.36, 1.06), 0.018, 0.018, 5, "Metal")
    m.cyl((-0.28, -0.32, 1.06), (0.28, -0.32, 1.06), 0.016, 0.016, 5, "Metal")
    for x in (-0.28, 0.28):
        m.cyl((x, -0.32, 1.06), (x, -0.24, 1.06), 0.022, 0.022, 5, "Black")
    m.boxr(-0.17, 0.17, -0.82, -0.52, 0.78, 1.0, "Metal")  # mamachari basket
    m.boxr(-0.14, 0.14, -0.79, -0.55, 0.81, 1.0, "Interior")
    m.boxr(-0.03, 0.03, 0.05, 0.5, 0.36, 0.48, "BikeFrame")  # chain guard
    m.cyl((0.06, 0.4, 0.3), (0.2, 0.55, 0.02), 0.012, 0.012, 4, "Metal")  # kickstand
    return m.build()

@asset
def bike_shelter():
    m = Mesh("BikeShelter")
    W = 9.0
    for k in range(4):
        x = -W / 2 + 0.2 + k * (W - 0.4) / 3
        m.cyl((x, 0, 0), (x, 0, 2.45), 0.06, 0.06, 8, "Green")
        m.cyl((x, 2.4, 0), (x, 2.4, 2.15), 0.06, 0.06, 8, "Green")
    slope = math.degrees(math.atan2(0.4, 2.8))
    m.box((0, 1.2, 2.32), (W + 0.3, 3.0, 0.03), "Roof", rot=rot_x(slope))
    for k in range(int(W / 0.25)):
        x = -W / 2 + k * 0.25
        m.box((x, 1.2, 2.35), (0.06, 3.0, 0.04), "Metal", rot=rot_x(slope))
    m.cyl((-W / 2, 1.6, 0.45), (W / 2, 1.6, 0.45), 0.03, 0.03, 6, "Metal")
    for k in range(10):
        x = -W / 2 + 0.6 + k * 0.85
        m.cyl((x, 1.6, 0), (x, 1.6, 0.45), 0.02, 0.02, 5, "Metal")
    m.boxr(-W / 2 - 0.3, W / 2 + 0.3, -0.3, 2.9, 0, 0.06, "Concrete")
    return m.build()

# ------------------------------------------------------------------ gate, walls, street

@asset
def school_gate():
    m = Mesh("SchoolGate")  # street side is -Y, yard side +Y
    for x in (-4.3, 4.3):
        m.boxr(x - 0.45, x + 0.45, -0.45, 0.45, 0, 2.6, "Concrete")
        m.boxr(x - 0.55, x + 0.55, -0.55, 0.55, 2.6, 2.75, "Trim")
    m.boxr(-4.5, -4.1, 0.45, 0.5, 0.7, 2.3, "Wood")   # name plate (text added in Unity)
    m.boxr(-3.85, 3.85, -0.08, 0.08, 0, 0.03, "Metal")  # track
    # Sliding leaf, half open and knocked slightly off its rail.
    m.cyl((-3.85, 0.0, 0.18), (0.2, 0.0, 0.18), 0.04, 0.04, 6, "Green")
    m.cyl((-3.85, 0.0, 1.7), (0.2, 0.0, 1.62), 0.04, 0.04, 6, "Green")
    x = -3.8
    while x < 0.2:
        m.cyl((x, 0.0, 0.18), (x, 0.0, 1.7 - (x + 3.85) * 0.02), 0.018, 0.018, 4, "Green")
        x += 0.16
    for x in (-3.6, 0.0):
        m.cyl((x - 0.08, 0, 0.08), (x + 0.08, 0, 0.08), 0.07, 0.07, 8, "Black")
    return m.build()

@asset
def boundary_wall():
    m = Mesh("BoundaryWall")  # 10 m segment along X
    m.boxr(-5, 5, -0.13, 0.13, 0, 1.5, "Concrete")
    m.boxr(-5, 5, -0.18, 0.18, 1.5, 1.6, "Trim")
    chain_fence(m, -5, 5, 0, 1.6, 1.2)
    return m.build()

@asset
def utility_pole():
    m = Mesh("UtilityPole")
    m.cyl((0, 0, 0), (0, 0, 9.0), 0.17, 0.12, 8, "Concrete")
    for z, w in ((8.4, 1.6), (7.6, 1.2)):
        m.boxr(-w / 2, w / 2, -0.06, 0.06, z - 0.06, z + 0.06, "Metal")
        for x in (-w / 2 + 0.1, 0.0, w / 2 - 0.1):
            m.cyl((x, 0, z + 0.06), (x, 0, z + 0.22), 0.04, 0.03, 6, "White")
    m.cyl((0.35, 0, 5.8), (0.35, 0, 6.8), 0.25, 0.25, 10, "Metal")
    for k in range(10):
        m.cyl((0, 0, 2.0 + k * 0.45), (0.25, 0, 2.0 + k * 0.45), 0.012, 0.012, 4, "Metal")
    m.boxr(-0.2, 0.2, -0.18, -0.12, 2.2, 2.8, "Yellow")  # address plate
    return m.build()

def roof_gable(m, x0, x1, y0, y1, z, rise, mat, wall):
    ov = 0.35
    X0, X1, Y0, Y1 = x0 - ov, x1 + ov, y0 - ov, y1 + ov
    ym = (y0 + y1) / 2
    m.poly([(X0, Y0, z), (X1, Y0, z), (X1, ym, z + rise), (X0, ym, z + rise)], mat, double=True)
    m.poly([(X1, Y1, z), (X0, Y1, z), (X0, ym, z + rise), (X1, ym, z + rise)], mat, double=True)
    m.poly([(x0, y0, z), (x0, ym, z + rise), (x0, y1, z)], wall, double=True)
    m.poly([(x1, y1, z), (x1, ym, z + rise), (x1, y0, z)], wall, double=True)

def roof_hip(m, x0, x1, y0, y1, z, rise, mat):
    ov = 0.35
    X0, X1, Y0, Y1 = x0 - ov, x1 + ov, y0 - ov, y1 + ov
    ym = (y0 + y1) / 2
    inset = (y1 - y0) / 2
    r0, r1 = (x0 + inset, ym, z + rise), (x1 - inset, ym, z + rise)
    m.poly([(X0, Y0, z), (X1, Y0, z), r1, r0], mat, double=True)
    m.poly([(X1, Y1, z), (X0, Y1, z), r0, r1], mat, double=True)
    m.poly([(X0, Y1, z), (X0, Y0, z), r0], mat, double=True)
    m.poly([(X1, Y0, z), (X1, Y1, z), r1], mat, double=True)

def house(name, w, d, floors, wall, roof, kind):
    m = Mesh(name)
    fh = 2.9
    h = fh * floors
    m.boxr(-w / 2, w / 2, 0, d, 0, h, wall)
    for f in range(floors):
        for k in range(max(1, int(w // 2.6))):
            n = max(1, int(w // 2.6))
            cx = -w / 2 + (k + 0.5) * w / n
            z0 = f * fh + 0.9
            m.boxr(cx - 0.55, cx + 0.55, -0.06, 0.0, z0 - 0.05, z0 + 1.25, "Frame")
            m.front_quad(cx - 0.48, cx + 0.48, z0, z0 + 1.2, -0.065, "GlassDark")
    if floors >= 2:
        m.boxr(-w / 2 + 0.3, w / 2 - 0.3, -0.9, 0.0, fh - 0.1, fh, "Concrete")
        for k in range(int((w - 0.6) / 0.15)):
            x = -w / 2 + 0.35 + k * 0.15
            m.cyl((x, -0.88, fh), (x, -0.88, fh + 0.95), 0.012, 0.012, 4, "Metal")
        m.cyl((-w / 2 + 0.3, -0.88, fh + 0.95), (w / 2 - 0.3, -0.88, fh + 0.95), 0.025, 0.025, 5, "Metal")
        ac_unit(m, (w / 2 - 1.0, -0.5, fh + 0.3))
    m.boxr(-w / 2 + 0.4, -w / 2 + 1.3, -0.04, 0.0, 0, 2.0, "Wood")
    if kind == "gable":
        roof_gable(m, -w / 2, w / 2, 0, d, h, 1.6, roof, wall)
    elif kind == "hip":
        roof_hip(m, -w / 2, w / 2, 0, d, h, 1.5, roof)
    else:
        m.boxr(-w / 2 - 0.1, w / 2 + 0.1, -0.1, d + 0.1, h, h + 0.5, wall)
        m.box((0, -0.8, 2.6), (w, 1.6, 0.06), "Awning", rot=rot_x(-12))
        m.boxr(-w / 2 + 0.5, w / 2 - 0.5, -0.12, -0.02, h + 0.05, h + 0.45, "Red")
    return m.build()

@asset
def house_a(): return house("House_A", 8.0, 7.0, 2, "HouseCream", "RoofBlue", "gable")
@asset
def house_b(): return house("House_B", 9.0, 7.5, 2, "HouseGrey", "RoofBrown", "hip")
@asset
def house_c(): return house("House_C", 7.0, 8.0, 3, "HouseBlue", "RoofDark", "flat")

@asset
def apartment():
    m = Mesh("Apartment")
    w, d, floors, fh = 24.0, 10.0, 5, 2.9
    m.boxr(-w / 2, w / 2, 0, d, 0, floors * fh, "HouseCream")
    for f in range(floors):
        z = f * fh
        m.front_quad(-w / 2 + 0.3, w / 2 - 0.3, z + 0.3, z + 2.5, -0.01, "GlassDark")
        if f > 0:
            m.boxr(-w / 2, w / 2, -1.2, 0, z - 0.12, z, "Concrete")
            m.boxr(-w / 2, w / 2, -1.25, -1.1, z, z + 1.05, "WallLight")
        for k in range(1, 6):
            x = -w / 2 + k * w / 6
            m.boxr(x - 0.12, x + 0.12, -1.2, 0, z, z + fh, "HouseCream")
    m.boxr(-w / 2 - 0.1, w / 2 + 0.1, -0.1, d + 0.1, floors * fh, floors * fh + 0.4, "Trim")
    return m.build()

# ------------------------------------------------------------------ props

@asset
def vending_machine():
    m = Mesh("VendingMachine")  # front -Y
    m.boxr(-0.5, 0.5, 0, 0.75, 0.05, 1.85, "VendingRed")
    m.boxr(-0.52, 0.52, -0.02, 0.77, 0, 0.08, "Black")
    m.boxr(-0.5, 0.5, -0.04, 0.0, 1.62, 1.82, "White")
    m.boxr(-0.42, 0.42, -0.02, 0.05, 0.92, 1.58, "VendingLight")
    cans = ["Red", "Blue", "Yellow", "Leaf", "White", "Orange"]
    for row in range(3):
        z = 0.97 + row * 0.21
        m.boxr(-0.42, 0.42, -0.12, -0.02, z - 0.02, z, "Frame")
        for k in range(6):
            x = -0.35 + k * 0.14
            m.cyl((x, -0.07, z), (x, -0.07, z + 0.13), 0.035, 0.035, 8, cans[(k + row * 2) % 6])
    for k in range(6):
        m.boxr(-0.37 + k * 0.14, -0.33 + k * 0.14, -0.04, 0.0, 0.85, 0.89, "Yellow")
    m.boxr(0.22, 0.42, -0.04, 0.0, 0.45, 0.8, "Metal")
    m.boxr(-0.38, 0.15, -0.06, 0.0, 0.18, 0.4, "Black")
    return m.build()

@asset
def trash_bag():
    m = Mesh("TrashBag")
    m.ico((0, 0, 0.26), (0.32, 0.28, 0.27), "TrashBag", sub=2, jitter=0.03)
    m.cyl((0, 0, 0.48), (0.02, 0, 0.62), 0.06, 0.02, 6, "TrashBag")
    return m.build()

@asset
def trash_bag_blue():
    m = Mesh("TrashBagBlue")
    m.ico((0, 0, 0.24), (0.3, 0.27, 0.25), "TrashBlue", sub=2, jitter=0.03)
    m.cyl((0, 0, 0.45), (-0.02, 0, 0.58), 0.06, 0.02, 6, "TrashBlue")
    return m.build()

@asset
def cardboard_box():
    m = Mesh("CardboardBox")
    m.boxr(-0.3, 0.3, -0.22, 0.22, 0, 0.4, "Cardboard")
    m.box((0, -0.32, 0.47), (0.58, 0.2, 0.01), "Cardboard", rot=rot_x(-50))
    m.box((0, 0.3, 0.45), (0.58, 0.18, 0.01), "Cardboard", rot=rot_x(65))
    m.boxr(-0.31, 0.31, -0.03, 0.03, 0.4, 0.401, "Yellow")
    return m.build()

@asset
def can():
    m = Mesh("Can")
    m.cyl((0, 0, 0), (0, 0, 0.12), 0.033, 0.033, 8, "Red")
    m.cyl((0, 0, 0.12), (0, 0, 0.125), 0.03, 0.03, 8, "Metal")
    return m.build()

@asset
def bench():
    m = Mesh("Bench")
    for k in range(3):
        m.boxr(-0.9, 0.9, -0.2 + k * 0.14, -0.1 + k * 0.14, 0.42, 0.46, "Wood")
    for k in range(2):
        m.box((0, 0.24, 0.62 + k * 0.16), (1.8, 0.04, 0.1), "Wood", rot=rot_x(-12))
    for x in (-0.75, 0.75):
        m.boxr(x - 0.03, x + 0.03, -0.2, 0.2, 0, 0.42, "Metal")
        m.box((x, 0.22, 0.6), (0.06, 0.04, 0.4), "Metal", rot=rot_x(-12))
    return m.build()

@asset
def street_lamp():
    m = Mesh("StreetLamp")
    m.cyl((0, 0, 0), (0, 0, 5.0), 0.08, 0.06, 8, "Metal")
    m.cyl((0, 0, 5.0), (0, -0.8, 5.25), 0.04, 0.04, 6, "Metal")
    m.boxr(-0.18, 0.18, -1.15, -0.65, 5.12, 5.3, "Metal")
    m.boxr(-0.15, 0.15, -1.12, -0.68, 5.08, 5.12, "LampGlow")
    return m.build()

@asset
def traffic_cone():
    m = Mesh("TrafficCone")
    m.boxr(-0.2, 0.2, -0.2, 0.2, 0, 0.04, "Orange")
    m.cyl((0, 0, 0.04), (0, 0, 0.7), 0.15, 0.025, 10, "Orange")
    m.cyl((0, 0, 0.3), (0, 0, 0.42), 0.112, 0.09, 10, "White")
    return m.build()

@asset
def crow():
    m = Mesh("Crow")  # faces -Y
    m.ico((0, 0, 0.2), (0.09, 0.16, 0.1), "Black", sub=1, rot=rot_x(-15))
    m.ico((0, -0.15, 0.3), (0.065, 0.07, 0.065), "Black", sub=1)
    m.cyl((0, -0.2, 0.3), (0, -0.29, 0.29), 0.022, 0.003, 6, "Beak")
    m.box((0, 0.2, 0.17), (0.08, 0.18, 0.02), "Black", rot=rot_x(20))
    for x in (-0.03, 0.03):
        m.cyl((x, 0, 0.12), (x, -0.01, 0.0), 0.008, 0.008, 4, "Beak")
        m.ico((x * 2.6, -0.06, 0.32), (0.012, 0.012, 0.012), "White", sub=1)
    for s in (-1, 1):
        m.box((0.08 * s, 0.02, 0.21), (0.03, 0.22, 0.1), "Black", rot=rot_z(6 * s))
    return m.build()

def tree(name, leaf_mats, height, spread, clusters, seed, droop=0.0):
    r = random.Random(seed)
    m = Mesh(name)
    base = Vector((0, 0, 0))
    top = Vector((r.uniform(-0.4, 0.4), r.uniform(-0.4, 0.4), height * 0.55))
    m.cyl(base, top, 0.22, 0.15, 7, "Bark")
    m.cyl((0, 0, 0), (0, 0, 0.25), 0.34, 0.22, 7, "Bark")
    tips = []
    for k in range(5):
        a = math.radians(k * 72 + r.uniform(-20, 20))
        tip = top + Vector((math.cos(a) * spread * 0.55, math.sin(a) * spread * 0.55, r.uniform(0.6, 1.4)))
        m.cyl(top, tip, 0.12, 0.05, 6, "Bark")
        tips.append(tip)
    for k in range(clusters):
        anchor = tips[k % len(tips)] if k < len(tips) * 2 else top + Vector((0, 0, 1.2))
        c = anchor + Vector((r.uniform(-0.9, 0.9), r.uniform(-0.9, 0.9), r.uniform(-0.3, 0.7) - droop))
        s = r.uniform(0.9, 1.5) * spread / 3.2
        m.ico(c, (s * 1.15, s * 1.15, s * 0.8), leaf_mats[k % len(leaf_mats)], sub=1)
    m.ico(top + Vector((0, 0, 1.3)), (spread * 0.42, spread * 0.42, spread * 0.3), leaf_mats[0], sub=1)
    return m.build()

@asset
def sakura_a(): return tree("Sakura_A", ["Pink", "PinkLight", "PinkDeep"], 5.0, 3.6, 13, 11, droop=0.2)
@asset
def sakura_b(): return tree("Sakura_B", ["PinkLight", "Pink", "PinkDeep"], 4.2, 3.0, 11, 23, droop=0.1)
@asset
def green_tree(): return tree("GreenTree", ["Leaf", "LeafDark"], 6.0, 3.4, 12, 37)

@asset
def bush():
    m = Mesh("Bush")
    for k in range(5):
        m.ico((rng.uniform(-0.5, 0.5), rng.uniform(-0.25, 0.25), 0.35 + rng.uniform(0, 0.15)),
              (0.45, 0.4, 0.38), "LeafDark" if k % 2 else "Leaf", sub=1)
    return m.build()

@asset
def planter():
    m = Mesh("Planter")
    m.boxr(-1.2, 1.2, -0.35, 0.35, 0, 0.5, "Concrete")
    m.boxr(-1.1, 1.1, -0.25, 0.25, 0.4, 0.48, "Bark")
    for k in range(7):
        x = -0.9 + k * 0.3
        m.cyl((x, 0, 0.45), (x + rng.uniform(-0.05, 0.05), 0, 0.75), 0.01, 0.01, 4, "Leaf")
        m.ico((x, 0, 0.78), (0.07, 0.07, 0.05), ["Yellow", "Pink", "White"][k % 3], sub=1)
    return m.build()

# ------------------------------------------------------------------ build, export, preview

objects = []
for fn in ASSETS:
    o = fn()
    objects.append(o)
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(FBX_DIR, o.name + ".fbx"), use_selection=True,
                             object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False)
    print("EXPORTED", o.name, len(o.data.polygons))

# Preview sheet: lay everything out in rows by size.
rows, row, width = [], [], 0
for o in sorted(objects, key=lambda o: -max(o.dimensions)):
    row.append(o); width += o.dimensions.x + 2
    if width > 70:
        rows.append(row); row, width = [], 0
if row:
    rows.append(row)
y = 0
for r in rows:
    x = 0
    depth = max(o.dimensions.y for o in r)
    for o in r:
        o.location = (x + o.dimensions.x / 2, y, 0)
        x += o.dimensions.x + 2
    y += depth + 6

scene = bpy.context.scene
world = bpy.data.worlds.new("Sky"); scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (*srgb("#8FD0FF"), 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(-30)))
bpy.context.active_object.data.energy = 3.0
bpy.context.active_object.data.color = (1.0, 0.92, 0.8)
for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
    try:
        scene.render.engine = eng; break
    except TypeError:
        pass
scene.view_settings.view_transform = "Standard"
scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
bpy.ops.object.camera_add()
cam = bpy.context.active_object; scene.camera = cam
cam.data.type = "ORTHO"
for i, r in enumerate(rows):
    xs = [o.location.x for o in r]
    cx = (min(xs) + max(xs)) / 2
    cy = r[0].location.y
    size = max(max(xs) - min(xs) + 14, max(o.dimensions.z for o in r) * 2.2)
    cam.data.ortho_scale = size
    tgt = Vector((cx, cy, max(o.dimensions.z for o in r) * 0.4))
    cam.location = tgt + Vector((size * 0.35, -size * 0.9, size * 0.45))
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (tgt - cam.location).to_track_quat("-Z", "Y")
    scene.render.filepath = os.path.join(PREVIEW_DIR, f"env_row{i}.png")
    bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(PREVIEW_DIR, "MenuEnvironment.blend"))
print("DONE", len(objects))
