using System.Collections.Generic;
using Funseki.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Funseki.Heroes
{
    // The three heroes of the day scene (GDD 2.1). Registered as IHeroRoster (read it via HeroService).
    // Tab (Gameplay/HeroMenu) switches to the next hero (HeroSettings.tabCyclesHeroes), or opens the switch window
    // that pauses the game; there 1 / 2 / 3 (HeroSelect map) or a click
    // picks a hero. Control and camera then move to that hero in HeroSettings.switchTime and the other two follow.
    // Q (Gameplay/HeroAbility) fires the leader's unique action.
    // Until HeroSettings.partyUnlockFlag is set only startHero is in the party: the other two stand where they were put
    // (HeroUnit.MakeIdle) and switching is closed; when the flag is set they join and follow.
    // Saved under "heroes" (ISaveable): the hero in control and where each hero stands.
    [DefaultExecutionOrder(-800)]
    public class HeroParty : MonoBehaviour, IHeroRoster, ISaveable
    {
        [System.Serializable]
        class SavedHero { public int id; public Vector3 position; public Quaternion rotation; }

        [System.Serializable]
        class SavedParty { public int current; public List<SavedHero> heroes = new(); }

        public string SaveKey => "heroes";
        SavedParty restoredParty;

        public const string GameplayMap = "Gameplay";
        public const string SelectMap = "HeroSelect";

        [SerializeField] HeroSettings settings;
        [SerializeField] InputActionAsset actions;
        [Tooltip("In the order of the keys 1 / 2 / 3")]
        [SerializeField] HeroUnit[] heroes;

        public HeroId Current => leader != null ? leader.Data.id : settings.startHero;
        public GameObject CurrentObject => leader != null ? leader.gameObject : null;
        public bool IsSwitching => Time.time < switchEndsAt;
        public HeroUnit Leader => leader;

        HeroUnit leader;
        float switchEndsAt = -1f;
        HeroSelectView view;
        InputActionMap gameplay, select;
        InputAction menuAction, abilityAction, closeAction;
        readonly InputAction[] pickActions = new InputAction[3];
        float timeScaleBefore = 1f;
        GameObject ownEventSystem;
        bool owner;

        void Awake()
        {
            if (ServiceLocator.IsRegistered<IHeroRoster>())
            {
                Debug.LogWarning("[HeroParty] Another hero roster is already registered; this one is ignored.");
                enabled = false;
                return;
            }
            owner = true;
            ServiceLocator.Register<IHeroRoster>(this);

            gameplay = actions.FindActionMap(GameplayMap, true);
            menuAction = gameplay.FindAction("HeroMenu", true);
            abilityAction = gameplay.FindAction("HeroAbility", true);
            select = actions.FindActionMap(SelectMap, true);
            for (int i = 0; i < pickActions.Length; i++) pickActions[i] = select.FindAction($"Hero{i + 1}", true);
            closeAction = select.FindAction("Close", true);

            var data = new List<HeroData>();
            foreach (var h in heroes) data.Add(h != null ? h.Data : null);
            view = new HeroSelectView(settings, data, transform);
            view.CardClicked += Pick;
            // A pending saved party (Continue) arrives right here, before Start.
            SaveRegistry.Register(this);
        }

        void Start()
        {
            var startId = settings.startHero;
            if (restoredParty != null)
            {
                foreach (var saved in restoredParty.heroes)
                {
                    var unit = Find((HeroId)saved.id);
                    if (unit != null) unit.PlaceAt(saved.position, saved.rotation);
                }
                startId = (HeroId)restoredParty.current;
                restoredParty = null;
            }
            var first = Find(startId);
            if (first == null || !IsInParty(first.Data.id)) first = Find(settings.startHero) ?? (heroes.Length > 0 ? heroes[0] : null);
            if (first != null) SetLeader(first, 0f);
        }

        // ---------------------------------------------------------------- save

        public string ToJson()
        {
            var party = new SavedParty { current = (int)Current };
            foreach (var h in heroes)
                if (h != null && h.Data != null)
                    party.heroes.Add(new SavedHero { id = (int)h.Data.id, position = h.transform.position, rotation = h.transform.rotation });
            return JsonUtility.ToJson(party);
        }

        // Before Start (Continue) the party waits for Start; later (a save loaded in place) it is applied at once.
        public void LoadJson(string json)
        {
            var party = JsonUtility.FromJson<SavedParty>(json);
            if (party == null) return;
            if (leader == null) { restoredParty = party; return; }
            restoredParty = party;
            Start();
        }

        void OnEnable()
        {
            gameplay?.Enable();
            GameEvents.OnWorldFlagChanged += OnWorldFlagChanged;
        }

        void OnDisable()
        {
            GameEvents.OnWorldFlagChanged -= OnWorldFlagChanged;
            if (view != null && view.IsOpen) CloseMenu();
        }

        void OnDestroy()
        {
            if (!owner) return;
            SaveRegistry.Unregister(this);
            if (ServiceLocator.TryGet<IHeroRoster>(out var r) && ReferenceEquals(r, this)) ServiceLocator.Unregister<IHeroRoster>();
            select?.Disable();
            view?.Destroy();
            if (ownEventSystem != null) Destroy(ownEventSystem);
        }

        void Update()
        {
            if (view.IsOpen)
            {
                for (int i = 0; i < pickActions.Length; i++)
                    if (pickActions[i].WasPressedThisFrame()) { Pick(i); return; }
                if (menuAction.WasPressedThisFrame() || closeAction.WasPressedThisFrame()) CloseMenu();
                return;
            }

            var state = ServiceLocator.TryGet<GameStateMachine>(out var fsm) ? fsm.Current : GameState.Break;
            if (menuAction.WasPressedThisFrame() && settings.CanSwitchIn(state) && !IsSwitching && PartySize > 1)
            {
                if (settings.tabCyclesHeroes) CycleNext();
                else OpenMenu();
            }
            else if (abilityAction.WasPressedThisFrame() && settings.CanUseAbilityIn(state) && !IsSwitching && leader != null)
                leader.GetComponent<HeroAbilityRunner>()?.TryUse();
        }

        // ---------------------------------------------------------------- menu

        void OpenMenu()
        {
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)) fsm.Pause();
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            EnsureEventSystem();
            select.Enable();
            view.Show(System.Array.IndexOf(heroes, leader));
        }

        void CloseMenu()
        {
            view.Hide();
            select.Disable();
            Time.timeScale = timeScaleBefore;
            if (ServiceLocator.TryGet<GameStateMachine>(out var fsm)) fsm.Resume();
        }

        void Pick(int index)
        {
            if (!view.IsOpen) return;
            CloseMenu();
            if (index < 0 || index >= heroes.Length || heroes[index] == null || heroes[index] == leader) return;
            if (!IsInParty(heroes[index].Data.id)) return;
            SetLeader(heroes[index], settings.switchTime);
        }

        // Tab without the window: control goes to the next hero in the 1 / 2 / 3 order, round and round.
        void CycleNext()
        {
            int start = System.Array.IndexOf(heroes, leader);
            for (int step = 1; step <= heroes.Length; step++)
            {
                var next = heroes[(start + step + heroes.Length) % heroes.Length];
                if (next == null || next == leader || !IsInParty(next.Data.id)) continue;
                SetLeader(next, settings.switchTime);
                return;
            }
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null || ownEventSystem != null) return;
            ownEventSystem = new GameObject("EventSystem (Heroes)", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownEventSystem.transform.SetParent(transform, false);
        }

        // ---------------------------------------------------------------- switching

        public bool IsInParty(HeroId id)
        {
            if (string.IsNullOrEmpty(settings.partyUnlockFlag) || id == settings.startHero) return true;
            return ServiceLocator.TryGet<WorldFlags>(out var flags) && flags.GetFlag(settings.partyUnlockFlag);
        }

        int PartySize
        {
            get
            {
                int n = 0;
                foreach (var h in heroes)
                    if (h != null && h.Data != null && IsInParty(h.Data.id)) n++;
                return n;
            }
        }

        // The other two join (or leave, on a new game) when the unlock flag changes.
        void OnWorldFlagChanged(string id, bool value)
        {
            if (leader == null || id != settings.partyUnlockFlag) return;
            Debug.Log($"[HeroParty] {(value ? "Рэй и Кайто в группе" : "В группе только " + settings.startHero)}.");
            if (!IsInParty(leader.Data.id)) { SetLeader(Find(settings.startHero) ?? leader, 0f); return; }
            PlaceOthers(leader);
        }

        public GameObject GetHero(HeroId id)
        {
            var h = Find(id);
            return h != null ? h.gameObject : null;
        }

        HeroUnit Find(HeroId id)
        {
            foreach (var h in heroes)
                if (h != null && h.Data != null && h.Data.id == id) return h;
            return null;
        }

        void SetLeader(HeroUnit next, float time)
        {
            var previous = leader;
            leader = next;
            next.MakeLeader();
            PlaceOthers(next);
            switchEndsAt = Time.time + time;
            GameEvents.RaiseHeroSwitched(previous != null ? previous.gameObject : null, next.gameObject, time);
        }

        // Party members follow the leader; heroes not in the party yet stay where they stand.
        void PlaceOthers(HeroUnit lead)
        {
            int slot = 0;
            foreach (var h in heroes)
            {
                if (h == null || h == lead) continue;
                if (IsInParty(h.Data.id)) h.MakeFollower(lead, slot++);
                else h.MakeIdle();
            }
        }
    }
}
