using Funseki.Core;
using UnityEngine;

namespace Funseki.Heroes
{
    // One playable hero (GDD 2.1): name, portrait for the switch window, model prefab, color, dialogue speaker
    // and the unique action on Q. Assets/_Project/Data/Heroes/Hero_*.asset.
    [CreateAssetMenu(fileName = "Hero_", menuName = "Funseki/Heroes/Hero")]
    public class HeroData : ScriptableObject
    {
        public HeroId id;
        [Tooltip("Full name, shown in the switch window")]
        public string displayName;
        [Tooltip("Short name for barks and hints")]
        public string shortName;
        [Tooltip("Portrait in the switch window; empty = a colored card with the first letter")]
        public Sprite portrait;
        [Tooltip("Model with a humanoid Animator; the hero builder puts it under the hero object")]
        public GameObject prefab;
        [Tooltip("Card frame and accents in the switch window")]
        public Color color = Color.white;
        [Tooltip("Funseki.Dialogue Speaker of this hero: name, portrait and mumble in dialogues and barks")]
        public ScriptableObject speaker;
        [Tooltip("The unique action on Q (a HeroAbility asset)")]
        public HeroAbility ability;
    }
}
