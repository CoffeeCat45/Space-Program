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
- edge stitching / skirts — не завершены
- рассинхрон pad rotation vs mesh geography на западных долготах — гипотеза в [[history/lod_problem.md]] §3

Канон координат при любых правках terrain: [[systems/work_terrain.md]], [[CONVENTIONS.md]].

---

## Технический долг (кластеры, сверять с кодом)

Статусы кластеров в [[roadmap/PROGRESS.md]] могут отставать. Ниже — проверенные «живые» пункты:

- **C20:** UDP lockstep MVP ✅ (2026-07-12); polish MP-F1…F4 — открыт
- **C21.4:** ReStock pipeline — multi-material split (`LV_T30_Boat` на `restock-engine-reliant-2`); cupola + endcap attachment в Blender export
- **C18.6:** debris view — polish (`DebrisView` отдельно от `BodyView`)
- **C19.5:** migrator snapshot v1→v2 — stub
