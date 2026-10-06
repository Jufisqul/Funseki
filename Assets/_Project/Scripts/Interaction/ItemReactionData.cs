using Funseki.Core;
using UnityEngine;

namespace Funseki.Interaction
{
    // What an ItemReactionTarget accepts and what the hero says. Assets/_Project/Data/Interaction/.
    [CreateAssetMenu(fileName = "Reaction_", menuName = "Funseki/Interaction/Item Reaction")]
    public class ItemReactionData : ScriptableObject
    {
        [Tooltip("Items that work on this target")]
        public ItemData[] acceptedItems;
        [Tooltip("Off: LMB and RMB both work. On: only LMB")]
        public bool primaryOnly;
        [Tooltip("Can be done once; later uses say 'Already done' line")]
        public bool once = true;

        [Header("Lines")]
        [TextArea] public string successLine;
        [TextArea] public string alreadyDoneLine;

        [Header("Look after use")]
        public bool recolor = true;
        public Color doneColor = new(0.2f, 0.35f, 0.9f);

        [Header("World memory")]
        [Tooltip("snake_case flag set after a successful use; empty for none")]
        public string worldFlag;

        public bool Accepts(ItemData item, bool primary) =>
            item != null && (primary || !primaryOnly) && System.Array.IndexOf(acceptedItems, item) >= 0;
    }
}
