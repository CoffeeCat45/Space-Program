# LOD — planet quadtree (Flight mode)

> **Канон координат / UV / GEBCO:** [[systems/work_terrain.md]] — **не менять** без сверки Stage 3.  
> **Эталон старта на паде 🔒 (не править split/merge/догон «улучшая» кольца):** [[systems/LOD_BASELINE_PAD_START.md]]  
> **Океан на чанках:** [[systems/OCEAN.md]] · **атмосфера / облака:** [[systems/ATMOSPHERE_CLOUDS.md]]  
> **Симптомы / F8 / тупики (живой):** [[systems/LOD_FAILURES.md]]  
> Split/merge/догон — cube UV / горизонт / RING на любом теле (§5.2.1). Нет `if (Moon)` / `if (Earth)`.  
> **История (закрыто):** [[history/lod_problem.md]] — cube-sphere inverse **исправлен 2026-08-07**.  
> **Долгосрочный план PQS (архив):** [[history/terrain_plan.md]].

---

## 1. Где это живёт

### Main game (Flight)

| Что | Путь |
|-----|------|
| LOD driver | `UnityAdapter/Flight/FlightPlanetSphere.cs` (LateUpdate, order 250) |
| Quadtree | `UnityAdapter/Flight/PlanetQuadtreeNode.cs` |
| Cube-sphere math | `SimulationCore/World/PlanetCubeSphereCoords.cs`, `UnityAdapter/Flight/PlanetCubeSphere.cs` |
| Focus / planet slide | `UnityAdapter/Rendering/WorldVisualBridge.cs`, `UnityAdapter/Play/FlightUniverseMovement.cs` |
| Настройки (PlayerPrefs) | `UnityAdapter/Flight/PlanetLodSettings.cs` |
| Панель настроек | VAB Options → **Terrain Graphics** (`VabOptionsUi.cs`) |

### Earth Sandbox (эталон / отладка)

| Что | Путь |
|-----|------|
| Сцена | `Assets/Scenes/Earth_Sandbox_Test.unity` |
| Менеджер LOD | `Assets/Scripts/EarthSandbox/EarthSandboxQuadtreeManager.cs` |
| Панель | `EarthSandboxTerrainGraphicsDebugPanel.cs` |

---

## 2. Архитектура дерева

```
Планета (6 граней куба, LOD 0)
└── каждая грань — своё quadtree
    └── Split → 4 ребёнка (SW, SE, NW, NE), LOD+1
    └── Merge ← 4 листа схлопываются в родителя
```

- **Лист** — активный узел без детей; держит mesh + optional collider.
- **Координаты чанка** — UV на грани `(MinU..MaxU, MinV..MaxV)` + `FaceIndex` (0…5).
- **Геометрия** — Mesh Frame (`mesh.x = -sim.x`), UV из sim direction. См. `work_terrain.md`.

**Два направления в LOD:**

| Вектор | Источник | Для чего |
|--------|----------|----------|
| **Viewer focus (sim)** | Ракета в frame-zero → `InverseTransformPoint` → `MeshFrameUnitToSimUnit` | Кольца детализации **вокруг корабля** + горизонт (геоцентрический диск) |
| **Camera** | `Camera.main` | Frustum cull отрисовки; FOV/pixelHeight для screen-space error. Поворот **не** split/merge |

---

## 3. Что было сломано (2026-08-07, исправлено)

### Cube-sphere inverse mismatch

| Направление | Было | Стало |
|-------------|------|-------|
| UV → direction (меш) | Philip Nowell `SpherifyCubePoint` | без изменений |
| direction → UV (поиск чанка) | **гномоника** `dir / \|dot\|` | **точная инверсия Nowell** (`UnspherifyToFaceTangentCoords`) |

На Greenwich (центр грани +Z) обе проекции совпадали → LOD казался правильным.  
На Cape Canaveral промах до **681 км** — max LOD рисовался далеко от ракеты.

**Симптом:** детальный чанк не под кораблём; thrashing (split/merge), потому что containment-тест и arc-distance считали разные точки.

**Фикс:** `PlanetCubeSphereCoords.UnspherifyToFaceTangentCoords` + делегирование из `PlanetCubeSphere.TryDirectionToFaceUv`.

---

## 4. Цикл кадра (main game Flight)

```
FlightUniverseMovement (100)  — planet slide/rotate
FlightPlanetSphere (250)      — SyncViewerLodFocusFromSimulation
                              — RefreshLodLookAhead (split/merge по таймеру)
                              — DispatchCompletedMeshes
```

Focus direction обновляется **каждый кадр** после движения планеты.

**Profiler (Play):** CPU Hierarchy → `FlightLod.Tick` (quadtree split/merge). After warmup the walk stops at ~6 ms and leftover splits continue next tick; new child meshes attach via `FeedMeshes` (not 16 sync builds inside `Split`). `DispatchMeshes` — CPU mesh jobs. `SyncCoverage` — parent/child swap. Tick Self used to be 50–140 ms from `Mesh.vertices` + stitch clones; vertices are cached. Если Tick мал, а кадр всё равно дорогой — GPU: `EarthPlanetLocalChunks`. F8: `chunks=` / 640 и `maxL`.

**🔒 Поворот планеты / A/D:** `GetFlightPlanetLocalRotation` = ship `FromTo(sim→+Y)` × `FromTo(mesh→sim)`. Не инвертировать yaw в Core. Канон: [[systems/work_terrain.md]] §3.1.

---

## 5. Когда делится (split) — текущая логика

Лист делится, если выполнены условия ниже (порядок важен).

### 5.1 Блокировки

| Проверка | Смысл |
|----------|--------|
| `level >= GetActiveMaxLodLevel()` | Потолок LOD (слайдер + кап по высоте) |
| `ShouldBlockSplitForVisibility` | За горизонтом по **ближайшей точке** чанка — не split |
| `!CanAllocateMoreChunks()` | Бюджет активных чанков |
| Crack prevention | `CanSplitWithoutLodCrack` (сосед не отстаёт > 1 уровня) |

Камера **не** блокирует split. Диск вокруг корабля остаётся, пока вы смотрите в сторону.

### 5.2 Restricted quadtree (соседи)

Соседи по ребру (в том числе через грань куба) не отстают больше чем на **1 LOD**. Общего правила достаточно — без if под площадку или планету.

1. **Кольца +1 (канон)** — под кораблём finest 2×2; шаг = **viewerSpan**. Высота капа = min(слайдер, уровень `V`). Догон: gap&gt;1; same-level только пока `V` ещё хочет split. **RING не стопит соседа, из‑за которого `V` в CRACK**, и RING-merge не схлопывает его, пока CRACK (иначе `chunks=17`, `RING cap=1`). Sibling-balance только у parent листа `V`.
2. **Желание split** — SSE. С кораблём: slant = высота, target не грубее 64 m. Сосед **по ребру**: ближайшая точка. Сосед **только углом** (три L1 на центре грани): центр чанка — иначе slant≈высота режет три иглы L7. Не выше кольцевого капа.
3. **Разрешение split** — `CanSplitWithoutLodCrack`: все 4-соседи того же уровня или мельче.
4. **Догон** — мелкий лист режет соседа. Gap&gt;1 **не** режется RING. На центре грани диагональный L1 догоняет **до LOD корабля** (иначе `L1q 8/7/8/8`); merge не схлопывает этот лист даже на том же LOD. **Не больше 2 split за тик**.
5. **Один split на лист за тик** — после Split дети не сверлятся в том же кадре.
6. **Sibling-balance** — только leftover в finest 2×2 (parent = parent листа `V`). Иначе кузены догонялись до того же LOD, что ракета (пятно / полоса). Split сразу арендует 4 GO.

Горизонт для SSE-split — ближайшая точка UV-прямоугольника. **Merge «целиком за горизонтом»** — тоже ближайшая точка (не farthest).

На шве разного LOD мелкая плитка **пришивает ребро** к полилинии соседа (clone из кэша только на границе).

При `chunks > 75%` капа SSE дальше `0.85×horizon` не идут (`gate=PERIPH`). Clamp 2.5× slant — запас, если диск всё равно набит. F8: `clamp=` (живой), `L1q minL=` (`minL=2` на face-center = горизонт L2, не дыра под ракетой).

Бюджет кадра сначала в ребёнка с кораблём. UV на шве mid — одна клетка (half-open). Надир горизонта — направление корабля, не look-ahead скорости.

Debug-сетка LOD — **local space** чанка (как меш).

**LOD DIAG** (левый верх, скрыт до **F8**): почему каждая четверть 2×2 не split/mesh. F8 → панель + Console. Shift+F8 скрывает.

### 5.2.1 Инварианты 2026-09-12 (все тела, не хаки планет)

Правила в `PlanetPqsLodRules` — **cube UV, горизонт, лист корабля, RING**. Нет `if (Moon)` / `if (Earth)`. Луна и мыс — разные **точки на грани**, один алгоритм.

| Правило | Срабатывает когда | Зачем |
|---------|-------------------|--------|
| Догон через шов L1 до LOD корабля | \|u−0.5\| и \|v−0.5\| ≤ 1/32 грани (четыре L1 сходятся) | Pad на **центре грани** (лунный спавн между 4 чанками). Мыс `uv≈0.39,0.16` **не** входит — регрессия, что Cape не сверлят |
| Не RING-merge внутренние L6–L8 у 0.5,0.5 | тот же порог + клетка касается шва 0.5 | Иначе догон L8 сразу схлопывается (`lastFlap split\|merge`) |
| После maxL догон только от листа `V` | `ViewerLeafNeedsSplit()==false` | Иначе каждое L7 кольца режет кузенов (на Земле L5 **в диске**, на Луне те же L5 часто за лимбом) |
| RING-merge 2×2 | все не-exempt дети сверх капа, не один дальний | Смешанный 2×2 (у пада cap=7, снаружи cap=0) не схлопывать |
| Не RING-merge поддерево с кораблём | UV корабля внутри узла | Pad 2×2 (`parentL7` 4×finest) не merge/split каждый тик |

Проверка: Луна `uv≈0.500,0.500` `L1q 8/8/8/8` `flap 0`; мыс `uv≈0.390,0.159` `flap 0`. Эталон колец на паде 🔒 [[LOD_BASELINE_PAD_START.md]] — не переписывать split/merge «улучшая» Луну.

### 5.3 Обычный split — screen-space error на диске корабля

```
slantRange = расстояние ship → ближайшая точка чанка (не центр)
pixelError = clamp(48 × LodDistanceFactor, 24, 256)
targetSpan = slantRange × (2 × tan(fovY/2) × pixelError / screenHeight) × altitudeMultiplier
```

Split если `nodeSpan > targetSpan` и ближайшая точка чанка ещё не за горизонтом.

У ног (маленький slant) targetSpan мал → LOD 7–9 (pad всё ещё до 64 m).
У горизонта slant ~8 км на паде → targetSpan сотни–тысячи метров, даже если Max LOD = 9.

**Merge hysteresis:** схлопывание когда **span этого узла** `≤ targetSpan × 0.72` (тот же span, что у split), либо все дети **целиком за геометрическим горизонтом** и merge не ломает crack (сосед не более чем на 1 LOD мельче). Без crack-check кольцо сразу после split схлопывалось обратно. Сравнивать span детей (половина) с `target × 0.72` нельзя: кольцо LOD сразу после split попадало в merge и чанки дёргались каждый тик.

**За камерой:** дерево **не** схлопывается. Unity frustum-culls draw. Память на диск дешевле, чем spike при каждом обороте.

**Idle skip:** если корабль сдвинулся < 2.5 m и высота < 3 m, полный `RefreshLodLookAhead` / feed очереди не бежит. Поворот камеры это не триггерит. Skip **не** действует, пока bootstrap/warmup, `DiskHasUnfinishedLod`, нет меша под кораблём, pad грубее finest, gap > 1, чанков ≥ 640, прошлый тик ещё хотел split/mesh, или на центре грани какой-то L1 грубее листа корабля. Split/merge/догон после прогрева — §5.2.1. F8: `flap s=/m=` оба 0.

**Посадка после высоты:** PNG-шелл и collapse в L0: Земля ~0.12 R (~76 км); Луна: coarsen с ~2 км, L2 **~48 км** (не 21 км: иначе с 23 км весь диск — плитки 87 км, шар, потом RING сносит остальное разом), L0 **72 км**, PNG ~75 км. шелл вкл/выкл по фактической высоте (не сглаженной); чанки снова с ~62 км на спуске. Поддерево под кораблём на Луне не merge по SSE до L2 (~48 км); потолок высоты всё равно режет max LOD. Force-split 64 м на Луне гаснет с coarsen start (~2 км), не с 48 км — иначе split дрался с капом (дыры 1–3 плитки, не 2×2). Timewarp >×4: только coarsen/merge, без split (не копить выгрузку до x1).

**Старт / прогрев:** Play после позы зовёт `PrimeFlightTerrainLod`. На диске меши **синхронные только в bootstrap** (warmup — async). Bootstrap не кончается, пока pad грубее finest possible (даже после 180 кадров). Пауза **не** стопит bootstrap/догон. Когда `gap=1` (сосед **на диске**, не L2 за лимбом), 2 split резерв на догон. Пока клетка под кораблём хочет split — дальние SSE не берут слоты; merge при капе трогает диск, не только «мельче viewer».

**SoI Earth↔Moon:** не `Destroy` дерева и не `Prime` в том же кадре. Старое тело hibernate (чанки в RAM), новое стримит с обычным бюджетом кадра; GEBCO/LDEM и splat-паки кэшируются. Как Scaled Space + PQS в KSP.

**1% lows:** merge ≤ 2/тик (altitude-cap merge без этого лимита); GPU upload ≤ 2 меша/кадр; Load Speed выше 4 не увеличивает hitch; догон соседей ≤ 2 split/тик; пауза (`P`) стопит LOD только когда диск уже готов (bootstrap/warmup/gap всё равно тикают); timewarp >×4 не стопит coarsen; пустой feed не обходит дерево; после hitch физика не делает sim×2 в том же кадре.

При бюджете 640: сначала merge дальних 2×2 (не pad, без crack), потом split у корабля, потом догон. Слайдер **Detail reach влево** = мельче плитки дальше от корабля → диск съедает 640 на L8–L9 (`viewerSpan=679m`, `needL=14`).

### 5.4 Merge

- Дети-листья; merge по SSE или когда **весь** узел за горизонтом (**ближайшая** точка > horizon). Farthest-angle считал пересекающие лимб плитки «целиком за» и сразу merge после sibling-split.
- При `chunks ≥ 75%` / нет 4 слотов: merge finest **вне кольца** вокруг корабля (slant > 2.5× плитки с `V`), затем split у корабля. Не держать всё L1 с кораблём — иначе полоса L8 сбоку при плоском поле под ракетой.
- Поддерево viewer/launch site **не merge** ниже 30 km (`PadPlanetRotationLockAltitudeMeters`).
- Parent-coverage: грубый шелл, пока **прямые дети** не готовы. Дети скрыты, только если у родителя **есть** меш. Нет меша — детей не гасить (иначе чёрный квадрат). Split ждёт шелл родителя (sync под кораблём). Hide ≠ destroy. Restricted quadtree действует **и через ребро куба** (`TryResolveUvProbe`). Skirts с LOD≥1 (L0 шелл грани без юбки), обход CCW, cap 400 м. **Шов разного LOD:** мелкое ребро кладётся на ребро грубого соседа (не shared cache). Slope-clamp: интерьер 2D, **ребро 1D из raw** (соседи одного LOD совпадают). Радиальные нормали.

---

## 6. Видимость: два независимых предела

Ключевое правило: **горизонт — геоцентрический угол; split/merge — диск вокруг корабля + SSE; конус камеры — только draw.**
Смешивать горизонт со слайдером View Width нельзя, именно на этом ломалась прогрузка пол-планеты.

### 6.1 Горизонт (по высоте)

```
horizon = acos(R / (R + altitude)) + margin
```

При R = 637 км: на 1 км высоты это **3.2°** (36 км дуги), на 10 км — **10°** (112 км).
Всё дальше — за кривизной планеты, на экран попасть не может → `Dead`, merge + unload.
Считается от **надира корабля**, не от точки прицела (`GetGeocentricAngleFromViewerDegrees`).

### 6.2 Конус взгляда (только отрисовка)

Unity `MeshRenderer` уже отсекает то, что вне frustum. Конус **не** задаёт residency.

Split/merge по **взгляду камеры** отложен: при вращении камеры это давало hitch (split/unload каждый кадр). Диск вокруг корабля + SSE. Поворот камеры только frustum-cull.

`GetViewZone` (Focus/Active/Padding/Dead по `camera.forward`) остаётся для отладки. Split, allocate и merge его не читают.

Слайдер **View Width** в Terrain Graphics больше не грузит/не выгружает чанки.

### 6.3 Иерархическое отсечение (важно)

Проверки видимости применяются к **ограничивающей шапке** узла, а не к его центру или углам:

```
nearest = angleToCenter − angularRadius(span)
```

Крупный узел покрывает огромный участок. Если отсечь его по центру/углам, вместе с ним
отсекается **всё поддерево**, включая потомков, которые были бы видны. Симптом: соседние с
кораблём квадранты навсегда застревали на LOD 1, оставалась только воронка под ракетой —
около 50 чанков при любых настройках слайдеров.

Правило: **SSE-split** разрешён, если ближайшая точка ещё в горизонтном диске; **sibling-balance** на pad 2×2 — даже если ближайшая точка за лимбом; **merge/unload** — только когда невидима вся плитка (ближайшая точка за горизонтом). Поворот камеры не считается.

Родительский чанк **не рисуется**, как только все 4 прямых потомка могут рисовать свой квад. Дети скрыты, пока нет всех 4 мешей. `SyncDrawnCoverage()` каждый LateUpdate (не только LOD-тик / idle skip). Merge: родитель on, дети off. Меш родителя не destroy.

Merge: если шелла нет — parent mesh **синхронно** до Dispose детей, затем дети скрываются и снимаются.

Crack prevention: сосед **за горизонтом** не блокирует split у чанка на краю диска. Сосед **на другой грани куба** — настоящий сосед (UV вне [0,1] больше не «нет соседа»).

### 6.4 Почему грузилось пол-планеты

Слайдер `ViewConeHalfAngleDegrees` (40°–110°) сравнивался с **геоцентрическим** углом
(`GetClosestAngularDistanceToChunkDegrees` — угол между направлениями из центра планеты).
Доля сферы внутри геоцентрического угла θ равна `(1 − cos θ) / 2`:

| Слайдер | Доля планеты в зоне загрузки |
|---------|------------------------------|
| 40° | 12% |
| 75° (дефолт) | 37%, с padding +15° → **50%** |
| 110° | 67% |

То есть даже на минимуме запрашивался кусок в сотни километров, а видно с 1 км высоты — 36 км.
Отсюда «прогружается пол-планеты, слайдер ничего не меняет».

**Velocity look-ahead:** focus для чанка под ракетой смещается по `Velocity × LeadTime` (2.5 s).

**Backpressure:** при PendingAsync ≥ 64 split только чанк под ракетой.

### 6.5 Высота (доля радиуса планеты)

Пороги в `PlanetLodAltitudePolicy` — **не абсолютные метры**, а доля `altitude / planetRadius`
(для Земли R ≈ 637 км):

| Доля R | ~метры | Поведение |
|--------|--------|-----------|
| 0 – 0.8% | 0 – 5 км | Полный ground max LOD |
| 0.8% – 4% | 5 – 25 км | Max LOD падает к 2, targetSpan × до 24 |
| 4% – 8% | 25 – 51 км | Max LOD → 0, агрессивный merge |
| ≥ 12% | ≥ 76 км | Земля: PNG-shell; GEBCO скрыты. Луна: PNG с **~75 км** (после L0 на 72 км) |
| ≤ 10% | ≤ 64 км | Земля: GEBCO снова. Луна: чанки с **~62 км** на спуске |

Раньше `GetActiveMaxLodLevel` был **инвертирован**: выше 30 км разрешал max LOD 15 вместо
упрощения — отсюда «на высоте чанки не упрощаются».

Гистерезис высоты (медленный coarsen вверх, snap вниз) **не** держит слайдер **Max LOD**:
потолок режется сразу; подъём 1→8 снова разрешает split без спуска на 250 м.

### 6.6 Дальний глобус (как Scaled Space в KSP)

Камера полёта: near 1 м, far **2 500 000 м**. Дальний край Земли ≈ высота + 2R. Около **1 млн м** диск начинает срезаться клипом — PNG «пропадает». Это не смена суток.

`PlanetDistantVisualPolicy` + `WorldVisualBridge.GetFlightDistantVisualScale`: **только LOD-глобус** сжимается и подтягивается к кораблю (угловой размер тот же, внутри far clip). `FlightUniverseRoot` не масштабировать — с Луны это тащило Землю и «крутило» фон до входа в SoI Земли. Поворот планеты не меняется ([[systems/PLANET_SPIN.md]]).

| Высота | Картинка |
|--------|----------|
| с ~76 км | PNG-shell вместо чанков |
| с **1,5 млн м** | на оболочке принудительно **2K** (`earth_geographic.png`) |
| с **5 млн м** | та же 2K, грубее сетка (subdivision 1) |

Карта (M) по-прежнему свой масштаб 0.001 и far 100 млн — этот scale к ней не применяется.

---

## 7. Панель Terrain Graphics (main game)

Дублёр слайдеров **Graphics / VAB → Ландшафт (LOD)** (`PlanetLodSettings`). В полёте и map mode панель скрыта; открывается временной кнопкой **GFX** над **CoM**. Sandbox — сразу в Play Mode.

| Слайдер | PlayerPrefs / API | Диапазон | Default | Эффект |
|---------|-------------------|----------|---------|--------|
| **Detail reach** (↓ = farther) | `LodDistanceFactor` | 0.25 – 10 | 2.5 | **Ниже → деталь дальше** (мельче `targetSpan`). **Выше (4–5) → деталь ближе к камере**, меньше CPU |
| **Load Speed (deeper)** | `LoadSpeedPerFrame` | 1 – 12 | 8 | Split + mesh build за кадр; async completions ≈ ×2 |
| **Max LOD (ground detail)** | `MaxLod` | 1 – 15 | 15 | Потолок глубины дерева. Для pad/полёта практично **6–7** |
| **View Width (look cone)** | `ViewConeHalfAngleDegrees` | 40° – 110° | 75° | **Не задаёт LOD.** Отрисовку режет frustum камеры. Слайдер оставлен, чтобы не ломать панель. |

### Почему «LOD Distance» раньше почти не работал

1. **`SplitTargetSpanFactor` записывался, но не использовался** — split шёл через `nodeSpan × LodDistanceFactor`, игнорируя span factor.
2. **`IsPriorityForceSplit`** для чанка под ракетой **обходил** LodDistance полностью (split до 64 m без учёта слайдера).
3. **Жёсткий `SplitDistanceMeters = 10000`** отсекал split далеко от viewer — при этом формула distance включала altitude, ломая логику на высоте.

**Сейчас:** слайдер задаёт pixel error: `48 × LodDistanceFactor` (clamp 24–256 px) в `ComputeSplitTargetSpanMeters`.  
Пример: 1080p, FOV 60°, factor 2.5 → ~120 px. На slant 50 m targetSpan ~6 m (пол поднимет pad-кап 64 m); на slant 8 км → ~1 км.

---

## 8. Ключевые константы (main game)

```csharp
// FlightPlanetSphere
DefaultMinChunkSpanMeters = 24f
DefaultLaunchSiteMaxChunkSpanMeters = 64f
DefaultMaxLodLevel = 15
PadPlanetRotationLockAltitudeMeters = 30000  // WorldVisualBridge

// FlightPlanetTerrainTuning
SplitSpanFactorPerLodDistanceUnit = 0.14f
LodDistanceFactorDefault = 2.5f

// PlanetQuadtreeLod
SeamDetailHalfAngleDegrees = 52f
```

---

## 9. Earth Sandbox (отдельная сцена)

Sandbox использует тот же cube-sphere canon и похожий quadtree, но:

- Focus = **camera local position** на планете (не frame-zero ship anchor).
- View axis = camera look (как main game после фикса 2026-08-07).
- `MaxLod = 6`, отдельные бюджеты и HUD.

Подробности sandbox-цикла — §3–§6 старой версии документа; поведение split/merge/cone **согласовано** с main game по идее, реализация в `EarthSandboxQuadtreeManager.cs`.

---

## 10. Быстрая проверка

**Корабль в центре finest LOD (Cape / не Greenwich):**

1. Flight с pad scenario, LOD debug wireframe ON.
2. Max LOD 15, Load Speed 8+, View Width 75°.
3. Finest wireframe квадрат(ы) должны быть **под ракетой**, не в стороне.
4. Вокруг — кольца coarser LOD, не «один детальный угол».

**LOD Distance slider:**

1. Смотреть на горизонт с высоты 1–5 km.
2. Factor **0.5** → заметно больше мелких чанков вокруг.
3. Factor **8–10** → крупные чанки ближе к камере, меньше split.

**View Width:**

Слайдер больше не выгружает меши за спиной. Поверните камеру на 90°: очередь/async должны остаться пустыми, дерево не перестраивается.

---

## 11. Рабочий снимок (2026-08-23, main game Flight)

Split/merge = SSE + диск корабля. Камера не перестраивает дерево.

| Тема | Как сейчас |
|------|------------|
| Кэш мешей | Shared cache mesh (без `Instantiate`/`Destroy` на split). GO чанков в пуле |
| Async | `PlanetFlightChunkMeshPipeline`: **1 waiter на ключ**, лимит concurrent ~10–20; HUD `waiters` ≈ `async` |
| За камерой | Диск вокруг корабля **живёт**. Камера только frustum-culls draw. Merge только за геометрическим горизонтом |
| Parent coverage | Атомный swap: шелл **или** дети. Merge hysteresis по span **узла**, не детей |
| Split до меша родителя | Ждём шелл (`TryEnsureMeshBeforeSplit`); sync под кораблём |
| Край конуса | Конус больше не режет дерево |
| Idle | Корабль почти стоит → без `RefreshLodLookAhead` / feed очереди |
| Старт | Bootstrap 3/2 на грани корабля; 3 с warmup 4/2 на диск; без split×2 |
| NaN | Sanitize вершин в CPU builder; reject invalid bounds; LineRenderer не ставит NaN corners |

**Рекомендуемые слайдеры (pad / низкий полёт):** Detail reach **2.5–5**, Load Speed **8–12**, Max LOD **7–9**. View Width игнорируется.

**Проверка:** Play → pad → очередь/async = 0 при повороте камеры; 1% low не должен падать только от look-around. Dual-frame 🔒 не трогать.

Файлы: `PlanetQuadtreeNode.cs`, `PlanetFlightChunkMeshPipeline.cs`, `PlanetFlightChunkMeshCache.cs`, `PlanetMeshPool.cs`, `FlightPlanetSphere.cs`, `PlanetPqsAsyncLodSettings.cs`, `PlanetLodViewPolicy.cs`, `DevSettingsHud.cs` (HUD очереди), `TerrainGraphicsDebugPanel.cs`.

---

## 12. Луна (тот же quadtree)

`CelestialTerrainRuntime.LodBodyName`: старт с мыса — `Planet`, `MoonSurfaceStart` — `Moon`. В полёте следует SoI: вход в пузырь Луны пересаживает quadtree/чанки на Луну, Земля становится дальним глобусом (и обратно). Один `FlightPlanetSphere` на LOD-теле.

| Карта | Путь |
|-------|------|
| Цвет | `StreamingAssets/MapMode/moon_geographic.png` (LROC 4K) или `moon_geographic_8k.png` (8192×4096, dev 4K/8K) |
| Высота | `StreamingAssets/Planet/moon_elevation.raw` Int16 м, 64 ppd **23040×11520** из NASA CGI `ldem_64.tif` (float km × 1000). Источник: `StreamingAssets/MapMode/ldem_64.tif` |

Без облаков, атмосферы, океана и land/ocean vertex paint (моря Луны ниже 0 м — это рельеф, не вода). Чанки без vertex COLOR → Unity `a=1` → шейдер Земли двигал вершины зыбью (~1–2 m). `_HasOcean=0`. LDEM 64 ppd ≈ **470 м/пиксель** (GEBCO у Земли тоже ~460 м/пиксель). На Луне это сильнее видно: серый splat без океана, крутые кратеры/горы на частоте Найквиста DEM → «оригами» на среднем плане, не битый `.raw`. Швы квадратов — mixed LOD / stitch (не другая карта). Дальний шелл / карта — LROC PNG. Ближние чанки — тот же splat-шейдер, что у Земли (`EarthPlanetLocalChunks`): RSS-Terrain `MoonLow/Mid/High/Steep` + LROC как макро. Смесь по знаковой высоте LDEM и уклону, без снега/травы/океана. `Moon_Influence` если чёрный — игнорируется (не fallback в земную траву). Pack: `Assets/Content/Environment/RSS-Terrain/Moon`, иначе `PluginData/03-01_Moon`. Нет DDS — fallback на `moon_geographic` PNG. Detail UV: wrap **центрирован** (`PlanetFlightDetailUv`), иначе шов `[0, 16384)` даёт серый крест на 0°N 0°E (пад Луны). На `MoonSurfaceStart` — Cove, flatten к signed LDEM, спин — утро к Солнцу. Skybox полный. Dual-frame 🔒: центр LOD на −Y. С мыса: дальняя Луна **не** тянется на 120 км (это ломало диск перед облаками); 120 км только Земля с лунного пада. SoI → swap LOD mid-flight. SMA Луны пока toy **4.5 Mm**.

---

## 13. История

| Дата | Что |
|------|-----|
| 2026-08-04 | Sandbox quadtree + Terrain Graphics panel |
| 2026-08-07 | **Fix:** cube-sphere inverse (Nowell vs gnomonic) — LOD под ракетой |
| 2026-08-07 | **View LOD pass:** split/merge по camera look + hysteresis; smoothed view axis; merge/cull за спиной; sibling balance off |
| 2026-08-08 | **Fix:** конус взгляда сравнивался с геоцентрическим углом → грузилось до 50% планеты. Разделено на горизонт (по высоте) и конус камеры (frustum + bounding sphere чанка); slant range берётся от камеры |
| 2026-08-08 | **Fix:** орбитальные линии рисовались в sim-осях поверх mesh-frame глобуса в map mode → траектория зеркалилась |
| 2026-08-08 | **Fix:** тест горизонта отсекал крупные узлы целиком → поддеревья рядом с кораблём не могли делиться, потолок ~50 чанков. Перешёл на bounding cap (`centre − angularRadius`) |
| 2026-08-08 | **Perf:** `GetActiveChunkCount()` обходил всё дерево на каждый узел каждый тик (O(N²)) → живой счётчик. Лимит чанков 112 → 640 |
| 2026-08-08 | **Fix:** parent coverage до готовности 4 детей; farthest-angle unload; crack exempt для Dead-соседей; async `TryEnsureMeshBeforeSplit` |
| 2026-08-13 | **Flight LOD snapshot:** кэш+TTL, дедуп waiters, не destroy backside; L0 шелл снимается после split (`FaceN_L0` больше не кроет pad) |
| 2026-08-23 | **SSE + ship disk:** split по projected px; кольца от корабля; не merge за камерой; idle skip тика |
| 2026-08-25 | **Coverage holes:** шелл пока descendant без меша; L0 hide только на паде; merge sync если нет parent mesh |
| 2026-08-26 | **Coverage / seams:** шелл по прямым детям; crack через куб; рёбра 1D-raw; skirts CCW LOD≥4 cap 400 м |
| 2026-08-30 | **Швы дальних чанков** | Юбки только с L4 — кольцо L1–L3 без заслона щели. Юбки с L1, cap 400 м; L0 без юбки. Stitch без RecalculateNormals (радиальные). |
| 2026-08-26 | **Merge hysteresis:** span узла vs target × 0.72 (не span детей). Иначе кольцо split/merge каждый тик |
