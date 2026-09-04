# Промпт-контекст: Земля работает (Этап 4) → подготовка Этапа 5

Скопировать блок ниже в другую нейросеть. Полный канон: [[systems/work_terrain.md]] (не менять).

---

## PROMPT

Проект: Unity space sim, Земля equirect + GEBCO. **Сейчас работает Этап 4.** Этап 5 (единое 3D без X-flip) **уже пробовали и откатили** — снова в Mesh Frame. Не ломать ассеты и UV-канон.

### Канон sim (симуляция, диск, CPU, UV)

- `lon ∈ [−π,+π]`, восток +; `lat`, север +.
- `geographicU = lon/(2π)+0.5`, `geographicV = lat/π+0.5`. Север PNG = v=1.
- Sim unit: `x=cos(lat)sin(lon)`, `y=sin(lat)`, `z=cos(lat)cos(lon)` → **запад = −X**.
- Контроль: Канаверал 28.57°N, −80.65°W → u≈0.276, v≈0.658, sim unit ≈ (−0.867, 0.478, 0.143), LAND.
- Ассеты **as-is**: `earth_geographic.png` (4096×2048), `earth_elevation.raw` (86400×43200 Int16, north=row0). **Все flip flags = false** (`PlanetMapLayoutPolicy.CleanCanon`). **Никакого `1-U`** для NASA PNG в CPU/GPU.

### Mesh Frame (только Unity позиции геометрии и маркеров на глобусе)

- `mesh.x = -sim.x` (`FlipMeshVertexPositionX` / `SimToUnityMeshFramePosition`).
- **UV vertex = канон из sim direction, без flip.** Шейдеры сэмплят vertex UV, не world position.
- Mesh Frame X-flip **реверсит winding** → после построения треугольников **обязательно** `PlanetMeshWindingUtility.EnsureRenderableOutwardNormals`, затем `RecalculateNormals()`.

### Песочница (эталон «работает»)

- Сцена `Earth_Sandbox_Test`, `EarthAssetValidator`: Stage 3 **3/3 MATCH** elev+color; globe paint: mesh unit.x>0 при sim.x<0, UV OK, LAND.
- `EarthSandboxGlobePaint`: UV-сфера lat∈[−85°,+85°], `ringVerts=longitudeSegments` (без дубликата +180°).
- **Шов ±180°:** отдельные seam-вершины (та же 3D что lon=0, но **U=1**) для квадов/капов через меридиан — иначе GPU интерполирует UV 0.99→0 через середину текстуры.
- Полярные капы: после global winding fix — один flip **только** cap triangles.
- GPU: `EarthSandboxGeographicRaster`, `wrapMode=Repeat`.

### Основная игра (должна совпасть с песочницей)

- PQS: `PlanetSpherifiedChunkMeshBuilder` — flip X на vertex, UV из sim, winding `i,i+res+1,i+res` / `i,i+1,i+res+1` + `EnsureRenderableOutwardNormals`.
- Map mode (M): `FlightPlanetMapSphere` + `BodyView` — mesh frame **позиции**, UV канон.
- Шейдеры: `KerbinPlanetLocalChunks`, `MapModeEarthSurface`, `KerbinPlanetSurface`, `SandboxEarthGlobe` — vertex geographic UV, flip shader props = 0.

### Запрещено

- `texU=1-u`, `_ColorMapFlipLongitude≠0`, UV из mesh-frame world direction, ручные inverted normals вместо winding.
- Менять flip flags по `PlanetAssetProber Ambiguous` если sandbox Stage 3 MATCH.
- Вернуть `ringVerts=segments+1` с Lerp(−180,+180) на UV-сфере.

### Этап 5 (цель перехода — обсуждать, не делать вслепую)

**Идея:** один 3D базис (убрать Mesh Frame X-flip), сохранить **тот же визуал**: Флорида на Флориде, E/W не зеркально, шов дата-линии чистый, нормали наружу, Stage 3 MATCH.

**Почему откатили:** убрали flip без полной синхронизации winding + UV + map mode + маркеров → зеркало/inside-out/маркер≠текстура.

**План перехода (минимум):**

1. Зафиксировать эталон: sandbox Stage 3/4 + скриншот шва и Канаверала.
2. Одна формула позиций везде (sim unit на mesh) **или** явная матрица/конвенция с документированным winding.
3. Пересчитать winding без `EnsureRenderableOutwardNormals` как костыля — или оставить utility, но без X-flip.
4. UV и сэмплеры **не трогать** (sim geographicU).
5. Синхронно: PQS, map icosphere, sandbox, `WorldVisualBridge`, маркеры, VAB presentation.
6. Проверка: Cape unit.x<0, u≈0.276, LAND; шов Тихого океана; map M; flight PQS.

Ключевые файлы: `PlanetGeographicCoords.cs`, `PlanetCubeSphere.cs`, `PlanetMeshWindingUtility.cs`, `PlanetSpherifiedChunkMeshBuilder.cs`, `FlightPlanetMapSphere.cs`, `WorldVisualBridge.cs`, `BodyView.cs`, `EarthSandboxGlobePaint.cs`, `EarthAssetValidator.cs`.
