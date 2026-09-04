# Earth Ground Textures — роадмап (основная игра)

> **Назначение:** живой чеклист перехода от серого GEBCO-only пола к нормальному визуалу с **своими** tileable-текстурами.  
> **Не путать с:** [[systems/work_terrain.md]] (UV/Mesh Frame канон) · [[systems/EARTH_TERRAIN_NOTES.md]] (GEBCO/NASA макро) · sandbox Stage 3.  
> **Статус кластера:** см. [[roadmap/PROGRESS.md]] — здесь только ground-splat / flight close-up.

**Как пользоваться:** отмечай `[x]` по мере готовности. После значимого шага — одна строка в [[roadmap/CHANGELOG.md]].

---

## Легенда

| Маркер | Значение |
|--------|----------|
| `[ ]` | не начато |
| `[x]` | готово |
| `🟡` | в работе (опционально в тексте пункта) |

---

## Три слоя (не смешивать)

| Слой | Ассет / код | Роль | Статус |
|------|-------------|------|--------|
| **Рельеф** | `earth_elevation.raw` + GEBCO mmap | форма меша (sim 1:1, Unity ×0.1) | `[ ]` проверен в flight |
| **Макро-география** | `earth_geographic.png` | «где на планете» (орбита, macro blend) | `[ ]` проверен в flight/map |
| **Пол close-up** | 4× tileable Low/Mid/High/Steep | детальный ground-splat у земли | `[ ]` свои тайлы подключены |

---

## Фаза 0 — Baseline: включить «живой» пол (без своего арта)

Цель: убедиться, что **не GEBCO-only**, а `KerbinPlanetLocalChunks` + splat вообще работают в основной игре.

- [ ] **0.1** Editor → `Space Program → Planet Elevation → Legacy RSS Textures + Detail` (`UseGebcoOnlyPipeline = false`)
- [ ] **0.2** Play → в логе **нет** `GEBCO-only sandbox flight chunks`; есть `KerbinPlanetLocalChunks` / terrain set
- [ ] **0.3** GEBCO raw загружен (`PlanetElevationBootstrap`, high-res `.raw`)
- [ ] **0.4** `earth_geographic.png` на месте; после смены PNG — перезапуск Play (кэш `PlanetTerrainTextureResources`)
- [ ] **0.5** Flight у земли: чанки **не однотонно серые** (RSS placeholder или partial set OK)
- [ ] **0.6** Map mode (M): макро-цвет по географии, Флорида не зеркально
- [ ] **0.7** Finest LOD под ракетой (Cape), не у Greenwich — см. [[systems/LOD.md]]

**Критерий закрытия фазы:** рельеф GEBCO + хоть какие-то splat-тайлы на полу + макро NASA читается.

---

## Фаза A — Быстрый мост (RSS Parallax как placeholder)

Цель: «чтобы не серое» за один вечер, **не** финальный арт.

- [ ] **A.1** RSS-Terrain / Parallax DDS на диске (Kerbin `PluginData` или `Configure RSS-Terrain Earth Textures`)
- [ ] **A.2** В логе `FlightPlanetSphere`: Influence, Low/Mid/High/Steep albedo — не `missing`
- [ ] **A.3** Полёт с падa: текстура тайлится, не «одна серая плита»
- [ ] **A.4** Macro blend на низкой высоте (~0–300 m): видна география NASA поверх detail
- [ ] **A.5** Зафиксировать скрин/заметку «как выглядит RSS bridge» — эталон до замены на свои тайлы

**Критерий:** пригодно для **LOD-debug** и отладки split/merge с нормальным полом.

---

## Фаза B — Свой арт (tileable, не «текстура всей Земли»)

### B.1 Ассеты (8 файлов минимум)

Папка (целевая): `StreamingAssets/Terrain/Earth/`

| Слот | Файл | Смысл | Готово |
|------|------|-------|--------|
| Low albedo | `low_albedo.png` | песок, берег, сухая равнина | `[ ]` |
| Low normal | `low_normal.png` | опционально | `[ ]` |
| Mid albedo | `mid_albedo.png` | трава, лес, степь | `[ ]` |
| Mid normal | `mid_normal.png` | опционально | `[ ]` |
| High albedo | `high_albedo.png` | снег / альпийский камень | `[ ]` |
| High normal | `high_normal.png` | опционально | `[ ]` |
| Steep albedo | `steep_albedo.png` | скалы, обрывы | `[ ]` |
| Steep normal | `steep_normal.png` | опционально | `[ ]` |
| Splat noise | `splat_noise.png` | опционально (вместо Influence) | `[ ]` |

Требования к арту:

- [ ] **B.1a** Seamless tileable, 1024×1024 или 2048×2048
- [ ] **B.1b** Единый PBR-стиль (albedo sRGB, normal linear)
- [ ] **B.1c** Лицензия своя / CC0 / собственная — **не** KSP mod assets в релиз

### B.2 Код: свой loader (без DDS / KSP paths)

- [ ] **B.2.1** `PlanetEarthTerrainPaths.cs` — пути `StreamingAssets/Terrain/Earth/`
- [ ] **B.2.2** Загрузка PNG через Unity Texture2D (без `DdsTextureLoader` для ground)
- [ ] **B.2.3** `PlanetBiomeTerrainMaterialBuilder` / `PlanetParallaxTextures` — ветка «свои Earth tiles» vs legacy RSS
- [ ] **B.2.4** PlayerPrefs или флаг: `UseCustomEarthGroundTiles` (или замена default при наличии папки)
- [ ] **B.2.5** `UseGebcoOnlyPipeline` остаётся **false** для flight с тайлами

### B.3 Пороги высоты / склона (Земля, не KSP Parallax.cfg)

Текущий KSP-канон: `PlanetBiomeTerrainConfig` (снег с ~2572 m). Целевой — ближе к `BiomeEvaluator`:

| Параметр | KSP (сейчас) | Целевое (Земля) | Готово |
|----------|--------------|-----------------|--------|
| Coast sand | все низменности | пустыни (NASA tan) + берег моря 0–90 m, не озёра | `[x]` 2026-08-14 |
| Snow | высота 2200 m (всё плато) | широта + макушки (склон), полюса ≥60° | `[x]` 2026-08-14 |
| Desert ≠ snow | luma → снег | тёплый R&gt;G не снег | `[x]` 2026-08-14 |
| Tile scale | 4 m / Parallax 0.55 | RSS PQS 200/800 **real-m**, × (R_unity / 6371000) для 1:10→1:1 | `[x]` 2026-08-14 |
| Macro blend | высота **террейна** | высота **камеры** (реальные м × world scale) | `[x]` 2026-08-14 |
| Steep slope | dot 0.7 | ~0.48 | `[x]` базово |

- [x] **B.3.1** Пороги в `PlanetBiomeTerrainConfig` + шейдер
- [x] **B.3.2** Cape/берег: песок у моря; Tibet: камень на плато, снег на пиках; Atlas: снег на макушках, не NASA-белый

### B.4 Упрощение шейдера (опционально, после B.2)

- [ ] **B.4.1** Режим без `_SplatMap` / Influence — веса только height + slope + macro NASA
- [ ] **B.4.2** Убрать мёртвый `ApplyEarthColorMapLayoutUv` из fragment или документировать «не использовать»
- [ ] **B.4.3** `PlanetSpherifiedChunkMeshBuilder.TestHeightScale = 0` для честного рельефа ×0.1 (не ×2 debug)

**Критерий закрытия фазы B:** flight у земли на **своих** тайлах, без RSS DDS, макро NASA на низкой высоте, Cape визуально узнаваем.

---

## Фаза C — Приёмка (основная игра)

- [ ] **C.1** Stage 3 sandbox (если меняли PNG/raw): elevation + color **MATCH** 3/3 — [[systems/work_terrain.md]]
- [ ] **C.2** Flight pad Cape: finest LOD **под ракетой**
- [ ] **C.3** Map (M): Флорида не зеркально; орбита не flipped
- [ ] **C.4** Нет вертикальной полосы на даталайне (шов ±180°)
- [ ] **C.5** С высоты 1–5 km: нет «корзинной сетки» (стохастический detail-тайлинг работает)
- [ ] **C.6** Океан на 0 m (`FlightPlanetOceanSphere`), батиметрия под водой
- [ ] **C.7** Обновить [[roadmap/PROGRESS.md]] + строка в [[roadmap/CHANGELOG.md]]

---

## Что НЕ делать (чеклист «не сломать»)

- [ ] Не трогать UV / Mesh Frame / `FlipMeshVertexPositionX` ради ground-текстур
- [ ] Не `texU = 1-u` / `_ColorMapFlipLongitude` для NASA PNG
- [ ] Не рисовать equirect «всей Земли» для пола — только tileable слои
- [ ] Не смешивать RSS `EarthColor.dds` с каноном `earth_geographic.png`
- [ ] Не переписывать шейдер с нуля, пока не проверен `KerbinPlanetLocalChunks` с placeholder

---

## Ключевые файлы

```
# Шейдер / материал
Assets/Shaders/KerbinPlanetLocalChunks.shader
UnityAdapter/Flight/PlanetBiomeTerrainMaterialBuilder.cs
UnityAdapter/Flight/PlanetParallaxTextures.cs
UnityAdapter/Flight/PlanetBiomeTerrainConfig.cs
UnityAdapter/Flight/PlanetTerrainPipelineSettings.cs   # UseGebcoOnlyPipeline
UnityAdapter/Flight/FlightPlanetSphere.cs              # CreateLocalChunksMaterial

# Макро + рельеф (не ground tiles)
UnityAdapter/Flight/PlanetTerrainTextureResources.cs
SimulationCore/World/PlanetElevationCatalog.cs
StreamingAssets/MapMode/earth_geographic.png
StreamingAssets/Planet/earth_elevation.raw

# Editor toggles
UnityAdapter/Editor/PlanetElevationEditorPrefs.cs
  → Legacy RSS Textures + Detail
  → GEBCO-Only Pipeline (recommended)  ← выключить для splat

# Симуляция (пороги для согласования)
SimulationCore/World/BiomeEvaluator.cs
```

---

## Связь с другими планами

| Тема | Где |
|------|-----|
| UV / координаты / sandbox Stage 3 | [[systems/work_terrain.md]] |
| LOD split/merge | [[systems/LOD.md]] |
| GEBCO / NASA пайплайн | [[systems/EARTH_TERRAIN_NOTES.md]] |
| Изолированная калибровка ассетов (архив) | [[history/test_earth_sandbox_roadmap.md]] |
| Mesh Frame Этап 5 (отложено) | [[deferred/work_terrain_stage5_prompt.md]] |

---

## История отметок (кратко)

| Дата | Шаг | Результат |
|------|-----|-----------|
| 2026-08-13 | Роадмап создан | чеклист из сессии AI, фазы 0 → C |
| 2026-08-14 | B.3 coast/peaks/tiling | песок у морей, снег на макушках и полюсах, RSS PQS scale |
