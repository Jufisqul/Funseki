using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only: import settings for the playable hero (Humanoid, URP material from the Tripo textures)
// and the Mixamo clips in Assets/Anim. Re-runnable.
// The hero FBX comes from Art/Characters/Hero/rig_hero_glb.py (rigged from the Tripo .glb).
public static class HeroImportSetup
{
    const string HeroDir = "Assets/_Game/Art/Characters/Hero/";
    public const string HeroModel = HeroDir + "Hero_Game.fbx";
    const string HeroMaterial = HeroDir + "Hero.mat";
    public const string WalkFbx = "Assets/Anim/Walking (1).fbx";
    public const string RunFbx = "Assets/Anim/Run.fbx";
    public const string JumpFbx = "Assets/Anim/Jumping Up.fbx";

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
        mi.SaveAndReimport();

        // Every material slot of the model points at one URP material built from the textures.
        var mat = BuildMaterial();
        foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(HeroModel).OfType<Material>())
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), mat);
        mi.SaveAndReimport();
    }

    static Material BuildMaterial()
    {
        var normalPath = HeroDir + "Textures/Hero_normal.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(normalPath);
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(HeroMaterial);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, HeroMaterial);
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(HeroDir + "Textures/Hero_basecolor.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.35f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
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
