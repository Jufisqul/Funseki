using System.Collections.Generic;
using UnityEngine;

namespace Funseki.Interaction
{
    // Light highlight without render features: appends one extra (additive) material to every renderer
    // of the target and restores the original materials when the target is lost.
    public class InteractionHighlight
    {
        readonly List<(Renderer renderer, Material[] original)> applied = new();

        public void Apply(GameObject target, Material material)
        {
            Clear();
            if (material == null) return;
            foreach (var r in target.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var original = r.sharedMaterials;
                var withHighlight = new Material[original.Length + 1];
                original.CopyTo(withHighlight, 0);
                withHighlight[^1] = material;
                r.sharedMaterials = withHighlight;
                applied.Add((r, original));
            }
        }

        public void Clear()
        {
            foreach (var (r, original) in applied)
                if (r != null) r.sharedMaterials = original;
            applied.Clear();
        }
    }
}
