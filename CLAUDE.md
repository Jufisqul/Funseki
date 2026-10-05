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

Модули: Core, Player, Interaction, Heroes, Dialogue, DayCycle, NPC, Pranks, Lessons, UI, Save, Audio, Telemetry.
Каждый модуль — отдельная сборка `Funseki.<Module>.asmdef` в своей папке Scripts, она ссылается только на `Funseki.Core` (и при необходимости на Unity.InputSystem, Unity.TextMeshPro, Unity.Cinemachine и т. п.). Ссылок модуль → модуль нет. Asmdef создаётся вместе с первым скриптом модуля; сейчас сборки есть у Core и UI.

Старое, пока не перенесённое:
- `Assets/Scenes/MainMenu.unity` + `Assets/MainMenu/` — главное меню, используется как сцена MainMenu (кнопки «Новая игра» и «Продолжить» грузят Slice_Day1).
- `Assets/_Game/` — первый контроллер героя, `GameControls.inputactions`, сцены PlayerTest и Day1_Greybox.
- Сторонние ассеты персонажей: `Anime Girl`, `3d-character_animeGirlAkane`, `RoloArt`, `Yukki`, `lilToon`.

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
