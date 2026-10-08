using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Inventory
{
    // The shared inventory of all three heroes (GDD 5.1): up to InventorySettings.capacity items,
    // one of them selected (in the hand). Registered in ServiceLocator by InventoryService.
    // Changes raise GameEvents.OnItemAdded / OnItemRemoved / OnSelectedItemChanged.
    public class Inventory : IItemBag
    {
        [Serializable]
        struct SaveData
        {
            public List<string> items;
            public int selected;
        }

        readonly List<ItemData> items = new();

        public InventorySettings Settings { get; }
        public IReadOnlyList<ItemData> Items => items;
        public int Capacity => Settings.capacity;
        public int Count => items.Count;
        public bool IsFull => items.Count >= Settings.capacity;
        public int SelectedIndex { get; private set; } = -1;
        public ItemData Selected => SelectedIndex >= 0 && SelectedIndex < items.Count ? items[SelectedIndex] : null;

        public Inventory(InventorySettings settings) => Settings = settings;

        public bool Contains(ItemData item) => items.Contains(item);

        public bool TryAdd(ItemData item)
        {
            if (item == null || IsFull) return false;
            items.Add(item);
            GameEvents.RaiseItemAdded(item);
            if (Settings.selectPickedItem || SelectedIndex < 0) Select(items.Count - 1);
            return true;
        }

        public bool Remove(ItemData item)
        {
            int index = items.IndexOf(item);
            if (index < 0) return false;
            var before = Selected;
            items.RemoveAt(index);
            if (index < SelectedIndex || SelectedIndex >= items.Count) SelectedIndex--;
            if (items.Count > 0 && SelectedIndex < 0) SelectedIndex = 0;
            GameEvents.RaiseItemRemoved(item);
            if (Selected != before) GameEvents.RaiseSelectedItemChanged(Selected);
            return true;
        }

        // Wraps around in both directions. direction: +1 next, -1 previous.
        public void Cycle(int direction)
        {
            if (items.Count > 0 && direction != 0)
                Select(((SelectedIndex + direction) % items.Count + items.Count) % items.Count);
            GameEvents.RaiseItemCycled(Selected);
        }

        public void Select(int index)
        {
            index = items.Count == 0 ? -1 : Mathf.Clamp(index, 0, items.Count - 1);
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            GameEvents.RaiseSelectedItemChanged(Selected);
        }

        public void Clear()
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                items.RemoveAt(i);
                GameEvents.RaiseItemRemoved(item);
            }
            if (SelectedIndex != -1)
            {
                SelectedIndex = -1;
                GameEvents.RaiseSelectedItemChanged(null);
            }
        }

        // ---- save / load (ids are resolved through InventorySettings.allItems)
        public string ToJson()
        {
            var data = new SaveData { items = new List<string>(), selected = SelectedIndex };
            foreach (var item in items) data.items.Add(item.id);
            return JsonUtility.ToJson(data);
        }

        public void LoadJson(string json)
        {
            Clear();
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data.items == null) return;
            foreach (var id in data.items)
            {
                var item = Settings.FindItem(id);
                if (item == null) { Debug.LogWarning($"[Inventory] Unknown item id '{id}' in save, skipped."); continue; }
                if (IsFull) break;
                items.Add(item);
                GameEvents.RaiseItemAdded(item);
            }
            Select(data.selected);
        }
    }
}
