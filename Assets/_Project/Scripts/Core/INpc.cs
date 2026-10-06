using UnityEngine;

namespace Funseki.Core
{
    // A non-playable character the heroes can affect (Кайто's kick) and that can witness pranks.
    // Implemented by Funseki.NPC.NpcActor; found through colliders with GetComponentInParent<INpc>().
    public interface INpc
    {
        bool IsTeacher { get; }
        /// <summary>Can the character see this world point right now (view distance, cone, walls)?</summary>
        bool CanSee(Vector3 point);
    }

    // Something lying in the world that can be picked up (Funseki.Inventory.PickupItem). Рюта's «Голоса» look for these.
    public interface IPickup
    {
        ItemData Item { get; }
        /// <summary>Рюта's hint line about this spot ("Там, за автоматом…"); empty = a generic line.</summary>
        string VoiceHint { get; }
    }

    // Plays a conversation (Funseki.Dialogue.DialogueRunner). graph is a DialogueGraph asset; npc may be null
    // (a phone call: the camera frames the hero alone).
    public interface IDialogueService
    {
        bool IsRunning { get; }
        bool StartDialogue(ScriptableObject graph, GameObject npc, GameObject hero);
    }
}
