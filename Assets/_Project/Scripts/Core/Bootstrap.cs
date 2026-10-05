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
            if (string.IsNullOrEmpty(flow.firstScene)) return;
            if (Application.CanStreamedLevelBeLoaded(flow.firstScene))
                SceneManager.LoadScene(flow.firstScene);
            else
                Debug.LogError($"[Bootstrap] Scene '{flow.firstScene}' is not in Build Settings.");
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
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => started = false;
    }
}
