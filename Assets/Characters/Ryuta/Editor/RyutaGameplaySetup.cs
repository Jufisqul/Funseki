using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Funseki.Heroes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Funseki.EditorTools
{
    public static class RyutaGameplaySetup
    {
        const string Dir="Assets/Characters/Ryuta";
        const string Old="Assets/_Project/Art/Heroes/Models/Ryuta_Model.prefab";
        [MenuItem("Tools/Funseki/Ryuta V3/Use new Ryuta in gameplay")]
        public static void Integrate()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before integration");
            var player=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animation/Player.controller");
            PlayerSliceSetup.ConnectKickAnimation(player as UnityEditor.Animations.AnimatorController);
            var idle=AssetDatabase.LoadAllAssetsAtPath(Dir+"/Animations/Ryuta_Idle.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            string controllerPath=Dir+"/Ryuta_Player.overrideController";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
            if(controller==null){controller=new AnimatorOverrideController(player);AssetDatabase.CreateAsset(controller,controllerPath);}
            var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(pairs);
            int replaced=0;
            for(int i=0;i<pairs.Count;i++)if(pairs[i].Key.name.IndexOf("idle",StringComparison.OrdinalIgnoreCase)>=0)
            {pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pairs[i].Key,idle);replaced++;}
            if(replaced!=1)throw new Exception("Expected exactly one locomotion Idle clip; found "+replaced);
            controller.ApplyOverrides(pairs);EditorUtility.SetDirty(controller);
            var preview=EditorSceneManager.NewPreviewScene();GameObject gameplay;
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Ryuta.prefab"),preview);
                var animator=instance.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=controller;
                instance.name="Ryuta_Gameplay";
                gameplay=PrefabUtility.SaveAsPrefabAsset(instance,Dir+"/Ryuta_Gameplay.prefab");
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            var data=AssetDatabase.LoadAssetAtPath<HeroData>("Assets/_Project/Data/Heroes/Hero_Ryuta.asset");
            Undo.RecordObject(data,"Update Ryuta model");data.prefab=gameplay;EditorUtility.SetDirty(data);
            int models=0;
            var active=SceneManager.GetActiveScene();
            string scenePath="Assets/_Project/Scenes/Slice_Day1.unity";
            var scene=SceneManager.GetSceneByPath(scenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();
                var instances=roots.SelectMany(root=>root.GetComponentsInChildren<Transform>(true))
                    .Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)&&PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)==Old)
                    .Select(t=>t.gameObject).ToArray();
                foreach(var old in instances)
                {
                    var hero=old.GetComponentInParent<HeroUnit>();
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(old,gameplay,InteractionMode.AutomatedAction);
                    var animator=old.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                    if(hero!=null)
                    {
                        foreach(var component in hero.GetComponents<MonoBehaviour>())
                        {
                            var so=new SerializedObject(component);var p=so.FindProperty("animator");
                            if(p!=null&&p.propertyType==SerializedPropertyType.ObjectReference){p.objectReferenceValue=animator;so.ApplyModifiedProperties();}
                        }
                        var cc=hero.GetComponent<CharacterController>();cc.height=1.816f;cc.center=new Vector3(0,cc.height*.5f+cc.skinWidth,0);
                        foreach(var camera in roots.SelectMany(root=>root.GetComponentsInChildren<Funseki.Player.PlayerCameraController>(true)))
                        {
                            var so=new SerializedObject(camera);
                            var motor=so.FindProperty("motor").objectReferenceValue as Component;
                            if(motor==null||motor.gameObject!=hero.gameObject)continue;
                            var arr=so.FindProperty("hideInFirstPerson");var renderers=old.GetComponentsInChildren<Renderer>(true);
                            arr.arraySize=renderers.Length;
                            for(int i=0;i<renderers.Length;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i];
                            so.ApplyModifiedProperties();
                        }
                    }
                    models++;
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened){if(active.IsValid())SceneManager.SetActiveScene(active);EditorSceneManager.CloseScene(scene,true);}}
            // Refresh Ryuta's existing HUD portrait without re-rendering other heroes.
            typeof(HeroesSetup).GetMethod("RenderPortrait",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{data,true});
            AssetDatabase.SaveAssets();
            Debug.Log("[RyutaGameplay] Updated HeroData, "+models+" scene instances, HUD portrait and gameplay controller. Walk/Run/Jump retained.");
        }
        [MenuItem("Tools/Funseki/Ryuta V3/Check runtime integration")]
        public static void CheckRuntime()
        {
            var hero=Object.FindObjectsByType<HeroUnit>(FindObjectsInactive.Include).First(h=>h.Data!=null&&(int)h.Data.id==0);
            var animator=hero.GetComponentInChildren<Animator>();
            var mesh=hero.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.sharedMesh.blendShapeCount>0&&r.name!="Ryuta_Outline");
            if(mesh.sharedMesh.GetBlendShapeIndex("BeltSlip")<0||!animator.avatar.isHuman||!animator.avatar.isValid)throw new Exception("Wrong Ryuta runtime model");
            Debug.Log("[RyutaGameplay] RUNTIME PASS: "+hero.name+", mesh="+mesh.sharedMesh.name+", controller="+animator.runtimeAnimatorController.name+", Avatar valid=True, height="+hero.GetComponent<CharacterController>().height);
        }

        [MenuItem("Tools/Funseki/Ryuta V3/Check gameplay kick playback")]
        public static void CheckKickPlayback()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Start Quick Play first.");
            var hero = Object.FindObjectsByType<HeroUnit>(FindObjectsInactive.Exclude)
                .First(h => h.Data != null && (int)h.Data.id == 0);
            var animator = hero.GetComponentInChildren<Animator>();
            var runner = hero.GetComponent<HeroAbilityRunner>();
            if (runner.Ability is not KickAbility) throw new Exception("Ryuta does not have Kick.");
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Vector3 initialFoot = hero.transform.InverseTransformPoint(foot.position);
            var oldCulling = animator.cullingMode;
            bool oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (!runner.TryUse()) { animator.cullingMode = oldCulling; Application.runInBackground = oldBackground; throw new Exception("Kick rejected (cooldown?)."); }
            bool cooldownBlocked = !runner.TryUse();
            double started = EditorApplication.timeSinceStartup;
            float gameStarted = Time.time;
            bool sawKick = false;
            float displacement = 0f;
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (!EditorApplication.isPlaying || animator == null)
                { EditorApplication.update -= poll; return; }
                bool kicking = animator.GetCurrentAnimatorStateInfo(0).IsName("Kick");
                sawKick |= kicking;
                if (kicking) displacement = Mathf.Max(displacement,
                    Vector3.Distance(initialFoot, hero.transform.InverseTransformPoint(foot.position)));
                if (Time.time - gameStarted < 2.5f && EditorApplication.timeSinceStartup - started < 20) return;
                EditorApplication.update -= poll;
                animator.cullingMode = oldCulling;
                Application.runInBackground = oldBackground;
                bool returned = animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");
                string result = $"Kick state={sawKick}, foot displacement={displacement:F3} m, returned={returned}, cooldown blocked={cooldownBlocked}, game elapsed={Time.time - gameStarted:F2} s";
                if (sawKick && displacement > 0.1f && returned && cooldownBlocked)
                    Debug.Log("[RyutaKick] PLAYBACK PASS: " + result);
                else Debug.LogError("[RyutaKick] PLAYBACK FAIL: " + result);
            };
            EditorApplication.update += poll;
        }
    }
}
