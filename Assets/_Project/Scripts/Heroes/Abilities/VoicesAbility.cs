using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    // Кайто «Голоса» (GDD 5.2): the nearest item that can be picked up within radius glows for a few seconds
    // and Кайто says a hint about where it is (the pickup's own VoiceHint, or a generic line).
    [CreateAssetMenu(fileName = "Ability_Voices", menuName = "Funseki/Heroes/Abilities/Voices (Kaito)")]
    public class VoicesAbility : HeroAbility
    {
        [Header("Search")]
        [Tooltip("m")]
        public float radius = 15f;
        [Tooltip("Layers searched for pickups")]
        public LayerMask layers = ~0;

        [Header("Glow")]
        [Tooltip("s")]
        public float highlightSeconds = 5f;
        [Tooltip("Spawned on the item instead of the default light; destroyed after highlightSeconds")]
        public GameObject markerPrefab;
        public Color glowColor = new(0.55f, 0.85f, 1f);
        [Tooltip("Light range, m")]
        public float glowRange = 3f;
        public float glowIntensity = 8f;
        [Tooltip("Pulses per second")]
        public float glowPulse = 1.5f;
        [Tooltip("Transparent material of the light beam over the item; empty = no beam")]
        public Material beamMaterial;
        [Tooltip("m")]
        public float beamHeight = 2.5f;
        [Tooltip("m")]
        public float beamWidth = 0.25f;

        [Header("Lines")]
        [Tooltip("Said when the pickup has no VoiceHint of its own")]
        public string[] genericHints = { "Голоса шепчут… что-то лежит рядом.", "Там что-то есть. Чую." };
        [Tooltip("Said when nothing is in range; the cooldown doesn't start")]
        public string[] nothingLines = { "Голоса молчат.", "Тишина. Даже голоса ушли на перемену." };

        static readonly HashSet<Component> Seen = new();

        public override bool TryUse(HeroAbilityContext ctx)
        {
            var target = FindNearest(ctx.Hero.transform.position);
            if (target == null)
            {
                ctx.Bark(HeroAbilityContext.PickLine(nothingLines));
                return false;
            }

            Highlight(target.gameObject);
            string hint = ((IPickup)target).VoiceHint;
            ctx.Bark(!string.IsNullOrEmpty(hint) ? hint : HeroAbilityContext.PickLine(genericHints));
            return true;
        }

        Component FindNearest(Vector3 from)
        {
            // Allocating query on purpose: a school corridor easily has hundreds of colliders within 15 m,
            // and a fixed buffer would silently drop the item.
            var hits = Physics.OverlapSphere(from, radius, layers, QueryTriggerInteraction.Collide);
            Component best = null;
            float bestDist = float.MaxValue;
            Seen.Clear();
            foreach (var hit in hits)
            {
                var pickup = hit.GetComponentInParent<IPickup>() as Component;
                if (pickup == null || !Seen.Add(pickup) || !pickup.gameObject.activeInHierarchy) continue;
                float d = (pickup.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = pickup; }
            }
            return best;
        }

        // A pulsing light and a beam of light over the item; it follows the item and vanishes when the item is taken.
        void Highlight(GameObject item)
        {
            var glow = markerPrefab != null ? Instantiate(markerPrefab) : new GameObject("VoicesGlow");
            var follow = glow.GetComponent<VoicesGlow>();
            if (follow == null) follow = glow.AddComponent<VoicesGlow>();
            Light light = null;
            if (markerPrefab == null)
            {
                light = glow.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = glowColor;
                light.range = glowRange;
                light.shadows = LightShadows.None;
                if (beamMaterial != null) AddBeam(glow.transform);
            }
            follow.Setup(item.transform, light, glowIntensity, glowPulse);
            Destroy(glow, highlightSeconds);
        }

        void AddBeam(Transform parent)
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "Beam";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(parent, false);
            beam.transform.localPosition = Vector3.up * beamHeight * 0.5f;
            beam.transform.localScale = new Vector3(beamWidth, beamHeight * 0.5f, beamWidth);
            var r = beam.GetComponent<Renderer>();
            r.sharedMaterial = beamMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
