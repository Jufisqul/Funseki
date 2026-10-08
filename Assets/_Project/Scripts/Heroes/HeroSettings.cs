using Funseki.Core;
using TMPro;
using UnityEngine;

namespace Funseki.Heroes
{
    // Tunables of hero switching (GDD 2.1), following and the switch window.
    // Assets/_Project/Data/Heroes/HeroSettings.asset.
    [CreateAssetMenu(fileName = "HeroSettings", menuName = "Funseki/Heroes/Hero Settings")]
    public class HeroSettings : ScriptableObject
    {
        [Header("Switching")]
        [Tooltip("Hero under control when the scene starts")]
        public HeroId startHero = HeroId.Ryuta;
        [Tooltip("Control and camera move to the chosen hero in this time, s")]
        public float switchTime = 0.5f;
        [Tooltip("On: Tab switches straight to the next hero (Рюта → Рэй → Кайто → Рюта). Off: Tab opens the switch window")]
        public bool tabCyclesHeroes = true;
        [Tooltip("WorldFlag that brings the other two heroes into the party (day 1: they join in room 7). Until it is set only startHero is played: the others stay where they stand, switching is closed and the HUD shows only startHero. Empty = all three from the start")]
        public string partyUnlockFlag = "heroes_unlocked";
        [Tooltip("Game states in which Tab switches heroes (closed in Lesson, Dialogue, Cutscene...)")]
        public GameState[] switchStates = { GameState.Break };
        [Tooltip("Game states in which Q works")]
        public GameState[] abilityStates = { GameState.Break };

        [Header("Following (NavMeshAgent)")]
        [Tooltip("Followers stop when they are this close to the leader, m")]
        public float followMinDistance = 1.5f;
        [Tooltip("Followers start walking when the leader is farther than this, m")]
        public float followMaxDistance = 3f;
        [Tooltip("How far behind the leader the follow spots are, m")]
        public float followBehind = 2f;
        [Tooltip("How far left / right of the leader's back the two follow spots are, m")]
        public float followSide = 0.9f;
        [Tooltip("Walking speed of followers, m/s")]
        public float followWalkSpeed = 1.8f;
        [Tooltip("Followers run to catch up when farther than this, m")]
        public float catchUpDistance = 5f;
        [Tooltip("Running speed of followers, m/s")]
        public float followRunSpeed = 4.8f;
        [Tooltip("A follower farther than this and out of the camera's view appears behind the leader, m")]
        public float teleportDistance = 25f;
        [Tooltip("A follower that wants to move but barely moves this long is unstuck (new path or teleport), s")]
        public float stuckTime = 1.5f;
        [Tooltip("Agent radius (also for the NavMesh bake), m")]
        public float agentRadius = 0.28f;
        [Tooltip("How fast an idle follower turns to face the leader, degrees/s")]
        public float idleTurnSpeed = 240f;

        [Header("NavMesh (baked at runtime from the loaded school)")]
        [Tooltip("Layers whose colliders form walkable ground and walls")]
        public LayerMask navMeshLayers = ~0;
        public float agentHeight = 1.7f;
        [Tooltip("Highest step followers walk up, m")]
        public float agentClimb = 0.35f;
        public float agentSlope = 50f;
        [Tooltip("The NavMesh is rebuilt this long after the hero interacts with something (a door opened), s; 0 = never")]
        public float rebuildAfterInteract = 1.2f;

        [Header("Switch window")]
        public TMP_FontAsset font;
        public string title = "Кто идёт?";
        [Tooltip("Hint at the bottom of the window")]
        public string hint = "1 / 2 / 3 — выбрать     Tab — закрыть";
        [Tooltip("Label on the card of the hero already in control")]
        public string currentLabel = "сейчас";
        public Color backgroundColor = new(0f, 0f, 0f, 0.65f);
        public Color cardColor = new(0.12f, 0.12f, 0.14f, 0.95f);
        public Color textColor = Color.white;
        [Tooltip("Card size in pixels at 1920x1080")]
        public Vector2 cardSize = new(300f, 400f);

        public bool CanSwitchIn(GameState state) => System.Array.IndexOf(switchStates, state) >= 0;
        public bool CanUseAbilityIn(GameState state) => System.Array.IndexOf(abilityStates, state) >= 0;
    }
}
