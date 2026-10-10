import bpy, math, os, json, shutil
from mathutils import Vector as V, Matrix
ROOT=r'C:\One Funseki, Seven Days'
OUT=os.path.join(ROOT,'output','characters','Ryuta')
DEST=os.path.join(ROOT,'Assets','Characters','Ryuta')
scene=bpy.context.scene
ob=next(o for o in scene.objects if o.type=='MESH')
rig=next(o for o in scene.objects if o.type=='ARMATURE')
for obj,name in ((ob,'Ryuta_Mesh'),(rig,'Ryuta_Rig')):
    old=bpy.data.objects.get(name)
    if old and old!=obj: old.name=name+'_Previous'
    obj.name=name
for mat,name in zip(ob.data.materials,['Ryuta_Body','Ryuta_Face','Ryuta_Hair']):
    old=bpy.data.materials.get(name)
    if old and old!=mat:old.name=name+'_Previous'
    mat.name=name
scene.render.fps=30
# Use the project's existing rendering/pose helpers in the isolated scene.
source=open(os.path.join(ROOT,'Tools','CharacterGen','ryuta_render.py'),encoding='utf-8').read().split('# ------------------------------------------------------------ sheets')[0]
source=source.replace('OUT = os.path.join(ROOT, "Art", "Characters", "Ryuta", "previews")','OUT = '+repr(os.path.join(OUT,'previews')))
source=source.replace('world.node_tree.nodes["Background"]','next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")')
source=source.replace('"Ryuta_Hair": (0.55, 0.56, 0.78)','"Ryuta_Hair": (0.78, 0.65, 0.42)')
__file__=os.path.join(ROOT,'Tools','CharacterGen','ryuta_render.py')
exec(compile(source,__file__,'exec'),globals())
BASEOUT=os.path.join(ROOT,'output','characters','Ryuta')
# Flat accessory materials (linear shader values from sRGB palette).
def material(name,hexcolor):
    m=bpy.data.materials.new(name);m.use_nodes=True
    rgb=[int(hexcolor[i:i+2],16)/255 for i in (0,2,4)]
    rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*rgb,1);bs.inputs['Roughness'].default_value=.85
    m.diffuse_color=(*rgb,1)
    return m
paper=material('Ryuta_CigarettePaper','F4F4F4')
filtermat=material('Ryuta_CigaretteFilter','D5AC77')
ember=material('Ryuta_CigaretteTip','66535C')
gold=material('Ryuta_Buttons','FFD23F')
def attach(obj,bone):
    obj.parent=rig
    g=obj.vertex_groups.new(name=bone);g.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    obj.modifiers.new('Skin','ARMATURE').object=rig
def cylinder(name,a,b,r,mat,bone,verts=12):
    a,b=V(a),V(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=(b-a).length,location=(a+b)/2)
    o=bpy.context.object;o.name=name;o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    o.data.materials.append(mat);attach(o,bone)
    return o
reset_pose()
# Cigarette is a separate named object, weighted only to Head.
a=V((-.014,-.096,1.544));b=V((-.054,-.153,1.532));d=b-a
cig=cylinder('Ryuta_Cigarette',a,a+d*.80,.0027,paper,'Head')
cylinder('Ryuta_Cigarette_Filter',a,a+d*.22,.0028,filtermat,'Head')
cylinder('Ryuta_Cigarette_Tip',a+d*.80,b,.00275,ember,'Head')
for i,z in enumerate((1.355,1.245,1.135)):
    x=-.077 if z>1.3 else -.067
    cylinder('Ryuta_Button_%02d'%i,(x,-.129,z),(x,-.134,z),.008,gold,'Chest' if z>1.2 else 'Spine')
# Remove the preview-only outline material/modifier for exports; Unity has the existing outline shader.
for mod in list(ob.modifiers):
    if mod.type=='SOLIDIFY':ob.modifiers.remove(mod)
if len(ob.data.materials)>3:ob.data.materials.pop(index=3)
slip=ob.shape_key_add(name='BeltSlip',from_mix=False)
for v in ob.data.vertices:
    p=v.co
    weights={ob.vertex_groups[g.group].name:g.weight for g in v.groups}
    # Pelvis/legs of trousers; shirt/jacket and exposed hands remain in place.
    if .65<p.z<1.013 and abs(p.x)<.18 and abs(p.y)<.13 and (weights.get('Hips',0)>.5 or any('Leg' in n for n in weights)):
        slip.data[v.index].co.z-=.036*min(1,max(0,(p.z-.65)/.18))
slip.value=0
def idle(t=0):
    reset_pose()
    for b,dg in (('Spine',4),('Chest',3),('UpperChest',2),('Neck',5),('Head',-8)):
        rot(b,X,dg+(math.sin(t)*.4 if b=='Chest' else 0))
    # Outside elbow, wrist above front edge, fingers directed down behind cloth.
    two_bone('RightUpperArm','RightLowerArm',(-.052,-.085,1.020),(-.46,-.05,1.12))
    point('RightHand',(-.038,.023,.866))
    two_bone('LeftUpperArm','LeftLowerArm',(.245,-.02,.930),(.4,.07,1.1))
    point('LeftHand',(.242,-.024,.80))
    for side in ('Right','Left'):
        for f in ('Index','Middle','Ring','Little'):
            pb=rig.pose.bones[side+f+'Proximal'];pb.rotation_mode='XYZ';pb.rotation_euler.x=.10
    rot('Head',Z,-3+math.sin(t)*.5)
    ob.data.shape_keys.key_blocks['smirk'].value=.45
def key(frame):
    for pb in rig.pose.bones:
        pb.rotation_mode='QUATERNION'
        pb.keyframe_insert('rotation_quaternion',frame=frame)
        pb.keyframe_insert('location',frame=frame)
        pb.keyframe_insert('scale',frame=frame)
    for name in ('smirk','angry','BeltSlip'):
        ob.data.shape_keys.key_blocks[name].keyframe_insert('value',frame=frame)
rig.animation_data_create()
ob.data.shape_keys.animation_data_create()
clips=[]
for name,end in (('Ryuta_Idle',121),('Ryuta_Kick',46),('Ryuta_BeltGag',91)):
    act=bpy.data.actions.new(name);rig.animation_data.action=act
    faceact=bpy.data.actions.new(name+'_Face');ob.data.shape_keys.animation_data.action=faceact
    for fr in range(1,end+1):
        t=(fr-1)/(end-1);idle(t*2*math.pi)
        if name=='Ryuta_Kick':
            wind=math.sin(math.pi*min(1,t/.25)) if t<.25 else 0
            kick=math.sin(math.pi*max(0,min(1,(t-.22)/.55)))**.7 if .22<t<.77 else 0
            rot('LeftUpperLeg',X,wind*22-kick*88)
            rot('LeftLowerLeg',X,wind*45+kick*14)
            rot('LeftFoot',X,kick*12)
            rot('Spine',X,kick*9)
            ob.data.shape_keys.key_blocks['angry'].value=kick*.8
        elif name=='Ryuta_BeltGag':
            release=math.sin(math.pi*min(1,max(0,(t-.10)/.62))) if .10<t<.72 else 0
            two_bone('RightUpperArm','RightLowerArm',(-.052-release*.09,-.085-release*.16,1.020+release*.07),(-.46,-.05,1.12))
            point('RightHand',(-.038-release*.13,.023-release*.30,.866+release*.17))
            slip.value=.75*math.sin(math.pi*min(1,max(0,(t-.20)/.60))) if .20<t<.80 else 0
            rot('Head',X,release*11)
        key(fr)
    act.use_fake_user=True;faceact.use_fake_user=True
    clips.append((name,end,act,faceact))
# Export each clip independently, avoiding service bones and NLA naming surprises.
meshes=[o for o in scene.objects if o.type=='MESH' and o!=globals().get('ground')]
def export(path,animation=False):
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes+[rig]:o.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,mesh_smooth_type='OFF',add_leaf_bones=False,use_armature_deform_only=True,bake_anim=animation,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',path_mode='STRIP',bake_space_transform=True)
rig.animation_data.action=None;ob.data.shape_keys.animation_data.action=None
reset_pose();export(os.path.join(DEST,'Ryuta.fbx'))
os.makedirs(os.path.join(DEST,'Animations'),exist_ok=True)
for name,end,act,faceact in clips:
    rig.animation_data.action=act;ob.data.shape_keys.animation_data.action=faceact
    scene.frame_start=1;scene.frame_end=end;scene.frame_set(1)
    export(os.path.join(DEST,'Animations',name+'.fbx'),True)
rig.animation_data.action=clips[0][2];ob.data.shape_keys.animation_data.action=clips[0][3]
scene.frame_start=1;scene.frame_end=121;scene.frame_set(1)
# Pack images in source; FBX materials still remap to relative Unity texture assets.
for im in bpy.data.images:
    if im.source=='FILE' and im.filepath and os.path.isfile(bpy.path.abspath(im.filepath)):
        im.pack()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASEOUT,'Ryuta.blend'))
shutil.copy2(os.path.join(DEST,'Ryuta.fbx'),os.path.join(BASEOUT,'Ryuta.fbx'))
tri=sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons)
report={'triangles':tri,'bones':len(rig.data.bones),'blend_shapes':[k.name for k in ob.data.shape_keys.key_blocks],'clips':[{ 'name':n,'frames':e,'fps':30} for n,e,_,_ in clips],'height_m':max(v.co.z for v in ob.data.vertices),'source':'Existing project generator adapted to concept v3; original resources preserved'}
open(os.path.join(BASEOUT,'geometry_report.json'),'w',encoding='utf-8').write(json.dumps(report,ensure_ascii=False,indent=2))
look((2.7,-4.5,2.0),(0,0,1.0),ortho=2.1)
scene.render.film_transparent=False
scene.render.resolution_x=720;scene.render.resolution_y=900
scene.render.filepath=os.path.join(BASEOUT,'previews','blender_idle.png')
bpy.ops.render.render(write_still=True)
print(json.dumps(report,ensure_ascii=False))
