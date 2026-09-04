# lod_problem.md — LOD terrain в Flight mode

**Статус:** ✅ **корневая причина найдена и исправлена (2026-08-07).** Ждёт проверки в игре.  
**Канон координат:** см. [[systems/work_terrain.md]] — **не ломать** Mesh Frame (`mesh.x = -sim.x` только для позиций вершин).

**Симптом:** в основной игре (Flight, не EarthSandbox) чанк с **максимальным LOD** визуально **смещён** относительно ракеты — в Scene view wireframe детальный чанк не под кораблём. Иногда **LOD thrashing** (постоянный split/merge).

**Ключевой диагностический факт:** на **Greenwich 0°N 0°E** всё совпадает; на **Cape Canaveral** и других долготах max LOD грузится далеко от ракеты.

---

## 0. РЕШЕНИЕ: прямое и обратное cube-sphere отображение были **разными проекциями**

Проблема **не** в Mesh Frame, не в камере, не в timing и не в pad rotation. Все гипотезы
ниже (§2, §3) были мимо, потому что focus direction всё это время вычислялся **правильно**.
Неправильным был перевод этого направления обратно в face UV.

| Направление | Функция | Проекция |
|-------------|---------|----------|
| **face UV → direction** (строит меш чанка) | `PlanetCubeSphereCoords.FaceUvToUnitDirection` → `SpherifyCubePoint` | **Philip Nowell** spherification |
| **direction → face UV** (ищет чанк под ракетой) | `PlanetCubeSphere.TryDirectionToFaceUv`, `PlanetCubeSphereCoords.TryUnitDirectionToFaceUv` | **гномоническая** (`dir / abs(dot(dir, normal))`) — инверсия обычного `normalize`, **не** Nowell |

Эти две проекции совпадают **только** в центрах граней и на рёбрах куба, а между ними
расходятся до **6.7° = 747 км** на Земле.

Измеренная ошибка «где чанк по мнению LOD» vs «где чанк реально рисуется»:

| Точка | face | uv (было, гномоника) | uv (стало, Nowell) | ошибка на грунте |
|-------|------|----------------------|--------------------|------------------|
| Greenwich 0°N 0°E | 4 | (0.5000, 0.5000) | (0.5000, 0.5000) | **0 км** ← центр грани +Z |
| Cape Canaveral | 2 | (0.4177, 0.2241) | (0.3902, 0.1591) | **681 км** |
| Baikonur | 0 | (0.9320, 0.2828) | (0.9605, 0.2393) | 441 км |
| Kourou | 2 | (0.1201, 0.4424) | (0.0724, 0.4257) | 535 км |
| worst case (57°S 45°W) | — | — | — | **747 км** |

Greenwich «работал» не потому что там нет X-flip, а потому что `(0, 0, 1)` попадает
**ровно в центр грани +Z** (`u = v = 0.5`) — единственная точка, где обе проекции
тождественны. Это и объясняет весь диагностический факт §3.1.

**Последствия бага:** `ChunkContainsViewerSurface()` / `ChunkContainsLaunchSiteDirection()`
возвращали true для чанка, который рисуется за сотни километров от ракеты. Через них
работают `IsPriorityForceSplit` (форс-сплит до max LOD), `EstimateDistanceToCameraMeters`
(возвращает голую высоту для «чанка под наблюдателем»), защита от merge, `IsChunkNearViewer`
и приоритет сборки мешей. Весь детальный «воронкой» LOD уезжал в чужой чанк, а под ракетой
оставалась грубая геометрия — плюс thrashing, потому что честная метрика расстояния
(`EstimateArcDistanceToChunk`, она считает через **Nowell**, т.е. верно) тянула детализацию
в одно место, а сломанный containment-тест — в другое.

**Фикс:**

1. `PlanetCubeSphereCoords.UnspherifyToFaceTangentCoords` — точная аналитическая инверсия
   Nowell в замкнутой форме. Из прямого отображения: `6a² = cu²(3 − cv²)`, `6b² = cv²(3 − cu²)`,
   откуда `cu² − cv²` берётся напрямую, а `cu² + cv²` — меньший корень квадратного уравнения.
   Корень взят в рационализированной форме (`(2T − d²)/(3 + √D)` вместо `3 − √D`), иначе
   у центра грани теряется мантисса.
2. `PlanetCubeSphereCoords.TryUnitDirectionToFaceUv` использует её вместо гномоники.
3. `PlanetCubeSphere.TryDirectionToFaceUv` (Unity) больше **не** дублирует математику —
   делегирует в SimulationCore. Дубли `GetFaceAxisU/V` в `PlanetCubeSphere` удалены:
   именно дублирование базиса и породило расхождение.

**Проверено** (скомпилированный прогон реальных исходников): round-trip UV по всем 6 граням —
ошибка 2.8e-16; Cape/Baikonur/Kourou — 0.0000 м; полный обход сферы 1°×1° — worst 0.13 м
(обусловленность на рёбрах куба, при минимальном чанке 24 м несущественно).

**Регрессия закрыта тестами** в `PlanetCubeSphereCoordsTests.cs`:
`UnitDirectionToFaceUv_RoundTripsThroughSphere_OnEveryFace`,
`LatitudeLongitudeToFaceUv_ChunkUnderSite_RendersAtThatSite`.

**Почему не поймали раньше:** существовавший тест `FaceUvToCubePoint_RoundTrip_RecoversUvExactly`
делает round-trip только через **куб** (`FaceUvToCubePoint`), минуя сферификацию, а
`FaceUvToUnitDirection_PreservesDominantFace` проверяет только индекс грани и игнорирует UV
(`out _, out _`). Ни один не проходил через `Spherify` + инверсию.

---

## 1. Как сейчас работает LOD (Flight mode)

### 1.1 Общая схема

```
SimulationRunner (order 0)
  └─ ship localPosition = Vector3.zero (floating origin)
  └─ SyncPlanetLodRuntime() — altitude, velocity, anchor; БЕЗ focus direction

FlightUniverseMovement (order 100)
  └─ планета сдвигается/вращается под ракетой (KSP scaled space)
  └─ pad-locked rotation через WorldVisualBridge.GetPadLaunchPlanetRotation

FlightPlanetSphere (order 250, LateUpdate)
  └─ SyncViewerLodFocusFromSimulation() — КАЖДЫЙ кадр, после движения планеты
  └─ quadtree.RefreshLodLookAhead() — split/merge по таймеру
  └─ quadtree.DispatchCompletedMeshes()

PlanetQuadtreeNode
  └─ split/merge, mesh build, collider
  └─ ChunkContainsViewerSurface() — face-UV bounds test
  └─ EstimateDistanceToCameraMeters() — arc distance + altitude
```

### 1.2 Фокус LOD (куда смотрит квадродерево)

**Источник:** `WorldVisualBridge.ResolveLodFocusDirectionSim()`  
**Вызывается из:** `FlightPlanetSphere.SyncViewerLodFocusFromSimulation()` (LateUpdate, order 250).

**Алгоритм (текущий):**
1. Если есть anchor (`HasViewerWorldAnchor`) и `planetTransform`:
   - `meshLocal = planetTransform.InverseTransformPoint(viewerAnchorWorld)`  
   - `focusSim = MeshFrameUnitToSimUnit(meshLocal.normalized)`
2. Иначе fallback: `simulation.GetShipRadialOutDirectionRelativeToActivePrimary()`
3. Иначе: `GetLaunchSiteNormalUnity()`

**Anchor:** `WorldVisualBridge.FlightFrameViewerAnchorWorld` = `Vector3.zero` (корабль в frame-zero).  
`HasViewerWorldAnchor` — отдельный флаг (ноль валиден, не «пустая позиция»).

**Синхронизация sim/visual:**
```csharp
ViewerFocusDirectionSim = focusSim;
ViewerFocusDirectionVisual = PlanetCubeSphere.SimDirectionToMeshFrame(focusSim);
```

### 1.3 Split / merge критерии

| Проверка | Где | Что делает |
|----------|-----|------------|
| `ChunkContainsViewerSurface()` | `PlanetQuadtreeNode` | `PlanetCubeSphere.IsDirectionInsideFaceUvBounds(focusSim, face, u, v)` |
| `EstimateDistanceToCameraMeters()` | `PlanetQuadtreeNode` | arc distance от focus до углов чанка + `sqrt(alt² + arc²)` |
| `GetViewAxisDirectionSim()` | `PlanetQuadtreeLod` | **всегда** = `GetLodFocusDirectionSim()` (не камера) |
| View cone / horizon merge | `PlanetQuadtreeNode` | угол от focus, не от camera look |
| Budget | `RefreshLodLookAhead` | `maxLodSplitsPerFrame`, `maxMeshBuildsPerFrame` |

### 1.4 Построение меша чанка

- **Sim UV / face tests:** без X-flip (канон `work_terrain.md`)
- **Позиции вершин:** `FlipMeshVertexPositionX` / Mesh Frame
- **Высота:** `PlanetFlightChunkCpuBuilder` + GEBCO/NASA assets
- **Async:** split-before-mesh, backpressure, collider на близких чанках

### 1.5 Floating origin и планета

- Корабль: `localPosition = Vector3.zero` в `SimulationRunner`
- Планета: `FlightUniverseMovement` + `WorldVisualBridge.GetFlightPlanetFixedLocalPosition`
- `BodyView.ApplyFlightModeCelestialBody()` **не** дублирует transform планеты, если активен `FlightUniverseMovement`
- Pad rotation: `GetPadLaunchPlanetRotation` — выравнивание востока площадки с mesh-frame geography

### 1.6 EarthSandbox (эталон, работает)

Отдельная сцена `EarthSandboxQuadtreeManager` — **не** основной flight pipeline.  
Там focus берётся из **camera local position** на планете; distance — arc + altitude.  
Flight mode намеренно использует **ship anchor** (frame-zero), не камеру.

---

## 2. Что уже пробовали и НЕ помогло

> Всё в этом разделе — попытки чинить **не ту** подсистему. Focus direction был корректен
> с самого начала; ломался перевод direction → face UV (§0). Оставлено как история.

### 2.1 Фокус LOD

| Попытка | Результат |
|---------|-----------|
| Sim ship radial only (`GetShipRadialOutDirectionRelativeToActivePrimary`) | Не помогло |
| Camera position для focus / distance | Не помогло |
| Camera look direction для view cone | Не помогло; убрали, view cone = ship focus |
| `InverseTransformPoint(ship)` со **старым** rotation планеты (до LateUpdate) | Частично поняли timing; перенос в LateUpdate **не решил** смещение |
| Mesh X-flip на focus direction | Против канона; отклонено |
| Phase offset / «подкрутка» focus vector | Отклонено |
| Focus из `SetViewerMotion` в `SyncPlanetLodRuntime` | Убрали; focus только в LateUpdate |

### 2.2 Камера

| Попытка | Результат |
|---------|-----------|
| Две камеры (VAB Main Camera + FlightCamera) | **Исправлено** (`FlightCameraBootstrap`) — одна активная камера |
| После фикса камеры LOD всё равно смещён | Подтверждено пользователем — **корневая причина не в камере** |

### 2.3 Timing / execution order

| Попытка | Результат |
|---------|-----------|
| `SyncViewerLodFocusFromSimulation` каждый кадр в LateUpdate (250), после `FlightUniverseMovement` (100) | Не помогло |
| Убрать focus из `SimulationRunner.SyncPlanetLodRuntime` (order 0) | Не помогло |
| `BodyView` не дублирует planet transform при `FlightUniverseMovement` | Не помогло |

### 2.4 Метрика расстояния

| Попытка | Результат |
|---------|-----------|
| Euclidean planet-local distance | Заменили на arc + altitude (как EarthSandbox) | Не помогло |

### 2.5 Координатный канон

| Попытка | Результат |
|---------|-----------|
| Этап 5: убрать Mesh Frame, единое 3D | **Откатили** — визуал сломался (`work_terrain.md`) |

---

## 3. Наблюдения и гипотезы (не проверены до конца)

### 3.1 Greenwich OK, Cape FAIL

| Точка | sim unit (пример) | mesh unit |
|-------|-------------------|-----------|
| Greenwich 0°N 0°E | `(0, y, +z)` — **x ≈ 0** | x-flip не влияет |
| Cape Canaveral ~28.57°N 80.65°W | `(-0.867, 0.478, 0.143)` | `(+0.867, 0.478, 0.143)` |

**Гипотеза:** `GetPadLaunchPlanetRotation` (twist для east-alignment) может рассинхронизировать **mesh geography** и **cube-sphere UV** на западных долготах. На Greenwich twist минимален.

### 3.2 Визуал vs sim geography

Возможно LOD **корректен в sim UV**, но под ракетой рисуется **другая география** (например Тибет вместо Флориды) — тогда проблема в pad rotation / mesh frame, а не в quadtree split.

**Диагностика:** `LaunchSiteTerrain.LogViewerGeography` vs `LogShipGeography` при включённом LOD debug.

### 3.3 Другие ветки split

Neighbor parity, crack prevention, launch-site direction fallback (`ChunkContainsLaunchSiteDirection`), budget limits — могут держать max LOD не на viewer leaf.

---

## 4. Ключевые файлы

```
FlightPlanetSphere.cs              — LateUpdate LOD, SyncViewerLodFocusFromSimulation
PlanetQuadtreeNode.cs              — split/merge, ChunkContainsViewerSurface, distance
FlightUniverseMovement.cs          — planet slide/rotate (order 100)
WorldVisualBridge.cs               — ResolveLodFocusDirectionSim, GetPadLaunchPlanetRotation
SimulationRunner.cs                — SyncPlanetLodRuntime, ship at zero
FlightCameraBootstrap.cs           — single flight camera (FIXED)
PlanetFlightChunkCpuBuilder.cs     — mesh vertex build (flip X on positions)
LaunchSiteTerrain.cs               — LogViewerGeography, LogShipGeography
work_terrain.md                    — coordinate canon (DO NOT BREAK)
EarthSandboxQuadtreeManager.cs     — reference (works; separate scene)
```

---

## 5. Что НЕ трогать без согласования

- `texU = 1 - u` или flip UV в шейдере/CPU для NASA PNG
- Убрать Mesh Frame X-flip с позиций вершин (Этап 5 уже откатили)
- Второй X-flip в UV bounds / face tests
- Phase offset hacks на focus vector

---

## 6. Следующие шаги

1. **Проверить в игре** на Cape Canaveral: max LOD должен быть под ракетой, thrashing уйти.
2. Если thrashing остался — смотреть §7, это уже другая (тюнинговая) проблема.

---

## 7. Открытые вопросы в логике разбивки (не блокеры, отдельно от §0)

Найдено при разборе, **не исправлено** — сначала проверить §0 в игре.

1. **Жёсткий отсечка `SplitDistanceMeters` (default 4000 м).** В `WantsSplit()`:
   `if (distanceToCamera >= lod.SplitDistanceMeters) return false;` — а `distanceToCamera`
   включает высоту (`sqrt(alt² + arc²)`). Выше 4 км **ни один** чанк не проходит обычный
   тест на split; работает только `IsPriorityForceSplit` (чанк наблюдателя / стартовой
   площадки, и только пока alt < 30 км). Выше 30 км не сплитится вообще ничего.
   Отсюда «странно развиваются»: вместо плавных концентрических колец детализации —
   одна глубокая воронка под ракетой и грубая геометрия вокруг. Обычный PQS-критерий
   `distance < span * factor` уже сам ограничивает глубину; дополнительная отсечка
   по абсолютному расстоянию его перебивает.
2. **Neighbor sync выключен именно у земли.** `SynchronizeNeighborLevels()` делает
   early-return при `ViewerAltitudeMeters < PadPlanetRotationLockAltitudeMeters` (30 км),
   т.е. на всём наземном участке. Вместе с тем, что `IsPriorityForceSplit` обходит
   `CanSplitWithoutLodCrack`, рядом со стартом получается обрыв уровня 0 → 18 без
   промежуточных ступеней.
3. **`EstimateNodeWorldSpanMeters()` = `2R / 2^level`** завышает реальную дугу чанка:
   грань куба — это 90°, т.е. `≈ 1.5708 * R / 2^level`. Систематическое завышение на ~27%
   (не баг сам по себе, но `LaunchSiteMaxChunkSpanMeters = 64 м` на деле даёт ~50 м).
4. **`InsetPolarFaceUv`** сдвигает UV у центров полярных граней (радиус 0.015), а инверсия
   его не отменяет. У полюсов остаётся рассинхрон до ~1.7°. Далеко от Cape/Baikonur,
   но у полюсов chunk lookup будет промахиваться.

---

## 8. История правок этого файла

| Дата | Что |
|------|-----|
| 2026-08-07 | Первый снимок: текущий pipeline, все неудачные попытки, гипотезы Greenwich vs Cape |
| 2026-08-07 | **Решено:** обратное cube-sphere отображение было гномоническим вместо инверсии Nowell (§0). Добавлен §7 — открытые вопросы тюнинга разбивки |
