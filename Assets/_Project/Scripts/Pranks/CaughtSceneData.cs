using UnityEngine;

namespace Funseki.Pranks
{
    public enum CaughtSceneKind
    {
        /// <summary>Fade, the hero stands in the corridor holding two buckets.</summary>
        Buckets,
        /// <summary>The witness whistles and the hero runs laps around him, then the fade.</summary>
        Laps,
    }

    // One «Поймали» scene (GDD 5.4): what the punishment looks like and what it leaves behind. No game over:
    // the noise drops to 0 and the break goes on with timeToBell seconds left. Played by CaughtDirector.
    // Assets: Data/Pranks/CaughtScene_*.
    [CreateAssetMenu(fileName = "CaughtScene_", menuName = "Funseki/Pranks/Caught Scene")]
    public class CaughtSceneData : ScriptableObject
    {
        public CaughtSceneKind kind = CaughtSceneKind.Buckets;

        [Header("Lines (a random one)")]
        [Tooltip("The witness, at the moment of catching")]
        [TextArea(1, 2)] public string[] witnessLines = { "А ну стоять!" };
        [Tooltip("The hero during the punishment")]
        [TextArea(1, 2)] public string[] heroLines;

        [Header("Timing, s")]
        [Tooltip("From the catch to the punishment (the witness says his line)")]
        public float beforePunishment = 1.2f;
        public float fadeTime = 0.5f;
        [Tooltip("How long the punishment is shown")]
        public float punishmentTime = 5f;
        [Tooltip("The sign on black")]
        public string signText = "Через 10 минут…";
        public float signTime = 2f;

        [Header("Laps")]
        [Tooltip("m")]
        public float lapRadius = 3f;
        [Tooltip("m/s")]
        public float lapSpeed = 4.5f;
        [Tooltip("The witness's lines while the hero runs (whistles)")]
        [TextArea(1, 2)] public string[] lapWitnessLines = { "Фьють!", "Фьють-фьють!", "Шевелись!" };
        public float lapLineEvery = 1.2f;

        [Header("Buckets")]
        public Vector3 bucketSize = new(0.26f, 0.16f, 0.26f);
        public Color bucketColor = new(0.55f, 0.6f, 0.65f);

        [Header("After")]
        [Tooltip("The break bell rings this many seconds after the scene")]
        public float timeToBell = 60f;
    }
}
