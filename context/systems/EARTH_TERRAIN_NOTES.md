# Earth Terrain / LOD / Textures — рабочие заметки

> **Канон координат, UV, Mesh Frame:** [[work_terrain.md]] — читать **первым** перед правками terrain/map.
> Этот файл — детальные заметки по пайплайну, LOD и истории миграции (RSS → GEBCO/NASA).
> Живой план: [[roadmap/PROGRESS.md]] · Changelog: [[roadmap/CHANGELOG.md]].
>
> Последнее обновление заметок: 2026-08-11.

## 1. Что это и зачем

Земля рисуется как **cube-sphere с квадродеревом LOD** (не Unity Terrain). Цель —
KSP-подобный полёт с **честным рельефом GEBCO** (sim = visual = реальные метры) и
**цветом NASA** без RSS/KSP-костылей.

**Источники данных (актуальный канон — см. [[work_terrain.md]]):**

| Ассет | Файл | Назначение |
| --- | --- | --- |
| **Цвет NASA (канон)** | `StreamingAssets/MapMode/earth_geographic.png` | 4096×2048 equirect, Clean Canon CPU/GPU |
| **Высота GEBCO (канон)** | `StreamingAssets/Planet/earth_elevation.raw` | Int16 86400×43200, north = row 0 |
| Цвет fallback | `StreamingAssets/MapMode/Earth21600x10800_geo.tif` | если PNG отсутствует (`MapModeEarthResources`) |
| Высота fallback | `StreamingAssets/Planet/earth_elevation_5400x2700.bin` | если `.raw` недоступен (`PlanetElevationBootstrap`) |

**RSS-Terrain, `.dds`, `DdsTextureLoader`, parallax-биомы — legacy; не смешивать с Clean Canon** (см. §9).

---

## 2. Каноничная математика координат (SimulationCore)

Единый источник: `Assets/Scripts/SimulationCore/World/PlanetGeographicCoords.cs`.

Оси на единичной сфере:
- **+Y** = северный полюс
- **+X** = восток (долгота растёт при движении на восток)
- **lon = atan2(x, z)**

### Долгота → U

```
U = lon / (2π) + 0.5
```

Гринвич (lon = 0) → **U = 0.5** (центр текстуры). Без знаковых инверсий, без `0.75 - U`.

### Широта → V

```
V = lat / π + 0.5
```

| Точка | lat | V |
| --- | --- | --- |
| Южный полюс | −π/2 | **0.0** (низ файла / row 0) |
| Экватор | 0 | 0.5 |
| Северный полюс | +π/2 | **1.0** (верх файла) |

**Важно:** это **противоположно** старой RSS-конвенции (`V = 1 - (lat/π + 0.5)`, север = 0).
Все шейдеры, mesh UV bake и CPU-сэмплеры должны использовать **новую** формулу.

### API

```csharp
PlanetGeographicCoords.DirectionToGeographicUv(direction, out u, out v);
PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRad, lonRad);
double meters = PlanetElevationCatalog.GetElevationAtDirection(direction);
```

### Cube-to-Sphere (terrain_plan 1.1 ✅)

Единый источник: `PlanetCubeSphereCoords.cs` (SimulationCore, Philip Nowell).

```csharp
// Lat/lon → доминантная грань куба + UV [0,1] (для выбора чанка)
PlanetCubeSphereCoords.LatitudeLongitudeRadiansToFaceUv(latRad, lonRad, out face, out u, out v);

// PQS mesh: UV грани → Nowell → GEBCO (вершина меша)
double meters = PlanetCubeSphereCoords.SampleElevationAtFaceUv(field, face, u, v);

// Geographic query: точная высота по lat/lon
double meters = PlanetCubeSphereCoords.SampleElevationAtLatitudeLongitude(field, latRad, lonRad);

// Lat/lon + назначение грани куба, высота по geographic direction
double meters = PlanetCubeSphereCoords.SampleElevationWithCubeFaceAssignment(
    field, latRad, lonRad, out face, out u, out v);
```

Тесты: `Assets/Scripts/Tests/Editor/PlanetCubeSphereCoordsTests.cs` (Unity Test Runner).

### Биомы (terrain_plan 1.2 ✅)

```csharp
BiomeProperties biome = BiomeEvaluator.Evaluate(
    latitudeRadians,
    longitudeRadians,
    elevationMeters,
    surfaceSlopeDotUp   // Dot(normal, up), 1 = flat
);

// или с нормалью поверхности:
BiomeProperties biome = BiomeEvaluator.Evaluate(
    latitudeRadians, longitudeRadians, elevationMeters,
    surfaceNormal, surfaceUp
);
```

Правила (приоритет сверху вниз):

| Условие | Биом | μ |
| --- | --- | --- |
| высота < 0 м | Ocean | 0.15 |
| \|широта\| > 70° | Ice | 0.05 |
| уклон > 35° | Rock | 0.85 |
| высота < 100 м и \|широта\| < 30° | Sand | 0.55 |
| высота ≥ 3500 м | Snow | 0.35 |
| иначе | Grass | 0.65 |

Тесты: `Assets/Scripts/Tests/Editor/BiomeEvaluatorTests.cs`.

### Crack prevention (terrain_plan 1.3 ✅)

**SimulationCore — edge stitching** (честная коллизия):

```csharp
// Fine chunk edge → привязка к coarse-соседу
PlanetPqsEdgeStitcher.StitchHeightGridEdge(
    heights, resolution, PlanetChunkEdgeSide.MinU,
    neighborCoarseEdge, neighborCoarseResolution);

// LOD-правила (как в PlanetQuadtreeNode)
PlanetPqsLodRules.CanSplitWithoutCrack(localLevel, nMinU, nMaxU, nMinV, nMaxV);
PlanetPqsLodRules.RequiresEdgeStitching(localLevel, neighborLevel);
```

**Unity — skirts** (визуал, roadmap 2.4): `PlanetPqsSkirtBuilder.ComputeSkirtDepthMeters(chunkSpan)`.

Тесты: `Assets/Scripts/Tests/Editor/PlanetPqsCrackPreventionTests.cs`.

Unity-обёртка (пока дублирует формулу): `PlanetCubeSphere.DirectionToGeographicUv` — будет
приведена к SimCore на этапе интеграции.

---

## 3. Высота GEBCO — детерминированный пайплайн

### 3.1 Правила

- Значения в TIFF: **реальные метры над уровнем моря** (Int16 или Float32).
- **Nodata для океана нет** — отрицательные метры = батиметрия (до ~−11000 m). Sim знает глубину.
- **Никакого** `VisualHeightScale`, RSS `offset/deformity`, runtime V-flip, column remap.
- **Sim resolution:** 5400×2700 (`PlanetGeographicCoords.SimulationElevationWidth/Height`).
- **Интерполяция:** bilinear на CPU, тип `double`, U wrap, V clamp (`PlanetElevationSampler`).
- **Океан:** `FlightPlanetOceanSphere` на **0 m**. Всё ниже уровня моря визуально под водой.

### 3.2 Масштаб Unity (визуальное преувеличение рельефа)

| Величина | Значение |
| --- | --- |
| Sim radius | `637100 m` (`CelestialBody.Earth.Radius`) |
| Unity flight radius | `637100 m` (`PlanetSurfaceConstants.UnityFlightPlanetRadiusMeters`) |
| **Базовый** mesh offset | `elevationMeters × UnityVisualHeightScale` (`PlanetSurfaceConstants`, сейчас **0.4**) |
| **Flight PQS** (основная игра) | `elevationMeters × ActiveVisualHeightScale` |

Sim и физика — **реальные метры GEBCO 1:1**. На меш идёт только визуальный множитель.

**Два рычага (не путать):**

| Рычаг | Файл | Сейчас | Назначение |
| --- | --- | --- | --- |
| `UnityVisualHeightScale` | `PlanetSurfaceConstants.cs` | `0.4f` | Базовый масштаб высоты на меше; sandbox mini-globe, шейдерные пороги биома |
| `TestHeightScale` | `PlanetSpherifiedChunkMeshBuilder.cs` | `>0` → **заменяет** базу для flight-чанков; `0` → берётся `UnityVisualHeightScale` | Временная калибровка «гиперболы» у земли |

```csharp
// PlanetSpherifiedChunkMeshBuilder.ActiveVisualHeightScale
TestHeightScale > 0 ? TestHeightScale : UnityVisualHeightScale
```

**Эффективный масштаб flight-чанков** = `TestHeightScale` (пока > 0), иначе `0.4`.  
Пример Эверест ~8848 m: при `TestHeightScale=1.0` смещение вершины ≈ **8.8 km** (не 884 m «честного» 0.1).

**Sandbox mini-globe (r≈100):** отдельно `EarthSandboxGlobePaint.DefaultMiniGlobeReliefExaggeration = 10` — только чтобы рельеф был виден на маленькой сфере; flight на r=637100 использует exaggeration `1`.

**История:** CHANGELOG 2026-07-25 — `VisualHeightScale` откатывали 0.3→0.2 из‑за flatten у мыса. Целевой «честный» 1:10 в старых заметках = `0.1f`; в коде база выше для читаемости рельефа.

**Крутить:** уменьшить гиперболу → снизить `TestHeightScale` (или `=0` + уменьшить `UnityVisualHeightScale`); не трогать GEBCO raw / sim elevation.

### 3.3 Формат `earth_elevation.raw` (Int16 GEBCO, high-res)

| Поле | Значение |
| --- | --- |
| Файл | `StreamingAssets/Planet/earth_elevation.raw` |
| Формат | Int16 little-endian, row-major, без заголовка |
| Размер | 86400 × 43200 (~7.46 GB) |
| Значения | метры; отрицательные = батиметрия, 0 = уровень моря |
| Lon | −180°..+180°, Greenwich → **X = 43200** (канон = <see cref="PlanetGeographicCoords"/>) |
| Lat | +90°..−90°, север → **row 0** |

Stitcher `Gebco2026RawStitcher` клеит тайлы w−180° → e+180° слева направо без зеркала.
**Не** применять `1 − geographicU` при чтении `.raw` без доказательства пробером.

### 3.3.1 Валидация ассетов (PlanetAssetProber)

Перед Play / после bake запускать:

- **Editor:** `Space Program → Probe Earth Assets (Canonical Layout)`
- **Play (Editor):** автолог `PlanetAssetProberRuntime` при старте сцены

Классы: `PlanetAssetProber`, `PlanetMapLayoutPolicy`, `PlanetAssetProberMenu`.

На 6 канонических точках (Cape, Morocco, Tibet, 80°W, 60°E, Pacific) для каждого файла
сравнивается сэмпл при **U** и **1−U**. Результат: `Canonical`, `Mirrored` или `Ambiguous`.
Рекомендуемая `PlanetMapLayoutPolicy` — только после пробы; флаги flip применять
централизованно (один сэмплер на ассет), не в лоадере + шейдере раздельно.

Классы:

- `PlanetElevationMmapField` — MemoryMappedFile, не грузит в RAM целиком
- `PlanetElevationRawCoords` — lat/lon → pixel (отдельно от canonical UV старого .bin)
- `PlanetElevationHighResSampler.GetHeightBilinearNew` — новый сэмплер
- `PlanetElevationCatalog.UseHighResElevation` — переключатель A/B (legacy SPEL .bin остаётся)

PlayerPrefs: `SpaceProgram.PlanetElevation.UseHighResRaw` = 1 → high-res при старте (по умолчанию включается, если legacy .bin отсутствует).

**Сборка `.raw` из GEBCO 2026 (8 тайлов):**

```text
dotnet run -c Release --project Tools/Gebco2026RawStitcher
```

Источник: `StreamingAssets/MapMode/gebco_2026_sub_ice_topo_geotiff/`  
Выход: `StreamingAssets/Planet/earth_elevation.raw` (~7.46 GB, построчная склейка, RAM ≈ одна строка).

### 3.4 Формат `.bin` (SPEL v1, legacy debug)

Класс: `PlanetElevationBinaryFormat.cs`

```
Offset  Size   Поле
0       4      magic "SPEL"
4       4      version = 1
8       4      width  = 5400
12      4      height = 2700
16      4      storage = 1 (float32 meters, little-endian)
20      4      reserved
24      ...    width × height × 4 bytes
```

Строки: **row 0 = южный полюс (V=0)**, последняя row = север (V=1).

### 3.4 Классы SimulationCore

| Класс | Файл | Роль |
| --- | --- | --- |
| `PlanetGeographicCoords` | `SimulationCore/World/PlanetGeographicCoords.cs` | UV, lat/lon, без RSS |
| `PlanetCubeSphereCoords` | `SimulationCore/World/PlanetCubeSphereCoords.cs` | Cube-to-sphere (Nowell), PQS sampling |
| `BiomeType` / `BiomeEvaluator` | `SimulationCore/World/Biome*.cs` | Rule-based биомы + трение (terrain_plan 1.2) |
| `PlanetPqsEdgeStitcher` | `SimulationCore/World/PlanetPqsEdgeStitcher.cs` | Edge stitching высот (terrain_plan 1.3) |
| `PlanetPqsSkirtBuilder` | `SimulationCore/World/PlanetPqsSkirtBuilder.cs` | Skirt depth math (roadmap 2.4) |
| `PlanetFlatChunkMeshBuilder` | `UnityAdapter/Flight/PlanetFlatChunkMeshBuilder.cs` | Плоский чанк 33×33 (roadmap 2.1) |
| `PlanetSpherifiedChunkMeshBuilder` | `UnityAdapter/Flight/PlanetSpherifiedChunkMeshBuilder.cs` | Nowell + GEBCO (roadmap 2.2) |
| `PlanetSurfaceConstants` | `SimulationCore/World/PlanetSurfaceConstants.cs` | Радиус, scale 0.1, имена файлов |
| `PlanetElevationBinaryFormat` | `SimulationCore/World/PlanetElevationBinaryFormat.cs` | Чтение/запись `.bin` |
| `PlanetElevationSampler` | `SimulationCore/World/PlanetElevationSampler.cs` | Bilinear double, U wrap, V clamp |
| `PlanetElevationField` | `SimulationCore/World/PlanetElevationField.cs` | Массив + `GetElevationAtDirection` |
| `PlanetElevationCatalog` | `SimulationCore/World/PlanetElevationCatalog.cs` | Process-wide singleton для sim/Unity |

### 3.5 Editor bake

| Класс | Файл | Роль |
| --- | --- | --- |
| `GeotiffElevationReader` | `UnityAdapter/Editor/GeotiffElevationReader.cs` | TIFF → float[] meters (Int16/Float32, uncompressed strip) |
| `GebcoElevationBaker` | `UnityAdapter/Editor/GebcoElevationBaker.cs` | Downsample box-filter → `.bin` |

**Меню:** `Space Program → Bake GEBCO Elevation (.bin)`

По умолчанию:
- Источник: `StreamingAssets/MapMode/Earthrev_elev_21600x10800.tif`
- Выход: `StreamingAssets/Planet/earth_elevation_5400x2700.bin`

> Если TIFF сжат (LZW/deflate) — байкер выдаст ошибку. Нужен uncompressed export
> или добавить декомпрессию в `GeotiffElevationReader`.

### 3.6 Runtime load

| Класс | Файл | Роль |
| --- | --- | --- |
| `PlanetDataPaths` | `UnityAdapter/Flight/PlanetDataPaths.cs` | Пути StreamingAssets |
| `PlanetElevationBootstrap` | `UnityAdapter/Flight/PlanetElevationBootstrap.cs` | Auto-load `.bin` при Play |

Лог при успехе: `PlanetElevationBootstrap: loaded 5400x2700 elevation grid from ...`

---

## 4. Цвет NASA — загрузка (Clean Canon ✅)

**Архитектура «Вариант А: Чистый Канон»** — единая формула для CPU и GPU:

```
texU = geographicU
texV = geographicV   (для NASA PNG; RSS legacy — см. MapGeographicVToRssEarthTextureV)
```

### Пайплайн (линейный, без модификаторов)

```
earth_geographic.png (диск, каноничный)
    → LoadImage()
    → Apply(true)
    → GPU шейдер: DirectionToGeographicUv → sample(U, V)
    → CPU: SampleEarthColorAtGeographicUv / MapGeographicUToEarthColorTextureU → U
```

**Классы:**

| Класс | Файл | Роль |
| --- | --- | --- |
| `PlanetEquirectangularTextureLoader` | `UnityAdapter/Flight/PlanetEquirectangularTextureLoader.cs` | `LoadRgbFromImageBytes` — без flip/mirror |
| `PlanetTerrainTextureResources` | `UnityAdapter/Flight/PlanetTerrainTextureResources.cs` | Runtime cache, `SampleEarthColorAtGeographicUv` |
| `EarthColorMapBaker` | `UnityAdapter/Editor/EarthColorMapBaker.cs` | TIFF → PNG bake (каноничный, as-is) |
| `MapModeEarthResources` | `UnityAdapter/Flight/MapModeEarthResources.cs` | Map mode (M) — та же текстура |

**Шейдеры** (`KerbinPlanetLocalChunks`, `MapModeEarthSurface`, `KerbinPlanetSurface`):
- UV = `lon/(2π)+0.5`, `lat/π+0.5` из нормали/направления
- **Нет** `_ColorLongitudeMirror`, **нет** `1-U`, **нет** `0.75-U`

### Источники

- Runtime: `StreamingAssets/MapMode/earth_geographic.png` (bake из `Earth21600x10800_geo.tif`)
- GPU: полное или даунсэмпленное разрешение для flight chunks
- Map mode (M): **та же** NASA-текстура через `PlanetTerrainTextureResources`

---

## 5. Площадки старта

| Сценарий | Координаты | Примечание |
| --- | --- | --- |
| `PlanetSurfaceStart` | 28.57°N, 80.65°W | Мыс Канаверал / KSC |
| `PlanetSurfaceMorocco` | 31.63°N, 7.99°W | Атлас, западное Марокко |
| `PlanetSurfaceTibet` | 31.48°N, 91.10°E | Тибетское плато |

Конфиг: `LaunchSiteConfig.cs`, меню: `SimulationHud.DrawMainMenu()`.

### Высота падов (правило B + C)

1. **Базовая высота** поверхности из GEBCO по lat/lon площадки (`PlanetElevationCatalog`).
2. **Flatten pad** — принудительное выравнивание рельефа в радиусе вокруг площадки
   (`FlightPlanetSphere.DefaultFlattenRadiusMeters` / `DefaultHardFlattenRadiusMeters`).
3. **+7 m deck** — константа `LaunchPadConstants.PadDeckHeightAboveSphereMeters` поверх
   выровненной поверхности (коллайдер палубы не пересекает террейн).

`LaunchSiteTerrain.Refresh()` должен читать GEBCO через новый API (интеграция в процессе).

---

## 6. Ключевые файлы (актуальные и целевые)

### SimulationCore (готово — фундамент)

- `PlanetGeographicCoords.cs` — каноничная UV
- `PlanetCubeSphereCoords.cs` — cube-to-sphere (Philip Nowell), terrain_plan 1.1 ✅
- `PlanetElevation*.cs` — GEBCO grid, bake format, catalog
- `PlanetSurfaceConstants.cs` — scale, пути
- `LaunchSiteConfig.cs`, `LaunchPadConstants.cs` — площадки (интеграция с GEBCO — TODO)

### Unity — целевые после интеграции

| Файл | Роль |
| --- | --- |
| `PlanetCubeSphere.cs` | UV bake в mesh (новая V) |
| `PlanetQuadtreeNode.cs` | Меш: смещение вершин по GEBCO × 0.1 |
| `PlanetHeightSampler.cs` | Сэмпл из `PlanetElevationCatalog` (не R16 DDS) |
| `PlanetTerrainTextureResources.cs` | Кэш NASA color TIFF |
| `FlightPlanetSphere.cs` | Один color + elevation, без RSS slots |
| `KerbinPlanetLocalChunks.shader` | Простой Lit, прямые UV |
| `KerbinPlanetSurface.shader` / `MapModeEarthSurface.shader` | Map mode, те же UV |
| `LaunchPadView.cs` | Позиционирование палубы |
| `FlightPlanetOceanSphere.cs` | Сфера на 0 m |

### Удалить при интеграции (legacy RSS)

- `DdsTextureLoader.cs`
- `RssEarthTerrainLoader.cs`, `RssTerrainPaths.cs`
- `PlanetSurfaceMaps.cs` (RSS offset/deformity) — заменить или вычистить
- `PlanetBiomeTerrainConfig.cs`, `PlanetBiomeTerrainMaterialBuilder.cs`
- `PlanetParallaxTextures.cs` (parallax paths)
- `EarthTerrainOrientationDiagnostics.cs`, `EarthTerrainReferenceData.cs`, `EarthGlobeVisualDiagnostics.cs`
- `MapModeEarthResources.cs`
- Editor: `RssEarthTexturesSetup.cs`, `RssTerrainTexturesSetup.cs`, `ParallaxTerrainTexturesSetup.cs`

---

## 7. Квадродерево LOD — известные ограничения (без изменений)

Эти баги/компромиссы **не связаны с сменой RSS → NASA** и остаются актуальными:

- **Дедлок сплита** — исправлен через `IsPriorityForceSplit()` у площадки.
- **Осцилляция split/merge** — `TrySplitOnce()` не обходит `WantsSplit`.
- **Стыки граней куба** — нет синхронизации LOD между разными face куб-сферы.
- **Пропадание земли у поверхности** — не расследовано; нужен репро с `LOD Debug: ON`.

Подробности истории фиксов — в git history / старых версиях этого файла.

---

## 8. Площадка Cove_Launch_Site — префаб

`Assets/GameObject/Cove_Launch_Site.prefab` — грязный импорт (разные `localScale` на уровнях).

`LaunchPadView.TryGetPadDeckContactWorldPosition()`:
1. X/Z от `SpawnPoint`
2. Y от `Renderer.bounds` всей инстанции (mesh-top)
3. Лог: `LaunchPadView: DeckAnchor local Y=...`

Порядок инициализации: `LaunchSiteTerrain.Refresh()` **до** `new Simulation(...)` в
`SimulationRunner.InitializeSimulation()`.

---

## 9. Статус миграции (2026-07-26)

### ✅ Сделано (фундамент)

- [x] `PlanetGeographicCoords` — новая UV, RSS-методы удалены
- [x] `PlanetCubeSphereCoords` — Philip Nowell cube-to-sphere + EditMode tests (terrain_plan 1.1)
- [x] `BiomeEvaluator` — rule-based биомы + трение (terrain_plan 1.2)
- [x] `PlanetPqsEdgeStitcher` / `PlanetPqsSkirtBuilder` — crack prevention (terrain_plan 1.3)
- [x] `PlanetElevationField` + `PlanetElevationCatalog` + bilinear sampler
- [x] `PlanetElevationBinaryFormat` (SPEL v1)
- [x] Editor: `GebcoElevationBaker`, `GeotiffElevationReader`
- [x] Runtime: `PlanetElevationBootstrap`, `PlanetDataPaths`
- [x] Константы: реальные метры, `UnityVisualHeightScale = 0.1f`

### 🔲 Следующие шаги (интеграция — проект пока может не компилироваться)

1. Запустить **Bake GEBCO Elevation (.bin)** в Editor
2. `PlanetCubeSphere` — новая V (`lat/π + 0.5`), pole: north → v=1
3. `PlanetHeightSampler` → `PlanetElevationCatalog`
4. `PlanetQuadtreeNode` — убрать `SampleEarthHeightMeters`, реальные метры × 0.1 на меше
5. `GeoTiffColorLoader` — NASA color без flip
6. Шейдеры — прямые UV, убрать parallax/HSV
7. `LaunchSiteTerrain` / `LaunchPadConstants` — GEBCO + flatten + deck
8. Удалить legacy RSS (§6)
9. Map mode — NASA texture, удалить `MapModeEarthResources`

### 🧪 Проверки после интеграции

| Точка | Ожидание GEBCO |
| --- | --- |
| Cape Canaveral 28.57°N, 80.65°W | ~0–5 m (full GEBCO) / ~0–50 m (Earthrev 8-bit) |
| Tibet 31.48°N, 91.10°E | ~4500 m (full GEBCO) / ~7000–8500 m (Earthrev 8-bit, грубее) |
| Эверест | ~8800 m |
| Океан (любая точка) | отрицательные метры (батиметрия) |

Сверка: `PlanetElevationCatalog.GetElevationAtLatitudeLongitudeDegrees(lat, lon)`.

---

## 10. Чего больше НЕ делать

- ❌ V-flip / column-flip / horizontal mirror при загрузке NASA `earth_geographic.png`
- ❌ `_ColorLongitudeMirror`, `texU = 1 - geographicU`, runtime «переводчики» знаков для цвета
- ❌ `MapGeographicUToRssEarthTextureU`, `0.75 - geographicU`, runtime column remap
- ❌ `VisualHeightScale = 0.4` и RSS `EarthHeightOffsetMeters` / `DeformityMeters`
- ❌ Диагностики «legacy-rss» / «mirror(1-geo)» / Cape-Tibet scoring probes в лоадере
- ❌ Parallax Low/Mid/High/Steep для макро-географии
- ❌ Разные U-конвенции для CPU color vs GPU shader

---

## 11. Быстрый чеклист для новой нейронки

1. Прочитать §2 (UV) и §3 (GEBCO) — это закон.
2. Убедиться, что `.bin` существует (`StreamingAssets/Planet/`).
3. При ошибке bake — проверить сжатие TIFF (нужен uncompressed).
4. Любой новый сэмплер высоты/цвета — только через `PlanetGeographicCoords` + `PlanetElevationCatalog`.
5. Цвет NASA: `texU = geographicU` на CPU **и** GPU — без flip/mirror (§4). Сомнения → `PlanetAssetProber` (§3.3.1).
6. Unity mesh offset = `meters * 0.1f`, sim = meters без масштаба.
7. Океан на 0 m, батиметрия под водой.
8. Площадка: GEBCO base → flatten → +7 m deck.
