using UnityEngine;

namespace Funseki.Core
{
    // Short speech bubble over a character's head that doesn't stop the game (GDD 5.3).
    // Implemented by Funseki.Dialogue.BarkService: ServiceLocator.Get<IBarkService>().Say(npc, "Привет").
    public interface IBarkService
    {
        void Say(GameObject speaker, string text);
    }
}
