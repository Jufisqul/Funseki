using Funseki.Core;
using UnityEngine;

namespace Funseki.Inventory
{
    // Creates the shared Inventory and registers it in ServiceLocator (and in SaveRegistry as "inventory").
    // Normally a child of [Bootstrap]; a copy in a test scene steps aside if Bootstrap already made one.
    [DefaultExecutionOrder(-900)]
    public class InventoryService : MonoBehaviour, ISaveable
    {
        [SerializeField] InventorySettings settings;

        Inventory inventory;

        public string SaveKey => "inventory";
        public string ToJson() => inventory.ToJson();
        public void LoadJson(string json) => inventory.LoadJson(json);

        void Awake()
        {
            if (ServiceLocator.IsRegistered<Inventory>()) { Destroy(gameObject); return; }
            inventory = new Inventory(settings);
            ServiceLocator.Register(inventory);
            SaveRegistry.Register(this);
        }

        void OnEnable() => GameEvents.OnItemGiven += OnItemGiven;
        void OnDisable() => GameEvents.OnItemGiven -= OnItemGiven;

        void OnItemGiven(ItemData item)
        {
            if (inventory != null) inventory.TryAdd(item);
        }

        void OnDestroy()
        {
            if (inventory == null) return;
            SaveRegistry.Unregister(this);
            if (ServiceLocator.TryGet<Inventory>(out var current) && current == inventory)
                ServiceLocator.Unregister<Inventory>();
        }
    }
}
