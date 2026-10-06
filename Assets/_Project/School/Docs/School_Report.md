# Серая коробка школы: отчёт по шагам 2–4

## Как пользоваться

| Меню | Что делает |
|---|---|
| Tools → Funseki → Generate Kit | Генерирует меши, материалы и префабы `PF_Kit_*` из `KitSettings.asset`. Повторный запуск обновляет ассеты на месте, ссылки не теряются. |
| Tools → Funseki → Build School | Окно сборщика: поля `SchoolLayout` и `KitSettings`, кнопки Build School и Capture top views. |
| Tools → Funseki → Rebuild School (no window) | То же, что Build School, без окна. |
| Tools → Funseki → Reset School Layout To Plan | Перезаполняет `SchoolLayout` зонами из таблицы шага 1. Спрашивает подтверждение. |
| Tools → Funseki → Render Kit Preview | Рендерит все префабы кита в `Docs/Kit_Preview.png`. |
| Tools → Funseki → Capture School Top Views | Снимает этажи сверху в `Docs/School_TopView_F1.png` и `_F2.png`. |

Сборка удаляет корень `School` и строит заново в `Assets/_Project/School/Scenes/School_Greybox.unity`. Два запуска подряд дают одинаковые 4312 объектов без дублей (проверено). Если в открытой сцене есть несохранённые изменения, окно предложит их сохранить, а запуск без окна откроет школу рядом, ничего не закрывая.

## Проверка

- Консоль Unity: ошибок нет. Есть одно старое предупреждение в `MainMenu/Editor/MainMenuBuilder.cs`, его не трогал.
- Виды сверху: `School_TopView_F1.png`, `School_TopView_F2.png`.
- Кадр у двери «Совещание идёт»: `Meeting_Door.png`. Кадр северного коридора 1-го этажа: `Corridor_F1.png`.

## Числа

**Зоны: 58.**
- 1-й этаж: 34 зоны.
- 2-й этаж: 23 зоны.
- 3-й этаж: 1 зона (за дверью «Совещание идёт»).

По группам иерархии: `Floor_1` 27, `Floor_2` 16, `Gym` 7, `Dorm` 8.

**Префабы кита: 36.**
- Архитектура (16): Wall, Wall_1m, WallWindow, WallDoorway, WallDoorwayDouble, WallSlidingDoor, CornerOuter, CornerInner, Floor, Floor_1x1, Ceiling, Ceiling_1x1, StairFlight, Railing, Column, StageBlock.
- Двери (3): Door_Swing, Door_Sliding, Door_Double.
- Пропсы (17): Desk, Chair, TeacherDesk, Blackboard, CarpetBoard, ShoeLocker, Bench, BunkBed, Sink, ToiletStall, Shower, VendingMachine, WashingMachine, Statue, ElevatorCabin, SignMeeting, TrashBin.

Сверх списка из задачи добавлены: Wall_1m (добивка нечётных длин), WallDoorwayDouble (проём 2,4 м под двойную дверь), Floor_1x1, Ceiling_1x1, StageBlock (сцена) и TrashBin (мусорки у технического выхода).

**Двери с `LockedDoor`: 17.**

## Зоны по дням открытия

| День | Зоны |
|---|---|
| 1 | Все остальные: классы 1-го этажа, класс героев (`heroes_class`), жилые комнаты `dorm_1..5`, коридоры, двор, лестницы, лифт, туалеты, хозкомнаты, комната наказаний, прачечная, душевые, фойе и актовый зал |
| 2 | `gym`, `weights`, `equipment`, `pe_office`, `locker_a`, `locker_b`, `gym_vestibule` |
| 3 | `canteen`, `kitchen` |
| 4 | `staff_room` |
| 5 | `principal` |
| 6 | Нет зон: крыши нет |
| 7 | `floor_3` (дверь «Совещание идёт» в конце лестницы `stairs_3f`) |

## Как устроена сборка

- **Стены.** Каждый этаж раскладывается в клетки 1×1 м. Стена ставится один раз на каждую границу между разными зонами, на сторону зоны с большим приоритетом: комната, потом улица, потом коридор. Коридоры с общим `openGroup` стен между собой не получают.
- **Окна.** Если у зоны включено `autoWindows`, окна идут через модуль на стенах, которые выходят наружу, во двор или в световой колодец.
- **Двери.** Двери из `SchoolLayout` вырезают проём в стене соседа, так что дверь объявляется один раз.
- **Высокие зоны.** У спортзала и лестницы на 3-й этаж стены идут в два ряда, щель 0,3 м между рядами закрыта полосой.
- **Потолок.** Потолочные панели ставятся только там, где сверху ничего нет. Под вторым этажом потолком служит его плита.
- **Пропсы.** Расставляются по пресету зоны (поле `furnish`): класс, общежитие, туалет, столовая, актовый зал и т. д.

## Известные упрощения

- Угловые модули (CornerOuter, CornerInner) есть в ките, но сборщик их не ставит. На нескольких Т-образных стыках, где стена переходит на другую сторону линии, могут остаться щели 0,2×0,2 м.
- Двери стоят закрытыми и неподвижно. `LockedDoor` и `Zone` пока только хранят данные, логики открытия нет. Открытые проходы в закрытые зоны (вход в столовую) перекрыты невидимым коллайдером `LockedPassage`.
- Крытые галереи во дворе (серые полосы на плане) и пунктирная перегородка в учительской не построены.
- Лестницы только П-образные и только с входом с севера или юга. Пролёт 3 м, площадка на половине высоты этажа.
- Крыши нет: на последнем этаже потолочные панели служат крышей.
- В жилой комнате 2 двухъярусные кровати (4 места на 3 учеников), отдельной кровати в ките нет.
- Все классы обставлены одинаково: доска на западной стене, 20 парт. В Геометрии вместо доски ковёр, в Анатомии добавлена статуя-скелет.
- Текст на табличке «Совещание идёт» мелкий (TextMesh со шрифтом по умолчанию).
- Освещение, NavMesh и контроллер персонажа не добавлялись, как и просили.

## Файлы

- `Assets/_Project/School/Scripts/`: `KitSettings.cs`, `SchoolLayout.cs`, `Zone.cs`, `LockedDoor.cs`
- `Assets/_Project/School/Kit/`: `KitSettings.asset`, `Prefabs/` (36), `Meshes/`, `Materials/` (5), `Textures/T_Kit_Grid.png`
- `Assets/_Project/School/Data/SchoolLayout.asset`
- `Assets/_Project/School/Scenes/School_Greybox.unity`
- `Assets/_Project/School/Docs/`: этот отчёт, разбор шага 1, превью и скриншоты
- `Assets/Editor/Funseki/`: `KitMeshBuilder.cs`, `KitGenerator.cs`, `KitPreview.cs`, `SchoolLayoutDefaults.cs`, `SchoolBuilder.cs`, `SchoolFurnisher.cs`, `SchoolBuilderWindow.cs`
- `ProjectSettings/TagManager.asset`: добавлен слой `Environment` (слот 8)
