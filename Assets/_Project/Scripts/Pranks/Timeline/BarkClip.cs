using Funseki.Core;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Funseki.Pranks
{
    // A line in a BarkTrack: says its text once when the playhead enters it.
    public class BarkClip : PlayableAsset, ITimelineClipAsset
    {
        [TextArea(1, 3)] public string text;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<BarkBehaviour>.Create(graph);
            playable.GetBehaviour().text = text;
            return playable;
        }
    }

    public class BarkBehaviour : PlayableBehaviour
    {
        public string text;
        bool said;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (said || !Application.isPlaying || string.IsNullOrEmpty(text)) return;
            if (playerData is not GameObject speaker || speaker == null) return;
            said = true;
            if (ServiceLocator.TryGet<IBarkService>(out var barks)) barks.Say(speaker, text);
            else GameEvents.RaiseHeroBark(speaker, text);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info) => said = false;
    }
}
