"""Adapt the existing project character generator without changing its files."""
import os, math, bpy
ROOT = r'C:\One Funseki, Seven Days'
OUT = os.path.join(ROOT, 'output', 'characters', 'Ryuta')
DEST = os.path.join(ROOT, 'Assets', 'Characters', 'Ryuta')
os.makedirs(OUT, exist_ok=True)
os.makedirs(DEST, exist_ok=True)
src = open(os.path.join(ROOT,'Tools','CharacterGen','ryuta_build.py'), encoding='utf-8').read()
src = src.replace('ART = os.path.join(ROOT, "Art", "Characters", "Ryuta")', 'ART = '+repr(OUT))
src = src.replace('UNITY = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters", "Ryuta")', 'UNITY = '+repr(DEST))
src = src.replace('bpy.ops.wm.read_factory_settings(use_empty=True)', "\nnew_scene = bpy.data.scenes.new('Ryuta_V3_Work')\nbpy.context.window.scene = new_scene")
src = src.replace('nt.nodes["Principled BSDF"]','next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")')
src = src.replace('HEAD_N = 28','HEAD_N = 48')
src = src.replace('lerp(0.0032, 0.0017, sa)','lerp(0.0052, 0.0030, sa)')
src = src.replace('build_collar()\nbuild_arm(1)', '''build_collar()
# Close the yoke between the jacket and standing collar.
g=math.radians(44)
loft([hring(1.468,.118,.079,.081,.016,24,arc=(g,2*math.pi-g)),hring(1.452,.082,.075,.077,.018,24,arc=(g,2*math.pi-g))],"jacket","body",MAT_BODY,lambda p,k,i:{"UpperChest":1.0},lambda p,k,i:1.0)
build_arm(1)''')
src = src.replace('def build_hair():\n    build_hair_cap()', '''def build_hair():
    global SURF_LOCKS, FREE_LOCKS
    SURF_LOCKS = [(-50,1.716,.012,-58,1.664,.02,.012,.027,.015),(-28,1.727,.012,-32,1.671,.028,.015,.030,.016),(0,1.730,.012,8,1.683,.028,.014,.029,.016),(25,1.725,.012,34,1.672,.028,.015,.031,.016),(47,1.716,.012,59,1.662,.028,.012,.028,.015)]
    FREE_LOCKS = [(a,1.705+(i%3)*.016,24+(i%4)*13,(-1 if i%2 else 1)*17,.068+(i%3)*.008,.029,.018,0.005) for i,a in enumerate(range(-180,180,24))]
    FREE_LOCKS += [(-140,1.66,-8,-12,.047,.026,.013,.005),(140,1.66,-8,12,.047,.026,.013,.005),(180,1.64,-20,0,.05,.025,.013,.005)]
    build_hair_cap()''')
src = src.replace('stations=(0.0, 0.12, 0.3, 0.5, 0.7, 0.86)', 'stations=(0.0, 0.24, 0.58, 0.84)')
src = src.replace('build_ears()\nbuild_face_cards()', '''# Broader clothing and large sneakers; neutral rig remains intact.
LEG = [(z,rx*(1.18+0.09*math.sin(z*25)),ry*1.2) for z,rx,ry in LEG]
SHOE = [(y*1.2,hw*1.36,h*1.2,xs) for y,hw,h,xs in SHOE]
SLEEVE = [(x,rz*1.32,ry*1.3,dz) for x,rz,ry,dz in SLEEVE if x<=.59]
SLEEVE += [(.605,.060,.062,0)]
build_ears()
build_face_cards()''')
src = src.replace('cx=cx))', 'cx=cx))')
src = src.replace('hring(z, rx, ry, ry, cy, 16, cx=cx)', 'hring(z, rx, ry, ry, cy, 24, cx=cx)')
src = src.replace('rings = [tring(V((x, cy, cz))', '''# Exposed forearm between rolled jacket cuff and hand.
    skinrings = [tring(V((x,arm_line_y(x),1.428)),X,V((0,0,1)),r,r*.90,14) for x,r in ((.588,.039),(.615,.036),(.65,.031),(.681,.027),(.704,.025))]
    loft(skinrings,"hand","body",MAT_BODY,lambda p,k,i: arm_w(p.x),lambda p,k,i: 1.0,flip=flip,xf=xf)
    rings = [tring(V((x, cy, cz))''')
src = src.replace('out[front & c1 & ~c2, :3] = PRINT', 'out[front & c1 & ~c2, :3] = SHIRT_C')
src = src.replace('out[front & (np.hypot(x - sx, z - sz) < r), :3] = PRINT', 'out[front & (np.hypot(x - sx, z - sz) < r), :3] = SHIRT_C')
src = src.replace('PAINT["lining_jacket"] = lambda P: p_jacket(P, lining=True)', '''PAINT["lining_jacket"] = lambda P: p_jacket(P, lining=True)
# Flat palette: no baked light or hair highlight bands.
SKIN = C(241/255,201/255,176/255)
JACKET_C = C(28/255,27/255,34/255)
PANTS_C = C(31/255,33/255,51/255)
SHIRT_C = C(244/255,244/255,244/255)
GOLD = C(1,210/255,63/255)
PAINT['hair'] = lambda P: base(len(P), C(233/255,215/255,151/255))
PAINT['shirt'] = lambda P: base(len(P), SHIRT_C)
PAINT['hand'] = lambda P: base(len(P), SKIN,0.0)
PAINT['neck'] = lambda P: base(len(P), SKIN,0.0)
PAINT['lining'] = lambda P: base(len(P), JACKET_C)
old_sleeve_paint = p_sleeve
def v3_sleeve(P):
    out = base(len(P),JACKET_C)
    out[P[:,0]>.565,:3]=SHIRT_C
    return out
PAINT['sleeve'] = v3_sleeve
old_head_paint = p_head
def v3_head(P):
    out = base(len(P),SKIN)
    x,y,z=P.T
    # The subtle asymmetric resting smile is flat ink, independent of light.
    curve=1.542 + .004*np.maximum(x/.020,0)**1.5
    out[(y<-.04)&(np.abs(x)<.022)&(np.abs(z-curve)<.001),:3]=MOUTH_LINE
    return out
PAINT['head']=v3_head
PAINT['ear']=lambda P: base(len(P),SKIN)
old_pants_paint=p_pants
def v3_pants(P):
    out=old_pants_paint(P)
    out[P[:,2]<.95,:3]=PANTS_C
    return out
PAINT['pants']=v3_pants
ACCENT=C(31/255,33/255,51/255)
''')
exec(compile(src, os.path.join(ROOT,'Tools','CharacterGen','ryuta_build.py'),'exec'),globals())
print('RYUTA V3 base generated',flush=True)
