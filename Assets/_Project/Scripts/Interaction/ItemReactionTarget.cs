using Funseki.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Funseki.Interaction
{
    // Simple IItemTarget: reacts to the items listed in ItemReactionData (recolors itself, says a line,
    // sets a world flag, fires onUsed). Any other item returns false, so the hero says "won't work".
    public class ItemReactionTarget : MonoBehaviour, IItemTarget
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int Color = Shader.PropertyToID("_Color");

        [SerializeField] ItemReactionData data;
        public UnityEvent<ItemData> onUsed;

        public bool Done { get; private set; }

        void Start()
        {
            if (!string.IsNullOrEmpty(data.worldFlag) && ServiceLocator.TryGet<WorldFlags>(out var flags) && flags.GetFlag(data.worldFlag))
                MarkDone();
        }

        public bool UseItem(ItemData item, bool primary)
        {
            if (!data.Accepts(item, primary)) return false;

            if (Done && data.once)
            {
                Bark(data.alreadyDoneLine);
                return true;
            }

            MarkDone();
            if (!string.IsNullOrEmpty(data.worldFlag) && ServiceLocator.TryGet<WorldFlags>(out var flags))
                flags.SetFlag(data.worldFlag);
            Bark(data.successLine);
            onUsed?.Invoke(item);
            return true;
        }

        void MarkDone()
        {
            Done = true;
            if (!data.recolor) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block, 0);
                block.SetColor(BaseColor, data.doneColor);
                block.SetColor(Color, data.doneColor);
                r.SetPropertyBlock(block, 0);
            }
        }

        // IItemTarget doesn't get the hero; null means "the hero the player controls now".
        void Bark(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            GameEvents.RaiseHeroBark(null, line);
        }
    }
}
