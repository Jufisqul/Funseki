using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.DayCycle
{
    // Loads the school greybox scene (School_Greybox, built from KitSettings by the school builder)
    // additively, so Slice_Day1 always plays in the current school without copying it.
    // Only the roots listed in keepRoots stay active: School_Greybox's own test player, cameras and sun are switched off.
    public class SchoolSceneLoader : MonoBehaviour
    {
        [SerializeField] string sceneName = "School_Greybox";
        [Tooltip("Used in the Editor when the scene is not in Build Settings")]
        [SerializeField] string scenePath = "Assets/_Project/School/Scenes/School_Greybox.unity";
        [SerializeField] string[] keepRoots = { "School" };
        [Tooltip("The game starts here, in front of the main entrance: doors within doorRadius of this point open at load")]
        [SerializeField] Vector3 mainEntrance = new(25.2f, 0f, -42f);
        [SerializeField] float doorRadius = 2f;
        [Tooltip("Doors swing away from this point (where the hero stands)")]
        [SerializeField] Vector3 openFrom = new(25.2f, 1f, -44f);

        void Awake()
        {
            if (SceneManager.GetSceneByName(sceneName).isLoaded) return;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
                return;
            }
#if UNITY_EDITOR
            Debug.LogWarning($"[SchoolSceneLoader] {sceneName} is not in Build Settings; loading it from {scenePath} (Editor only).");
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            Debug.LogError($"[SchoolSceneLoader] {sceneName} is not in Build Settings.");
#endif
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != sceneName) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;

            int hidden = 0, doors = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (System.Array.IndexOf(keepRoots, root.name) < 0) { root.SetActive(false); hidden++; continue; }
                doors += OpenEntranceDoors(root.transform);
            }
            Debug.Log($"[SchoolSceneLoader] {sceneName} loaded: {hidden} extra roots switched off, {doors} main entrance door(s) opened.");
        }

        // Door lives in the school's own assembly, so it is reached by name: Door.Toggle(Vector3 from) animates it open.
        // Every other door stays as the school builder left it.
        int OpenEntranceDoors(Transform root)
        {
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var door = t.GetComponent("Door");
                if (door == null) continue;
                Vector3 d = t.position - mainEntrance;
                d.y = 0f;
                if (d.magnitude > doorRadius) continue;
                door.SendMessage("Toggle", openFrom, SendMessageOptions.DontRequireReceiver);
                n++;
            }
            return n;
        }
    }
}
