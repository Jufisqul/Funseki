using UnityEngine;

namespace Funseki.Core
{
    // One item the heroes can carry (GDD 5.1). Lives in Core because Interaction targets (IItemTarget)
    // and the Inventory module both need it, and modules may only share types through Core.
    [CreateAssetMenu(fileName = "Item_", menuName = "Funseki/Items/Item")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("snake_case English id, used by saves and world flags")]
        public string id;
        [Tooltip("Name shown to the player")]
        public string displayName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("In the hero's hand")]
        [Tooltip("Model shown in the right hand while the item is selected; may be empty")]
        public GameObject handPrefab;
        public Vector3 handPosition;
        public Vector3 handRotation;
        public float handScale = 1f;
    }
}
