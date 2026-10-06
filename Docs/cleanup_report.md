# Уборка проекта: отчёт аудита (этап 1)

Дата: 2026-10-06. Снимок до уборки: коммит `9967154` «chore: snapshot before cleanup».
Ничего не перенесено и не удалено, это только отчёт.

## Как проверял

- Прошёл все 1576 файлов в `Assets/` (1773 GUID в `.meta`).
- Построил граф ссылок по GUID: сцены, префабы, ScriptableObject, материалы, контроллеры, Timeline, asmdef и сами `.meta` (ремапы материалов у FBX).
- Корни графа:
  - живой поток игры: `Bootstrap`, `MainMenu`, `Slice_Day1` и `School_Greybox` (Slice_Day1 грузит её в рантайме);
  - ссылки из `ProjectSettings` (URP, Input System и т. п.);
  - всё в папках `Resources`;
  - весь код и asmdef, кроме `Assets/_Game`;
  - пути вида `"Assets/..."` в коде (их читают меню Tools > Funseki).
- Отдельно проверил, что всем типам из `_Project/Scripts` есть ссылки из других скриптов или ассетов: неиспользуемых скриптов там нет.
- Сцены `SampleScene`, `PlayerTest` и `Day1_Greybox` стоят в Build Settings, но живой поток их не открывает. Поэтому всё, что тянут только они, считается старым.
- Addressables в проекте нет.

Важно про `_Trash`: папка лежит внутри `Assets`, поэтому перенесённые туда скрипты продолжают компилироваться, а ассеты импортироваться. Место на диске освободится только после того, как ты удалишь `_Trash`. Из истории git файлы при этом не пропадут.

---

## A. Кандидаты в _Trash

| # | Путь | Файлов | Размер | Причина | Уверенность |
|---|---|---|---|---|---|
| A1 | `Assets/Scenes/SampleScene.unity` | 1 | 11 KB | шаблонная сцена URP, нигде не используется. **Стоит в Build Settings** | высокая |
| A2 | `Assets/_Game/Scenes/PlayerTest.unity` | 1 | 94 KB | старая версия: тест первого контроллера. **Стоит в Build Settings** | высокая |
| A3 | `Assets/_Game/Scenes/Day1_Greybox.unity` | 1 | 862 KB | старая версия: серая коробка до School_Greybox. **Стоит в Build Settings** | высокая |
| A4 | `Assets/_Game/Editor/` (Day1GreyboxBuilder, HeroAnimatorBuilder, HeroImportSetup, HeroModelSwap, PlayerSmokeTest, PlayerTestSceneBuilder) | 6 | 40 KB | старая версия: меню `Tools > One Funseki`, которые строят A2, A3 и старого героя | высокая |
| A5 | `Assets/_Game/Art/Greybox/` | 34 | 130 KB | материалы серой коробки, нужны только A2 и A3 | высокая |
| A6 | `Assets/_Game/Art/Characters/Hero/` без `Hero.controller` (Hero_Game.fbx, Hero.mat, Textures/) | 4 | 6,2 MB | старый герой, нужен только A3 | высокая |
| A7 | `Assets/_Game/Art/Characters/Tomura/Tomura_Ryuta_Game.fbx` | 1 | 118 KB | дубликат `Art/Characters/Tomura/Tomura_Ryuta_Game.fbx`, нужен только A2 | высокая |
| A8 | `Assets/_Game/Scripts/Util/FaceCamera.cs` | 1 | 1 KB | нужен только A3 | высокая |
| A9 | `Assets/_Game/Data/`, `Assets/_Game/Prefabs/` | 0 | — | пустая папка | высокая |
| A10 | `Assets/_/`, `Assets/Anim/`, `Assets/Animation/`, `Assets/MainMenu/Sprites/` | 0 | — | пустая папка (у `_`, `Anim`, `Animation` остались .meta) | высокая |
| A11 | `Новая папка/`, `GeneratedAssets/` в корне репозитория (вне Assets, не в git) | 0 | — | пустая папка | высокая |
| A12 | `Assets/New Cubemap.png` | 1 | 18 KB | временный файл, не используется | высокая |
| A13 | `Assets/Screenshots/` | 27 | 6,7 MB | скриншоты из MCP, уже в .gitignore, нигде не используются | высокая |
| A14 | `.gitkeep` в непустых папках `_Project` (Art/Placeholders, Prefabs, Scripts/Audio, DayCycle, Dialogue, Heroes, Interaction, Lessons, NPC, Player, Pranks, Save) | 12 | 0 | временный файл. В пустых `Audio/` и `Scripts/Telemetry/` оставляю | высокая |
| A15 | `Assets/_Project/Scripts/DayCycle/CutscenePlaceholder.cs` | 1 | 1 KB | старая версия: в сценах не стоит, в CLAUDE.md помечен «можно удалить». DayCycleSetup и PlayerSliceSetup упоминают только имя объекта строкой | высокая |
| A16 | `Assets/_Project/Editor/PlayerSliceSmokeTest.cs` | 1 | 11 KB | меню `Slice > Smoke Test Movement` больше не используется, но скрипт с `[InitializeOnLoad]` | средняя |
| A17 | `Assets/MainMenu/Generated/Materials/` | 31 | 120 KB | нигде не используются: ни сцена MainMenu, ни MainMenuBuilder | средняя |
| A18 | `Assets/MainMenu/Generated/Textures/BrokenGlass.png`, `VendingFront.png` | 2 | 12 KB | не используются. Graffiti_RGBA и GrimeStreaks из той же папки нужны MainMenuBuilder, они остаются | средняя |
| A19 | `Assets/MainMenu/Materials/M_Glass_Greybox`, `M_Ground_Placeholder`, `M_Petal`, `M_School_Greybox` | 4 | 14 KB | не используются | средняя |
| A20 | `Assets/MainMenu/Sky/MorningSky_Procedural.mat`, `Assets/MainMenu/MainMenu_VolumeProfile.asset` | 2 | 2 KB | не используются: меню берёт Sky_Day1 и MainMenu_Day1_Profile | средняя |
| A21 | `Assets/MainMenu/Models/Characters/anime+character+3d+model.glb` | 1 | 7,9 MB | не используется; появился сегодня, до снимка в git его не было | средняя |
| A22 | `Assets/Models/Yukki/` целиком | 64 | 12,5 MB | сторонний пак, нигде не используется, кроме собственных демо-сцен | средняя |
| A23 | `Assets/ThirdParty/Quaternius_Nature/` | 89 | 45,4 MB | не используется ни одной сценой, префабом или кодом | средняя |
| A24 | `Assets/ThirdParty/PolyHaven/` | 12 | 8,5 MB | не используется | средняя |
| A25 | `Assets/ThirdParty/Sky/` | 1 | 4,3 MB | не используется | средняя |
| A26 | `Assets/ThirdParty/Kenney_ModularBuildings/` | 110 | 2,8 MB | не используется | средняя |
| A27 | `Assets/ThirdParty/Kenney_CityCommercial/` | 43 | 2,0 MB | не используется | средняя |
| A28 | `Assets/ThirdParty/AmbientCG_Graffiti/` | 4 | 1,6 MB | не используется | средняя |

A23–A28 на всякий случай: возможно, паки скачали под будущий арт меню или двора. Если они нужны, перенеси эти строки в C.

**Build Settings.** A1–A3 стоят в Build Settings. При переносе в `_Trash` Unity сам перепишет их пути, и они продолжат попадать в билд уже из `_Trash`. Чтобы их убрать, нужно править `ProjectSettings/EditorBuildSettings.asset`, а это по правилам только с твоего «ок». Рекомендую убрать: живой поток грузит сцены по именам из GameFlowConfig, эти три сцены ему не нужны.

### Дубликаты

| Файлы | Что оставляем |
|---|---|
| `Art/Characters/Tomura/Tomura_Ryuta_Game.fbx` = `Assets/_Game/Art/Characters/Tomura/Tomura_Ryuta_Game.fbx` | исходник в `Art/`, копию в Assets — в _Trash (A7) |
| `Art/Characters/Tomura/Tomura_Ryuta.fbx` = `Assets/MainMenu/Models/Characters/Tomura_Ryuta.fbx` | обе: копия в Assets стоит на крыльце главного меню |
| `Assets/MainMenu/Generated/Textures/Graffiti_RGBA.png` = `Assets/MainMenu/Textures/Graffiti.png` (и так же GrimeStreaks) | обе: MainMenuBuilder копирует Generated → Textures |
| `T_Poster_Clean.png` = `T_Poster_Drawn_3.png` (Art/Placeholders/Interactables) | обе, см. C13 |

---

## B. Перенос

Код в `_Project` уже разложен по модулям (Scripts/<Модуль>, Data/<Модуль>), и это совпадает с CLAUDE.md. Почти все оставшиеся переносы задевают пути, которые прописаны строками в редакторских скриптах меню Tools > Funseki. Без правки этих строк меню после переноса создадут новые ассеты по старому пути. Поэтому у каждой строки указано, какую константу придётся поменять (одна строка пути, классы не переименовываются). Без твоего «ок» на эти правки я эти строки не выполню.

| # | Откуда → куда | Файлов | Что ещё задевает |
|---|---|---|---|
| B1 | `Assets/Scenes/MainMenu.unity` → `Assets/_Project/Scenes/MainMenu.unity` | 1 | `CoreScenesSetup.MainMenuPath`. Unity сам перепишет путь в Build Settings (это файл ProjectSettings). Рантайм грузит сцену по имени, это не ломается |
| B2 | `Assets/_Project/Scenes/Dialogue_Test.unity`, `Interaction_Test.unity` → `Assets/_Project/Scenes/_Sandbox/` | 2 | `DialogueTestSetup.ScenePath`, `InteractionTestSetup.ScenePath` |
| B3 | `Assets/Sound/BellRing.mp3` → `Assets/_Project/Audio/SFX/BellRing.mp3` (пустая `Assets/Sound` удаляется) | 1 | `AudioSetup.BellClipPath`. `BellSettings.asset` ссылается по GUID, это не ломается |
| B4 | `Assets/Models/` целиком (Animation, Kaito, Rey, Ryuto; Yukki уходит в A22) → `Assets/ThirdParty/Models/` | 124 | сторонние паки, нужен твой «ок». `HeroesSetup.RyutaModel` и `KaitoModel`, `PlayerSliceSetup.GirlFbx` и `AnimDir`. MToon внутри Ryuto ищется через `Shader.Find`, перенос ему не мешает |
| B5 | `Assets/_Project/Prefabs/Items/` → `Assets/_Project/Prefabs/Interactables/Items/` | 2 | `InteractionTestSetup.ItemPrefabs`. Не рекомендую: выигрыш маленький |

Сознательно оставляю на месте:
- `Assets/TextMesh Pro` и `Assets/Settings` (URP): стандартное место.
- `Assets/lilToon`: в его коде зашит путь `Assets/lilToon/...`, после переноса он сломается.
- `Assets/MainMenu`: твоё меню. На шрифты из `Assets/MainMenu/Fonts` ссылаются 9 скриптов меню Tools.
- `Assets/_Project/School` и `Assets/Editor/Funseki`: их ведёт школьный тред. `SchoolSceneLoader` хранит путь к School_Greybox в сцене.
- `Assets/_Design/Plans`: планы этажей.
- Имена папок Data/<Модуль>: в промте предложены Dialogues/Items/Schedules/Settings, но CLAUDE.md и меню Tools используют имена модулей, поэтому их не трогаю.
- `Art/Animation` в `Art/Animations` не переименовываю: это переименование, а не перенос, и путь контроллера прописан в коде.

---

## C. Не уверен (не трогаю)

| # | Путь | Почему не уверен |
|---|---|---|
| C1 | `Assets/PF_Kit_*.fbx` (30 файлов, 660 KB) в корне Assets | появились сегодня в 15:42 и нигде не используются. Похоже на экспорт школьного кита в FBX из школьного треда. Лучше спросить там |
| C2 | `Assets/InputSystem_Actions.inputactions` | шаблон Input System, ни к чему не подключён, но сегодня его кто-то правил (+40 строк) |
| C3 | `Assets/_Game/Scripts/Player/` (PlayerController, PlayerCameraRig, PlayerAnimator), `Assets/_Game/Art/Characters/Hero/Hero.controller`, `Assets/_Game/Input/GameControls.inputactions` | старый контроллер героя, но на него ссылаются объект игрока в `School_Greybox.unity` и `Assets/Editor/Funseki/SchoolPlayerSetup.cs` (зона школьного треда). В _Trash не переносить, иначе в School_Greybox появится Missing Script |
| C4 | `Assets/Models/Kaito/Ren/`: `_URP & HDRP Pack/` (2 .unitypackage, 32 MB), `Demo/` (18,6 MB), `FaceAnimations/`, `Prefabs/Ren_Magica2Setup.prefab` | не используются, но лежат внутри стороннего пака, а внутри паков по правилам ничего не двигаю. Кайто использует `Ren_BasicSetup.prefab`, `Ren.fbx`, материалы на lilToon и `Demo/RenDemoWalk.controller` с `WALK00_F.anim` |
| C5 | `Assets/Models/Ryuto/DemoScene/` (сцена SampleScene_Akane, анимации, скрипты) | не используются, но в этой же папке лежит MToon, а на него ссылаются материалы базового префаба Рюты `angGirl.prefab` |
| C6 | `Assets/Models/Rey/Model/Shademap_01.png` (3,4 MB), 5 текстур в `Assets/Models/Ryuto/Akane/textures/` | не используются, но внутри пака |
| C7 | `Assets/lilToon/` (Presets, CustomShaderResources, часть Texture) | не используются, но это пак, а материалы Кайто на lilToon. Оставить целиком |
| C8 | `Assets/_Project/Data/Lessons/Fizra/Rounds/FizraRound_Exam3in10.asset` | ни к чему не подключён, но LessonsSetup создаёт его для «Контрольной» |
| C9 | `Assets/_Project/Art/Placeholders/Interactables/M_Porcelain.mat` | не используется, но его создаёт InteractablesSetup, после переноса появится снова |
| C10 | `Assets/_Project/School/`: `Data/SchoolLayout.asset`, `Kit/KitSettings.asset`, `Docs/*`, меши и префабы Column/CornerInner/CornerOuter | билдер школы грузит их кодом, по GUID ссылок нет. Зона школьного треда |
| C11 | `Assets/_Design/Plans/Floor_1.PNG`, `Floor_2.PNG` | в Unity не используются, но это исходные планы этажей |
| C12 | `Art/` в корне репозитория (Blender-исходники Томуры и окружения меню, py-скрипты, 9 MB) | не ассеты Unity, а исходники. Оставить |
| C13 | `T_Poster_Clean.png` и `T_Poster_Drawn_3.png` побайтово одинаковые | оба подключены к Interactable_Poster. Возможно, третий вариант рисунка на плакате не сгенерировался. Это не уборка, просто отмечаю |

---

## .gitignore

В `.gitignore` уже есть всё из списка: Library, Temp, Obj, Build, Builds, Logs, UserSettings, `.vs`, `.vscode`, `.idea`, `*.csproj`, `*.sln`, `*.slnx`. Ни одна такая папка или файл в индекс не попали.

Проблемы:

| # | Что | Предложение |
|---|---|---|
| G1 | `Assets/MainMenu/Audio/MainMenu_Music.wav` (56 MB) записан в .gitignore, но уже лежит в индексе, так что правило не работает. Музыка нужна меню | убрать строку из .gitignore (рекомендую). Если сделать `git rm --cached`, у новой копии репозитория меню останется без музыки |
| G2 | `Art/Environment/preview/MenuEnvironment.blend1` в индексе: это автобэкап Blender | добавить `*.blend1` в .gitignore и сделать `git rm --cached` этого файла |
| G3 | `Assets/Screenshots.meta` в индексе, хотя папка игнорируется (правило `!/Assets/**/*.meta` возвращает .meta обратно) | `git rm --cached Assets/Screenshots.meta` (или убрать вместе с A13) |
| G4 | `GeneratedAssets/` в корне: сюда пишет unity-mcp | добавить `/GeneratedAssets/` в .gitignore |
| G5 | нет `.gitattributes` и LFS: модели (119 MB) и музыка ушли в git обычными файлами, `.git` уже 125 MB | не эта задача. Отмечаю, чтобы настроить LFS отдельно, пока репозиторий не вырос |

---

## Сводка

| Таблица | Строк | Файлов | Размер |
|---|---|---|---|
| A. Кандидаты в _Trash | 28 | 454 файла и 8 пустых папок | 99,3 MB (из них 92,7 MB в git, остальное — игнорируемые скриншоты) |
| — высокая уверенность | 15 | 90 | 14,1 MB |
| — средняя уверенность | 13 | 364 | 85,2 MB |
| B. Перенос | 5 | 130 | все строки требуют правки одной-двух строк пути в редакторских скриптах |
| C. Не уверен | 13 | — | не трогаю |
| .gitignore | 5 пунктов | | |

После удаления `_Trash` освободится около 99 MB в рабочей папке. Около 65 MB из них — шесть неиспользуемых паков в `Assets/ThirdParty`. Из истории git эти файлы не уйдут.

---

## Итог выполнения (этап 2–3)

Одобрено целиком («Ок»), включая правку констант путей, удаление трёх сцен из Build Settings и перенос `Assets/Models` в `ThirdParty`. Перенос делал скрипт `ProjectCleanupMoves` через `AssetDatabase.MoveAsset`, после работы он сам перенесён в `_Trash/_Project/Editor`.

- **B:** выполнены B1–B4. B5 (Prefabs/Items) не делал, как и рекомендовал в отчёте.
- **Правка путей** в `_Project/Editor`:
  - `CoreScenesSetup.MainMenuPath`
  - `DialogueTestSetup.ScenePath` и `InteractionTestSetup.ScenePath`
  - `AudioSetup.BellClipPath`
  - `HeroesSetup.RyutaModel` и `KaitoModel`
  - `PlayerSliceSetup.GirlFbx` и `AnimDir`
- **A:** все 28 строк в `Assets/_Trash/` с сохранением путей. `.gitkeep` перенесены через `git mv`.
- **Пустые папки удалены** вместе с .meta: `Assets/_`, `Anim`, `Animation`, `Scenes`, `Sound`, `Models`, `MainMenu/Sprites`, `_Game/Data`, `_Game/Prefabs`, `_Game/Scenes`, `_Game/Editor`, `_Game/Scripts/Util`, `_Game/Art/Greybox`, `_Game/Art/Characters/Tomura`. В корне репозитория удалены `Новая папка` и `GeneratedAssets`.
- **Build Settings:** Bootstrap, MainMenu, School_Greybox, Slice_Day1.
- **.gitignore:**
  - G1: строка про MainMenu_Music.wav убрана.
  - G2: добавлен `*.blend1`, `MenuEnvironment.blend1` убран из индекса (файл на диске остался).
  - G3: `Assets/_Trash/Screenshots/` игнорируется, в коммит скриншоты не попадают.
  - G4: добавлен `/GeneratedAssets/`.
- **Новый инструмент:** `Tools > Funseki > Validate References` (`_Project/Editor/ReferenceValidator.cs`).

Проверка:
- Компиляция без ошибок и предупреждений.
- Validate References до уборки: 9 сцен, 48 префабов, 4 проблемы, все в старом или неиспользуемом:
  - `PlayerTest`: потерян Animator Controller;
  - `Ren_Magica2Setup.prefab`: два Missing Script (Magica Cloth не установлен);
  - `Yukki/Underwear1.prefab`: потерян Avatar.
- Validate References после уборки: 6 сцен, 46 префабов, 2 проблемы. Обе те же самые старые, в `Ren_Magica2Setup.prefab`. Новых нет. В Bootstrap, MainMenu, Slice_Day1, School_Greybox и тестовых сценах проблем нет.
- Smoke Test Flow до Slice_Day1 не дошёл ни с таймаутом 20 с, ни со 120 с: меню показалось, «Новая игра» сработала, а затемнение не продолжилось. Причина не в переносе: в Player Settings `runInBackground: 0`, и Play стоит на месте, пока окно Unity не в фокусе, а я запускал его из фона. Нужна ручная проверка.
- Из переносов ничего возвращать не пришлось.
