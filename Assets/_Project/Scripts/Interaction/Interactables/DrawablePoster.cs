using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // Плакат или картина (GDD 5.9): with the marker in hand, LMB swaps the picture for a defaced one from PosterData
    // and the hero says a random line; a teacher who sees it raises GameEvents.OnNoiseMade («Шум»).
    // Without the marker E just inspects it. Saved: which defaced picture is shown (0 = clean).
    public class DrawablePoster : InteractableObject, IItemTarget
    {
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        [Tooltip("The picture surface")]
        [SerializeField] Renderer picture;

        PosterData D => (PosterData)data;
        MaterialPropertyBlock block;

        int Variant => Flags.GetCounter(Key("variant"));

        protected override void RestoreState() => ShowVariant(Variant);

        public bool UseItem(ItemData item, bool primary)
        {
            if (item == null || item != D.drawItem || !primary || D.drawnTextures == null || D.drawnTextures.Length == 0)
                return false;

            var hero = HeroService.CurrentObject;
            int next = PickNextVariant();
            Flags.SetCounter(Key("variant"), next);
            Flags.AddCounter(Key("drawings"));
            if (!string.IsNullOrEmpty(D.drawnFlag)) Flags.SetFlag(D.drawnFlag);
            ShowVariant(next);
            Report(hero, "poster_drawn");

            var witness = FindWatchingTeacher(hero);
            if (witness != null && D.drawNoiseAmount > 0)
                GameEvents.RaiseNoiseMade(hero, witness, D.drawNoiseReason, D.drawNoiseAmount);
            Say(hero, InteractableData.Pick(witness != null && D.drawSeenLines is { Length: > 0 } ? D.drawSeenLines : D.drawLines));
            return true;
        }

        // 1-based index into drawnTextures, different from the current one when there is a choice.
        int PickNextVariant()
        {
            int count = D.drawnTextures.Length;
            if (count == 1) return 1;
            int current = Variant;
            int next;
            do next = Random.Range(1, count + 1); while (next == current);
            return next;
        }

        void ShowVariant(int variant)
        {
            if (picture == null) return;
            Texture2D tex = variant > 0 && D.drawnTextures != null && variant <= D.drawnTextures.Length
                ? D.drawnTextures[variant - 1]
                : D.cleanTexture;
            if (tex == null) return;
            block ??= new MaterialPropertyBlock();
            picture.GetPropertyBlock(block);
            block.SetTexture(BaseMap, tex);
            block.SetTexture(MainTex, tex);
            picture.SetPropertyBlock(block);
        }
    }
}
