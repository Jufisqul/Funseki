using System.Collections;
using Funseki.Core;
using UnityEngine;

namespace Funseki.DayCycle
{
    // TEMPORARY: Slice_Day1 starts in Cutscene, but the opening cutscene doesn't exist yet.
    // Hands control to the player by switching to Break once the scene has loaded.
    // Delete this object when the real Timeline cutscene is in the scene.
    public class CutscenePlaceholder : MonoBehaviour
    {
        IEnumerator Start()
        {
            if (!ServiceLocator.TryGet<GameStateMachine>(out var fsm)) yield break;
            // Wait a moment so listeners (and the core smoke test) see the Cutscene state first.
            yield return new WaitForSecondsRealtime(0.5f);
            if (fsm.Current == GameState.Cutscene)
            {
                Debug.Log("[CutscenePlaceholder] No opening cutscene yet, starting the break.");
                fsm.ChangeState(GameState.Break);
            }
        }
    }
}
