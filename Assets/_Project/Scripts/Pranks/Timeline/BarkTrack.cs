using UnityEngine;
using UnityEngine.Timeline;

namespace Funseki.Pranks
{
    // Timeline track of speech bubbles: bind a character's GameObject, each clip says its text once (IBarkService)
    // when the playhead enters it. For world reaction Timelines (GDD 5.4).
    [TrackColor(1f, 0.8f, 0.2f)]
    [TrackClipType(typeof(BarkClip))]
    [TrackBindingType(typeof(GameObject))]
    public class BarkTrack : TrackAsset { }
}
