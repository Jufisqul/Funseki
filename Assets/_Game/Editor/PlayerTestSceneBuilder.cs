using System.IO;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Editor-only: builds a grey test yard to tune the hero controller and cameras (Roadmap Phase 2:
// Player Controller, Camera). Re-runnable: it rebuilds the scene from scratch.
public static class PlayerTestSceneBuilder
{
    const string Root = "Assets/_Game/";
    const string ScenePath = Root + "Scenes/PlayerTest.unity";
    const string MatDir = Root + "Art/Greybox/";
    const string ControlsPath = Root + "Input/GameControls.inputactions";

    [MenuItem("Tools/One Funseki/Build Player Test Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        Directory.CreateDirectory(MatDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);

        BuildLighting();
        BuildYard();
        var player = BuildPlayer(controls, out var cameraTarget, out var body);
        BuildCameras(controls, player, cameraTarget, body);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[One Funseki] Player test scene built: " + ScenePath);
    }

    static void BuildLighting()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("#BFD3E6");
        RenderSettings.ambientEquatorColor = Hex("#C9C3B8");
        RenderSettings.ambientGroundColor = Hex("#6F6A62");
    }

    // Ground, crates, a ramp, stairs and a wall: enough to feel speed, jumps, slopes and camera collisions.
    static void BuildYard()
    {
        var root = new GameObject("Yard").transform;
        Box(root, "Ground", new Vector3(0, -0.5f, 0), new Vector3(60, 1, 60), "Ground", "#A9A59C");

        for (int i = 0; i < 6; i++)
            Box(root, "Crate" + i, new Vector3(-8 + i * 3.2f, 0.5f + (i % 3) * 0.25f, 8), Vector3.one * (1 + (i % 3) * 0.5f), "Crate", "#C9A26B");

        var ramp = Box(root, "Ramp", new Vector3(8, 1, -2), new Vector3(3, 0.3f, 8), "Concrete", "#BDB8AE");
        ramp.transform.rotation = Quaternion.Euler(-15f, 0, 0);

        for (int i = 0; i < 8; i++)
            Box(root, "Step" + i, new Vector3(-8, 0.1f + i * 0.2f, -4 + i * 0.4f), new Vector3(3, 0.2f, 0.4f), "Concrete", "#BDB8AE");
        Box(root, "Landing", new Vector3(-8, 1.5f, 0.2f), new Vector3(3, 0.2f, 2.4f), "Concrete", "#BDB8AE");

        Box(root, "Wall", new Vector3(0, 2, -10), new Vector3(16, 4, 0.4f), "Wall", "#DEDAD3");
        Box(root, "Corridor_L", new Vector3(14, 1.5f, 6), new Vector3(0.3f, 3, 10), "Wall", "#DEDAD3");
        Box(root, "Corridor_R", new Vector3(16.4f, 1.5f, 6), new Vector3(0.3f, 3, 10), "Wall", "#DEDAD3");
        Box(root, "Corridor_Roof", new Vector3(15.2f, 3.1f, 6), new Vector3(2.7f, 0.2f, 10), "Wall", "#DEDAD3");
        Box(root, "DetailToInspect", new Vector3(2, 0.9f, 3), new Vector3(0.15f, 0.15f, 0.15f), "Accent", "#E23C3C");
        Box(root, "Desk", new Vector3(2, 0.4f, 3), new Vector3(1.2f, 0.8f, 0.6f), "Crate", "#C9A26B");
    }

    static GameObject BuildPlayer(InputActionAsset controls, out Transform cameraTarget, out Renderer[] body)
    {
        var go = new GameObject("Player") { tag = "Player" };
        go.transform.position = new Vector3(0, 0.05f, 0);

        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.7f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0, 0.85f, 0);
        cc.stepOffset = 0.35f;
        cc.slopeLimit = 45f;

        // Placeholder body until the hero models arrive: a capsule and a "nose" that shows facing.
        var capsule = Prim(PrimitiveType.Capsule, go.transform, "Body", new Vector3(0, 0.85f, 0), new Vector3(0.6f, 0.85f, 0.6f), "Hero", "#2F5D9E");
        var nose = Prim(PrimitiveType.Cube, go.transform, "Facing", new Vector3(0, 1.45f, 0.3f), new Vector3(0.25f, 0.1f, 0.2f), "Accent", "#FFD23F");
        body = new[] { capsule.GetComponent<Renderer>(), nose.GetComponent<Renderer>() };

        cameraTarget = new GameObject("CameraTarget").transform;
        cameraTarget.SetParent(go.transform, false);
        cameraTarget.localPosition = new Vector3(0, 1.55f, 0);

        var controller = go.AddComponent<PlayerController>();
        controller.actions = controls;
        return go;
    }

    static void BuildCameras(InputActionAsset controls, GameObject player, Transform cameraTarget, Renderer[] body)
    {
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        var brain = camGo.AddComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.25f);

        var tp = new GameObject("CM ThirdPerson").AddComponent<CinemachineCamera>();
        tp.Follow = cameraTarget;
        tp.Lens.FieldOfView = 50f;
        var orbit = tp.gameObject.AddComponent<CinemachineOrbitalFollow>();
        orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
        orbit.Radius = 4.2f;
        var tracker = orbit.TrackerSettings;
        tracker.BindingMode = BindingMode.WorldSpace;
        tracker.PositionDamping = new Vector3(0.1f, 0.2f, 0.1f);
        orbit.TrackerSettings = tracker;
        orbit.VerticalAxis.Range = new Vector2(-15f, 65f);
        orbit.VerticalAxis.Value = 15f;
        orbit.VerticalAxis.Center = 15f;
        var composer = tp.gameObject.AddComponent<CinemachineRotationComposer>();
        composer.Damping = new Vector2(0.1f, 0.1f);
        var deoccluder = tp.gameObject.AddComponent<CinemachineDeoccluder>();
        deoccluder.CollideAgainst = 1;   // Default layer
        deoccluder.IgnoreTag = "Player";
        deoccluder.MinimumDistanceFromTarget = 0.4f;

        var fp = new GameObject("CM FirstPerson").AddComponent<CinemachineCamera>();
        fp.Follow = cameraTarget;
        fp.Lens.FieldOfView = 60f;
        fp.Lens.NearClipPlane = 0.05f;
        fp.gameObject.AddComponent<CinemachineHardLockToTarget>();
        var panTilt = fp.gameObject.AddComponent<CinemachinePanTilt>();
        panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.World;

        var controller = player.GetComponent<PlayerController>();
        controller.cameraTransform = camGo.transform;

        var rig = new GameObject("CameraRig").AddComponent<PlayerCameraRig>();
        rig.actions = controls;
        rig.player = controller;
        rig.thirdPersonCamera = tp;
        rig.firstPersonCamera = fp;
        rig.hideInFirstPerson = body;
    }

    // ----------------------------------------------------------------- helpers

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, string mat, string hex)
        => Prim(PrimitiveType.Cube, parent, name, pos, size, mat, hex);

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, string mat, string hex)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = Mat(mat, hex);
        if (parent != null && parent.GetComponent<CharacterController>() != null)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Material Mat(string name, string hex)
    {
        string path = MatDir + "Grey_" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", Hex(hex));
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
}
