using System;
using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;

namespace Funseki.Pranks
{
    // Tunables of the «Шум» meter (GDD 5.4). Data/Pranks/NoiseSettings.asset.
    [CreateAssetMenu(fileName = "NoiseSettings", menuName = "Funseki/Pranks/Noise Settings")]
    public class NoiseSettings : ScriptableObject
    {
        [Serializable]
        public class ReasonAmount
        {
            [Tooltip("Reason id from GameEvents.OnNoiseMade: kick, poster_drawn, prank")]
            public string reason;
            [Min(0)] public int amount;
        }

        [Header("Stages")]
        public float max = 100f;
        [Tooltip("From here on: «Подозрительно»")]
        public float suspiciousAt = 40f;

        [Header("Growth (only from what a watcher sees)")]
        [Tooltip("Noise for each reason; a reason not in the list adds the amount its source sends")]
        public List<ReasonAmount> amounts = new()
        {
            new ReasonAmount { reason = "kick", amount = 15 },
            new ReasonAmount { reason = "poster_drawn", amount = 25 },
        };

        [Header("Decay")]
        [Tooltip("Per second while no watcher sees the hero")]
        public float decayPerSecond = 5f;
        [Tooltip("Decay is this many times faster in a hiding zone («Укрытие»)")]
        public float hideZoneMultiplier = 3f;
        [Tooltip("No decay for this long after the last noise, s")]
        public float decayDelay = 1f;
        [Tooltip("Game states in which the meter works")]
        public GameState[] activeStates = { GameState.Break };

        [Header("Watchers")]
        [Tooltip("Teachers farther than this are not checked, m")]
        public float watcherSearchRadius = 30f;
        [Tooltip("s")]
        public float visibilityCheckInterval = 0.2f;
        [Tooltip("The point a watcher must see on the hero, m above the feet")]
        public float heroChestHeight = 1.2f;

        [Header("«Поймали»")]
        [Tooltip("The scene when the meter fills up")]
        public CaughtSceneData defaultCaughtScene;

        public int AmountFor(string reason, int sent)
        {
            foreach (var a in amounts)
                if (a != null && a.reason == reason) return a.amount;
            return sent;
        }

        public NoiseStage StageOf(float value) =>
            value >= max ? NoiseStage.Caught : value >= suspiciousAt ? NoiseStage.Suspicious : NoiseStage.Quiet;

        public bool IsActive(GameState state) => Array.IndexOf(activeStates, state) >= 0;
    }
}
