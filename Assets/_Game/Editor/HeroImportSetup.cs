using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only: import settings for the playable Tomura (Humanoid, menu palette materials)
// and the Mixamo clips in Assets/Anim. Re-runnable.
public static class HeroImportSetup
{
    public const string HeroModel = "Assets/_Game/Art/Characters/Tomura/Tomura_Ryuta_Game.fbx";
    public const string WalkFbx = "Assets/Anim/Walking (1).fbx";
    public const string RunFbx = "Assets/Anim/Run.fbx";
    public const string JumpFbx = "Assets/Anim/Jumping Up.fbx";
    const string MenuMaterials = "Assets/MainMenu/Materials/";

    [MenuItem("Tools/One Funseki/Hero/1. Setup Imports")]
    public static void Run()
    {
        SetupModel();
        SetupClips(WalkFbx, Clip("Walk", loop: true));
        SetupClips(RunFbx, Clip("Run", loop: true));
        // No idle clip yet: borrow the standing pose from the first frames of the jump.
        // The take opens with a 0.4 s crouch; physics leaves the ground on the key press, so start at take-off.
        var jumpClips = new[] { Clip("Jump", loop: false, bakeHeight: false), Clip("Idle", loop: true) };
        jumpClips[0].firstFrame = 13;
        jumpClips[0].lastFrame = 25;
        jumpClips[1].firstFrame = 0;
        jumpClips[1].lastFrame = 2;
        SetupClips(JumpFbx, jumpClips);
        Debug.Log("[One Funseki] Hero imports set up.");
    }

    [MenuItem("Tools/One Funseki/Hero/Debug: Log Avatar And Clips")]
    static void LogInfo()
    {
        foreach (var path in new[] { HeroModel, WalkFbx, RunFbx, JumpFbx })
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var avatar = assets.OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[Hero] {path}: avatar {(avatar ? $"human {avatar.isHuman}, valid {avatar.isValid}" : "none")}");
            foreach (var clip in assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
            {
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var rootY = bindings.FirstOrDefault(b => b.propertyName == "RootT.y");
                string curve = "";
                if (rootY.propertyName != null)
                {
                    var c = AnimationUtility.GetEditorCurve(clip, rootY);
                    for (float t = 0; t <= clip.length + 0.001f; t += clip.length / 12f) curve += $"{t:F2}:{c.Evaluate(t):F2} ";
                }
                Debug.Log($"[Hero]   clip {clip.name}: {clip.length:F2}s, {clip.frameRate} fps, loop {clip.isLooping}, rootY {curve}");
            }
        }
    }

    static void SetupModel()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(HeroModel);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = false;
        mi.importCameras = false;
        mi.importLights = false;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (var name in new[] { "Jacket", "Pants", "Shirt", "Skin", "Hair", "Gold", "Shoes", "Eyes", "Brows", "Mouth", "Cig", "Ember" })
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MenuMaterials + name + ".mat");
            if (mat != null) mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
        }
        mi.SaveAndReimport();
    }

    static ModelImporterClipAnimation Clip(string name, bool loop, bool bakeHeight = true) => new ModelImporterClipAnimation
    {
        name = name,
        loopTime = loop,
        loopPose = loop,
        // The CharacterController moves the hero, so every clip plays in place.
        lockRootRotation = true,
        lockRootPositionXZ = true,
        lockRootHeightY = bakeHeight,
        keepOriginalOrientation = true,
        keepOriginalPositionY = true,
        keepOriginalPositionXZ = true,
    };

    static void SetupClips(string path, params ModelImporterClipAnimation[] clips)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(path);
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        var take = mi.defaultClipAnimations.FirstOrDefault();
        foreach (var c in clips)
        {
            c.takeName = take != null ? take.takeName : c.name;
            if (c.lastFrame <= 0 && take != null)
            {
                c.firstFrame = take.firstFrame;
                c.lastFrame = take.lastFrame;
            }
        }
        mi.clipAnimations = clips;
        mi.SaveAndReimport();
    }
}
