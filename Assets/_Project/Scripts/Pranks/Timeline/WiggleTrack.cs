using UnityEngine;
using UnityEngine.Timeline;

namespace Funseki.Pranks
{
    public enum WiggleMode
    {
        /// <summary>Trembles on the spot (panic).</summary>
        Shake,
        /// <summary>Hops up and down.</summary>
        Hop,
        /// <summary>Turns left and right (looking around).</summary>
        LookAround,
        /// <summary>Spins on the spot.</summary>
        Spin,
    }

    // Placeholder animation for grey-box characters in a Timeline: bind a Transform (an NPC's Body), each clip moves it
    // around its rest pose and puts it back when the clip ends.
    [TrackColor(0.4f, 0.8f, 1f)]
    [TrackClipType(typeof(WiggleClip))]
    [TrackBindingType(typeof(Transform))]
    public class WiggleTrack : TrackAsset { }
}
