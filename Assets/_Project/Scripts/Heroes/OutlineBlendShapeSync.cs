using UnityEngine;

namespace Funseki.Heroes
{
    // On the outline renderer of a toon character (Funseki/ToonOutline on a second SkinnedMeshRenderer that shares
    // the body mesh and bones). Bones move both renderers, blend shapes don't: this copies the face blend shape
    // weights from the body renderer every frame, so the outline follows the jaw when the mouth opens.
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class OutlineBlendShapeSync : MonoBehaviour
    {
        [SerializeField] SkinnedMeshRenderer source;

        SkinnedMeshRenderer target;

        public void SetSource(SkinnedMeshRenderer renderer) => source = renderer;

        void Awake() => target = GetComponent<SkinnedMeshRenderer>();

        void LateUpdate()
        {
            if (source == null || source.sharedMesh != target.sharedMesh) return;
            int n = target.sharedMesh.blendShapeCount;
            for (int i = 0; i < n; i++)
            {
                float w = source.GetBlendShapeWeight(i);
                if (target.GetBlendShapeWeight(i) != w) target.SetBlendShapeWeight(i, w);
            }
        }
    }
}
