using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Funseki.Heroes
{
    // Bakes the NavMesh for the followers at runtime from the colliders of every loaded scene: the school
    // (School_Greybox) is loaded additively and rebuilt by its own tools, so a baked asset would go stale.
    // Rebuilt when a scene is loaded and shortly after the hero interacts with something.
    // Triggers and characters (CharacterController, heroes, NPCs) are left out. Doors and other interactables are
    // left out of the bake too and carve the NavMesh instead (NavMeshObstacle), so an opened door lets the
    // followers through at once and a closed one blocks them, without a rebuild.
    public class HeroNavMesh : MonoBehaviour
    {
        [SerializeField] HeroSettings settings;

        NavMeshData data;
        NavMeshDataInstance instance;
        AsyncOperation pending;
        float rebuildAt = -1f;
        Bounds builtBounds;
        readonly List<NavMeshBuildSource> sources = new();
        readonly List<NavMeshBuildMarkup> markups = new();
        static readonly Bounds CollectBounds = new(Vector3.zero, new Vector3(4000f, 1000f, 4000f));

        void OnEnable()
        {
            data = new NavMeshData(0);
            instance = NavMesh.AddNavMeshData(data);
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameEvents.OnInteracted += OnInteracted;
            rebuildAt = 0f;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameEvents.OnInteracted -= OnInteracted;
            instance.Remove();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => rebuildAt = Time.unscaledTime + 0.1f;

        void OnInteracted(GameObject hero, GameObject target)
        {
            if (settings.rebuildAfterInteract > 0f) rebuildAt = Time.unscaledTime + settings.rebuildAfterInteract;
        }

        void Update()
        {
            if (pending != null && pending.isDone)
            {
                pending = null;
                Debug.Log($"[HeroNavMesh] NavMesh built from {sources.Count} colliders, area {builtBounds.size.x:0}x{builtBounds.size.z:0} m.");
            }
            if (rebuildAt < 0f || Time.unscaledTime < rebuildAt) return;
            if (pending != null) return; // wait for the running bake, then bake again
            rebuildAt = -1f;
            Rebuild();
        }

        void Rebuild()
        {
            NavMeshBuilder.CollectSources(CollectBounds, settings.navMeshLayers, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            sources.RemoveAll(Exclude);
            if (sources.Count == 0) return;

            // Bake only where there is geometry: a huge empty volume makes the bake slow and can drop tiles.
            bool first = true;
            foreach (var s in sources)
            {
                if (s.component is not Collider c) continue;
                if (first) { builtBounds = c.bounds; first = false; }
                else builtBounds.Encapsulate(c.bounds);
            }
            builtBounds.Expand(new Vector3(4f, 4f, 4f));

            var build = NavMesh.GetSettingsByID(0);
            build.agentRadius = settings.agentRadius;
            build.agentHeight = settings.agentHeight;
            build.agentClimb = settings.agentClimb;
            build.agentSlope = settings.agentSlope;
            pending = NavMeshBuilder.UpdateNavMeshDataAsync(data, build, sources, builtBounds);
        }

        bool Exclude(NavMeshBuildSource s)
        {
            if (s.component is not Collider col) return false;
            if (col.isTrigger || col is CharacterController) return true;
            if (col.GetComponentInParent<HeroUnit>() != null || col.GetComponentInParent<INpc>() != null) return true;
            if (col.GetComponentInParent<IInteractable>() == null) return false;
            Carve(col);
            return true;
        }

        // Door leaves move: they cut a hole where they are now instead of being baked in.
        void Carve(Collider col)
        {
            if (col is not BoxCollider box || col.GetComponent<NavMeshObstacle>() != null) return;
            var obstacle = col.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.carvingTimeToStationary = 0.1f;
        }
    }
}
