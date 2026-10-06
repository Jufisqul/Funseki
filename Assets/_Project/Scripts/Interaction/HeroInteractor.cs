using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Funseki.Interaction
{
    // On the hero. Every frame picks the closest IInteractable / IItemTarget within InteractionSettings.radius
    // in a cone in front of the character that the hero can actually see, highlights it and shows the prompt.
    // E (Gameplay/Interact) calls Interact and raises GameEvents.OnInteracted.
    // Doors (Funseki.School.Door) are IInteractable too, so the hero needs no other E handler.
    public class HeroInteractor : MonoBehaviour
    {
        public const string MapName = "Gameplay";

        [SerializeField] InputActionAsset actions;
        [SerializeField] InteractionSettings settings;

        public GameObject Target { get; private set; }

        InputActionMap map;
        InputAction interact;
        InteractionPrompt prompt;
        readonly InteractionHighlight highlight = new();
        readonly Collider[] hits = new Collider[64];
        readonly List<MonoBehaviour> candidates = new();
        IInteractable targetInteractable;
        IItemTarget targetItemTarget;
        ItemData heldItem;
        bool active = true;

        void Awake()
        {
            map = actions.FindActionMap(MapName, true);
            interact = map.FindAction("Interact", true);
            prompt = new InteractionPrompt(settings);
        }

        void OnEnable()
        {
            map.Enable();
            GameEvents.OnGameStateChanged += OnGameStateChanged;
            GameEvents.OnSelectedItemChanged += OnSelectedItemChanged;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)) active = settings.IsActive(fsm.Current);
        }

        void OnDisable()
        {
            GameEvents.OnGameStateChanged -= OnGameStateChanged;
            GameEvents.OnSelectedItemChanged -= OnSelectedItemChanged;
            SetTarget(null);
            prompt.Show(null, null);
        }

        void OnDestroy() => prompt.Destroy();

        void OnGameStateChanged(GameState previous, GameState current) => active = settings.IsActive(current);
        void OnSelectedItemChanged(ItemData item) => heldItem = item;

        void Update()
        {
            // Only the hero under control looks for targets (Funseki.Heroes: followers stay quiet).
            SetTarget(active && HeroService.IsInControl(gameObject) ? FindTarget() : null);

            if (Target != null && targetInteractable != null && interact.WasPressedThisFrame()
                && targetInteractable.CanInteract(gameObject))
            {
                var target = Target;
                targetInteractable.Interact(gameObject);
                GameEvents.RaiseInteracted(gameObject, target);
            }
        }

        void LateUpdate()
        {
            // The target may have hidden itself during Interact (a picked-up item).
            if (Target != null && !Target.activeInHierarchy) SetTarget(null);
            prompt.Show(Target, PromptText());
        }

        string PromptText()
        {
            if (Target == null) return null;
            string text = null;
            if (targetInteractable != null && targetInteractable.CanInteract(gameObject))
                text = string.Format(settings.interactFormat, targetInteractable.Prompt);
            if (targetItemTarget != null && heldItem != null)
            {
                string use = string.Format(settings.useItemFormat, heldItem.displayName);
                text = text == null ? use : text + "\n" + use;
            }
            return text;
        }

        GameObject FindTarget()
        {
            Vector3 chest = transform.position + Vector3.up * settings.chestHeight;
            Vector3 eyes = transform.position + Vector3.up * settings.eyeHeight;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float halfCone = settings.coneAngle * 0.5f;

            int n = Physics.OverlapSphereNonAlloc(chest, settings.radius, hits, settings.targetLayers, QueryTriggerInteraction.Collide);
            GameObject best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var col = hits[i];
                if (col.transform.IsChildOf(transform)) continue;
                var owner = FindOwner(col);
                if (owner == null || !owner.gameObject.activeInHierarchy) continue;

                Vector3 point = ClosestPoint(col, chest);
                float dist = Vector3.Distance(chest, point);
                if (dist > settings.radius || dist >= bestDist) continue;

                // In the cone if either the object's center or its nearest point is (long objects like a blackboard).
                if (!InCone(col.bounds.center, forward, halfCone) && !InCone(point, forward, halfCone)) continue;
                if (!CanSee(eyes, col, owner.transform)) continue;

                best = owner.gameObject;
                bestDist = dist;
            }
            return best;
        }

        bool InCone(Vector3 point, Vector3 forward, float halfCone)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            return flat.sqrMagnitude < 0.0001f || Vector3.Angle(forward, flat) <= halfCone;
        }

        // Nearest component up the hierarchy that is an IInteractable the hero can use now; otherwise an IItemTarget;
        // otherwise any IInteractable (shown highlighted, without the E prompt).
        MonoBehaviour FindOwner(Collider col)
        {
            col.GetComponentsInParent(false, candidates);
            MonoBehaviour itemTarget = null, fallback = null;
            foreach (var mb in candidates)
            {
                if (mb is IInteractable i)
                {
                    if (i.CanInteract(gameObject)) return mb;
                    fallback ??= mb;
                }
                if (mb is IItemTarget) itemTarget ??= mb;
            }
            return itemTarget != null ? itemTarget : fallback;
        }

        static Vector3 ClosestPoint(Collider col, Vector3 from)
        {
            bool exact = col is BoxCollider || col is SphereCollider || col is CapsuleCollider || (col is MeshCollider mc && mc.convex);
            return exact ? col.ClosestPoint(from) : col.bounds.ClosestPoint(from);
        }

        bool CanSee(Vector3 eyes, Collider col, Transform owner)
        {
            // Aim slightly inside the target so the ray ends on it, not just short of it.
            Vector3 point = Vector3.MoveTowards(ClosestPoint(col, eyes), col.bounds.center, 0.05f);
            if (!Physics.Linecast(eyes, point, out var hit, settings.occluderLayers, QueryTriggerInteraction.Ignore)) return true;
            return hit.transform.IsChildOf(owner) || hit.transform.IsChildOf(transform);
        }

        void SetTarget(GameObject target)
        {
            if (target == Target) return;
            highlight.Clear();
            Target = target;
            targetInteractable = null;
            targetItemTarget = null;
            if (target != null)
            {
                targetInteractable = target.GetComponent<IInteractable>();
                targetItemTarget = target.GetComponent<IItemTarget>();
                highlight.Apply(target, settings.highlightMaterial);
            }
            GameEvents.RaiseInteractionTargetChanged(gameObject, target);
        }
    }
}
