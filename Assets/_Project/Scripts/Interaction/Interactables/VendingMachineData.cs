using UnityEngine;

namespace Funseki.Interaction
{
    // Автомат с газировкой. The first-time / repeat lines of InteractableData go with an ordinary can (empty = silent).
    [CreateAssetMenu(fileName = "Interactable_VendingMachine", menuName = "Funseki/Interaction/Vending Machine")]
    public class VendingMachineData : InteractableData
    {
        [Header("Chances per use (rolled once: jam, else break, else one can)")]
        [Range(0f, 1f)] public float jamChance = 0.05f;
        [Range(0f, 1f)] public float breakChance = 0.2f;

        [Header("Cans")]
        [Tooltip("Can model with a collider; empty = a grey-box cylinder")]
        public GameObject canPrefab;
        public Vector3 canSize = new(0.066f, 0.12f, 0.066f);
        public Color canColor = new(0.85f, 0.12f, 0.12f);
        public float canMass = 0.35f;
        [Tooltip("Seconds a can lies around before it disappears")]
        public float canLifetime = 60f;
        [Tooltip("Push out of the slot, m/s")]
        public float ejectSpeed = 1.5f;
        [Tooltip("Random spread of the push, degrees")]
        public float ejectSpread = 25f;

        [Header("Jam")]
        public int jamCanCount = 50;
        [Tooltip("Seconds between cans while jammed")]
        public float jamInterval = 0.06f;
        public float jamEjectSpeed = 3.5f;
        [TextArea(1, 3)] public string[] jamLines = { "Эй-эй-эй! Хватит!" };

        [Header("Broken for good")]
        [TextArea(1, 3)] public string[] breakLines = { "Старая рухлядь." };
        [Tooltip("Prompt over the dead machine; empty = it offers no E")]
        public string brokenPrompt = "Постучать по автомату";
        [TextArea(1, 3)] public string[] brokenLines = { "Не, всё. Умер." };
    }
}
