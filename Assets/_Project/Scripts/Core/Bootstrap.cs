using UnityEngine;
using UnityEngine.SceneManagement;

namespace Funseki.Core
{
    // Lives in the Bootstrap scene and survives the whole game.
    // Creates and registers core services, then opens GameFlowConfig.firstScene.
    // Other modules' services are child objects here that register themselves in Awake.
    [DefaultExecutionOrder(-1000)]
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] GameFlowConfig flow;

        static bool started;
        bool owner;

        void Awake()
        {
            if (started) { Destroy(gameObject); return; }
            started = true;
            owner = true;
            DontDestroyOnLoad(gameObject);

            ServiceLocator.Register(flow);
            ServiceLocator.Register(new GameStateMachine());
            ServiceLocator.Register(new WorldFlags());

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void Start()
        {
            // Editor quick test (Tools > Funseki > Quick Play) skips straight to its scene.
            string first = QuickPlay.Active ? QuickPlay.Scene : flow.firstScene;
            if (string.IsNullOrEmpty(first)) return;
            if (Application.CanStreamedLevelBeLoaded(first))
                SceneManager.LoadScene(first);
            else
                Debug.LogError($"[Bootstrap] Scene '{first}' is not in Build Settings.");
        }

        void OnDestroy()
        {
            if (!owner) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (flow.TryGetStartState(scene.name, out var state))
                ServiceLocator.Get<GameStateMachine>().ChangeState(state);
            if (QuickPlay.Active && scene.name == QuickPlay.Scene && QuickPlay.TryGetSpawn(out var spawn))
                StartCoroutine(DropLeader(spawn));
        }

        // Quick Play from the Scene View: moves the leading hero to the chosen point once the heroes exist.
        System.Collections.IEnumerator DropLeader(Vector3 position)
        {
            // Give the heroes and the additively loaded school time to appear.
            yield return new WaitForSeconds(0.5f);
            var hero = HeroService.CurrentObject;
            if (hero == null) { Debug.LogWarning("[QuickPlay] No leading hero to move."); yield break; }
            var cc = hero.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            hero.transform.position = position;
            if (cc != null) cc.enabled = true;
            Debug.Log($"[QuickPlay] {hero.name} moved to {position}.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => started = false;
    }
}
