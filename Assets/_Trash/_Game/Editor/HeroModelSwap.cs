using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only: puts the current hero model (HeroImportSetup.HeroModel) under Player in the open scene,
// keeping everything else in the scene as it is. Use after the model changes instead of rebuilding scenes.
public static class HeroModelSwap
{
    [MenuItem("Tools/One Funseki/Hero/3. Swap Hero Model In Open Scene")]
    public static void Swap()
    {
        var player = GameObject.Find("Player");
        if (player == null) { Debug.LogWarning("[One Funseki] No Player in the open scene."); return; }

        var old = player.transform.Find("Model");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeroImportSetup.HeroModel);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.SetAsFirstSibling();

        var animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroAnimatorBuilder.ControllerPath);
        animator.applyRootMotion = false;
        player.GetComponent<PlayerAnimator>().animator = animator;

        var rig = Object.FindAnyObjectByType<PlayerCameraRig>();
        if (rig != null) rig.hideInFirstPerson = model.GetComponentsInChildren<Renderer>();

        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("[One Funseki] Hero model swapped in " + player.scene.path);
    }
}
