# One Funseki, Seven Days

## Игра

«One Funseki, Seven Days» — комедийное приключение от третьего лица про одну неделю в странной японской школе 2003 года. Играбельных героев трое: Рюта, Рэй и Кайто. День складывается из перемен-хабов, где можно свободно бродить, разговаривать и устраивать шалости, и уроков-мини-игр между ними. Мир реагирует на шалости: одноклассники, учителя и массовка это замечают и запоминают.

**Вертикальный срез — День 1 (старый, то, что сейчас в коде):** катсцена → перемена → урок физры → перемена → конец демо. Двор и первый этаж школы сделаны серой коробкой. В срезе 5–6 интерактивных предметов, одна шалость, один одноклассник с диалогом и массовка NPC. Цель среза — плейтест «смешно ли без объяснений»: игрок должен понять шутки и шалость без подсказок и туториалов. С 2026-10-08 его заменяет полный День 1 (раздел ниже); разделы модулей описывают код как есть.

Unity 6000.6.4f1, URP. Пакеты: Input System, Cinemachine, Timeline, TextMeshPro (входит в uGUI), AI Navigation. Управление редактором идёт через мост unity-mcp (com.coplaydev.unity-mcp). Дизайн-документ (GDD v0.2) лежит в Notion владельца, пространство IndiGGG.

## Полный День 1 (новый сценарий, 2026-10-08)

1. **Пролог.** На столе дело Тамуры Рюты: фото с двух ракурсов, ФИО, дата рождения, описание, за что переводят в Фунсэки. ЛКМ листает, в конце печать — начинается игра.
2. **Приезд.** Играем только за Рюту. Во двор выйти можно, за забор нельзя. У входа директор: правила, устав, расписание. Затем перенос в общежитие к комнате 7 (2-й этаж), директор заселяет и уходит.
3. **Комната 7.** Знакомство с Кайто и Рэй (приехали вчера). Открываются три героя и основной квест «Познакомься с группой».
4. **Комнаты 8–11.** 8: Такеши, Юкки, Масуми. 9: Хиро, Цубаки. 10–11 заперты (отмычка — День 3, сейчас не делаем).
5. **Физра.** Звонок → Хиро ведёт группу в спортзал, остальные помещения закрыты. Катсцена, мини-игра «Физра: Свисток», первый раунд обучающий — объясняет Хиро.
6. **Перемена.** Большая часть школы закрыта. Основной квест — цепочка знакомств с работниками: охранник Серёга (душит тупыми диалогами, за Рюту его можно послать) → слепой представитель по оценке и контролю → повар-индус на кухне (говорит непонятно) → уборщик. На уборщике звонок.
7. **Физика.** Катсцена, мини-игра «Crazy machines», гэги физички.
8. **Доп. квесты одноклассниц.** Юкки — знакомство с шалостями: Рюта пинает под зад в туалете, директор палит по скрипту → выговор и отработка. Масуми — у гача-автомата первая крутка, гарантированно самая дешёвая фигурка. Цубаки — украсть свисток физрука: первый свап героев и Голос Кайто (Кайто убеждает физрука, что автомат с газировкой выдаёт много банок, физрук уходит, свисток крадём), трофей.
9. **Вечер.** В комнате 7 на полке фигурка, на стене свисток. Итоговый разговор троих, сон, переход к Дню 2.

### Способности героев (целевые; в коде пока старые — см. «Три героя»)

- **Рюта — «Пинок под зад»:** Q, только со спины NPC.
- **Кайто — «Голос»:** 4-й вариант ответа в диалогах (уже есть в DialogueGraph) + на Q подсказка-подсветка ближайшего полезного предмета (бывшие «Голоса» Рюты).
- **Рэй — «Телефон»** со слухами, без изменений.
- 🟡 **Tab** открывает Дневник (квесты и коллекции); герои переключаются клавишами **1/2/3** напрямую, без окна.

### Персонажи

- Герои: Тамура Рюта, Кагами Рэй, Мидзуно Кайто (+ Голос в голове Кайто).
- Одноклассники: Такеши, Юкки, Масуми, Хиро (шутник, переводит свистки физрука; модель `NPC_Joker`), Цубаки.
- Взрослые: директор, физрук Тоёда Кацуо (`NPC_Toyoda`), физичка, охранник Серёга, слепой представитель по оценке и контролю, повар-индус, уборщик.

### Задачи 17–21: полный День 1

- **17. Основа нового дня.** Квесты (основной + доп., шаги по WorldFlags) и Дневник на Tab (квесты + коллекции, вместо окна героев); переключение 1/2/3; режим «только Рюта» до комнаты 7; способности по новому списку; доступ к помещениям по фазам дня (открыто только нужное); новое расписание `DaySchedule_Day1` с фазами пролог → приезд → общежитие → физра → перемена → физика → свободное время → вечер.
- **18. Пролог и общежитие.** Дело Тамуры (листание, печать), граница двора у забора, директор у входа, перенос к комнате 7, заселение, знакомство с Кайто и Рэй, комнаты 8–9 с одноклассниками, 10–11 заперты; вечер: полка и стена с трофеями, разговор троих, сон, переход к Дню 2.
- **19. Физра по новому сценарию и перемена с работниками.** Хиро ведёт группу (LessonWalk), катсцена, обучающий раунд с Хиро; цепочка Серёга → слепой представитель → повар → уборщик, звонок на уборщике.
- **20. Урок Физики «Crazy machines».** Новая мини-игра на каркасе Funseki.Lessons, катсцена и гэги физички.
- **21. Доп. квесты одноклассниц.** Юкки (пинок в туалете, директор по скрипту, выговор и отработка), Масуми (гача-автомат, фигурка в коллекцию), Цубаки (свисток через Голос Кайто, первый свап, трофей).

Решения владельца (2026-10-08):
- Комнаты общежития `dorm_1…5` переименовать в комнаты 7–11 (правка в папке треда школы согласована).
- Кухня открыта с Дня 1 (повар на кухне), вопреки GDD.
- 2-й этаж закрыт на переменах; общежитие открывается по сюжету.
- Режим Физры «без свистка» пока не трогать (в Дне 1 он не срабатывает, раз свисток крадут после Физры).
- Пинок Кайто по автомату в шалости со свистком остаётся запасным путём рядом с Голосом.
- Доп. квесты одноклассниц доступны с перемены после Физры.
- Модели новых взрослых — Blender-скриптом, как NPC (`Art/Characters/NPC/build_npcs.py`).

## Структура папок

```
Assets/_Project/
  Scripts/<Module>/   код, namespace Funseki.<Module>
  Data/<Module>/      ScriptableObject с настройками и текстами; в корне GameFlowConfig.asset и PlayerSettings.asset
  Prefabs/            Items, Lessons
  Scenes/             Bootstrap, MainMenu, Slice_Day1; _Sandbox/ — Dialogue_Test, Interaction_Test
  Art/                Animation (Player.controller), Heroes (модели-варианты, материалы, портреты), Placeholders
  Audio/SFX/          BellRing.mp3
  Editor/             инструменты редактора (Tools > Funseki), Validate References
  School/             школьный модульный кит и School_Greybox (свой тред, Funseki.School)
Assets/MainMenu/      главное меню: скрипты, шрифты (их берут все меню Tools), материалы, модели крыльца
Assets/ThirdParty/Models/  модели героев из сторонних паков: Ryuto (Akane, MToon), Rey (Anime Girl), Kaito (Ren, lilToon), Animation (Mixamo)
Assets/lilToon/       шейдер Кайто; в коде пака зашит путь Assets/lilToon, не переносить
Assets/TextMesh Pro/, Assets/Settings/ (URP)  стандартные места
Assets/Editor/Funseki/  билдер школы (тред школы)
Assets/_Design/Plans/   планы этажей
Assets/_Trash/        кандидаты на удаление после уборки (Docs/cleanup_report.md); удаляет владелец
Art/                  вне Assets: Blender-исходники и скрипты генерации
Docs/                 отчёты (cleanup_report.md)
```

Модули: Core, Player, Interaction, Inventory, Heroes, Dialogue, DayCycle, NPC, Pranks, Lessons, Quests, Story, UI, Save, Audio, Telemetry.
Каждый модуль — отдельная сборка `Funseki.<Module>.asmdef` в своей папке Scripts, она ссылается только на `Funseki.Core` (и при необходимости на Unity.InputSystem, Unity.TextMeshPro, Unity.Cinemachine и т. п.). Ссылок модуль → модуль нет. Asmdef создаётся вместе с первым скриптом модуля; сейчас сборки есть у Core, UI, Player, DayCycle, Interaction, Inventory, Dialogue, Audio, Heroes, NPC, Save, Pranks, Lessons, Quests и Story.

Пути к ассетам прописаны строками в редакторских скриптах (`_Project/Editor/*Setup.cs`, `MainMenu/Editor/MainMenuBuilder.cs`, `Assets/Editor/Funseki`). Перенос ассета — через AssetDatabase.MoveAsset вместе с правкой этих констант. После переноса — `Tools > Funseki > Validate References` (Missing Script / Missing Reference по сценам и префабам).

Старое:
- `Assets/_Game/` — остатки первого контроллера героя (`Scripts/Player`, `Hero.controller`, `Input/GameControls.inputactions`). Не удалять: на них ссылается объект игрока в School_Greybox и `SchoolPlayerSetup` (тред школы).
- `Assets/PF_Kit_*.fbx` в корне Assets и `Assets/InputSystem_Actions.inputactions` — назначение не выяснено, не трогать (см. Docs/cleanup_report.md, таблица C).

## Герой (Funseki.Player)

- Ввод — `Assets/_Project/Data/Input/GameInput.inputactions`, карта `Gameplay` (клавиатура и геймпад). Move, Look, Sprint, Jump, FirstPerson работают; Interact, UseItem, AltUseItem, CycleItem, HeroMenu, HeroAbility, Pause только объявлены.
- Все числа движения и камеры — `Assets/_Project/Data/PlayerSettings.asset`, включая список состояний, где ввод героя выключен (Cutscene, Dialogue, Paused, Lesson).
- Компоненты на объекте Player: PlayerInputReader, PlayerMotor (CharacterController), PlayerAnimator (Speed, Grounded, Jump в `Assets/_Project/Art/Animation/Player.controller`), PlayerCameraController (CM ThirdPerson с Deoccluder, CM FirstPerson по удержанию F).

## Три героя (Funseki.Heroes)

- Данные в `Data/Heroes`: `HeroSettings.asset` (переключение 0,5 с, следование 1,5–3 м, догонялки, телепорт >25 м вне кадра, NavMesh, окно выбора), `Hero_Ryuta/Rei/Kaito` (имя, портрет, модель, цвет, Speaker, способность), `Abilities/Ability_*` (Q: «Голоса», «Телефон», «Пинок», кулдауны в них; `startHero` — Рюта, `partyUnlockFlag` — см. «Начало Дня 1»; сейчас «Голоса» у Рюты, «Пинок» у Кайто — по новому плану наоборот, см. «Способности героев»), `RumorSet_Day1` + `Rumors/Dialogue_Rumor_*` (звонки Рэй). Модели героев — варианты префабов в `Art/Heroes/Models` (Ryuto/Akane, Rey/Anime Girl, Kaito/Ren; MToon и сломанный lilToon заменены копиями URP Lit), портреты в `Art/Heroes/Portraits`.
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

## Интерактивные предметы среза (Funseki.Interaction, GDD 5.9 и спеки «Интерактивные предметы» в Notion)

- База: `InteractableObject` (IInteractable) + `InteractableData` (подсказка, повторяемость Once / OncePerDay / Always, кто может — `allowedHeroes`, в какие дни — `onlyOnDays`, реплики первый раз / повтор по каждому герою, реплика при учителе рядом, флаги, «Шум»). Реплики идут через `IBarkService`. Состояние объекта хранится в WorldFlags под `obj_<objectId>_<имя>` и восстанавливается в Start, поэтому попадает в сейв вместе с миром. Инвентарь предметы видят через `IItemBag` (Core, его реализует `Inventory`).
- Предметы: `WaterTap` (кран, бурая вода при первом открытии, учитель кричит «Не трать воду!»), `VendingMachine` (банки из пула, заклинило / сломался, шансы в данных), `RyutaLocker` (спека «Кабинки»: дверца, CM-камера крупного плана, подписи `InspectDetail` по наведению), `DrawablePoster` (плакаты и картины: Рюта / Кайто с маркером в инвентаре по E или ЛКМ рисуют прямо по картинке — камера крупного плана, ЛКМ / A, курсор стиком, Esc / E готово; один раз на картинку; учитель видит — сразу `OnCaught`; после перезагрузки вместо рисунка одна из `drawnTextures`), `BookletRack` (спека «Стелаж с журналами»: только Рюта, один раз, день 1), `ShowerHead` (душ: E вкл / выкл; Рюта / Кайто с «Краской» в руке заливают её в лейку, флаг `shower_paint_ready`, дальше душ льёт краской). Данные в `Data/Interaction/Interactables`, плейсхолдеры в `Art/Placeholders/Interactables`, предмет `Data/Inventory/Items/Item_Paint`.
- Шкафчик, буклет и рисование держат игру в состоянии Dialogue (ввод героя выключен, курсор свободен) и читают карту ввода `Inspect` (Esc / E / B, A / D / стрелки / d-pad, указатель, Paint — ЛКМ / A, Cursor — левый стик).
- `GameEvents.OnObjectUsed(hero, obj, objectId, action)` — что сделал предмет (tap_opened, machine_broken, poster_drawn, shower_painted…).
- Расстановка: `Tools > Funseki > Interaction > Place slice interactables in Slice_Day1` (пересобирает весь корень `Interactables`, данные не трогает) или `Add spec items to Slice_Day1 (keeps the rest)` (пересобирает только `Interactables/Spec` — ещё 3 крана, 6 плакатов, 4 картины, душ в туалете М, краска у кабинета рисования — и плакат в коридоре). Душ стоит в туалете М 1-го этажа временно: душевые по плану на 2-м этаже.

## Сохранение (Funseki.Save)

- Автосейв GDD 5.7: `SaveService` под `[Bootstrap]` (ISaveSystem в Core) пишет все `ISaveable` из `SaveRegistry` в один JSON в persistentDataPath с версией формата (`SaveService.FormatVersion`, файлы новее не читаются), сценой и причиной. Части: `world` (WorldFlags, в них и состояния предметов), `inventory`, `journal` (сервисы Bootstrap), `day` (`DayCycleDirector`: фаза и её таймеры), `heroes` (`HeroParty`: кто играет, позиции героев).
- Когда пишет: начало каждой фазы дня, кроме уроков (начало дня, перемена после катсцены и после урока), перед уроком (`LessonDirector` → `OnAutosaveRequested`), после шалости (`OnPrankDone`). Сохраняет в конце кадра, в MainMenu и SliceEnd не пишет. Иконка — `Funseki.UI.SaveIndicator` (по `OnGameSaved`).
- «Продолжить»: `ISaveSystem.PrepareContinue` сразу грузит части Bootstrap, остальные ждут в `SaveRegistry` и отдаются владельцу при регистрации (в его Awake, до Start); `DayCycleDirector` тогда входит в сохранённую фазу вместо первой (если звонок уже был — звонит снова), `HeroParty` ставит героев. «Новая игра» — `ResetForNewGame` (флаги, инвентарь, журнал как при запуске).
- Debug: F5 — сохранить, F6 — «Продолжить» из сейва (`Data/Save/SaveSettings.asset`, там же выключатель автосейва).
- `Tools > Funseki > Save > Add inventory and save services to Bootstrap` — ставит `InventoryService` + `InventoryPanel` и `SaveService` под `[Bootstrap]`.

## Начало Дня 1: пролог → общежитие (Funseki.Story, Funseki.Quests)

- Расписание `DaySchedule_Day1_Slice`: `prologue` (Cutscene, флаг `day1_prologue_done`) → `arrival` (Break, цель «Встреться с директором у входа», флаг `day1_director_done`) → `dorm` (Break, «Зайди в комнату 7», флаг `quest_q_day1_main_done`) → дальше старые break_1 / lesson_fizra / break_2. Фаза `intro` и катсцена «трио у ворот» из расписания убраны (корень `Cutscene_Intro` остался, но не играет).
- Пролог — внутри Slice_Day1, не отдельной сценой: Build Settings, главное меню и сейв не меняются, школа грузится под экраном. `PrologueDossier` (корень `Day1_Start/Prologue`) рисует стол и папку «Дело» поверх всего; данные и тексты [TODO] — `Data/Story/Prologue_Day1.asset` (фото анфас / профиль — пустые спрайты = серые заглушки). Карта ввода `Prologue`: Next (ЛКМ / Enter / A) листает 3 страницы, на последней ставит печать (падение, звук-заглушка, тряска); Skip (Пробел / Start) удерживать 1 с. Затемнение → флаг → arrival.
- Приезд — `Day1Arrival` (`Day1_Start/Arrival`, данные `Data/Story/Day1Start.asset`): на старте arrival Рюта у ворот, Кайто и Рэй в комнате 7 (флаг `day1_start_placed`, повторно не ставит). Директор (капсула `NPC_Director` на крыльце) в 3 м сам начинает `Day1_Director_Intro` → затемнение, Рюта и директор у двери комнаты 7 → `Day1_Director_Room` → директор уходит к лестнице СВ и исчезает. Флаги `day1_director_met`, `day1_director_done`. Если день начат позже цепочки (Quick Play в break_1), `skipFlags` из данных засчитывают её целиком.
- Забор — `Day1_Start/Fence`: решётка в воротах, низкий забор по бокам двора, невидимые стены 3 м; `StoryBoundary` говорит `fenceLine` у забора.
- Группа героев: пока не стоит `HeroSettings.partyUnlockFlag` (`heroes_unlocked`), в группе только `startHero` (Рюта): остальные стоят на месте (`HeroUnit.MakeIdle`), Tab не работает, HUD показывает одну карточку (`IHeroRoster.IsInParty`, `HeroService.IsInParty`). Флаг ставит `RoomMeeting` (`Day1_Start/Room7`) после `Day1_Room7_Meet`, который сам начинается, когда Рюта входит в комнату 7.
- Квесты — `Funseki.Quests.QuestService` под `[Bootstrap]/Quests` (IQuestLog в Core, сейв `quests`). `QuestData` (`Data/Quests/Q_*.asset`): id, название, Main / Side, флаг старта, шаги (цель, «готово, когда стоят все флаги», награда — флаги и предметы), флаг конца (`quest_<id>_done`). Все квесты перечислены в `Data/Quests/QuestSettings.asset`. Шаг активного основного квеста — цель HUD (перекрывает цель фазы через кадр после её старта). Старт квеста и каждый шаг — автосейв. События `OnQuestStarted/OnQuestStepChanged/OnQuestCompleted`.
- `Q_Day1_Main` «Познакомься с группой» стартует по `heroes_unlocked`: комната 8 (`day1_met_takeshi/yukki/masumi`) → комната 9 (`day1_met_hiro/tsubaki`). Флаги ставит `DialogueNpc.talkedFlag` после разговора по E.
- Общежитие (School_Greybox, правка согласована): `dorm_5…dorm_1` = комнаты 7…11 (по displayName, id зон прежние), двери 10 и 11 — `LockedDoor` до дня 3 «Заперто. Тут нужна отмычка», двухъярусные кровати выключены, в `School_Props/Dorm_Day1` по 3 кровати и тумбы на комнату. В комнате 7 (Slice_Day1) `CollectionShelf` (6 мест) и `TrophyWall` (14 мест) — пока пустые. Одноклассники: Такеши (Jock), Юкки (Gossip_A), Масуми и Цубаки (Gossip_B, пока один и тот же), Хиро (Joker); директор — капсула.
- Диалоги (все [TODO]): `Data/Dialogue/Day1/` — Day1_Director_Intro, Day1_Director_Room, Day1_Room7_Meet, Day1_Meet_Takeshi, Day1_Meet_Yukki, Day1_Meet_Masumi, Day1_Meet_Hiro, Day1_Meet_Tsubaki. Новые спикеры — `Speaker_Director/Takeshi/Yukki/Masumi/Hiro/Tsubaki`.
- Сборка: `Tools > Funseki > Day1 > Setup Day 1 start (prologue → dorm)` (данные создаёт один раз, `[Bootstrap]/Quests`, `Day1_Start` и `School_Props/Dorm_Day1` пересобирает). Проверка: `Tools > Funseki > Day1 > Smoke Test Day 1 start` (вся цепочка, сейв и «Продолжить» на втором шаге квеста; затирает обычный сейв), `Play to the prologue` — Новая игра и остановка на прологе.

## Меню, пауза, катсцена и конец демо

- Главное меню — старая сцена `MainMenu` (`MainMenuController`): «Новая игра» сбрасывает прогресс и грузит Slice_Day1 (там первой идёт фаза пролога), «Продолжить» серая без сейва и ведёт к последнему автосейву.
- Под `[Bootstrap]/Menus` (Funseki.UI, uGUI + TMP, вид из `HudTheme`, тексты в `Data/UI/MenuSettings.asset`): `PauseMenu` (Esc / Start — копия `Gameplay/Pause`; в состояниях из `pausableStates`; timeScale 0 и пауза звука; «Журнал недели», «Настройки», «В главное меню» без сохранения, «Выход»), `SaveIndicator`, `SliceEndView` (в состоянии SliceEnd: исход физры по флагам `fizra_day1_result_*`, записи журнала X из `endJournalTotal`, «Анкета» по `surveyUrl` — пустая ссылка прячет кнопку).
- Настройки игрока — `Core/PlayerOptions` (PlayerPrefs): громкость (AudioListener), множитель мыши (читает `PlayerInputReader`), скорость текста (читает `DialogueRunner`), оконный / полноэкранный (тот же ключ, что у меню).
- Старая катсцена «Приезд в Фунсэки» (с 2026-10-09 не играет: фазы `intro` в расписании нет): `Funseki.DayCycle.StoryCutscene` (корень `Cutscene_Intro` в Slice_Day1) играет на старте фазы `intro`: Timeline `Data/Cutscenes/Cutscene_Day1_Intro.playable` (трио у ворот-заглушки → пролёт над двором → титр «День 1» → трио на крыльце), на `dialogueAt` Timeline ждёт диалог `Data/Dialogue/Cutscene_Day1_Intro` (реплики «[TODO]»), затем затемнение, флаг `day1_intro_done` и перемена — герои уже в холле за главными дверями. Пропуск — удержание `Cutscene/Skip` (Пробел / A) 1 с, кроме диалога. Настройки — `Data/Cutscenes/Cutscene_Day1_Intro.asset`. Герои в катсцене — куклы-модели, не игровые объекты.
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
- Звук звонка: `Funseki.Audio.BellSoundPlayer` (объект BellSound под `[Bootstrap]`) слушает `OnBell`, клипы и громкость по типу звонка в `Data/Audio/BellSettings.asset` (сейчас везде `Assets/_Project/Audio/SFX/BellRing.mp3`). Ставится через `Tools > Funseki > Audio > Add bell sound to Bootstrap`.
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

## Стиль школы (палитра и текстуры)

- Палитра — `Data/Visual/SchoolPalette.asset` (`Funseki.Core.SchoolPalette`): 8 цветов визуальной доски + цвета поверхностей (стены, полы по типам зон, двери, мебель), плотность текстур, шум, point-фильтр.
- `Tools > Funseki > School Style > Apply Palette and Textures to School` печёт текстуры (`Art/School/Textures`), URP Lit материалы `M_School_*` (`Art/School/Materials`) и детальные меши `SM_School_*` (`Art/School/Meshes`: парта, стул, стол учителя, доска, скамья, двухъярусная кровать, кабинка, урна, плиты пола с потолком снизу, рама и стекло окна) и назначает их рендерерам School_Greybox на месте: объекты не двигаются, Build School не нужен. Материал пола и стен выбирается по зоне (`Zone_<id>` в иерархии). В проёмы окон добавляется `Style_Window` без коллайдера.
- Первый Apply пишет `Art/School/SchoolStyleBackup.json`; `Revert School to Greybox Look` возвращает серую коробку. `Rebake Textures and Materials Only` — после правки палитры. `Capture Before/After Shots` — кадры в `Docs/SchoolStyle`.
- Generate Kit материалы `M_School_*` не трогает: стиль держится на переопределениях в сцене.

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
- `Tools > Funseki > Slice > Build Slice_Day1 (player + school loader)` — пересобирает в Slice_Day1 трёх героев (Funseki.Player + Funseki.Heroes), камеры Cinemachine, SchoolLoader и HeroBarks (реплики героя). На каждом герое `HeroInteractor`, `HeroItemUser`, `HeldItemView`. Анимации — `Assets/ThirdParty/Models/Animation` (Idle, Walk, Run, Jumping Up, Talking, Getting Hit; новые Generic-файлы переводятся в Humanoid). Школа не копируется: SchoolLoader при старте подгружает School_Greybox, выключает в ней всё, кроме корня School, и открывает только дверь главного входа. School_Greybox стоит в Build Settings последней.
