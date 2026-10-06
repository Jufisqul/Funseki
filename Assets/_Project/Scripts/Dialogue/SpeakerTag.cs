using UnityEngine;

namespace Funseki.Dialogue
{
    // Put on a character's root: who it is for barks (mumble sound) and where its head is (bubble position).
    public class SpeakerTag : MonoBehaviour
    {
        public Speaker speaker;
        [Tooltip("Head top above the object's origin, m; 0 = take it from the renderers' bounds")]
        public float headHeight;
    }
}
