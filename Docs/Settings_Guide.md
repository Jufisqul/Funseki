# Гайд: где что настраивать

Почти всё, что можно крутить в Дне 1, лежит в ассетах `Assets/_Project/Data`. Код трогать не нужно: выдели ассет в Project, поменяй поле в Inspector и сохрани проект (**Ctrl+S** или File → Save Project). Изменение подхватится при следующем запуске.

Названия полей ниже даны так, как их показывает Inspector (**Max Duration**). Числа в скобках «сейчас» — значения в ассетах на момент написания гайда.

## Как это устроено

- **Данные** (ScriptableObject в `Data/`) — числа, тексты, списки. Правишь и запускаешь, больше ничего нажимать не надо.
- **Сборщики** в меню **Tools → Funseki** — пересобирают объекты в сценах (`Slice_Day1`, `Bootstrap`, `School_Greybox`). Нужны, только если поменялось устройство сцены, а не числа. Каждый сборщик удаляет свой корневой объект и строит его заново, поэтому **ручные правки внутри этих корней пропадают** при пересборке. Данные сборщики не трогают: ассеты создаются один раз и дальше остаются твоими.
- **Запуск** — всегда из Bootstrap, обычная кнопка Play из `Slice_Day1` запустит сцену без сервисов (сейв, диалоги, меню), и игра поведёт себя странно. Два способа:
  - **Tools → Funseki → Core → Play From Bootstrap** — вся игра: главное меню → «Новая игра» → пролог → директор → общежитие → перемена → физра.
  - **Tools → Funseki → Quick Play → Play Break** (Ctrl+Alt+P) — сразу первая перемена `break_1`, без меню и пролога; начало дня засчитано, все три героя в группе; автосейв выключен, твой сейв не затрётся. **Play Break Here (from Scene View)** (Ctrl+Alt+Shift+P) — то же, но герой встаёт туда, куда смотрит камера Scene View. Если Unity пишет, что Ctrl+Alt+P занят командой «Play Mode/Step», выбери строку Quick Play, поставь **Rebind to selected command** и нажми **Perform Selected**.

Новый ассет любого типа создаётся через **Create → Funseki → …** в Project (например, `Funseki/Dialogue/Dialogue Graph`).

Списки состояний игры (поля вида **… States**) выбираются из: MainMenu, Cutscene, Break (перемена), Lesson (урок), Dialogue, Paused, Caught («Поймали»), SliceEnd (конец демо).

---

## 1. Герой и камера — `Data/PlayerSettings.asset`

Один ассет на всех трёх героев.

| Что хочу | Поле | Сейчас |
|---|---|---|
| Скорость шага / бега | **Walk Speed** / **Sprint Speed**, м/с | 1,8 / 4,5 |
| Разгон и торможение | **Acceleration** / **Deceleration** | 14 / 18 |
| Управление в воздухе | **Air Control** (0…1) | 0,4 |
| Высота прыжка | **Jump Height**, м | 0,6 |
| Гравитация | **Gravity** (отрицательная) | −20 |
| Прощение прыжка | **Coyote Time**, **Jump Buffer**, с | 0,12 / 0,1 |
| Ступеньки и склоны | **Step Offset** (м), **Slope Limit** (°), **Ground Snap Distance** | 0,35 / 50 / 0,45 |
| Чувствительность мыши | **Mouse Sensitivity**, ° на пиксель | 0,12 |
| Чувствительность стика | **Gamepad Sensitivity**, °/с | 180 |
| Инверсия по вертикали | **Invert Y** | выкл. |
| Дальность камеры | **Camera Distance**, м | 3,5 |
| Высота точки, на которую смотрит камера | **Pivot Height** (доля роста) | 0,9 |
| Угол обзора | **Third Person Fov** | 55 |
| Наклон камеры | **Pitch Range** (мин/макс), **Default Pitch** | −20…65 / 12 |
| Отставание камеры | **Follow Damping**, с | 0,1 |
| Камера и стены | **Camera Collision Layers**, **Camera Radius**, **Min Camera Distance** | Default+Environment / 0,25 / 0,4 |
| Вид от первого лица (держать F) | **Eye Height**, **First Person Fov**, **First Person Pitch Range**, **First Person Blend Time** | 0,93 / 65 / −70…70 / 0,3 |
| Когда ввод героя выключен | **Input Blocked States** | Cutscene, Dialogue, Paused, Lesson, Caught |

Чувствительность мыши игрок может ещё умножить в меню паузы → «Настройки» (сохраняется у него в PlayerPrefs, ассет не меняет).

Анимации ходьбы и бега: контроллер `Art/Animation/Player.controller`. Если поменял клипы, нажми **Tools → Funseki → Slice → Rebuild Player Animator**.

---

## 2. Управление — `Data/Input/GameInput.inputactions`

Двойной клик откроет окно Input Actions. Действия разложены по картам; у каждого действия есть клавиатура и геймпад, и при правке надо держать обе раскладки.

| Карта | Когда работает | Действия (клавиатура / геймпад) |
|---|---|---|
| **Gameplay** | перемена | Move (WASD, стрелки / L-стик), Look (мышь / R-стик), Sprint (Shift / L3), Jump (Пробел / A), FirstPerson (F / LB), Interact (E / X), UseItem (ЛКМ / RT), AltUseItem (ПКМ / LT), CycleItem (колесо / d-pad ←→), HeroMenu (Tab / Y), HeroAbility (Q / B), Pause (Esc / Start) |
| **Dialogue** | разговор | Advance (Пробел, Enter, ЛКМ / A), FastForward (Ctrl / RB), Auto (Alt / Y), Skip (Backspace / Select), Navigate (W/S, стрелки / d-pad) |
| **HeroSelect** | старое окно выбора героя | Hero1–3 (1/2/3 / d-pad), Close (Esc / B) |
| **Inspect** | шкафчик, буклет, рисование на плакате | Close (Esc, E / B), Next / Previous (D, A, стрелки / d-pad, бамперы), Point, Paint (ЛКМ / A — рисовать), Cursor (L-стик — курсор кисти) |
| **Lesson** | мини-игра урока | Jump (Пробел / A), StepUp/Down/Left/Right (WASD, стрелки / d-pad, L-стик), Sit (Shift / B), Catch (E / X) |
| **Cutscene** | старая катсцена | Skip (Пробел / A, держать) |
| **Prologue** | пролог «Дело» | Next (ЛКМ, Enter / A — перевернуть страницу, на последней — печать), Skip (Пробел / Start, держать — пропустить) |

Если переназначил клавишу, поправь и подписи, которые её называют текстом:
- подсказки HUD — `Data/UI/HudSettings.asset`, список **Hints**, поля **Keyboard Label** / **Gamepad Label**;
- подсказка «E — …» — `Data/Interaction/InteractionSettings.asset`, **Interact Format** и **Use Item Format**;
- «Esc / E — закрыть» у шкафчика и буклета — **Close Hint** в их ассетах;
- подсказка пропуска катсцены — `Data/Cutscenes/Cutscene_Day1_Intro.asset`, **Skip Hint Keyboard / Gamepad**;
- подсказки пролога — `Data/Story/Prologue_Day1.asset`, **Next Hint**, **Stamp Hint**, **Skip Hint**;
- «ЛКМ / A — рисовать · Esc / E — готово» — **Draw Hint** в `Interactable_Poster` и `Interactable_Painting`.

Пауза в меню берёт действие, указанное в `Data/UI/MenuSettings.asset` → **Pause Action** (`Gameplay/Pause`).

---

## 3. Три героя — `Data/Heroes`

**`HeroSettings.asset`** — общее для партии:

| Что хочу | Поле | Сейчас |
|---|---|---|
| Кто начинает | **Start Hero** | Ryuta |
| Когда Кайто и Рэй входят в группу | **Party Unlock Flag** — пока флаг не стоит, играешь только за **Start Hero**: остальные стоят на месте, Tab не работает, в HUD одна карточка. Пусто = все трое сразу | `heroes_unlocked` (ставится после знакомства в комнате 7) |
| Как быстро переключается управление | **Switch Time**, с | 0,5 |
| Tab — следующий герой или окно выбора | **Tab Cycles Heroes** | вкл. (карусель Рюта → Рэй → Кайто) |
| Где работают Tab и Q | **Switch States**, **Ability States** | Break |
| Насколько близко ходят ведомые | **Follow Min / Max Distance**, **Follow Behind**, **Follow Side**, м | 1,5 / 3 / 2 / 0,9 |
| Скорость ведомых | **Follow Walk Speed**, **Follow Run Speed**; бегут, если дальше **Catch Up Distance** | 1,8 / 4,8; 5 м |
| Телепорт отставших | **Teleport Distance** (вне кадра), **Stuck Time** | 25 м / 1,5 с |
| NavMesh для ведомых | **Agent Radius / Height / Climb / Slope**, **Nav Mesh Layers**, **Rebuild After Interact** | 0,28 / 1,7 / 0,35 / 50; 1,2 с |
| Окно выбора (если карусель выключена) | **Title**, **Hint**, **Current Label**, цвета, **Card Size** | |

**`Hero_Ryuta / Hero_Rei / Hero_Kaito.asset`** — имя (**Display Name**, **Short Name**), портрет, модель (**Prefab**), цвет героя в HUD, **Speaker** (как он подписан в диалогах) и **Ability**.

**`Abilities/Ability_*.asset`** — способность на Q. У всех есть **Display Name**, **Description**, **Cooldown** (с), **Uses Per Break** (0 = без лимита) и реплики **Cooldown Line** / **Out Of Uses Line**.

| Способность | Ассет | Главные поля |
|---|---|---|
| «Голоса» (Рюта) | `Ability_Voices` | кулдаун 20 с, **Radius** 15 м, **Highlight Seconds** 5, вид луча и свечения, реплики **Generic Hints** / **Nothing Lines** |
| «Телефон» (Рэй) | `Ability_Phone` | 1 раз за перемену, **Rumors** → `RumorSet_Day1`, мемы раз в **Meme Interval** (45–110 с), **Meme Lines** |
| «Пинок» (Кайто) | `Ability_Kick` | кулдаун 3 с, **Reach** 1,5 м, **Cone Angle** 120°, **Witness Range** 30 м, **Noise Reason** `kick` |

По новому плану Дня 1 «Пинок под зад» (только со спины) переходит к Рюте, а подсветка полезного предмета («Голоса») — к Кайто. В данных это пока не переставлено: способность героя — поле **Ability** в `Hero_*.asset`.

Слухи Рэй — `RumorSet_Day1.asset`: в списке **Rumors** у каждого слуха диалог (`Rumors/Dialogue_Rumor_*`), условия по флагам и **Heard Flag**, который ставится после звонка.

Сколько Шума даёт пинок, решает не ассет способности, а `Data/Pranks/NoiseSettings.asset` (см. раздел 8).

Обзор учителей и NPC (кто «видит» шалость) — `Data/NPC/NpcSettings.asset`: **View Distance** 15 м, **View Angle** 120°, **Eye Height** 1,6, **Occluder Layers**. Там же анимация NPC-моделей: **Speed Damping** (сглаживание скорости ходьбы, 0,15 с), **Idle Below Speed** (0,1 м/с), **Arm Point Speed** (как быстро физрук тычет рукой, 10).

Модели NPC (физрук Тоёда, Шутник = Хиро, 5 учеников) собирает Blender-скрипт `Art/Characters/NPC/build_npcs.py`. Если поменял FBX, нажми **Tools → Funseki → NPC → Import NPC models and dress the slice NPCs**: он переимпортирует модели и переоденет капсулы-NPC в сцене и в префабе урока.

Если поменял модель в `Art/Heroes/Models`, перерисуй портреты: **Tools → Funseki → Heroes → Re-render portraits**.

---

## 4. Расписание дня и перемены — `Data/DayCycle/DaySchedule_Day1_Slice.asset`

Список **Phases** идёт сверху вниз, игра проходит фазы по порядку:

| Id | Тип | Состояние | Цель для HUD | Конец фазы |
|---|---|---|---|---|
| `prologue` | StoryScene | Cutscene | — | флаг `day1_prologue_done` (печать или пропуск) |
| `arrival` | StoryScene | Break | «Встреться с директором у входа» | флаг `day1_director_done` (директор ушёл) |
| `dorm` | StoryScene | Break | «Зайди в комнату 7», потом цель квеста | флаг `quest_q_day1_main_done` (квест знакомства пройден) |
| `break_1` | Break | Break | «Устрой шалость, пока не прозвенел звонок» | флаг `day1_break1_goal` или **120 с** |
| `lesson_fizra` | Lesson | Lesson | «Урок физкультуры» | флаг `lesson_fizra_done` |
| `break_2` | Break | Break | «Поговори с одноклассником до звонка» | флаг `day1_break2_goal` или **480 с** |

После последней фазы игра переходит в **End State** = SliceEnd (экран конца демо).

**Длина перемены** — у фазы раскрой **End** и поменяй **Max Duration** (секунды игры до звонка). Фаза кончается тем, что наступит раньше: выставлен **Goal Flag** или истекло время. **0** у перемены значит «взять **Default Break Duration**» (сейчас 40 с), у урока и катсцены — «без лимита».

Остальные поля фазы:
- **Objective** — текст цели в левом верхнем углу HUD. Пока идёт основной квест, вместо неё показывается шаг квеста (раздел 5).
- **Go To Lesson Objective** (только у урока) — цель после звонка, пока герой идёт на урок: «Звонок! Беги на физру в спортзал».
- **Lesson Id** — id урока (`fizra`); должен совпадать с id в `Lesson_Fizra.asset`.
- **Time Of Day** — время суток фазы: иконка в HUD и какие комнаты открыты (Dawn = утро, Sun = день, Sunset = вечер, см. ниже).
- **Id** — snake_case, по нему сейв знает, на какой фазе остановились. Меняя id, правь и места, где он указан: `Prank_Whistle` (**Phase Ids**), `WhistleRoutine` (**Phase Id**), `LessonWalk_Fizra` (**After Phase Id**), `Prologue_Day1` (**Phase Id**), `Day1Start` (**Arrival Phase Id**, **Chain Phase Ids**); Quick Play всегда стартует с `break_1`.

Старой фазы `intro` (катсцена «трио у ворот») в расписании больше нет: её заменили пролог и приезд. Если вернуть фазу с id `intro`, катсцена снова заиграет.

Опоздание на урок (низ ассета):
- **Late After Bell** — через сколько секунд после звонка герой считается опоздавшим и ставится флаг **Late Flag** (`lesson_late`); физрук тогда играет «опоздательное» интро. Сейчас 33 с.
- **Second Bell After** — через сколько секунд звенит второй звонок и появляется стрелка к уроку. Сейчас 33 с.

Debug в редакторе: **F9** — сразу следующая фаза, **F10** — выставить флаг цели текущей фазы.

Если в сцене нет объектов дня (DayCycle, вход на урок), нажми **Tools → Funseki → DayCycle → Setup Day 1 schedule in Slice_Day1**. Существующее расписание он не трогает.

### Комнаты открыты / закрыты по времени суток — `Data/DayCycle/RoomAccess.asset`

В списке **Rooms** по строке на каждую зону школы («Этаж 2 · Комната 7 (dorm_5)»). У каждой три галочки: **Morning** (утро), **Day** (день), **Evening** (вечер). Снята галочка — в это время суток все двери этой комнаты заперты, герой говорит **Closed Line** («Сейчас тут закрыто.»). Время суток берётся из **Time Of Day** текущей фазы расписания. Зона, которой нет в списке, открыта всегда. Замок по дням (комнаты 10–11 до 3-го дня) работает поверх галочек.

- Двери переключаются, только когда меняется время суток (утро → день → вечер), поэтому сценарные двери внутри одного времени (LessonWalk открывает спортзал) не перезапираются.
- Открытая дверь закрывается сама, если рядом нет героя ближе **Keep Open Near Hero** (3 м); иначе остаётся открытой, но закрыть её уже нельзя, так герой не застрянет внутри.
- Дверь принадлежит комнате, если стоит у её границы (**Door Touch Distance**, 0,75 м). Дверь между коридором и комнатой заперта, если закрыто хотя бы одно из двух.
- Комнату без дверей (только открытый проём) закрыть нельзя: консоль пишет об этом предупреждение `[RoomAccess]`.

Новые зоны школы добавляет **Tools → Funseki → DayCycle → Setup room access (fill rooms from the school)**: старые галочки сохраняются, новые комнаты встают открытыми.

---

## 5. Начало Дня 1: пролог, директор, общежитие, квесты — `Data/Story`, `Data/Quests`

Как это идёт в игре: «Новая игра» → на столе дело Тамуры Рюты, листаешь и ставишь печать → Рюта один у ворот школы → у входа директор сам начинает разговор → затемнение, Рюта и директор у двери комнаты 7 на 2-м этаже → директор уходит → в комнате 7 знакомство с Кайто и Рэй, они входят в группу и стартует квест «Познакомься с группой» → знакомство с комнатами 8 и 9 → перемена `break_1`.

### Пролог «Дело» — `Story/Prologue_Day1.asset`

Пролог — фаза `prologue` внутри `Slice_Day1`: экран стола поверх загружающейся школы, отдельной сцены нет.

| Что хочу | Поле |
|---|---|
| Тексты дела (сейчас «[TODO]») | **Folder Label**, **Full Name**, **Birth Date**, **Description**, **Transfer Reason** и подписи к ним (**… Label**) |
| Фото анфас и профиль | **Photo Front**, **Photo Profile** (спрайты; пусто = серая заглушка «ФОТО»), подписи **Photo Front / Profile Caption** |
| Печать | **Stamp Text** («ПЕРЕВЕДЁН — ФУНСЭКИ»), **Stamp Color**, **Stamp Angle** (−12°), **Stamp Sound** (пусто = синтезированный глухой удар), **Stamp Volume** |
| Удар печати | **Stamp Drop Time** (0,12 с), тряска **Shake Strength** (14) и **Shake Time** (0,3 с), пауза **Hold After Stamp** (1,3 с) |
| Листание и затемнения | **Flip Time** (0,3 с), **Fade Out Time** / **Black Time** / **Fade In Time** (0,8 / 0,4 / 0,8 с) |
| Пропуск | **Skip Hold Time** (держать Пробел 1 с) |
| Вид | шрифты **Title Font** / **Body Font**, цвета стола, папки, страниц, чернил, заглушки фото и подсказок |

Страниц три: фото + ФИО + дата рождения, характеристика, причина перевода с местом для печати.

### Приезд и общежитие — `Story/Day1Start.asset`

| Что хочу | Поле | Сейчас |
|---|---|---|
| С какого расстояния директор сам заговорит | **Director Talk Radius**, м | 3 |
| Диалоги | **Director Intro** (у входа), **Director Room** (у двери комнаты 7), **Room7 Meet** (Кайто и Рэй) | `Dialogue/Day1/…` |
| Скорость директора, когда он уходит | **Director Walk Speed**, м/с | 1,5 |
| Затемнение при переносе к комнате 7 | **Fade Out Time** / **Black Time** / **Fade In Time** | 0,6 / 0,5 / 0,6 с |
| Реплика Рюты у забора и как часто | **Fence Line**, **Fence Line Cooldown** | «[TODO] Забор. Дальше мне нельзя.», 5 с |
| Флаги цепочки | **Placed Flag**, **Director Met Flag**, **Director Done Flag**, **Party Flag** | `day1_start_placed`, `day1_director_met`, `day1_director_done`, `heroes_unlocked` |
| Что засчитать, если день начат позже (Quick Play в перемену) | **Skip Flags**, ставятся по порядку | все флаги цепочки и знакомств |

Места в сцене (Рюта у ворот, Кайто и Рэй в комнате 7, директор, его маршрут к лестнице, забор) — объекты внутри корня `Day1_Start` в `Slice_Day1`. Их можно двигать руками, но **Setup Day 1 start** вернёт их на места из кода.

Комнаты общежития на 2-м этаже пронумерованы от лестницы СВ на запад: 7, 8, 9, 10, 11 (зоны `dorm_5` … `dorm_1`). В 7 живут герои, в 8 — Такеши, Юкки, Масуми, в 9 — Хиро и Цубаки. Комнаты 10 и 11 заперты до дня 3 («Заперто. Тут нужна отмычка»): это `LockedDoor` на двери в `School_Greybox`, день — **Unlock Day**, реплика — **Locked Line** у `Door` и `LockedDoor`. В комнате 7 стоят пустые полка для фигурок (`CollectionShelf`, 6 мест) и стена трофеев (`TrophyWall`, 14 мест): их заполнят будущие задачи.

Одноклассники — объекты `Day1_Start/Classmates/NPC_*`. У каждого компонент `DialogueNpc`: **Dialogue** (его разговор), **Prompt** («Познакомиться»), **Talked Flag** (ставится после разговора, например `day1_met_takeshi`).

### Квесты — `Quests/QuestSettings.asset`, `Quests/Q_*.asset`

**`QuestSettings.asset`** — список **Quests** (квест, которого нет в списке, никогда не стартует) и **Autosave On Progress** (автосейв при старте квеста и на каждом шаге, вкл.).

**`Q_Day1_Main.asset`** и любой новый квест (**Create → Funseki → Quests → Quest**):

| Поле | Что это | У Q_Day1_Main |
|---|---|---|
| **Id** | уникальный id, по нему сейв | `Q_Day1_Main` |
| **Title** | название для Дневника | «Познакомься с группой» |
| **Kind** | Main (основной: его шаг — цель в HUD) или Side | Main |
| **Start Flag** | флаг, по которому квест стартует сам | `heroes_unlocked` |
| **Done Flag** | флаг, который ставится в конце; пусто = `quest_<id>_done` | пусто → `quest_q_day1_main_done` |
| **Steps** | шаги сверху вниз | 2 шага |

У шага: **Objective** (текст цели), **Done Flags** (шаг закрыт, когда стоят **все** флаги), награда — **Reward Flags** и **Reward Items** (предметы падают в общий инвентарь).

Шаги Q_Day1_Main: «Познакомься с соседями из комнаты 8» (`day1_met_takeshi`, `day1_met_yukki`, `day1_met_masumi`) → «Познакомься с соседями из комнаты 9» (`day1_met_hiro`, `day1_met_tsubaki`).

Если в сцене пропали объекты начала дня: **Tools → Funseki → Day1 → Setup Day 1 start (prologue → dorm)**. Данные (диалоги, квест, `Prologue_Day1`, `Day1Start`, спикеры) он создаёт только один раз, а `Day1_Start` в Slice_Day1, `Quests` под `[Bootstrap]` и кровати общежития пересобирает.

Проверка всей цепочки без рук: **Tools → Funseki → Day1 → Smoke Test Day 1 start** (в консоли `[Day1StartSmokeTest] PASS / FAIL`; пишет свой сейв поверх обычного). **Play to the prologue (New Game, no skip)** — новая игра и остановка на экране дела.

---

## 6. Звонок — `Data/Audio/BellSettings.asset`

Три звонка, у каждого **Clip**, **Volume** (0…1), **Pitch** и **Max Length** (сколько секунд клипа играть, 0 = целиком):

| Звонок | Поле | Сейчас |
|---|---|---|
| Перемена кончилась, на урок | **Lesson Start** | `BellRing.mp3`, 0,8, высота 1 |
| Повторный, герой ещё не на уроке | **Second** | тот же клип, высота 1,05 |
| Урок кончился | **Lesson End** | тот же клип, 0,8, высота 1 |

Пустой **Clip** — звонок без звука (событие при этом всё равно происходит). Свои звуки клади в `Assets/_Project/Audio/SFX` и перетаскивай в поле.

Если звонка не слышно вовсе, проверь, что под `[Bootstrap]` есть объект BellSound: **Tools → Funseki → Audio → Add bell sound to Bootstrap**.

---

## 7. Уроки и Физра — `Data/Lessons`

### Общий урок — `Lesson_Fizra.asset`

- **Id** `fizra`, **Title**, **Lead Hero** (кто ведёт урок), **Mini Game Prefab** (`Prefabs/Lessons/Lesson_Fizra`).
- **Intro** и **Late Intro** — реплики до начала (обычное и при опоздании). У реплики **Speaker** — роль (Teacher, Helper, …), **Text**, **Duration** в секундах.
- Пороги исходов: доля правильных ответов **Excellent From** (0,9) и **Normal From** (0,5); ниже — «Позорно».
- **Outcomes** — сцены исходов сверху вниз, срабатывает первая подходящая. У каждой: **Outcome** (Excellent / Normal / Shame), **Require Flags** / **Forbid Flags**, **Caption**, **Lines**, **Timeline** (необязательно), **Min Duration**, **Set Flags** (последствия). Пример: «Блестяще без свистка» требует `prank_whistle_done` и ставит `fizra_day1_voice_heard`; «Позорно» ставит `fizra_teacher_angry`.
- **Bell Delay** — пауза перед звонком на перемену после исхода.

### Мини-игра «Свисток» — `Lessons/Fizra`

**`FizraSettings.asset`:**
- **Rounds** — какие раунды и в каком порядке (`FizraRound_1…3`). Для «Контрольной» есть готовый `FizraRound_Exam3in10`.
- **Round Pause** (2,5 с), **New Command Demo** (2,5 с) — паузы между раундами и показ нового свистка.
- Режим «без свистка» (если свисток украли): **No Whistle Flag**, **No Whistle Intro**, **Helper Wrong Chance** (Шутник ошибается в переводе, 0,2), **Helper Translate Formats**.
- Ошибки учеников-массовки: **Student Error Chance** (0,15) и их реплики.
- Подписи «Есть!» / «Мимо!» / «Проспал!», **Show Score**, реплики героя при ошибке.
- Заглушечная анимация (высота прыжка, шаг, приседание) и синтезированные звуки (**Whistle Volume**, **Whistle Hz**, мелодия гимна).
- **Win Round Key** / **Lose Round Key** — debug-клавиши F7 / F8.

**`Rounds/FizraRound_*.asset`** — сложность раунда:

| Поле | Что это | Раунд 1 |
|---|---|---|
| **Commands** | какие свистки могут прозвучать | 4 команды |
| **New Command** | свисток, который раунд показывает перед стартом | |
| **Min / Max Commands** | сколько команд в раунде | 6–8 |
| **Reaction Window** | секунд на ответ | 1,5 |
| **Gap** | пауза между командами (меньше = быстрее) | 1,3 |
| **Total Time** | если > 0, все команды укладываются в это время («3 за 10 с») | 0 |
| **Show Captions** | подпись-перевод под пиктограммой | вкл. |
| **False Commands**, **False Count** | «ложные» свистки вроде чайника, которые надо игнорировать | нет |

**`Commands/Whistle_*.asset`** — сами свистки: **Caption** («Прыгай!»), **Glyph** (текстовая пиктограмма, пока нет иконки), **Icon**, **Whistle Clip** (пусто = синтез по **Synth Pattern**: `s` короткий, `l` длинный, `t` трель, `x` резкий, `k` чайник, `_` пауза), **Input Action** (какое действие карты Lesson нажимать; пусто = замереть), **Directional**, **Window** (0 = окно раунда), **Is False**.

### Проход в спортзал — `LessonWalk_Fizra.asset`

После звонка **After Phase Id** (`break_1`) физрук и ученики двора идут в спортзал: **Start Delay** 1,5 с, **Speed** 1,7 м/с, **Stand Spacing**, **Door Radius**, реплики **Teacher Start Lines** и **Student Lines** (с шансом **Student Line Chance**). Точки маршрута стоят в сцене (объект `Walk_fizra`).

Debug: **F7** — выиграть раунд, **F8** — проиграть.

Если поменял префаб урока или точки сцены, пересобери: **Tools → Funseki → Lessons → Setup Fizra lesson in Slice_Day1** (данные не трогает, пересобирает префаб `Lesson_Fizra` и корень `Lessons`).

---

## 8. Шалости и шкала Шума — `Data/Pranks`

### Шум — `NoiseSettings.asset`

| Что хочу | Поле | Сейчас |
|---|---|---|
| Максимум шкалы («Поймали») | **Max** | 100 |
| Когда шкала становится «Подозрительно» | **Suspicious At** | 40 |
| Сколько Шума за действие | **Amounts**: **Reason** → **Amount** | `kick` 15, `poster_drawn` 25 |
| Как быстро остывает, пока учитель не видит | **Decay Per Second**, **Decay Delay** | 5 в с, через 1 с |
| Остывание в укрытии (туалеты, шкафчики для обуви) | **Hide Zone Multiplier** | ×3 |
| Где шкала работает | **Active States** | Break |
| Как ищутся учителя-свидетели | **Watcher Search Radius**, **Visibility Check Interval**, **Hero Chest Height** | 30 м / 0,2 с / 1,2 |
| Сцена «Поймали» по умолчанию | **Default Caught Scene** | `CaughtScene_Buckets` |

Причины, которых нет в **Amounts**, добавляют столько, сколько прислал источник (например, шалость — своё **Noise When Seen**).

### Шалость «Украсть свисток» — `Whistle/Prank_Whistle.asset`

- **Title** и **Journal Caption** — запись в «Журнале недели» («Свистать всех наверх»).
- **Prompt** — подсказка на приманке («Взять свисток»). **Required Item** — если задан, шалость делается этим предметом (ЛКМ), а не E.
- Условия: **Heroes** (пусто = любой), **Phase Ids** (`break_1`), **Required Flags**, **Blocked By Flags**.
- Риск: **Watch Radius** (20 м), **Noise When Seen**, **Seen Means Caught** (вкл.: увидел учитель — сразу «Поймали»), **Caught Scene** (`CaughtScene_WhistleLaps`).
- Результат: **Done Flag** (`prank_whistle_done`), **Consequence Flags**, **Flags After Reaction** (`day1_break1_goal` — цель перемены, ставится после реакции мира, чтобы звонок её не обрезал), **Counters** (`pranks_done`), **Reward Item** (свисток), **Success Lines**, **Reaction Timeline**, **Reaction Radius**.

### Распорядок физрука — `Whistle/WhistleRoutine.asset`

| Поле | Что это | Сейчас |
|---|---|---|
| **First Trip Delay** | первый поход к автомату после начала перемены | 8 с |
| **Repeat Every** | как часто ходит | 90 с |
| **Fight Duration** | сколько воюет с автоматом (окно для кражи) | 20 с |
| **Glance Every Min / Max**, **Glance Duration** | как часто и как долго оглядывается на скамейку | 3–5 с, 1,2 с |
| **Distract Duration** | на сколько отворачивается после пинка Кайто по автомату | 5 с |
| **Walk Speed**, **Turn Speed** | ходьба | 1,8 / 300 |
| **Go Lines**, **Fight Lines** (раз в **Fight Line Every**), **Distract Lines**, **Give Up Lines** | реплики | |

### «Поймали» — `CaughtScene_Buckets.asset`, `CaughtScene_WhistleLaps.asset`

**Kind** — Buckets (вёдра в коридоре) или Laps (круги вокруг свидетеля). Дальше: реплики свидетеля и героя, **Before Punishment**, **Fade Time**, **Punishment Time**, табличка **Sign Text** («Через 10 минут…») и **Sign Time**, для кругов **Lap Radius** / **Lap Speed** / **Lap Witness Lines**, и **Time To Bell** — сколько секунд остаётся до звонка после наказания (60).

Если поменял расстановку шалости в сцене, пересобери: **Tools → Funseki → Pranks → Setup pranks in Slice_Day1** (пересобирает корень `Pranks`, данные не трогает).

---

## 9. Интерактивные предметы и инвентарь

### Поиск цели — `Data/Interaction/InteractionSettings.asset`

**Radius** 2 м и **Cone Angle** 90° — как далеко и в каком конусе перед героем ловится предмет. Там же **Interact Format** («E — {0}»), **Use Item Format** («ЛКМ — {0}»), шрифт и цвета подсказки, **Highlight Material** (подсветка) и **Active States** (Break).

### Семь видов предметов — `Data/Interaction/Interactables`

Общие поля у всех:
- **Prompt** — текст подсказки.
- **Repeat** — Once (один раз за игру), OncePerDay, Always. Сейчас у всех Always.
- **Allowed Heroes** — кому предмет даёт E (пусто = всем), **Only On Days** — в какие дни работает (пусто = всегда).
- **First Lines** / **Repeat Lines** — реплики героя в первый раз и потом; **Hero Lines** — свои реплики для конкретного героя (пустые берут общие).
- **Teacher Nearby Lines** и **Teacher Radius** (12 м) — реплика, если рядом учитель.
- **Noise Amount** / **Noise Reason** — Шум при использовании.
- **Required Flag**, **Blocked By Flag**, **Flags On First Use**, **Counters On Use**.

Свои поля:

| Предмет | Ассет | Что крутить |
|---|---|---|
| Кран | `Interactable_WaterTap` | **Open / Close Prompt**, **Dirty Duration** (2 с бурой воды), цвета воды, **Milestone Every** (реплика каждые N открытий), **Teacher Shout Lines** (учитель кричит «Не трать воду!» над собой) |
| Автомат | `Interactable_VendingMachine` | **Jam Chance** (0,05 — заклинило и сыплет банки), **Break Chance** (0,2 — сломался), банки (**Can Size / Color / Mass / Lifetime**, **Jam Can Count** 50), реплики заклинивания и поломки, **Broken Prompt** |
| Шкафчик Рюты | `Interactable_RyutaLocker` | **Captions** (подписи вещей при наведении), **Door Open Angle**, **Door Time**, **Close Hint** |
| Плакат и картина | `Interactable_Poster`, `Interactable_Painting` | Рисование прямо по картинке: кто (**Draw Heroes**, сейчас Рюта и Кайто), чем (**Draw Item** — маркер, достаточно иметь в инвентаре), **Draw Prompt**, **Draw Hint**, **Ink Color**, **Brush Radius** (4 px), **Cursor Speed** (курсор стиком), **Min Painted Pixels** (меньше — не считается). Реплики **Draw Lines** / **Hero Draw Lines** / **Draw Seen Lines**. Если учитель видит — **Seen Means Caught** (вкл.: сразу «Поймали», сцена **Caught Scene**) или Шум **Draw Noise Amount**. **Drawn Textures** — что показывать после загрузки сейва вместо рисунка. **Drawn Flag** `poster_drawn`. Каждую картинку можно разрисовать один раз |
| Стойка с буклетами | `Interactable_Booklet` | **Pages** (заголовок, картинка, текст), подписи кнопок, размеры и цвета «бумаги»; кто и когда — **Allowed Heroes**, **Repeat**, **Only On Days** |
| Душ | `Interactable_Shower` | E включает и выключает воду (**On / Off Prompt**, **Water Color**). Рюта или Кайто с «Краской» в руке заливают её в лейку (**Paint Item**, **Paint Heroes**, **Paint Prompt**, **Consume Item**), дальше душ льёт **Paint Color**. Реплики **Paint Lines**, **Painted Water Lines**, **Paint Seen Lines**; Шум при учителе **Paint Noise Amount** (25), флаг **Painted Flag** `shower_paint_ready` |

Где стоят предметы — решают два сборщика:
- **Tools → Funseki → Interaction → Place slice interactables in Slice_Day1** пересобирает **весь** корень `Interactables`;
- **Add spec items to Slice_Day1 (keeps the rest)** пересобирает только `Interactables/Spec`: ещё 3 крана, 6 плакатов, 4 картины, душ в туалете М 1-го этажа (временно, по плану душевые на 2-м этаже) и «Краску» у кабинета рисования.

Если двигал предметы руками, сборщик вернёт их на места из кода. Чтобы сдвинуть предмет навсегда, после сборщика ставь его заново или попроси поправить координаты в сборщике.

### Инвентарь — `Data/Inventory`

`InventorySettings.asset`: **Capacity** (8), **Select Picked Item** (взятый предмет сразу в руку), реплики **Wont Work Line** («Не сработает»), **Full Line**, **Pickup Prompt Format**, вид панели (размер слотов, цвета, **Panel Hide Delay**).

Предметы — `Items/Item_*.asset` (маркер, отвёртка, свисток, краска): **Id**, **Display Name**, **Icon**, **Description**, модель в руке (**Hand Prefab**, позиция, поворот, масштаб). Новый предмет — **Create → Funseki → Items → Item**.

---

## 10. Диалоги и реплики — `Data/Dialogue`

### Разговор — `Dialogue_*.asset` (DialogueGraph)

Список **Nodes** сверху вниз. У узла:
- **Id** — на него ссылаются переходы; **Speaker** — кто говорит (`Speakers/Speaker_*`); **Text**.
- **Conditions** — узел пропускается, если хоть одно не выполнено: FlagIsSet, FlagIsNotSet, CounterAtLeast, CounterBelow, HeroIs, HeroIsNot.
- **Actions** — что делает показ узла: SetFlag, ClearFlag, AddToCounter, GiveItem.
- **Choices** — до 3 обычных вариантов и 1 с галочкой **Voice** («Голос», виден только за Кайто). У варианта свои условия, действия, **Next** и **End**.
- **Next** — id следующего узла; пусто = следующий в списке. **End** — разговор заканчивается.

Образец со всеми приёмами — `Dialogue_Test.asset`. Проверять удобно в тестовой сцене: **Tools → Funseki → Dialogue → Build Dialogue_Test scene**, Play прямо из неё.

Диалоги начала Дня 1 — папка `Day1/`, все реплики «[TODO]»: `Day1_Director_Intro`, `Day1_Director_Room`, `Day1_Room7_Meet`, `Day1_Meet_Takeshi`, `Day1_Meet_Yukki`, `Day1_Meet_Masumi`, `Day1_Meet_Hiro`, `Day1_Meet_Tsubaki`. Реплики старой катсцены — `Cutscene_Day1_Intro.asset` (сейчас не играет).

### Говорящие — `Speakers/Speaker_*.asset`

**Display Name**, **Portrait**, **Name Color**, звуки «бормотания» (**Mumble Clips**, **Mumble Pitch**, **Mumble Volume**). Есть трое героев, директор (`Speaker_Director`), одноклассники (`Speaker_Takeshi`, `_Yukki`, `_Masumi`, `_Hiro`, `_Tsubaki`) и тестовые.

### Общие настройки — `DialogueSettings.asset`

- Скорость текста: **Chars Per Second** (50), **Fast Multiplier** (×5 при удержании Ctrl), **Auto Delay** (5 с в режиме «Авто»). Игрок может ещё умножить скорость в «Настройках».
- **Fallback Hero** (Kaito) — чей голос в сценах без трёх героев.
- Камера разговора: **Camera Blend Time**, **Camera Side Distance**, **Camera Height**, **Camera Fov Range**, **Npc Faces Hero**.
- Вид окна: шрифт, размеры, цвета облака, портрета, кнопок и вариантов, **Voice Prefix** («Голос: »).
- Пузыри-реплики над головами: **Bark Min / Max Duration** (2–3 с), **Bark Seconds Per Char**, **Bark Max Distance** (12 м от активного героя), **Bark Hide Behind Walls** (прятать пузырь за стеной, вкл.) и **Bark Occlusion Mask**, цвета.

### Короткие реплики — `BarkSet_*.asset`

Наборы фраз, которые NPC говорят при приближении (`BarkTrigger`) или ученики во дворе обсуждают шалость (`BarkSet_WhistleGossip`). Запасной вид реплик героя без сервиса диалогов — `Data/UI/BarkSettings.asset`.

---

## 11. HUD, меню и катсцена — `Data/UI`, `Data/Cutscenes`

### HUD

- **`HudTheme.asset`** — внешний вид: **Reference Resolution**, **Margin**, шрифты и размеры (**Day Font Size**, **Objective Font Size**…), цвета текста и панелей, иконки времени суток, портреты партии и кулдаун Q, мегафон Шума (иконка, цвета «Тихо / Подозрительно / Поймали», размер полоски), вид клавиш в подсказках, иконка журнала. Пустая иконка = нарисованная заглушка.
- **`HudSettings.asset`** — поведение: **Visible States** (Break), **Keep States** (Paused — HUD не прячется), **Day Format** («День {0}»), анимация смены цели, когда прятать шкалу Шума, всплывашка **Journal Toast Title** / **Journal Toast Time**.
- Подсказки управления — список **Hints**: **Trigger** (BreakTime — через **Delay** после начала перемены; InteractionTarget — когда впервые есть цель; ItemAdded; HeroSwitched), **Action**, **Text**, **Keyboard Label** / **Gamepad Label**, **Skip If Already Used** (не показывать, если игрок уже нажимал). Каждая подсказка показывается один раз за игру.

Если HUD пропал из сцены: **Tools → Funseki → UI → Setup HUD in Slice_Day1**.

### Меню паузы и конец демо — `MenuSettings.asset`

- **Pausable States** — где Esc открывает паузу (Break, Lesson, Cutscene, Caught).
- Все тексты кнопок и заголовков, размеры кнопок и шрифтов, цвет затемнения.
- Экран конца демо: **End Title**, **End Subtitle**, подписи исходов физры, **End Journal Total** (сколько записей журнала возможно в срезе, сейчас 1), **Survey Url** — ссылка на анкету. **Пока пусто, и кнопка «Анкета» спрятана**: вставь ссылку перед плейтестом.

### Старая вступительная катсцена — `Cutscenes/Cutscene_Day1_Intro.asset`

Сейчас не играет: фазы `intro` в расписании нет, День 1 начинается с пролога (раздел 5). Ассет и объект `Cutscene_Intro` оставлены на случай, если катсцену захочется вернуть.

**Timeline** (`Cutscene_Day1_Intro.playable`), **Dialogue** и **Dialogue At** (секунда Timeline, на которой он ждёт диалог, 37), титр **Title** / **Subtitle**, пропуск (**Skip Hold Time** 1 с, подсказки), затемнения **Fade In / Fade Out / Fade Back In**. Саму постановку правят в окне Timeline.

Если меню или катсцена пропали из сцен: **Tools → Funseki → Slice → Setup slice flow (menus, autosave, intro cutscene)**. Данные и Timeline он создаёт только один раз, а объекты `Menus` и `Cutscene_Intro` пересобирает.

---

## 12. Сохранение — `Data/Save/SaveSettings.asset`

- **File Name** — имя файла сейва в папке persistentDataPath (`funseki_save.json`).
- **Autosave** — выключатель автосейва (вкл.). Автосейв пишет в начале каждой фазы, кроме урока, перед уроком, после шалости, при старте квеста и на каждом его шаге. Квесты лежат в сейве вместе с остальным.
- **Debug Keys** — **F5** сохранить, **F6** «Продолжить» из сейва. Клавиши — **Quick Save Key** / **Quick Load Key**.

Чтобы начать с чистого листа, нажми «Новая игра» в главном меню: она сбрасывает флаги, инвентарь, журнал и квесты. Quick Play автосейв не пишет, а **Smoke Test Day 1 start** пишет свой сейв поверх обычного.

---

## 13. Школа — `Assets/_Project/School`

Подробно — в [School_Guide.md](../Assets/_Project/School/Docs/School_Guide.md). Коротко:

- **Школа заморожена: её правят руками.** Открой `School/Scenes/School_Greybox.unity` (или **Tools → Funseki → Quick Play → Edit Slice + School Together**, чтобы видеть школу и объекты `Slice_Day1` вместе), двигай и удаляй стены, двери, мебель, добавляй свои ассеты в `School_Props` и сохрани сцену. Slice_Day1 подгружает её при старте.
- **Build School** замороженную школу не пересобирает (на корне `School` компонент `SchoolFrozen`). **Unfreeze School** снимает заморозку, но тогда следующая сборка **сотрёт все ручные правки** внутри `School`. Без нужды не нажимай.
- **`School/Kit/KitSettings.asset`** — форма модулей (высота окон, цвета…). После правки **Tools → Funseki → Generate Kit**: модули в сцене обновятся на своих местах, но позиции не сдвинутся.
- **`School/Data/SchoolLayout.asset`** — план из зон. Пока школа заморожена, правки плана в сцену не попадают; **Unlock Day** у дверей меняется прямо на компоненте `LockedDoor` в сцене.
- **Reset School Layout To Plan** заменяет весь `SchoolLayout` исходным планом и **стирает все правки зон**.
- Общежитие (комнаты 7–11, кровати, замки на 10 и 11) описано в разделе 5; кровати лежат в `School_Props/Dorm_Day1`, старые двухъярусные выключены, а не удалены.

### Стиль школы — `Data/Visual/SchoolPalette.asset`

Палитра с визуальной доски (8 цветов) и цвета поверхностей: стены (**Plaster**, **Wainscot**, плитка, панели спортзала, цоколь), полы по типам зон (коридор, класс, спортзал, мокрые зоны, ковёр, двор), потолок, двери и окна, мебель, а ещё плотность текстур, шум и point-фильтр.

| Что нажать | Когда |
|---|---|
| **Tools → Funseki → School Style → Apply Palette and Textures to School** | первый раз или после перестройки школы: печёт текстуры и материалы и назначает их на месте |
| **Rebake Textures and Materials Only** | поменял цвета в палитре |
| **Revert School to Greybox Look** | вернуть серую коробку (из `Art/School/SchoolStyleBackup.json`) |
| **Capture Before / After / Style Shots** | кадры для сравнения в `Docs/SchoolStyle` |

Эту папку ведёт отдельный тред школы. Правь числа в ассетах сколько угодно, но о правках скриптов сборщика лучше договориться там.

---

## 14. Сборка Slice_Day1 и запуск

| Что нужно | Меню |
|---|---|
| Запустить игру | **Tools → Funseki → Core → Play From Bootstrap** |
| Сразу в перемену / в точку из Scene View | **Quick Play → Play Break** (Ctrl+Alt+P) / **Play Break Here (from Scene View)** (Ctrl+Alt+Shift+P) |
| Открыть Slice_Day1 и школу вместе для правки | **Quick Play → Edit Slice + School Together** |
| Проверить, что меню → «Новая игра» → Slice_Day1 работает | **Tools → Funseki → Core → Smoke Test Flow** (в консоли `[CoreFlowSmokeTest] PASS/FAIL`) |
| Проверить пролог → директор → общежитие → квест → сейв | **Day1 → Smoke Test Day 1 start** (`[Day1StartSmokeTest] PASS/FAIL`) |
| Вернуть пролог, директора, забор, общежитие, квесты | **Day1 → Setup Day 1 start (prologue → dorm)** |
| Пересобрать героев, камеры, SchoolLoader | **Tools → Funseki → Slice → Build Slice_Day1 (player + school loader)** (то же, что **Heroes → Build Slice_Day1 with the three heroes**) |
| Вернуть меню, автосейв, катсцену | **Slice → Setup slice flow (menus, autosave, intro cutscene)** |
| Вернуть объекты дня | **DayCycle → Setup Day 1 schedule in Slice_Day1** |
| Расставить интерактивные предметы | **Interaction → Place slice interactables in Slice_Day1** (все) или **Add spec items to Slice_Day1 (keeps the rest)** (только краны, плакаты, картины, душ, краска) |
| Переодеть NPC в модели | **NPC → Import NPC models and dress the slice NPCs** |
| Покрасить школу по палитре | **School Style → Apply Palette and Textures to School** |
| Вернуть шалость | **Pranks → Setup pranks in Slice_Day1** |
| Вернуть урок физры | **Lessons → Setup Fizra lesson in Slice_Day1** |
| Вернуть HUD | **UI → Setup HUD in Slice_Day1** |
| Сервисы в Bootstrap | **Save → Add inventory and save services to Bootstrap**, **Dialogue → Add dialogue services to Bootstrap**, **Audio → Add bell sound to Bootstrap**; квесты ставит **Day1 → Setup Day 1 start** |
| Пересоздать недостающие сцены ядра и Build Settings | **Core → Create Core Scenes** (существующие файлы не трогает) |
| Найти потерянные ссылки после переноса ассетов | **Tools → Funseki → Validate References** |

Стартовая сцена и состояние каждой сцены — `Data/GameFlowConfig.asset` (**First Scene** = MainMenu; MainMenu стартует в MainMenu, Slice_Day1 в Cutscene — в нём сразу идёт пролог). Обычно его трогать не нужно.

Каждый пункт пересобирает только свои объекты: **Build Slice_Day1** удаляет и строит заново героев, камеры, свет и SchoolLoader, а предметы, шалость, урок, HUD и меню не трогает. Поэтому после правки данных ничего пересобирать не нужно, а после поломки сцены жми только пункт того, что пропало.

---

## 15. Debug-клавиши в редакторе

| Клавиша | Что делает | Где настраивается |
|---|---|---|
| F1 | показать отладочную подпись состояния игры | — |
| F5 / F6 | сохранить / «Продолжить» из сейва | `SaveSettings` |
| F7 / F8 | выиграть / проиграть раунд физры | `FizraSettings` |
| F9 | следующая фаза дня | — |
| F10 | выставить флаг цели текущей фазы | — |
| Ctrl+Alt+P | Quick Play: сразу в перемену | меню **Tools → Funseki → Quick Play** |
| Ctrl+Alt+Shift+P | Quick Play в точку, куда смотрит Scene View | там же |

F9 и F10 работают и в начале дня: F9 в прологе сразу ведёт к приезду, а в фазе `arrival` — к общежитию (цепочку директора лучше проходить руками, F9 её не доигрывает).

---

## 16. Чего не делать

- Не переименовывай и не переноси ассеты из `Data/` мышкой, если не уверен: сборщики ищут их по пути. Если всё-таки перенёс, запусти **Validate References** и скажи об этом в проекте, чтобы поправили пути в сборщиках.
- Не меняй **Id** фаз, шалостей, уроков и флагов без нужды: на них завязаны сейв, условия диалогов и исходы уроков. Если поменял, ищи старое имя во всех ассетах `Data/`.
- Не правь руками объекты внутри корней, которые строят сборщики (`Heroes`, `Interactables`, `Pranks`, `Lessons`, `HUD`, `Menus`, `Cutscene_Intro`, `Day1_Start`, `School_Props/Dorm_Day1`, `[Bootstrap]/Quests`): следующая пересборка это сотрёт.
- **Unfreeze School** и **Reset School Layout To Plan** стирают ручные правки школы и плана.
- Не меняй **Id** квеста и флаги шагов, если квест уже есть в чьём-то сейве: старый сейв его не узнает.
- Не запускай игру обычной кнопкой Play из `Slice_Day1`: только **Play From Bootstrap**.
