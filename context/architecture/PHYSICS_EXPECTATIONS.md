# Ожидания по физике и известные ограничения

Практические критерии качества симуляции и актуальные ограничения.

Связанные заметки: [[ARCHITECTURE_RULES.md]], [[roadmap/PROGRESS.md]], [[systems/LOD.md]].

---

## Практические ожидания

- орбита не должна «плыть» без причины
- манёвр должен максимально совпадать с preview
- finite burn должен быть предсказуемым
- warp не должен перескакивать важные события
- SoI должен вести себя понятно (patched conics, KSP-style; Sun = root, ∞ SoI — [[systems/MAP_MODE_ORBIT_SOI.md]])
- pad launch: ракета на площадке, камера на корпус, география под pad = география в sim (см. terrain канон)

Целевое ощущение:

`как в KSP, но архитектурно чище и с большей физической честностью`

---

## SoI (актуальный канон)

- **Sun** — root body (`Body.isRootBody`, `CelestialBody.Sun.IsRootBody`): бесконечная SOI, не Laplace, не «межзвёздное пространство».
- **Earth** — playable SoI **14× equatorial radius** (~8.92×10⁶ m в sim).
- **Moon** — Laplace в `SimulationWorldBuilder` + scenario multipliers.
- Active primary: hysteresis, exit ×1.04; heliocentric шаг — `UpdateSolarSystem()`.
- Map overlay SoI: dev toggle, только конечные пузыри (`TryGetMapFocusSphereOfInfluence`).

Подробно: [[systems/MAP_MODE_ORBIT_SOI.md]].

---

## Орбитальная физика (известные ограничения)

- preview и реальный результат могут расходиться на длинных burn
- при слабой тяге circularize around Moon может не успевать отработать до изменения геометрии
- пересечение SoI за один `dt`: биссекция границы; Kepler rails не идут внутри 1.25× пузыря Луны (иначе пролёт без смены primary)
- корабль: Kepler rails включая гиперболу; SAS на rails крутит без тяги; крупный варп — подшаги ориентации ≤ 1 с
- нет Kepler on rails для планеты/луны — при очень большом time warp возможен численный дрейф тел
- HUD разрастается — периодический cleanup

---

## Terrain / LOD (активная зона)

- cube-sphere inverse на углах граней — **исправлен** (2026-08-07); история: [[history/lod_problem.md]]
- Earth Sandbox LOD (quadtree, GEBCO chunks) — валидация в `Earth_Sandbox_Test`, интеграция в Cove/pad launch ещё не закрыта
- edge stitching / skirts — pad-кольца Земля+Луна 2026-09-12 (общие правила UV, не хаки тел); идеальные кольца по всей сфере не закрыты
- рассинхрон pad rotation vs mesh geography на западных долготах — гипотеза в [[history/lod_problem.md]] §3

Канон координат при любых правках terrain: [[systems/work_terrain.md]], [[CONVENTIONS.md]].

---

## Посадка / удар

- Контакт, трение, drag и crash — в **рамке грунта**: `velocity − (v_body + ω×r)`, как HUD Surface. У мыса ω×r ≈ 160 m/s; сравнивать с ЦМ Земли = ложный взрыв и провал сквозь пад. Луна почти не крутится — там этого не видно.
- Палуба Cove: физика только в `PadPhysicsContactRadiusMeters` (48 м). Видимость пада и сглаживание GEBCO рядом — 100 м. Меш скругления двора — 100 м мягко / 40 м жёстко. Высота «ноль» в Core — плоскость spawn/deck; визуальный слайд вселенной должен снова прибавить высоту палубы, иначе в полёте ракета внутри настила, а Landed телепортит на SpawnPoint. Высота spawn/deck **только на палубе**, не на весь полёт «с мыса» (иначе зависание над песком).
- Вне пада контакт — **высота карты** (GEBCO/LDEM в рамке тела). Земля: дно океана → уровень моря 0. Луна: знак LDEM. Нет карты → прежняя сфера. Склоны пока с радиальной нормалью. Картинка гор ×0.4 при симе 1:1 — на холмах ракета может казаться выше меша. Смещение глобуса = локальная высота меша под кораблём (во дворе Cove — база пада).
- `Landed` не ставит нос «вверх от центра планеты». Наклон как сел; поза в рамке планеты (сутки крутят вместе). В полёте касание — выталкивание вдоль текущего радиуса, без телепорта центра на нормаль пада.
- Целый корабль: crash при грунте `|v|` > **18 m/s** (`LandingConfig.CrashSpeedThreshold`); «мягкая» посадка до **27 m/s** total (`SoftLandingTotalSpeed`). Dev **No crash damage** (`SimulationCheatFlags.DisableCrashDamage`) пропускает удар и разрушение деталей — корабль садится целым.
- Детали: `structuralStrength` по умолчанию ×1.5 (бак 21, двигатель 15). Сохранённые корабли оставляют записанную прочность.
- После взрыва двигателя: тяга, расход топлива и выхлоп только если в ступени ещё есть живой `IsEngineBlock`. Скаляр `stage.thrust` больше не держит призрак тяги.

## SAS / скорость (рамка как KSP)

Кнопка слева от компаса и свободный бинд (по умолч. **`**) циклируют **Surface / Orbit / Target**. HUD speed и SAS **2/3** смотрят в одну рамку: Surface = `velocity − ω×r`, Orbit = ЦМ тела, Target = Tab-цель (ЦМ тела или судно). Без цели Target показывает орбитальную скорость. Клавиша: `settings.json` → `play.speedReferenceCycleKey`. Не клеить ракету к воздуху. Подробнее: [[systems/CONTROLS.md]].

---

## Технический долг (кластеры, сверять с кодом)

Статусы кластеров в [[roadmap/PROGRESS.md]] могут отставать. Ниже — проверенные «живые» пункты:

- **C20:** UDP lockstep MVP ✅ (2026-07-12); polish MP-F1…F4 — открыт
- **C21.4:** ReStock pipeline — multi-material split (`LV_T30_Boat` на `restock-engine-reliant-2`); cupola + endcap attachment в Blender export
- **C18.6:** debris view — polish (`DebrisView` отдельно от `BodyView`)
- **C19.5:** migrator snapshot v1→v2 — stub
