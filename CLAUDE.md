# One Funseki, Seven Days

## Игра

«One Funseki, Seven Days» — комедийное приключение от третьего лица про одну неделю в странной японской школе 2003 года. Играбельных героев трое: Рюта, Рэй и Кайто. День складывается из перемен-хабов, где можно свободно бродить, разговаривать и устраивать шалости, и уроков-мини-игр между ними. Мир реагирует на шалости: одноклассники, учителя и массовка это замечают и запоминают.

**Вертикальный срез — День 1:** катсцена → перемена → урок физры → перемена → конец демо. Двор и первый этаж школы сделаны серой коробкой. В срезе 5–6 интерактивных предметов, одна шалость, один одноклассник с диалогом и массовка NPC. Цель среза — плейтест «смешно ли без объяснений»: игрок должен понять шутки и шалость без подсказок и туториалов.

Unity 6000.6.4f1, URP. Пакеты: Input System, Cinemachine, Timeline, TextMeshPro (входит в uGUI), AI Navigation. Управление редактором идёт через мост unity-mcp (com.coplaydev.unity-mcp). Дизайн-документ (GDD v0.2) лежит в Notion владельца, пространство IndiGGG.

## Структура папок

```
Assets/_Project/
  Scripts/<Module>/   код, namespace Funseki.<Module>
  Data/               ScriptableObject с настройками и текстами (GameFlowConfig.asset и др.)
  Prefabs/
  Scenes/             Bootstrap, Slice_Day1
  Art/Placeholders/   серая коробка и временные ассеты
  Audio/
  Editor/             инструменты редактора (Tools > Funseki > Core)
  School/             школьный модульный кит и School_Greybox (свой тред, Funseki.School)
```

Модули: Core, Player, Interaction, Inventory, Heroes, Dialogue, DayCycle, NPC, Pranks, Lessons, UI, Save, Audio, Telemetry.
Каждый модуль — отдельная сборка `Funseki.<Module>.asmdef` в своей папке Scripts, она ссылается только на `Funseki.Core` (и при необходимости на Unity.InputSystem, Unity.TextMeshPro, Unity.Cinemachine и т. п.). Ссылок модуль → модуль нет. Asmdef создаётся вместе с первым скриптом модуля; сейчас сборки есть у Core, UI, Player, DayCycle, Interaction, Inventory, Dialogue, Audio, Heroes, NPC, Save, Pranks и Lessons.

Старое, пока не перенесённое:
- `Assets/Scenes/MainMenu.unity` + `Assets/MainMenu/` — главное меню, используется как сцена MainMenu («Новая игра» и «Продолжить» через ISaveSystem, см. «Меню, пауза, катсцена и конец демо»).
- `Assets/_Game/` — первый контроллер героя (заменён модулем Funseki.Player, в Slice_Day1 не используется), `GameControls.inputactions`, сцены PlayerTest и Day1_Greybox.
- Сторонние ассеты персонажей: `Anime Girl`, `3d-character_animeGirlAkane`, `RoloArt`, `Yukki`, `lilToon`.

## Герой (Funseki.Player)

- Ввод — `Assets/_Project/Data/Input/GameInput.inputactions`, карта `Gameplay` (клавиатура и геймпад). Move, Look, Sprint, Jump, FirstPerson работают; Interact, UseItem, AltUseItem, CycleItem, HeroMenu, HeroAbility, Pause только объявлены.
- Все числа движения и камеры — `Assets/_Project/Data/PlayerSettings.asset`, включая список состояний, где ввод героя выключен (Cutscene, Dialogue, Paused, Lesson).
- Компоненты на объекте Player: PlayerInputReader, PlayerMotor (CharacterController), PlayerAnimator (Speed, Grounded, Jump в `Assets/_Project/Art/Animation/Player.controller`), PlayerCameraController (CM ThirdPerson с Deoccluder, CM FirstPerson по удержанию F).
- `Scripts/DayCycle/CutscenePlaceholder` — старая заглушка катсцены, в сцене не стоит (катсцену играет `StoryCutscene`), файл можно удалить.

## Три героя (Funseki.Heroes)

- Данные в `Data/Heroes`: `HeroSettings.asset` (переключение 0,5 с, следование 1,5–3 м, догонялки, телепорт >25 м вне кадра, NavMesh, окно выбора), `Hero_Ryuta/Rei/Kaito` (имя, портрет, модель, цвет, Speaker, способность), `Abilities/Ability_*` (Q: «Голоса», «Телефон», «Пинок», кулдауны в них), `RumorSet_Day1` + `Rumors/Dialogue_Rumor_*` (звонки Рэй). Модели героев — варианты префабов в `Art/Heroes/Models` (Ryuto/Akane, Rey/Anime Girl, Kaito/Ren; MToon и сломанный lilToon заменены копиями URP Lit), портреты в `Art/Heroes/Portraits`.
- В Slice_Day1 объект `Heroes`: `HeroParty` (IHeroRoster; Tab — следующий герой каруселью Рюта → Рэй → Кайто; с выключенным `HeroSettings.tabCyclesHeroes` — старое окно выбора с 1/2/3, паузой и timeScale 0; Q — способность лидера) и `HeroNavMesh` (NavMesh печётся в рантайме из коллайдеров загруженных сцен, перепекается после E). Под ним `Hero_*`: компоненты Funseki.Player + `HeroUnit` (лидер на CharacterController, ведомые на NavMeshAgent без коллайдера), `HeroAbilityRunner`, `SpeakerTag`.
- Кто играет: `HeroService.Current` / `CurrentObject` / `IsInControl(hero)` в Core. Компоненты героя слушают ввод, только если `IsInControl`. `PlayerCameraController` стоит на Main Camera и переключается по `GameEvents.OnHeroSwitched`.
- События в Core: `OnHeroSwitched`, `OnHeroAbilityUsed`, `OnKicked(npc, hero)`, `OnNoiseMade(hero, witness, reason, amount)` (для будущей шкалы «Шума»). Карта ввода `HeroSelect` (Hero1–3, Close).
- NPC пока заглушка: `Funseki.NPC.NpcActor` (INpc: isTeacher, обзор из `Data/NPC/NpcSettings.asset`, пинок — Debug.Log). Тестовые NPC и предмет — объект `HeroesTest` на крыльце.
- Пересборка: `Tools > Funseki > Heroes > Build Slice_Day1 with the three heroes` (то же, что Build Slice_Day1); портреты — `Re-render portraits`.

## Взаимодействие и инвентарь (Funseki.Interaction, Funseki.Inventory)

- Общие типы лежат в Core, потому что модули не ссылаются друг на друга: `IInteractable` (Prompt, CanInteract, Interact), `IItemTarget` (`bool UseItem(item, primary)`, false = «Не сработает»), `ItemData` (ScriptableObject предмета).
- `HeroInteractor` на герое: ближайшая видимая цель в радиусе и конусе из `Data/Interaction/InteractionSettings.asset`, подсветка, подсказка «E — …», `GameEvents.OnInteracted`. Школьные `Door` и `LockedDoor` тоже `IInteractable`, в Slice_Day1 двери открываются через него. Старый `School/Scripts/PlayerInteractor` ещё ставит `Assets/Editor/Funseki/SchoolPlayerSetup` (тред школы); вместе с `HeroInteractor` его не ставить, оба читают E.
- Инвентарь общий на троих: сервис `Inventory` (ServiceLocator, `ToJson/LoadJson`), правила и тексты в `Data/Inventory/InventorySettings.asset`, предметы в `Data/Inventory/Items`. На герое `HeroItemUser` (колесо, ЛКМ/ПКМ) и `HeldItemView` (предмет в правой руке). `PickupItem` кладёт предмет и прячется. Реплики героя — `GameEvents.OnHeroBark`, показывает `Funseki.UI.BarkView`.
- Тестовая сцена `Scenes/Interaction_Test`: `Tools > Funseki > Interaction > Build Interaction_Test scene`, Play прямо из неё.

## Интерактивные предметы среза (Funseki.Interaction, GDD 5.9)

- База: `InteractableObject` (IInteractable) + `InteractableData` (подсказка, повторяемость Once / OncePerDay / Always, реплики первый раз / повтор по каждому герою, реплика при учителе рядом, флаги, «Шум»). Реплики идут через `IBarkService`. Состояние объекта хранится в WorldFlags под `obj_<objectId>_<имя>` и восстанавливается в Start, поэтому попадает в сейв вместе с миром.
- Предметы: `WaterTap` (кран, бурая вода при первом открытии), `VendingMachine` (банки из пула, заклинило / сломался, шансы в данных), `RyutaLocker` (дверца, CM-камера крупного плана, подписи `InspectDetail` по наведению), `DrawablePoster` (IItemTarget, маркер меняет текстуру, учитель видит → `OnNoiseMade`), `BookletRack` (UI-буклет со страницами). Данные в `Data/Interaction/Interactables`, плейсхолдеры в `Art/Placeholders/Interactables`.
- Шкафчик и буклет держат игру в состоянии Dialogue (ввод героя выключен, курсор свободен) и читают карту ввода `Inspect` (Esc / E / B, A / D / стрелки / d-pad, указатель).
- `GameEvents.OnObjectUsed(hero, obj, objectId, action)` — что сделал предмет (tap_opened, machine_broken, poster_drawn…).
- Расстановка: `Tools > Funseki > Interaction > Place slice interactables in Slice_Day1` (пересобирает только корень `Interactables` и маркер во дворе, данные не трогает).

## Сохранение (Funseki.Save)

- Автосейв GDD 5.7: `SaveService` под `[Bootstrap]` (ISaveSystem в Core) пишет все `ISaveable` из `SaveRegistry` в один JSON в persistentDataPath с версией формата (`SaveService.FormatVersion`, файлы новее не читаются), сценой и причиной. Части: `world` (WorldFlags, в них и состояния предметов), `inventory`, `journal` (сервисы Bootstrap), `day` (`DayCycleDirector`: фаза и её таймеры), `heroes` (`HeroParty`: кто играет, позиции героев).
- Когда пишет: начало каждой фазы дня, кроме уроков (начало дня, перемена после катсцены и после урока), перед уроком (`LessonDirector` → `OnAutosaveRequested`), после шалости (`OnPrankDone`). Сохраняет в конце кадра, в MainMenu и SliceEnd не пишет. Иконка — `Funseki.UI.SaveIndicator` (по `OnGameSaved`).
- «Продолжить»: `ISaveSystem.PrepareContinue` сразу грузит части Bootstrap, остальные ждут в `SaveRegistry` и отдаются владельцу при регистрации (в его Awake, до Start); `DayCycleDirector` тогда входит в сохранённую фазу вместо первой (если звонок уже был — звонит снова), `HeroParty` ставит героев. «Новая игра» — `ResetForNewGame` (флаги, инвентарь, журнал как при запуске).
- Debug: F5 — сохранить, F6 — «Продолжить» из сейва (`Data/Save/SaveSettings.asset`, там же выключатель автосейва).
- `Tools > Funseki > Save > Add inventory and save services to Bootstrap` — ставит `InventoryService` + `InventoryPanel` и `SaveService` под `[Bootstrap]`.

## Меню, пауза, катсцена и конец демо

- Главное меню — старая сцена `MainMenu` (`MainMenuController`): «Новая игра» сбрасывает прогресс и грузит Slice_Day1, «Продолжить» серая без сейва и ведёт к последнему автосейву.
- Под `[Bootstrap]/Menus` (Funseki.UI, uGUI + TMP, вид из `HudTheme`, тексты в `Data/UI/MenuSettings.asset`): `PauseMenu` (Esc / Start — копия `Gameplay/Pause`; в состояниях из `pausableStates`; timeScale 0 и пауза звука; «Журнал недели», «Настройки», «В главное меню» без сохранения, «Выход»), `SaveIndicator`, `SliceEndView` (в состоянии SliceEnd: исход физры по флагам `fizra_day1_result_*`, записи журнала X из `endJournalTotal`, «Анкета» по `surveyUrl` — пустая ссылка прячет кнопку).
- Настройки игрока — `Core/PlayerOptions` (PlayerPrefs): громкость (AudioListener), множитель мыши (читает `PlayerInputReader`), скорость текста (читает `DialogueRunner`), оконный / полноэкранный (тот же ключ, что у меню).
- Катсцена «Приезд в Фунсэки»: `Funseki.DayCycle.StoryCutscene` (корень `Cutscene_Intro` в Slice_Day1) играет на старте фазы `intro`: Timeline `Data/Cutscenes/Cutscene_Day1_Intro.playable` (трио у ворот-заглушки → пролёт над двором → титр «День 1» → трио на крыльце), на `dialogueAt` Timeline ждёт диалог `Data/Dialogue/Cutscene_Day1_Intro` (реплики «[TODO]»), затем затемнение, флаг `day1_intro_done` и перемена — герои уже в холле за главными дверями. Пропуск — удержание `Cutscene/Skip` (Пробел / A) 1 с, кроме диалога. Настройки — `Data/Cutscenes/Cutscene_Day1_Intro.asset`. Герои в катсцене — куклы-модели, не игровые объекты.
- Сборка: `Tools > Funseki > Slice > Setup slice flow (menus, autosave, intro cutscene)` (данные и Timeline создаёт один раз, `Menus` и `Cutscene_Intro` пересобирает, треки Timeline привязывает по имени).

## Диалоги и barks (Funseki.Dialogue)

- Данные в `Data/Dialogue`: `DialogueSettings.asset` (скорости, камера, вид окна, тексты кнопок, bark-пузыри), `Speakers/Speaker_*` (имя, портрет, цвет, звуки «бормотания»), `Dialogue_*` (DialogueGraph), `BarkSet_*`.
- DialogueGraph — список узлов сверху вниз: реплика + до 3 выборов и 1 «Голос» (виден только за Кайто), условия по WorldFlags и герою, действия (флаг, счётчик, выдать предмет через `GameEvents.OnItemGiven`). Переходы по id в поле `next`, пустое — следующий узел.
- Сервисы — дети `[Bootstrap]`: `DialogueRunner` (карта ввода `Dialogue`, состояние Dialogue, камера CM Dialogue) и `BarkService` (`IBarkService` в Core; показывает и `OnHeroBark` пузырём над героем, `Funseki.UI.BarkView` остаётся запасным вариантом без сервиса).
- Текущий герой — `HeroService.Current` (Core); в сценах без трёх героев берётся `DialogueSettings.fallbackHero` (Кайто). Звонок Рэй идёт через `IDialogueService` (DialogueRunner).
- На NPC: `DialogueNpc` (E — поговорить), `BarkTrigger` (bark при приближении), `SpeakerTag`.
- Тестовая сцена: `Tools > Funseki > Dialogue > Build Dialogue_Test scene`, Play прямо из неё.

## День и звонок (Funseki.DayCycle)

- Расписание дня — `Data/DayCycle/DaySchedule_*` (DaySchedule): фазы сверху вниз (тип, состояние игры, иконка времени суток, lessonId, цель для HUD, условие конца: флаг цели и/или максимум секунд), состояние после последней фазы, правила звонка (опоздание 90 с → флаг `lesson_late`, второй звонок и стрелка через 60 с). `DayPhase`, `IDayCycle` и события `OnPhaseStarted/OnPhaseEnded/OnBell/OnObjectiveChanged/OnLessonHint/OnDayEnded` лежат в Core.
- В сцене дня: `DayCycleDirector` (объект DayCycle, регистрируется как `IDayCycle`, `ToJson/LoadJson` для сейва) и `LessonEntrance` (триггер, урок начинается, когда герой входит после звонка). Урок или катсцена завершают фазу флагом цели или `IDayCycle.CompletePhase()`.
- Физра дня 1 — в спортзале (`LessonEntrance_fizra` у восточной двери zone gym). Спортзал по SchoolLayout открыт со 2-го дня; в день 1 его открывает `Funseki.Lessons.LessonWalk` по звонку после break_1. Урок идёт без лимита времени, фазу завершает Funseki.Lessons флагом `lesson_fizra_done`.
- Debug в редакторе: F9 — следующая фаза, F10 — выставить флаг цели текущей фазы.
- Звук звонка: `Funseki.Audio.BellSoundPlayer` (объект BellSound под `[Bootstrap]`) слушает `OnBell`, клипы и громкость по типу звонка в `Data/Audio/BellSettings.asset` (сейчас везде `Assets/Sound/BellRing.mp3`). Ставится через `Tools > Funseki > Audio > Add bell sound to Bootstrap`.
- `Tools > Funseki > DayCycle > Setup Day 1 schedule in Slice_Day1` — создаёт расписание (существующее не трогает) и объекты в Slice_Day1; Build Slice_Day1 делает то же.

## Шалости, Шум и «Поймали» (Funseki.Pranks)

- Общие типы в Core (`Core/Pranks.cs`): `IPrank`, `NoiseStage`, `INoiseMeter`, `IKickable`, `JournalEntry`, `IWeekJournal`. События: `OnPrankDone(prank, position)`, `OnNoiseChanged(value, stage)`, `OnCaught(hero, witness, scene)`, `OnCaughtEnded(hero)`, `OnJournalEntryAdded`. `IDayCycle.SetTimeToBell(s)` — «до звонка 60 с».
- `PrankData` (`Data/Pranks/...`): журнал (название, подпись), условия (герои, предмет, фазы, флаги), Шум при свидетеле или сразу «Поймали», флаги последствий и флаги после реакции, награда-предмет, Timeline реакции. `PrankTrigger` на приманке (E или предмет ЛКМ), `SetAvailable` прячет/показывает её.
- Шум: `NoiseMeter` (INoiseMeter, в сцене) растёт только от `OnNoiseMade` (величины по причинам в `NoiseSettings.asset`: kick 15, poster_drawn 25, prank — из PrankData), падает 5/с, когда учитель не видит героя, ×3 в `HideZone` (туалеты, у шкафчиков для обуви). На 100 — `OnCaught`. Свидетели — `INpc.IsTeacher` с `CanSee`.
- «Поймали»: `CaughtDirector` (в сцене) по `CaughtScene_*`: Buckets (затемнение, коридор с вёдрами, `BucketSpot`) или Laps (круги вокруг свистящего свидетеля), табличка «Через 10 минут…», Шум 0, до звонка 60 с. Ввод героя в Caught выключен (PlayerSettings).
- «Журнал недели»: `WeekJournal` под `[Bootstrap]`, сохраняется в сейв ключом `journal`. UI — задача 12.
- Timeline: свои треки `BarkTrack` (реплика персонажа) и `WiggleTrack` (плейсхолдерная анимация Body NPC).
- Шалость среза «Украсть свисток»: `WhistleTeacher` (физрук, `Data/Pranks/Whistle/WhistleRoutine.asset`) на break_1 каждые 90 с идёт к автомату во дворе, кладёт свисток на скамейку и 20 с воюет с автоматом, поглядывая на скамейку. Пинок Кайто по автомату (`KickTarget`, KickAbility теперь бьёт и `IKickable`) — физрук отворачивается на 5 с. Видит кражу — «Поймали» с кругами. Успех: `prank_whistle_done`, свисток в инвентарь, запись «Свистать всех наверх», Timeline реакции 10 с, потом `day1_break1_goal`. Ученики во дворе обсуждают (`BarkSet_WhistleGossip`).
- Сборка: `Tools > Funseki > Pranks > Setup pranks in Slice_Day1` (пересобирает корень `Pranks`, данные не трогает, ставит WeekJournal в Bootstrap).

## Уроки (Funseki.Lessons)

- Каркас: `LessonData` (`Data/Lessons/Lesson_<id>.asset`: id, название, ведущий герой, префаб мини-игры, интро и интро при опоздании, пороги исходов, сцены исходов с условиями по флагам). `LessonDirector` (объект `Lessons` в Slice_Day1) на старте фазы Lesson: автосейв (`GameEvents.RaiseAutosaveRequested`, его слушает SaveService) → префаб на `LessonStage` (`Stage_fizra` в спортзале, места героев, прячет физрука и учеников перемены) → интро (при `lesson_late` — гэг) → мини-игра → сцена исхода → флаги → флаг цели фазы → звонок на перемену.
- Флаги результата: `<lessonId>_day<N>_result` (урок пройден), `..._result_excellent|normal|shame`, счётчик `..._result` (3/2/1). Последствия: `fizra_teacher_angry` (Позорно), `fizra_day1_voice_heard` (Блестяще без свистка). События `OnLessonStarted`, `OnLessonFinished(lessonId, outcome)`.
- Мини-игра — наследник `LessonMiniGame` (Setup → Play → Finish(score) → PlayGag → Cleanup). Ввод — карта `Lesson` (Jump, StepUp/Down/Left/Right, Sit, Catch; клавиатура и геймпад).
- «Физра: Свисток» (`Scripts/Lessons/Fizra`, данные в `Data/Lessons/Fizra`): `WhistleCommand` (5 свистков + чайник), `FizraRound` (3 раунда + `FizraRound_Exam3in10` для «Контрольной»), `FizraSettings` (режим «без свистка» по `prank_whistle_done`, Шутник переводит с ошибкой 20%, реплики, анимация-заглушка, звуки). `WhistleRoundRunner` — раунд без сцены, переиспользуется. Звуки свистков пока синтезируются (`WhistleSynth`), клипы можно положить в WhistleCommand.
- Переход на урок: `LessonWalk` (`Walk_fizra`, данные `Data/Lessons/LessonWalk_Fizra.asset`) после звонка break_1 ведёт физрука перемены и трёх учеников двора по точкам маршрута в спортзал (точки `Door_*` — двери школы, их LessonWalk отпирает и открывает; WhistleTeacher на время выключен). После урока они пешком возвращаются по маршруту на свои места во дворе, а ученики из префаба урока выходят из зала в коридор и исчезают (`LessonMiniGame.Leavers`).
- Debug в редакторе: F7 — выиграть раунд, F8 — проиграть.
- Сборка: `Tools > Funseki > Lessons > Setup Fizra lesson in Slice_Day1` (данные создаёт один раз, префаб `Prefabs/Lessons/Lesson_Fizra` и корень `Lessons` пересобирает).

## HUD (Funseki.UI)

- uGUI + TextMeshPro (как весь остальной UI проекта), строится кодом. `HudView` на объекте `HUD` в Slice_Day1. Вид — `Data/UI/HudTheme.asset` (шрифты, цвета, размеры, иконки; пустой спрайт = плейсхолдер из `HudArt`), поведение и тексты — `Data/UI/HudSettings.asset` (в каких состояниях виден: Break; Paused не меняет; подсказки первого раза).
- Углы: слева сверху иконка времени суток + «День N» + цель (OnPhaseStarted, OnObjectiveChanged); справа сверху мегафон «Шума» (OnNoiseChanged) и под ним «Новая запись в журнале» (OnPrankDone); слева снизу «Партия» с кулдауном Q (читает `IHeroStatus` в Core, его реализует `HeroAbilityRunner`); снизу по центру над инвентарём подсказки управления. Центр экрана свободен.
- Подсказки: триггеры BreakTime / InteractionTarget / ItemAdded / HeroSwitched, показанные запоминаются флагом `hud_hint_<id>`. Подписи по последнему устройству ввода: по умолчанию клавиатура и мышь, на геймпад переключаются только после нажатия кнопки или движения стика.
- `GameStateDebugLabel` скрыт, пока есть HUD; F1 показывает его.
- Сборка: `Tools > Funseki > UI > Setup HUD in Slice_Day1` (данные создаёт один раз, корень `HUD` пересобирает).

## Ядро (Funseki.Core)

- **Сцены.** `Bootstrap` (индекс 0 в Build Settings) создаёт сервисы, живёт всю игру (DontDestroyOnLoad) и открывает `GameFlowConfig.firstScene` (MainMenu). Дальше `MainMenu` → `Slice_Day1`.
- **Запуск.** Play всегда из Bootstrap: `Tools > Funseki > Core > Play From Bootstrap` (открытая сцена в редакторе не меняется). При Play напрямую из другой сцены сервисов не будет.
- **ServiceLocator.** `ServiceLocator.Register/Get/TryGet<T>()` для сервисов из Bootstrap. Сейчас зарегистрированы `GameStateMachine`, `WorldFlags`, `GameFlowConfig`. Сервисы других модулей — дочерние объекты `[Bootstrap]`, регистрируются в Awake.
- **GameEvents.** Статический класс с C#-событиями — единственная связь между модулями. Новые события добавляются туда же, сгруппированные по модулю, с методом `RaiseXxx`. Подписка в OnEnable, отписка в OnDisable.
- **WorldFlags.** Строковые флаги (`prank_whistle_done`) и int-счётчики. Id — snake_case на английском. `ToJson()/LoadJson()` для сейва. Изменения шлют `OnWorldFlagChanged` / `OnWorldCounterChanged`.
- **GameFlowConfig** (`Assets/_Project/Data/GameFlowConfig.asset`) — первая сцена и стартовое состояние каждой сцены.

### Состояния игры (GameStateMachine)

| Состояние | Когда |
|---|---|
| MainMenu | главное меню |
| Cutscene | катсцена (Timeline), управление у режиссёра |
| Break | перемена: свободное перемещение, предметы, шалости |
| Lesson | урок-мини-игра |
| Dialogue | разговор с NPC |
| Paused | пауза; `Resume()` возвращает предыдущее состояние |
| Caught | героя поймали на шалости |
| SliceEnd | конец демо |

Состояние меняет только `GameStateMachine.ChangeState()`, каждое изменение шлёт `GameEvents.OnGameStateChanged(previous, current)`. Стартовое состояние сцены берётся из GameFlowConfig при её загрузке.

## Правила

- Все настраиваемые числа и тексты — в ScriptableObject в `Assets/_Project/Data`, а не в коде: геймдизайнер правит их без программиста.
- Тексты игры на русском; идентификаторы, код и комментарии — на английском.
- Модули связаны через `GameEvents` (и сервисы через `ServiceLocator`), а не через `FindObjectOfType` / `FindFirstObjectByType` или прямые ссылки между модулями.
- Ввод — только через Input System, раскладка геймпада наравне с клавиатурой в каждом action map.
- Не добавлять пакеты, не удалять файлы и не менять Project Settings без вопроса.
- Папки `Assets/_Project/School` и `Assets/Editor/Funseki` ведёт другой тред (школьный кит); правки там — только с согласования.
- В конце каждой задачи: список созданных и изменённых файлов и что проверить руками.

## Проверка

- `Tools > Funseki > Core > Smoke Test Flow` — Play из Bootstrap, ждёт MainMenu, жмёт «Новая игра», ждёт Slice_Day1 в состоянии Cutscene. Результат в консоли: `[CoreFlowSmokeTest] PASS/FAIL`.
- `Tools > Funseki > Core > Create Core Scenes` — пересоздаёт недостающие сцены ядра, GameFlowConfig и порядок Build Settings (существующие файлы не трогает).
- `Tools > Funseki > Slice > Build Slice_Day1 (player + school loader)` — пересобирает в Slice_Day1 трёх героев (Funseki.Player + Funseki.Heroes), камеры Cinemachine, SchoolLoader и HeroBarks (реплики героя). На каждом герое `HeroInteractor`, `HeroItemUser`, `HeldItemView`. Анимации — `Assets/Models/Animation` (Idle, Walk, Run, Jumping Up, Talking, Getting Hit; новые Generic-файлы переводятся в Humanoid). Школа не копируется: SchoolLoader при старте подгружает School_Greybox, выключает в ней всё, кроме корня School, и открывает только дверь главного входа. School_Greybox стоит в Build Settings последней.
