# work_terrain.md — Канон координат Земли (Вариант А)

**Назначение:** снимок **рабочей** конфигурации геометрии планеты, текстур, карты и визуала. Если «Флорида на Тибете», «меш вывернут», «шов на дата-линии», «карта зеркально» — читать этот файл **до** правок кода.

**Статус (зафиксировано):** Этап 4 — **два слоя координат (Mesh Frame)**. Песочница `Earth_Sandbox_Test` — **Stage 3 MATCH 3/3** (elevation + color), globe paint check OK (Канаверал LAND, UV ≈ 0.276/0.658). Шов ±180° закрыт seam-дубликатами UV.

**Не откатывать:** Этап 5 (единое 3D без X-flip) пробовали и **откатили** — визуал снова в Mesh Frame.

| Слой | Правило X | UV / сэмплеры |
|------|-----------|----------------|
| **Sim + диск** | Запад = **−X** (канон) | `texU = geographicU`, без `1-U` |
| **Unity Mesh Frame** | Позиции вершин и маркеров на глобусе: `mesh.x = -sim.x` | UV из **sim** unit direction, **без** flip |

---

## 1. Священный канон (Вариант А)

| Правило | Формула / значение |
|--------|-------------------|
| Долгота | `lon` в радианах, −π…+π; восток положительный |
| Широта | `lat` в радианах, −π/2…+π/2; север положительный |
| Geographic U | `u = lon / (2π) + 0.5` = `(lon° + 180) / 360` |
| Geographic V | `v = lat / π + 0.5` = `(lat° + 90) / 180` |
| Полюса на картинке | **Север** = `v = 1` (верх PNG), **юг** = `v = 0` |
| Greenwich | `lon = 0` → `u = 0.5` |
| Запад (Флорида) | `lon < 0` → **отрицательный X** в sim 3D |
| Восток | `lon > 0` → **положительный X** в sim 3D |

**Контрольная точка:** Мыс Канаверал **28.57°N, −80.65°W**

- `u ≈ 0.276`, `v ≈ 0.658`
- sim unit: `x = cos(lat)·sin(lon) < 0` (запад = −X), пример **(−0.867, 0.478, 0.143)**
- mesh unit (Mesh Frame): **(+0.867, 0.478, 0.143)** — только X перевёрнут
- суша на `earth_geographic.png`, высота ≈ 5 m в `earth_elevation.raw`

**Код канона:** `SimulationCore/World/PlanetGeographicCoords.cs`

Дополнительные заметки по пайплайну: [[systems/EARTH_TERRAIN_NOTES.md]] · индекс кода: [[architecture/PROJECT_LAYOUT.md]].

```csharp
// sim unit direction
x = cos(lat) * sin(lon)
y = sin(lat)
z = cos(lat) * cos(lon)
lon = atan2(x, z)
```

---

## 2. Файлы на диске (as-is, без рантайм-переворотов)

| Ассет | Путь | Формат | Сэмплинг |
|-------|------|--------|----------|
| Цвет NASA | `StreamingAssets/MapMode/earth_geographic.png` | RGB, equirect 4096×2048 | `GetPixel` / `GetPixelBilinear(u, v)` — **канонический u, v** |
| Высота GEBCO | `StreamingAssets/.../earth_elevation.raw` | Int16, **86400×43200** | `col = round(u·86399)`, `row = round((90−lat°)/180·43199)` — **север = row 0** |
| RSS legacy | `EarthColor.dds`, `EarthHeight.dds` | DDS | **Отдельный путь:** row-flip + `MapGeographicUToRssEarthTextureU` — не смешивать с NASA PNG |

**Policy для NASA PNG + GEBCO:** `PlanetMapLayoutPolicy.CleanCanon` — **все flip = false** (`colorFlipU`, `colorFlipV`, `elevationFlipU`).

**Песочница Stage 3 (проверено):** Канаверал, Тибет, Индийский океан — **MATCH** при каноническом `u,v` без `1-U`.

`PlanetAssetProber` в главной сцене может писать `Color layout=Ambiguous` на 6 точках — это **не** причина менять flip flags, если sandbox Stage 3 = MATCH на целевых точках.

---

## 3. Поток данных (Sim + Mesh Frame)

```
lat/lon (симуляция, спавн, CPU probes)
    ↓
PlanetGeographicCoords.DirectionFromLatitudeLongitude  → sim (x,y,z), west = −X
    ↓
┌────────────────────────────┬─────────────────────────────┐
│ Unity mesh vertex position │ Vertex UV0                  │
│ FlipMeshVertexPositionX    │ DirectionToGeographicUv     │
│ mesh.x = -sim.x            │ (sim unit, без flip)          │
└────────────────────────────┴─────────────────────────────┘
    ↓                              ↓
Mesh Frame geometry            Shader / CPU at (u, v)
    ↓
earth_geographic.png / earth_elevation.raw — без flip
```

**После FlipMeshVertexPositionX на позициях** — обязательно:

```csharp
PlanetMeshWindingUtility.EnsureRenderableOutwardNormals(vertices, triangles);
mesh.RecalculateNormals();  // не normals[i] = unit вручную
```

Mesh Frame X-flip **меняет chirality** треугольников → без `EnsureRenderableOutwardNormals` меш «вывернут» (текстура видна изнутри).

**Запрещено:**

- `texU = 1 - u` в шейдере или CPU для NASA PNG
- `_ColorMapFlipLongitude` ≠ 0 при `earth_geographic.png`
- Пересчёт UV в fragment из world-space, если vertex UV уже канонический
- Этап 5: убрать X-flip и «подкрутить» только UV или winding

**Разрешено (Mesh Frame, только Unity позиции):**

- `PlanetCubeSphere.FlipMeshVertexPositionX` / `SimDirectionToMeshFrame`
- `WorldVisualBridge.SimToUnityMeshFramePosition` — map mode, celestial bodies на глобусе
- `WorldVisualBridge.SimNormalToMeshFrameUnity` — outward на mesh-frame глобусе
- `EarthSandboxGlobePaint.MeshDisplayUnitFromLatitudeLongitudeDegrees` — маркер в песочнице

---

## 3.1 Flight: два центра (🔒 не ломать, 2026-08-15)

Живой снимок суток / терминатора / сглаживания кадра: [[systems/PLANET_SPIN.md]] (Play 2026-08-19, без телепортов суши).

Проверено в Play: **A/D и тяга совпадают с носом**, земля уезжает назад.

| Слой | Центр | Что движется |
|------|--------|----------------|
| **SimulationCore** | планета / SoI | летит **ракета** |
| **Unity flight** | корабль в `(0,0,0)` | **планета** уезжает по −Y; ориентация — surface ENU (на pad постоянная) |

Корабль и планета в **одной** уникальной ENU-рамке (`GetFlightFrameRotationFromEastAndUp`): географический восток → +X, радиал → +Y.  
Планета: body-fixed точка под кораблём (InverseSpin × inertial radial) в Mesh Frame → та же ENU — `GetFlightPlanetLocalRotation` / `GetInterpolatedBodyFixedUnderShip`. Суточный spin **сокращается**: на pad кватернион постоянный (площадка-ребёнок не трясётся). После отрыва формула та же — нет скачка Landed→Flying.  
Mesh Frame — отражение: `Cross(meshEast, meshUp)` смотрит **против** `flipX(simNorth)`. Без коррекции грунт при полёте на север едет на 180° от носа/скорости (восток при этом совпадает). Визуал корабля, камеры и солнца: `AlignFlightVectorWithMeshFrameNorth` (инверсия visual Z). Не yaw 180° и не инверсия тяги в Core.  
Кадр: `nlerp(InverseSpin(r_prev), InverseSpin(r_curr))`, не `InverseSpin(сейчас)×lerp(r)` — иначе при включённом спине земля дёргается одинаковыми шагами (при выключенном спине шагов нет).  
Map: инерциальный глобус `GetMapPlanetMeshFrameSpinRotation` (F Spin F).

**Запрещено:**

- `Quaternion.FromToRotation` для ориентации планеты или ship-frame (свободный твист вокруг вертикали → тряска pad)
- Переключать pad-lock / shader-spin / inertial по высоте или по Landed/Flying (скачок долготы)
- Крутить **только** планету отдельным east-twist, корабль оставлять на FromTo (земля едет против носа)
- Инвертировать yaw/тягу в `SimulationCore` (`ShipManualAttitudeRamp` pad: `A/D` → `−yawInput` на +Z) «под картинку»
- Убирать Mesh Frame (Этап 5) ради «проще совместить оси»

Код: `WorldVisualBridge.GetFlightPlanetLocalRotation`, `GetSimToFlightFrameRotation`, `AlignFlightVectorWithMeshFrameNorth`, `FlightUniverseMovement`, `ShipManualAttitudeRamp.ResolveManualBodyRateTargets`.

---

## 4. Генерация меша планеты

### 4.1 PQS / flight чанки (основная игра)

| Компонент | Файл | Что делает |
|-----------|------|------------|
| Cube-sphere math | `PlanetCubeSphereCoords.cs`, `PlanetCubeSphere.cs` | Face UV → sim unit direction **без** negation X |
| Spherified chunk | `PlanetSpherifiedChunkMeshBuilder.cs` | `FlipMeshVertexPositionX(surfacePoint)`; UV из sim `unitDirection`; winding ниже + `EnsureRenderableOutwardNormals` |
| Flat chunk (LOD 0) | `PlanetFlatChunkMeshBuilder.cs` | `vertices.x = -localX`; UV из face UV |
| UV на меш | `PlanetCubeSphere.DirectionToGeographicUv` | Канонический geographic UV (sim direction) |

**Winding PQS (оригинальный, до Этапа 5):**

```
Tri1: i, i+res+1, i+res
Tri2: i, i+1, i+res+1
```

Затем `EnsureRenderableOutwardNormals`.

**Map mode icosphere:** `FlightPlanetMapSphere.cs` — Mesh Frame на позициях, UV из sim unit. Midpoint subdivision: average **sim** units, не mesh positions.

### 4.2 Песочница UV-сфера (`Earth_Sandbox_Test`)

`EarthSandboxGlobePaint.BuildCanonicalUvSphere` — **отдельная** сетка, не копия PQS winding.

| Параметр | Значение |
|----------|----------|
| Широта | кольца `0…latitudeSegments`, lat ∈ [−85°, +85°] (`MaxLatitudeDegrees = 85`) |
| Долгота | `ringVerts = longitudeSegments`, lon ∈ [−180°, +180°) **без** дубликата +180° |
| Позиция | `meshUnit = SimDirectionToMeshFrame(simUnit)` |
| UV | `PlanetGeographicCoords` из sim lat/lon |

**Body quads (lat растёт к северу):**

```
Tri1: current, east, next
Tri2: east, eastNext, next
```

где `east` на последнем lon-сегменте = **seam-дубликат** (см. §4.3), не `current+1`.

**Полярные капы:** после глобального `EnsureRenderableOutwardNormals` — **один** flip только cap-треугольников (`FlipCapTrianglesToMatchBody`), body не трогать.

**GPU текстура песочницы:** `EarthSandboxGeographicRaster` — `wrapMode = Repeat`, bilinear.

### 4.3 Шов дата-линии (±180°) — критично

**Проблема:** если квад через меридиан соединяет UV ≈ 0.99 и UV = 0, GPU линейно интерполирует через середину текстуры → вертикальная «рваная» полоса в Тихом океане.

**Решение (рабочее):**

1. Основная сетка: `ringVerts = longitudeSegments` (одна колонка на −180°, **нет** второй на +180°).
2. Дополнительные вершины `seamDuplicate[lat]`: **та же 3D-позиция** что `lon=0`, но **UV.x = 1.0** (не 0).
3. Квад с `lon = ringVerts−1` использует `seamDuplicate[lat]` как восточную вершину, не индекс `lon=0`.
4. Южный/северный кап: последний lon-сегмент тоже ведёт в `seamDuplicate`, не в `lon=0` с U=0.

**Не возвращать:** `ringVerts = longitudeSegments + 1` с `Lerp(−180°, +180°)` — две колонки в одной точке + плохая UV-интерполяция на шве.

---

## 5. CPU-сэмплеры цвета и высоты

| Функция | Файл | Канон (NASA PNG) |
|---------|------|------------------|
| `MapGeographicUToEarthColorTextureU` | `PlanetTerrainTextureResources.cs` | **return geographicU** |
| `MapGeographicVToEarthColorTextureV` | то же | **return geographicV** |
| `SampleEarthColorAtGeographicUv` | то же | `GetPixelBilinear(u, v)` |
| Высота GEBCO | `PlanetElevationCatalog.cs` | raw read по каноническому u + north-at-row-0 |
| Layout policy | `PlanetMapLayoutPolicy.CleanCanon` | все flip = false для NASA |

`ApplyEarthColorShaderProperties` → `_ColorMapFlipLongitude = 0`, `_ColorMapFlipLatitude = 0`.

---

## 6. GPU шейдеры

| Шейдер | UV в fragment |
|--------|----------------|
| `KerbinPlanetLocalChunks.shader` | `input.latLongUv` (vertex), **не** `ApplyEarthColorMapLayoutUv` |
| `MapModeEarthSurface.shader` | `input.geographicUv` |
| `KerbinPlanetSurface.shader` | `input.geographicUv` |
| `SandboxEarthGlobe.shader` | vertex `geographicUv` → `SAMPLE_TEXTURE2D(_BaseMap, geographicUv)` |

Формула пересчёта (только если vertex UV не задан):

```hlsl
longitudeRadians = atan2(direction.x, direction.z);
geographicU = longitudeRadians * (0.5 / PI) + 0.5;
geographicV = asin(direction.y) * (1.0 / PI) + 0.5;
```

На Mesh Frame глобусе direction в world-space **не** совпадает с sim — **не** пересчитывать UV из world position, использовать vertex UV.

---

## 7. Маркеры, спавн, корабли

**Симуляция / спавн / CPU probes** — sim unit, **без** X-flip:

```csharp
SimVector3 d = PlanetGeographicCoords.DirectionFromLatitudeLongitude(latRad, lonRad);
Vector3 simUnit = new Vector3((float)d.x, (float)d.y, (float)d.z);
position = simUnit * (radius + offset);
```

**Визуал на Mesh Frame глобусе / map mode (M)** — flip только **позиции**:

```csharp
// песочница
EarthSandboxGlobePaint.MeshDisplayUnitFromLatitudeLongitudeDegrees(lat, lon) * radius;

// основная игра
WorldVisualBridge.SimToUnityMeshFramePosition(simWorld, renderOrigin, positionScale);
```

Меш глобуса — **body-fixed** Mesh Frame. Корабль/орбиты в map — Mesh Frame **инерциальных** sim-позиций (`Spin * bodyFixed`). Поворот глобуса: `GetMapPlanetMeshFrameSpinRotation` (`R * FlipX(v) = FlipX(Spin * v)`). Не телепортировать корабль и не держать `rotation = identity` при ненулевом спине.

UV и сэмплинг текстур — всегда из **sim** канона, не из mesh-frame позиции.

`LaunchSiteTerrain.cs` → `PlanetCubeSphere.DirectionFromLatLongRadians` (sim канон).

---

## 8. RSS / KSP legacy (изолированный трек)

Только если **нет** `earth_geographic.png`:

- `DdsTextureLoader` — row flip для DDS (south→north)
- `MapGeographicVToRssEarthTextureV(v) = 1 - v`
- `MapGeographicUToRssEarthTextureU(u) = wrap(0.75 - u)`
- `PlanetAssetProber` может выставить `colorFlipU` для старых DDS

**Не переносить** эти правила в NASA PNG путь.

---

## 9. Чеклист отладки

1. **Stage 3 в песочнице** (`EarthAssetValidator`, сцена `Earth_Sandbox_Test`): elevation + color **3/3 MATCH**.
2. **Globe paint check:** `canon unit.x < 0`, `mesh unit.x > 0`, `UV ≈ (0.276, 0.658)`, GetPixel = **LAND**.
3. **Шов ±180°:** визуально нет вертикальной «рваной» полосы; Аляска и Сибирь стыкуются.
4. **Меш:** снаружи видна текстура; изнутри пустота/зеркало → winding + `EnsureRenderableOutwardNormals`, не UV flip.
5. **Карта M:** Флорида левее центра (запад = визуально «лево» на mesh frame), не зеркально.
6. **В игре:** `PlanetThreePointsOfTruthDiagnostic` / Cape/Tibet/Pacific без WARNING (если подключен).
7. После замены PNG: перезапуск Play Mode (кэш `PlanetTerrainTextureResources`).

**Косметика (не шов):** тёмное кольцо у полюсов — сетка до ±85° + pole vertex; отдельная задача.

---

## 10. Типичные ошибки и правильный фикс

| Симптом | Неверный «фикс» | Правильный фикс |
|---------|-----------------|-----------------|
| Текстура зеркально E/W | `uv.x = 1-u` в шейдере | PNG канонический; убрать flip U |
| Маркер на суши, под ним океан | X-flip только на меш без winding | `FlipMeshVertexPositionX` + `EnsureRenderableOutwardNormals` |
| Внутри меша карта «правильная» | invert normals вручную | winding / `EnsureRenderableOutwardNormals` |
| Вертикальная полоса на дата-линии | `ringVerts+1` или wrap UV 0.99→0 | seam-дубликаты с U=1 (§4.3) |
| Тибет вместо Флориды | Костыль −80° → +80° | `atan2(x,z)` и знак lon в sim |
| Map spawn в океане, flight ОК | Сдвинуть маркер к мысу | Спин глобуса: `GetMapPlanetMeshFrameSpinRotation`, не identity |
| Цвет ок, высота нет | `v*height` vs north-row-0 | raw: `row = (90-lat)/180 * (H-1)` |
| Убрать Mesh Frame (Этап 5) | один 3D без flip | **откат** — возврат Mesh Frame + winding |

---

## 11. Ключевые файлы (быстрый индекс)

```
SimulationCore/World/PlanetGeographicCoords.cs      — математика канона
SimulationCore/World/PlanetMapLayoutPolicy.cs       — flip flags (все false для А)
SimulationCore/World/PlanetElevationCatalog.cs      — GEBCO raw
SimulationCore/World/PlanetAssetProber.cs           — legacy DDS / диагностика
UnityAdapter/Flight/PlanetCubeSphere.cs             — FlipMeshVertexPositionX, SimDirectionToMeshFrame
UnityAdapter/Flight/PlanetMeshWindingUtility.cs       — EnsureRenderableOutwardNormals
UnityAdapter/Flight/PlanetSpherifiedChunkMeshBuilder.cs
UnityAdapter/Flight/PlanetFlatChunkMeshBuilder.cs
UnityAdapter/Flight/PlanetTerrainTextureResources.cs
UnityAdapter/Flight/FlightPlanetMapSphere.cs
UnityAdapter/Flight/LaunchSiteTerrain.cs
UnityAdapter/Rendering/WorldVisualBridge.cs         — SimToUnityMeshFramePosition, GetMapPlanetMeshFrameSpinRotation
UnityAdapter/Rendering/BodyView.cs                  — map mode mesh frame positions
Assets/Shaders/KerbinPlanetLocalChunks.shader
Assets/Shaders/MapModeEarthSurface.shader
Assets/Shaders/KerbinPlanetSurface.shader
Assets/Shaders/SandboxEarthGlobe.shader
EarthSandbox/EarthSandboxGlobePaint.cs              — UV sphere + seam duplicates
EarthSandbox/EarthSandboxGeographicRaster.cs          — CPU pixels + GPU Repeat
EarthSandbox/EarthAssetValidator.cs                   — Stage 3/4 validation + marker
history/test_earth_sandbox_roadmap.md               — изолированная калибровка (архив)
```

---

## 12. История (почему так, не менять логику)

1. Ассеты на диске проверены в песочнице — **канон u,v верен** (Stage 3 MATCH).
2. Баги были в **двойном базисе** и winding: sim (west = −X) vs mesh с `−X` на позициях без коррекции chirality.
3. **Этап 5** (убрать Mesh Frame, один 3D) ломал калибровку — **откачен**. Рабочий визуал = Этап 4.
4. Шов ±180° — отдельная UV-интерполяция; исправлен seam-дубликатами, не flip U.
5. Баг «в основной игре, не в ассетах» — если sandbox OK, искать в PQS/map mode/shader, не в PNG/raw.

*Этот файл описывает состояние, при котором песочница работала. Не править постфактум «для удобства» — только если архитектура снова меняется намеренно.*
