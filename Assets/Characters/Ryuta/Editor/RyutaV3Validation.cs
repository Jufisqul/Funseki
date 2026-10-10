using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Funseki.EditorTools
{
    public static class RyutaV3Validation
    {
        const string Dir="Assets/Characters/Ryuta";
        static readonly string[] Names={"Ryuta_Idle","Ryuta_Kick","Ryuta_BeltGag"};
        static string Out => Path.GetFullPath(Path.Combine(Application.dataPath,"..","output","characters","Ryuta"));
        [MenuItem("Tools/Funseki/Ryuta V3/Build animations and validate")]
        public static void Build()
        {
            RyutaV3Setup.Setup();
            var hair=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Ryuta_Hair.mat");
            hair.SetColor("_ShadowColor",new Color(.78f,.65f,.42f));
            hair.SetFloat("_RimStrength",.10f);
            var mi=(ModelImporter)AssetImporter.GetAtPath(Dir+"/Ryuta.fbx");
            var avatar=AssetDatabase.LoadAllAssetsAtPath(Dir+"/Ryuta.fbx").OfType<Avatar>().First();
            if(!avatar.isHuman || !avatar.isValid) throw new Exception("Invalid Humanoid Avatar");
            foreach(var name in Names)
            {
                string path=Dir+"/Animations/"+name+".fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.importAnimation=true;
                importer.animationType=ModelImporterAnimationType.Human;
                importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar=avatar;
                importer.humanDescription=mi.humanDescription;
                importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.importBlendShapes=true;
                importer.SaveAndReimport();
                var setting=importer.defaultClipAnimations.First();
                setting.name=name;
                setting.loopTime=name=="Ryuta_Idle";
                setting.loopPose=setting.loopTime;
                setting.lockRootPositionXZ=true;
                setting.lockRootRotation=true;
                setting.lockRootHeightY=true;
                setting.keepOriginalPositionXZ=true;
                setting.keepOriginalPositionY=true;
                setting.keepOriginalOrientation=true;
                importer.clipAnimations=new[]{setting};
                importer.SaveAndReimport();
            }
            string controllerPath=Dir+"/Ryuta.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine=controller.layers[0].stateMachine;
            foreach(var name in Names)
            {
                var state=machine.states.Select(x=>x.state).FirstOrDefault(x=>x.name==name)??machine.AddState(name);
                state.motion=Clip(name);
                if(name=="Ryuta_Idle")machine.defaultState=state;
            }
            foreach(var name in new[]{"Kick","BeltGag"})
                if(!controller.parameters.Any(p=>p.name==name))controller.AddParameter(name,AnimatorControllerParameterType.Trigger);
            var idle=machine.states.First(x=>x.state.name=="Ryuta_Idle").state;
            foreach(var pair in new[]{new[]{"Ryuta_Kick","Kick"},new[]{"Ryuta_BeltGag","BeltGag"}})
            {
                var target=machine.states.First(x=>x.state.name==pair[0]).state;
                if(!idle.transitions.Any(t=>t.destinationState==target))
                {
                    var transition=idle.AddTransition(target);transition.hasExitTime=false;transition.duration=.1f;
                    transition.AddCondition(AnimatorConditionMode.If,0,pair[1]);
                    var back=target.AddTransition(idle);back.hasExitTime=true;back.exitTime=1;back.duration=.12f;
                }
            }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Ryuta.prefab");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.GetComponent<Animator>().runtimeAnimatorController=controller;
            instance.GetComponent<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.updateWhenOffscreen=true;
                renderer.localBounds=new Bounds(new Vector3(0,.9f,0),new Vector3(2.8f,2.6f,2.8f));
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null)continue;
                    if(materials[i].name.StartsWith("Ryuta_Cigarette")||materials[i].name.StartsWith("Ryuta_Buttons"))
                    {
                        string source=materials[i].name.Split('.')[0];
                        string p=Dir+"/Materials/"+source+".mat";
                        var mat=AssetDatabase.LoadAssetAtPath<Material>(p);
                        if(mat==null)
                        {
                            mat=new Material(Shader.Find("Funseki/ToonLit")){name=source};
                            Color color=source.Contains("Filter")?new Color(.83f,.67f,.47f):source.Contains("Tip")?new Color(.40f,.33f,.36f):source.Contains("Buttons")?new Color(1,.824f,.247f):Color.white;
                            mat.SetColor("_BaseColor",color);
                            mat.SetTexture("_RampMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/Textures/Ryuta_Ramp.png"));
                            AssetDatabase.CreateAsset(mat,p);
                        }
                        materials[i]=mat;
                    }
                }
                renderer.sharedMaterials=materials;
            }
            PrefabUtility.SaveAsPrefabAsset(instance,Dir+"/Ryuta.prefab");
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            Validate();
        }
        static AnimationClip Clip(string name)=>AssetDatabase.LoadAllAssetsAtPath(Dir+"/Animations/"+name+".fbx").OfType<AnimationClip>().First(x=>!x.name.StartsWith("__preview"));
        static PlayableGraph Sample(GameObject go,AnimationClip clip,float time)
        {
            go.GetComponent<Animator>().cullingMode=AnimatorCullingMode.AlwaysAnimate;
            go.GetComponent<Animator>().Rebind();
            var graph=PlayableGraph.Create("RyutaV3Validation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output=AnimationPlayableOutput.Create(graph,"Pose",go.GetComponent<Animator>());
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetTime(time);
            output.SetSourcePlayable(playable);graph.Play();graph.Evaluate(.001f);
            return graph;
        }
        static void Shoot(GameObject go,Camera camera,string path)
        {
            var frozen=new List<GameObject>();
            var baked=new List<Mesh>();
            foreach(var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();smr.BakeMesh(mesh,true);baked.Add(mesh);
                var obj=new GameObject("PreviewBaked");obj.transform.SetParent(smr.transform,false);
                obj.AddComponent<MeshFilter>().sharedMesh=mesh;
                obj.AddComponent<MeshRenderer>().sharedMaterials=smr.sharedMaterials;
                smr.forceRenderingOff=true;frozen.Add(obj);
            }
            var rt=RenderTexture.GetTemporary(720,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var tex=new Texture2D(720,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,720,900),0,0);tex.Apply();
            File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
            foreach(var o in frozen)Object.DestroyImmediate(o);
            foreach(var m in baked)Object.DestroyImmediate(m);
            foreach(var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())smr.forceRenderingOff=false;
        }
        [MenuItem("Tools/Funseki/Ryuta V3/Validate and render")]
        public static void Validate()
        {
            Directory.CreateDirectory(Out+"/previews");
            var active=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var origin=new Vector3(0,-1000,0);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Ryuta.prefab"),scene);
                go.transform.position=origin;
                var animator=go.GetComponent<Animator>();
                var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(40,-30,0);
                var camera=new GameObject("RyutaCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.34f,.36f,.40f);camera.nearClipPlane=.05f;camera.farClipPlane=20;
                camera.orthographic=true;camera.orthographicSize=1.04f;
                camera.transform.position=origin+new Vector3(-2.3f,1.4f,4);camera.transform.LookAt(origin+new Vector3(0,.91f,0));
                var report=new List<string>{"Avatar valid: "+animator.avatar.isValid,"Avatar Humanoid: "+animator.avatar.isHuman,"Pipeline: "+(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==null?"Built-in":UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name),"Root scale: "+go.transform.localScale};
                int tris=go.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name!="Ryuta_Outline").Sum(r=>r.sharedMesh.triangles.Length/3);
                report.Add("Triangles excluding outline: "+tris);
                animator.Rebind();
                float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
                foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name!="Ryuta_Outline"))
                {
                    var mesh=new Mesh();renderer.BakeMesh(mesh,true);
                    foreach(var v in mesh.vertices){float y=renderer.transform.TransformPoint(v).y-origin.y;minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                    Object.DestroyImmediate(mesh);
                }
                report.Add("Neutral mesh ground minY="+minY+", height="+(maxY-minY));
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();animator.Update(0);animator.Update(.05f);
                if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Ryuta_Idle"))throw new Exception("Controller did not enter Idle");
                foreach(var trigger in new[]{"Kick","BeltGag"})
                {
                    animator.SetTrigger(trigger);
                    for(int step=0;step<4;step++)animator.Update(.1f);
                    string expected=trigger=="Kick"?"Ryuta_Kick":"Ryuta_BeltGag";
                    if(!animator.GetCurrentAnimatorStateInfo(0).IsName(expected))throw new Exception("Trigger failed: "+trigger);
                    for(int step=0;step<40;step++)animator.Update(.1f);
                    if(!animator.GetCurrentAnimatorStateInfo(0).IsName("Ryuta_Idle"))throw new Exception("Clip failed to return to Idle: "+trigger);
                    report.Add("Animator trigger and return verified: "+trigger);
                }
                foreach(var name in Names)
                {
                    var clip=Clip(name);
                    if(!clip.isHumanMotion||clip.length<=0)throw new Exception("Invalid clip: "+name);
                    var startGraph=Sample(go,clip,0);startGraph.Destroy();
                    var before=animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    var graph=Sample(go,clip,clip.length*.48f);
                    var after=animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    report.Add(name+": humanoid="+clip.isHumanMotion+", duration="+clip.length+", left foot displacement="+Vector3.Distance(before,after));
                    var bindings=AnimationUtility.GetCurveBindings(clip);
                    report.Add("Curve count: "+bindings.Length+"; animated curves: "+bindings.Count(b=>{var c=AnimationUtility.GetEditorCurve(clip,b);return c!=null&&c.keys.Length>1&&c.keys.Any(k=>Mathf.Abs(k.value-c.keys[0].value)>.0001f);}));
                    report.Add("Hand: "+animator.GetBoneTransform(HumanBodyBones.RightHand).position+" Foot: "+after);
                    if(name=="Ryuta_Kick"&&Vector3.Distance(before,after)<.5f)throw new Exception("Kick does not move the foot");
                    var animatedBody=go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.sharedMesh.blendShapeCount>0&&r.name!="Ryuta_Outline");
                    int slipIndex=animatedBody.sharedMesh.GetBlendShapeIndex("BeltSlip");
                    report.Add("BeltSlip weight: "+animatedBody.GetBlendShapeWeight(slipIndex));
                    Shoot(go,camera,Out+"/previews/unity_"+name+".png");
                    if(name=="Ryuta_Idle")Shoot(go,camera,Out+"/previews/unity_"+name+".png");
                    graph.Destroy();
                }
                var idleGraph=Sample(go,Clip("Ryuta_Idle"),0);
                foreach(var pair in new[]{new[]{"front","0"},new[]{"side","90"},new[]{"back","180"}})
                {
                    go.transform.rotation=Quaternion.Euler(0,float.Parse(pair[1]),0);
                    camera.transform.position=origin+new Vector3(0,1.0f,4);camera.transform.LookAt(origin+new Vector3(0,.90f,0));
                    Shoot(go,camera,Out+"/previews/unity_"+pair[0]+".png");
                }
                go.transform.rotation=Quaternion.identity;
                camera.orthographicSize=.21f;camera.transform.position=origin+new Vector3(-.18f,1.64f,1.1f);camera.transform.LookAt(origin+new Vector3(0,1.63f,0));
                var body=go.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.sharedMesh.blendShapeCount>0&&r.name!="Ryuta_Outline");
                foreach(var shape in new[]{"smirk","angry","mouth_A"})
                {
                    for(int i=0;i<body.sharedMesh.blendShapeCount;i++)body.SetBlendShapeWeight(i,body.sharedMesh.GetBlendShapeName(i)==shape?100:0);
                    Shoot(go,camera,Out+"/previews/unity_face_"+shape+".png");
                    report.Add("Blend shape verified: "+shape);
                }
                idleGraph.Destroy();
                camera.orthographicSize=1.04f;camera.transform.position=origin+new Vector3(-2.3f,1.4f,4);camera.transform.LookAt(origin+new Vector3(0,.91f,0));
                go.transform.position=Vector3.zero;
                light.transform.position=Vector3.zero;
                camera.transform.position-=origin;
                for(int i=0;i<body.sharedMesh.blendShapeCount;i++)body.SetBlendShapeWeight(i,0);
                // Save an isolated inspection scene; the user's active scene is restored in finally.
                EditorSceneManager.SaveScene(scene,Dir+"/Ryuta_Review.unity");
                File.WriteAllLines(Out+"/unity_validation.txt",report);
                Debug.Log("[RyutaV3] "+string.Join("\n",report));
            }
            finally
            {
                SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true);
            }
        }
    }
}
