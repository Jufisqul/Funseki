using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Editor-only: builds Hero.controller -- Locomotion blend tree (Idle / Walk / Run by Speed)
// and an Airborne state for jumps and falls. Re-runnable: it overwrites the controller.
public static class HeroAnimatorBuilder
{
    public const string ControllerPath = "Assets/_Game/Art/Characters/Hero/Hero.controller";

    [MenuItem("Tools/One Funseki/Hero/2. Build Animator Controller")]
    public static void Build()
    {
        var idle = Load(HeroImportSetup.JumpFbx, "Idle");
        var walk = Load(HeroImportSetup.WalkFbx, "Walk");
        var run = Load(HeroImportSetup.RunFbx, "Run");
        var jump = Load(HeroImportSetup.JumpFbx, "Jump");

        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter(PlayerAnimator.SpeedParam, AnimatorControllerParameterType.Float);
        ac.AddParameter(PlayerAnimator.GroundedParam, AnimatorControllerParameterType.Bool);
        ac.AddParameter(PlayerAnimator.VerticalSpeedParam, AnimatorControllerParameterType.Float);
        ac.parameters = ac.parameters.Select(p =>
        {
            if (p.name == PlayerAnimator.GroundedParam) p.defaultBool = true;
            return p;
        }).ToArray();

        var sm = ac.layers[0].stateMachine;
        var locomotion = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = PlayerAnimator.SpeedParam;
        tree.useAutomaticThresholds = false;
        // Speed is meters per second of the hero, so thresholds are the controller's walk and sprint speeds.
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 3.2f);
        tree.AddChild(run, 6f);
        sm.defaultState = locomotion;

        var airborne = sm.AddState("Airborne");
        airborne.motion = jump;

        var takeOff = locomotion.AddTransition(airborne);
        takeOff.hasExitTime = false;
        takeOff.duration = 0.08f;
        takeOff.AddCondition(AnimatorConditionMode.IfNot, 0, PlayerAnimator.GroundedParam);

        var land = airborne.AddTransition(locomotion);
        land.hasExitTime = false;
        land.duration = 0.15f;
        land.AddCondition(AnimatorConditionMode.If, 0, PlayerAnimator.GroundedParam);

        AssetDatabase.SaveAssets();
        Debug.Log("[One Funseki] Hero animator built: " + ControllerPath);
    }

    static AnimationClip Load(string fbx, string clip)
    {
        var c = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().FirstOrDefault(a => a.name == clip);
        if (c == null) throw new System.Exception($"Clip {clip} not found in {fbx}. Run Hero/1. Setup Imports first.");
        return c;
    }
}
