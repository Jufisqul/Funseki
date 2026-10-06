using Funseki.Core;
using TMPro;
using UnityEngine;

namespace Funseki.Inventory
{
    // Inventory rules, texts and the item panel look (GDD 5.1). Assets/_Project/Data/Inventory/InventorySettings.asset.
    [CreateAssetMenu(fileName = "InventorySettings", menuName = "Funseki/Inventory/Inventory Settings")]
    public class InventorySettings : ScriptableObject
    {
        [Header("Rules")]
        [Tooltip("Items the three heroes can carry together")]
        public int capacity = 8;
        [Tooltip("A picked-up item goes straight into the hand")]
        public bool selectPickedItem = true;
        [Tooltip("Every item in the game: saves store ids and look them up here")]
        public ItemData[] allItems;

        [Header("Texts")]
        [Tooltip("{0} is the item name")]
        public string pickupPromptFormat = "Взять «{0}»";
        [Tooltip("The hero's line when the item does nothing on the target")]
        public string wontWorkLine = "Не сработает";
        [Tooltip("The hero's line when the inventory is full and E is pressed on an item")]
        public string fullLine = "Больше не унесу";

        [Header("Panel")]
        [Tooltip("The panel hides after this long without scrolling, s")]
        public float panelHideDelay = 2f;
        [Tooltip("Fade in / out time, s")]
        public float panelFadeTime = 0.2f;
        public float slotSize = 72f;
        public float slotSpacing = 8f;
        [Tooltip("Distance from the bottom of the screen, px")]
        public float panelBottomOffset = 40f;
        public TMP_FontAsset font;
        public float nameFontSize = 26f;
        public float slotFontSize = 13f;
        public Color slotColor = new(0f, 0f, 0f, 0.55f);
        public Color selectedSlotColor = new(1f, 0.85f, 0.3f, 0.9f);
        public Color textColor = Color.white;

        [Header("States")]
        [Tooltip("Game states where items can be scrolled and used. Without Bootstrap they always can.")]
        public GameState[] activeStates = { GameState.Break };

        public bool IsActive(GameState state) => System.Array.IndexOf(activeStates, state) >= 0;

        public ItemData FindItem(string id)
        {
            if (allItems == null) return null;
            foreach (var item in allItems)
                if (item != null && item.id == id) return item;
            return null;
        }
    }
}
