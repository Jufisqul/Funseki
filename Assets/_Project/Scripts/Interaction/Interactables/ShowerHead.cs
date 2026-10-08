using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // Душевая (spec «Душевая»): E turns the water on / off. Рюта or Кайто with the paint in hand get E (or LMB) =
    // pour it into the shower head: the hero chuckles, the paint is used up, ShowerData.paintedFlag goes into the world,
    // and from then on this shower runs in the paint colour. A teacher who sees the pouring adds «Шум».
    // Saved: water on or off, painted or not.
    public class ShowerHead : InteractableObject, IItemTarget
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("The water stream; switched on while the shower runs")]
        [SerializeField] Renderer water;

        ShowerData D => (ShowerData)data;
        MaterialPropertyBlock block;

        bool IsOn => Flags.GetFlag(Key("on"));
        bool Painted => Flags.GetFlag(Key("painted"));

        public override string Prompt =>
            CanPaint(HeroService.CurrentObject) ? D.paintPrompt : IsOn ? D.offPrompt : D.onPrompt;

        protected override void RestoreState() => ShowWater();

        bool CanPaint(GameObject hero)
        {
            if (Painted || D.paintItem == null || !IsAllowed(D.paintHeroes, HeroOf(hero))) return false;
            var bag = Bag;
            return bag != null && bag.Selected == D.paintItem;
        }

        protected override void OnUsed(GameObject hero, bool first, GameObject witness)
        {
            if (CanPaint(hero)) { Pour(hero, witness); return; }

            if (IsOn)
            {
                Flags.SetFlag(Key("on"), false);
                ShowWater();
                Report(hero, "shower_off");
                return;
            }

            Flags.SetFlag(Key("on"));
            int ons = Flags.AddCounter(Key("ons"));
            ShowWater();
            Report(hero, "shower_on");
            if (Painted && !Flags.GetFlag(Key("paint_seen")))
            {
                Flags.SetFlag(Key("paint_seen"));
                Say(hero, InteractableData.Pick(D.paintedWaterLines));
            }
            else SayUsualLine(hero, ons == 1, witness);
        }

        // LMB with the paint in hand: the same as E for a hero who can pour it.
        public bool UseItem(ItemData item, bool primary)
        {
            if (item == null || item != D.paintItem || !primary) return false;
            var hero = HeroService.CurrentObject;
            if (!CanPaint(hero)) return false;
            Pour(hero, FindWatchingTeacher(hero));
            return true;
        }

        void Pour(GameObject hero, GameObject witness)
        {
            Flags.SetFlag(Key("painted"));
            if (!string.IsNullOrEmpty(D.paintedFlag)) Flags.SetFlag(D.paintedFlag);
            if (D.consumeItem) Bag?.Remove(D.paintItem);
            ShowWater();
            Report(hero, "shower_painted");

            if (witness != null && D.paintNoiseAmount > 0)
                GameEvents.RaiseNoiseMade(hero, witness, D.paintNoiseReason, D.paintNoiseAmount);
            Say(hero, InteractableData.Pick(witness != null && D.paintSeenLines is { Length: > 0 } ? D.paintSeenLines : D.paintLines));
        }

        void ShowWater()
        {
            if (water == null) return;
            water.gameObject.SetActive(IsOn);
            Color c = Painted ? D.paintColor : D.waterColor;
            block ??= new MaterialPropertyBlock();
            water.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            block.SetColor(ColorId, c);
            water.SetPropertyBlock(block);
        }
    }
}
