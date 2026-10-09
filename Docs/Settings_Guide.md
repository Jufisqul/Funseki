# Гайд: где что настраивать

Почти всё, что можно крутить в срезе Дня 1, лежит в ассетах `Assets/_Project/Data`. Код трогать не нужно: выдели ассет в Project, поменяй поле в Inspector и сохрани проект (**Ctrl+S** или File → Save Project). Изменение подхватится при следующем запуске.

Названия полей ниже даны так, как их показывает Inspector (**Max Duration**). Числа в скобках «сейчас» — значения в ассетах на момент написания гайда.

## Как это устроено

- **Данные** (ScriptableObject в `Data/`) — числа, тексты, списки. Правишь и запускаешь, больше ничего нажимать не надо.
- **Сборщики** в меню **Tools → Funseki** — пересобирают объекты в сценах (`Slice_Day1`, `Bootstrap`, `School_Greybox`). Нужны, только если поменялось устройство сцены, а не числа. Каждый сборщик удаляет свой корневой объект и строит его заново, поэтому **ручные правки внутри этих корней пропадают** при пересборке. Данные сборщики не трогают: ассеты создаются один раз и дальше остаются твоими.
- **Запуск** — всегда **Tools → Funseki → Core → Play From Bootstrap**. Обычная кнопка Play из `Slice_Day1` запустит сцену без сервисов (сейв, диалоги, меню), и игра поведёт себя странно.

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
| **Inspect** | шкафчик, буклет | Close (Esc, E / B), Next / Previous (D, A, стрелки / d-pad, бамперы), Point |
| **Lesson** | мини-игра урока | Jump (Пробел / A), StepUp/Down/Left/Right (WASD, стрелки / d-pad, L-стик), Sit (Shift / B), Catch (E / X) |
| **Cutscene** | катсцена | Skip (Пробел / A, держать) |

Если переназначил клавишу, поправь и подписи, которые её называют текстом:
- подсказки HUD — `Data/UI/HudSettings.asset`, список **Hints**, поля **Keyboard Label** / **Gamepad Label**;
- подсказка «E — …» — `Data/Interaction/InteractionSettings.asset`, **Interact Format** и **Use Item Format**;
- «Esc / E — закрыть» у шкафчика и буклета — **Close Hint** в их ассетах;
- подсказка пропуска катсцены — `Data/Cutscenes/Cutscene_Day1_Intro.asset`, **Skip Hint Keyboard / Gamepad**.

Пауза в меню берёт действие, указанное в `Data/UI/MenuSettings.asset` → **Pause Action** (`Gameplay/Pause`).

---

## 3. Три героя — `Data/Heroes`

**`HeroSettings.asset`** — общее для партии:

| Что хочу | Поле | Сейчас |
|---|---|---|
| Кто начинает | **Start Hero** | Ryuta |
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

Слухи Рэй — `RumorSet_Day1.asset`: в списке **Rumors** у каждого слуха диалог (`Rumors/Dialogue_Rumor_*`), условия по флагам и **Heard Flag**, который ставится после звонка.

Сколько Шума даёт пинок, решает не ассет способности, а `Data/Pranks/NoiseSettings.asset` (см. раздел 7).

Обзор учителей и NPC (кто «видит» шалость) — `Data/NPC/NpcSettings.asset`: **View Distance** 15 м, **View Angle** 120°, **Eye Height** 1,6, **Occluder Layers**.

Если поменял модель в `Art/Heroes/Models`, перерисуй портреты: **Tools → Funseki → Heroes → Re-render portraits**.

---

## 4. Расписание дня и перемены — `Data/DayCycle/DaySchedule_Day1_Slice.asset`

Список **Phases** идёт сверху вниз, игра проходит фазы по порядку:

| Id | Тип | Состояние | Цель для HUD | Конец фазы |
|---|---|---|---|---|
| `intro` | StoryScene | Cutscene | — | флаг `day1_intro_done` |
| `break_1` | Break | Break | «Устрой шалость, пока не прозвенел звонок» | флаг `day1_break1_goal` или **300 с** |
| `lesson_fizra` | Lesson | Lesson | «Урок физкультуры» | флаг `lesson_fizra_done` |
| `break_2` | Break | Break | «Поговори с одноклассником до звонка» | флаг `day1_break2_goal` или **480 с** |

После последней фазы игра переходит в **End State** = SliceEnd (экран конца демо).

**Длина перемены** — у фазы раскрой **End** и поменяй **Max Duration** (секунды игры до звонка). Фаза кончается тем, что наступит раньше: выставлен **Goal Flag** или истекло время. **0** у перемены значит «взять **Default Break Duration**» (сейчас 40 с), у урока и катсцены — «без лимита».

Остальные поля фазы:
- **Objective** — текст цели в левом верхнем углу HUD.
- **Go To Lesson Objective** (только у урока) — цель после звонка, пока герой идёт на урок: «Звонок! Беги на физру в спортзал».
- **Lesson Id** — id урока (`fizra`); должен совпадать с id в `Lesson_Fizra.asset`.
- **Time Of Day** — время суток фазы: иконка в HUD и какие комнаты открыты (Dawn = утро, Sun = день, Sunset = вечер, см. ниже).
- **Id** — snake_case, по нему сейв знает, на какой фазе остановились. Меняя id, правь и места, где он указан: `Prank_Whistle` (**Phase Ids**), `WhistleRoutine` (**Phase Id**), `LessonWalk_Fizra` (**After Phase Id**), `Cutscene_Day1_Intro` (**Phase Id**).

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

## 5. Звонок — `Data/Audio/BellSettings.asset`

Три звонка, у каждого **Clip**, **Volume** (0…1), **Pitch** и **Max Length** (сколько секунд клипа играть, 0 = целиком):

| Звонок | Поле | Сейчас |
|---|---|---|
| Перемена кончилась, на урок | **Lesson Start** | `BellRing.mp3`, 0,8, высота 1 |
| Повторный, герой ещё не на уроке | **Second** | тот же клип, высота 1,05 |
| Урок кончился | **Lesson End** | тот же клип, 0,8, высота 1 |

Пустой **Clip** — звонок без звука (событие при этом всё равно происходит). Свои звуки клади в `Assets/_Project/Audio/SFX` и перетаскивай в поле.

Если звонка не слышно вовсе, проверь, что под `[Bootstrap]` есть объект BellSound: **Tools → Funseki → Audio → Add bell sound to Bootstrap**.

---

## 6. Уроки и Физра — `Data/Lessons`

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

## 7. Шалости и шкала Шума — `Data/Pranks`

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

## 8. Интерактивные предметы и инвентарь

### Поиск цели — `Data/Interaction/InteractionSettings.asset`

**Radius** 2 м и **Cone Angle** 90° — как далеко и в каком конусе перед героем ловится предмет. Там же **Interact Format** («E — {0}»), **Use Item Format** («ЛКМ — {0}»), шрифт и цвета подсказки, **Highlight Material** (подсветка) и **Active States** (Break).

### Пять предметов — `Data/Interaction/Interactables`

Общие поля у всех:
- **Prompt** — текст подсказки.
- **Repeat** — Once (один раз за игру), OncePerDay, Always. Сейчас у всех Always.
- **First Lines** / **Repeat Lines** — реплики героя в первый раз и потом; **Hero Lines** — свои реплики для конкретного героя (пустые берут общие).
- **Teacher Nearby Lines** и **Teacher Radius** (12 м) — реплика, если рядом учитель.
- **Noise Amount** / **Noise Reason** — Шум при использовании.
- **Required Flag**, **Blocked By Flag**, **Flags On First Use**, **Counters On Use**.

Свои поля:

| Предмет | Ассет | Что крутить |
|---|---|---|
| Кран | `Interactable_WaterTap` | **Open / Close Prompt**, **Dirty Duration** (2 с бурой воды), цвета воды, **Milestone Every** (реплика каждые N открытий) |
| Автомат | `Interactable_VendingMachine` | **Jam Chance** (0,05 — заклинило и сыплет банки), **Break Chance** (0,2 — сломался), банки (**Can Size / Color / Mass / Lifetime**, **Jam Can Count** 50), реплики заклинивания и поломки, **Broken Prompt** |
| Шкафчик Рюты | `Interactable_RyutaLocker` | **Captions** (подписи вещей при наведении), **Door Open Angle**, **Door Time**, **Close Hint** |
| Плакат | `Interactable_Poster` | **Drawn Textures** (варианты «с усами»), **Draw Item** (маркер), **Draw Lines** / **Draw Seen Lines**, **Drawn Flag**; Шум за рисунок — `poster_drawn` в `NoiseSettings` |
| Стойка с буклетами | `Interactable_Booklet` | **Pages** (заголовок, картинка, текст), подписи кнопок, размеры и цвета «бумаги» |

Если двигал предметы по сцене, их места перезапишет **Tools → Funseki → Interaction → Place slice interactables in Slice_Day1**: он пересобирает корень `Interactables`. Чтобы сдвинуть предмет навсегда, после сборщика ставь его в нужное место заново или попроси поправить координаты в сборщике.

### Инвентарь — `Data/Inventory`

`InventorySettings.asset`: **Capacity** (8), **Select Picked Item** (взятый предмет сразу в руку), реплики **Wont Work Line** («Не сработает»), **Full Line**, **Pickup Prompt Format**, вид панели (размер слотов, цвета, **Panel Hide Delay**).

Предметы — `Items/Item_*.asset` (маркер, отвёртка, свисток): **Id**, **Display Name**, **Icon**, **Description**, модель в руке (**Hand Prefab**, позиция, поворот, масштаб). Новый предмет — **Create → Funseki → Items → Item**.

---

## 9. Диалоги и реплики — `Data/Dialogue`

### Разговор — `Dialogue_*.asset` (DialogueGraph)

Список **Nodes** сверху вниз. У узла:
- **Id** — на него ссылаются переходы; **Speaker** — кто говорит (`Speakers/Speaker_*`); **Text**.
- **Conditions** — узел пропускается, если хоть одно не выполнено: FlagIsSet, FlagIsNotSet, CounterAtLeast, CounterBelow, HeroIs, HeroIsNot.
- **Actions** — что делает показ узла: SetFlag, ClearFlag, AddToCounter, GiveItem.
- **Choices** — до 3 обычных вариантов и 1 с галочкой **Voice** («Голос», виден только за Кайто). У варианта свои условия, действия, **Next** и **End**.
- **Next** — id следующего узла; пусто = следующий в списке. **End** — разговор заканчивается.

Образец со всеми приёмами — `Dialogue_Test.asset`. Проверять удобно в тестовой сцене: **Tools → Funseki → Dialogue → Build Dialogue_Test scene**, Play прямо из неё.

Реплики вступительной катсцены — `Cutscene_Day1_Intro.asset` (сейчас там тексты «[TODO]»).

### Говорящие — `Speakers/Speaker_*.asset`

**Display Name**, **Portrait**, **Name Color**, звуки «бормотания» (**Mumble Clips**, **Mumble Pitch**, **Mumble Volume**).

### Общие настройки — `DialogueSettings.asset`

- Скорость текста: **Chars Per Second** (50), **Fast Multiplier** (×5 при удержании Ctrl), **Auto Delay** (5 с в режиме «Авто»). Игрок может ещё умножить скорость в «Настройках».
- **Fallback Hero** (Kaito) — чей голос в сценах без трёх героев.
- Камера разговора: **Camera Blend Time**, **Camera Side Distance**, **Camera Height**, **Camera Fov Range**, **Npc Faces Hero**.
- Вид окна: шрифт, размеры, цвета облака, портрета, кнопок и вариантов, **Voice Prefix** («Голос: »).
- Пузыри-реплики над головами: **Bark Min / Max Duration** (2–3 с), **Bark Seconds Per Char**, **Bark Max Distance** (25 м), цвета.

### Короткие реплики — `BarkSet_*.asset`

Наборы фраз, которые NPC говорят при приближении (`BarkTrigger`) или ученики во дворе обсуждают шалость (`BarkSet_WhistleGossip`). Запасной вид реплик героя без сервиса диалогов — `Data/UI/BarkSettings.asset`.

---

## 10. HUD, меню и катсцена — `Data/UI`, `Data/Cutscenes`

### HUD

- **`HudTheme.asset`** — внешний вид: **Reference Resolution**, **Margin**, шрифты и размеры (**Day Font Size**, **Objective Font Size**…), цвета текста и панелей, иконки времени суток, портреты партии и кулдаун Q, мегафон Шума (иконка, цвета «Тихо / Подозрительно / Поймали», размер полоски), вид клавиш в подсказках, иконка журнала. Пустая иконка = нарисованная заглушка.
- **`HudSettings.asset`** — поведение: **Visible States** (Break), **Keep States** (Paused — HUD не прячется), **Day Format** («День {0}»), анимация смены цели, когда прятать шкалу Шума, всплывашка **Journal Toast Title** / **Journal Toast Time**.
- Подсказки управления — список **Hints**: **Trigger** (BreakTime — через **Delay** после начала перемены; InteractionTarget — когда впервые есть цель; ItemAdded; HeroSwitched), **Action**, **Text**, **Keyboard Label** / **Gamepad Label**, **Skip If Already Used** (не показывать, если игрок уже нажимал). Каждая подсказка показывается один раз за игру.

Если HUD пропал из сцены: **Tools → Funseki → UI → Setup HUD in Slice_Day1**.

### Меню паузы и конец демо — `MenuSettings.asset`

- **Pausable States** — где Esc открывает паузу (Break, Lesson, Cutscene, Caught).
- Все тексты кнопок и заголовков, размеры кнопок и шрифтов, цвет затемнения.
- Экран конца демо: **End Title**, **End Subtitle**, подписи исходов физры, **End Journal Total** (сколько записей журнала возможно в срезе, сейчас 1), **Survey Url** — ссылка на анкету. **Пока пусто, и кнопка «Анкета» спрятана**: вставь ссылку перед плейтестом.

### Вступительная катсцена — `Cutscenes/Cutscene_Day1_Intro.asset`

**Timeline** (`Cutscene_Day1_Intro.playable`), **Dialogue** и **Dialogue At** (секунда Timeline, на которой он ждёт диалог, 37), титр **Title** / **Subtitle**, пропуск (**Skip Hold Time** 1 с, подсказки), затемнения **Fade In / Fade Out / Fade Back In**. Саму постановку правят в окне Timeline.

Если меню или катсцена пропали из сцен: **Tools → Funseki → Slice → Setup slice flow (menus, autosave, intro cutscene)**. Данные и Timeline он создаёт только один раз, а объекты `Menus` и `Cutscene_Intro` пересобирает.

---

## 11. Сохранение — `Data/Save/SaveSettings.asset`

- **File Name** — имя файла сейва в папке persistentDataPath (`funseki_save.json`).
- **Autosave** — выключатель автосейва (вкл.). Автосейв пишет в начале каждой фазы, кроме урока, перед уроком и после шалости.
- **Debug Keys** — **F5** сохранить, **F6** «Продолжить» из сейва. Клавиши — **Quick Save Key** / **Quick Load Key**.

Чтобы начать с чистого листа, нажми «Новая игра» в главном меню: она сбрасывает флаги, инвентарь и журнал.

---

## 12. Школа — `Assets/_Project/School`

Подробно — в [School_Guide.md](../Assets/_Project/School/Docs/School_Guide.md). Коротко:

- **`School/Kit/KitSettings.asset`** — размеры модулей (высота стен и этажей, двери, окна, лестницы) и цвета. После правки: **Tools → Funseki → Generate Kit**, потом **Tools → Funseki → Build School** → кнопка **Build School**. Если менял только цвета, хватит **Generate Kit**.
- **`School/Data/SchoolLayout.asset`** — план: зоны-комнаты (**rect**, двери и окна **openings**, **unlockDay**, мебель **furnish**). После правки: только **Build School**.
- Сборка удаляет корень `School` в `School_Greybox` и строит его заново. **Всё, что правили руками прямо в сцене, пропадёт**: меняй ассеты, а не сцену.
- **Tools → Funseki → Reset School Layout To Plan** заменяет весь `SchoolLayout` исходным планом и **стирает все правки зон**. Он спросит подтверждение; без нужды не нажимай.
- Проверка: **Tools → Funseki → Capture School Top Views** снимет оба этажа сверху, а консоль покажет сообщения `[Funseki]` о перекрытых зонах и пропущенных дверях.

Эту папку ведёт отдельный тред школы. Правь числа в ассетах сколько угодно, но о правках скриптов сборщика лучше договориться там.

---

## 13. Сборка Slice_Day1 и запуск

| Что нужно | Меню |
|---|---|
| Запустить игру | **Tools → Funseki → Core → Play From Bootstrap** |
| Проверить, что меню → «Новая игра» → катсцена работает | **Tools → Funseki → Core → Smoke Test Flow** (в консоли `[CoreFlowSmokeTest] PASS/FAIL`) |
| Пересобрать героев, камеры, SchoolLoader | **Tools → Funseki → Slice → Build Slice_Day1 (player + school loader)** (то же, что **Heroes → Build Slice_Day1 with the three heroes**) |
| Вернуть меню, автосейв, катсцену | **Slice → Setup slice flow (menus, autosave, intro cutscene)** |
| Вернуть объекты дня | **DayCycle → Setup Day 1 schedule in Slice_Day1** |
| Расставить интерактивные предметы | **Interaction → Place slice interactables in Slice_Day1** |
| Вернуть шалость | **Pranks → Setup pranks in Slice_Day1** |
| Вернуть урок физры | **Lessons → Setup Fizra lesson in Slice_Day1** |
| Вернуть HUD | **UI → Setup HUD in Slice_Day1** |
| Сервисы в Bootstrap | **Save → Add inventory and save services to Bootstrap**, **Dialogue → Add dialogue services to Bootstrap**, **Audio → Add bell sound to Bootstrap** |
| Пересоздать недостающие сцены ядра и Build Settings | **Core → Create Core Scenes** (существующие файлы не трогает) |
| Найти потерянные ссылки после переноса ассетов | **Tools → Funseki → Validate References** |

Стартовая сцена и состояние каждой сцены — `Data/GameFlowConfig.asset` (**First Scene** = MainMenu; MainMenu стартует в MainMenu, Slice_Day1 в Cutscene). Обычно его трогать не нужно.

Каждый пункт пересобирает только свои объекты: **Build Slice_Day1** удаляет и строит заново героев, камеры, свет и SchoolLoader, а предметы, шалость, урок, HUD и меню не трогает. Поэтому после правки данных ничего пересобирать не нужно, а после поломки сцены жми только пункт того, что пропало.

---

## 14. Debug-клавиши в редакторе

| Клавиша | Что делает | Где настраивается |
|---|---|---|
| F1 | показать отладочную подпись состояния игры | — |
| F5 / F6 | сохранить / «Продолжить» из сейва | `SaveSettings` |
| F7 / F8 | выиграть / проиграть раунд физры | `FizraSettings` |
| F9 | следующая фаза дня | — |
| F10 | выставить флаг цели текущей фазы | — |

---

## 15. Чего не делать

- Не переименовывай и не переноси ассеты из `Data/` мышкой, если не уверен: сборщики ищут их по пути. Если всё-таки перенёс, запусти **Validate References** и скажи об этом в проекте, чтобы поправили пути в сборщиках.
- Не меняй **Id** фаз, шалостей, уроков и флагов без нужды: на них завязаны сейв, условия диалогов и исходы уроков. Если поменял, ищи старое имя во всех ассетах `Data/`.
- Не правь руками объекты внутри корней, которые строят сборщики (`Heroes`, `Interactables`, `Pranks`, `Lessons`, `HUD`, `Menus`, `Cutscene_Intro`, `School`): следующая пересборка это сотрёт.
- **Reset School Layout To Plan** стирает все правки плана школы.
- Не запускай игру обычной кнопкой Play из `Slice_Day1`: только **Play From Bootstrap**.
