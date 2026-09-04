# Океан (Flight)

Визуал моря на LOD-чанках. **Не** глобальная водная сфера.

Канон координат / UV / GEBCO: [[work_terrain.md]]. Dual-frame 🔒: [[work_terrain.md]] §3.1 — не трогать. LOD: [[systems/LOD.md]]. Живой статус: [[roadmap/PROGRESS.md]].

---

## Статус (2026-08-16, тестовый билд)

Океан на чанках, сфера `Planet_Ocean` **выключена**. Физика приводнения не менялась под этот визуал.

---

## Правило поверхности

| Слой | Что считает «землёй» |
|------|----------------------|
| **Sim / посадка** | GEBCO **&lt; 0** → поверхность = **уровень моря (0 m)**, не дно траншеи. Суша = GEBCO as-is. |
| **Unity меш** | Та же логика: океанские вершины на сфере радиуса планеты (визуальный offset 0). Рельеф суши × `UnityVisualHeightScale` (**0.4**). |
| **Приводнение** | `Simulation.IsSplashdownSurface`: GEBCO &lt; 0 на радиале. |

Код: `BiomeEvaluator.GetPhysicalSurfaceElevationMeters`, `PlanetSurfaceConstants.SeaLevelMeters = 0`.

Отрицательный GEBCO = **батиметрия** (до ~−11 км), не nodata. См. [[systems/EARTH_TERRAIN_NOTES.md]].

---

## Как рисуется вода

Глобальная икосфера моря **не используется**. `FlightPlanetSphere.DisableLegacyOceanSphere()` гасит `Planet_Ocean` (класс `FlightPlanetOceanSphere` остался в репо, не спавнится).

Вода — **те же LOD-чанки**, что и суша:

1. CPU (`PlanetFlightChunkCpuBuilder.PackFlightSurfaceVertexColor`):
   - суша (GEBCO &gt; 0): `vertexColor.a = 0`
   - море (GEBCO ≤ 0): `a = 1`, `g` = глубина, упакованная в 0…1 на **2500 m** (`OceanDepthPackMeters`)
2. GPU (`KerbinPlanetLocalChunks.shader`):
   - `waterMask = max(vertexColor.a, lakeWater)`
   - **озёра**: NASA-синий на суше (`OceanLikeness`), не GEBCO≤0
   - зыбь (`EvalOceanSwell`): длина ~520 real-m, направление по широте (пассаты / вестерли)
   - opaque, не transparent-сфера

Константы волн: `PlanetBiomeTerrainConfig` (`WaveMetersPerTile`, `WaveHeightRealMeters` = 12, скорость 0.62). Масштаб тайлов: `planetRadius / 6371000`, чтобы 1:10 и будущий 1:1 совпадали в «реальных метрах на тайл».

---

## Файлы

| Файл | Роль |
|------|------|
| `UnityAdapter/Flight/PlanetFlightChunkCpuBuilder.cs` | Маска воды + упаковка глубины |
| `UnityAdapter/Flight/PlanetBiomeTerrainConfig.cs` | Волны, specular, blend NASA |
| `Assets/Shaders/KerbinPlanetLocalChunks.shader` | Альбедо моря, зыбь, берег, озёра |
| `UnityAdapter/Flight/FlightPlanetSphere.cs` | Сфера моря выкл. |
| `SimulationCore/World/BiomeEvaluator.cs` | Физическая поверхность = sea level над океаном |
| `SimulationCore/Simulation/Simulation.cs` | Splashdown |

---

## Не делать

- Не включать снова глобальную сферу воды «чтобы блестело» — будет z-fight с чанками.
- Не сплющивать GEBCO-дно в симе: глубина нужна, коллизия — на 0 m.
- Не путать озёра (NASA) с океаном (GEBCO≤0).
- Не менять Mesh Frame / dual-frame ради моря.
- **Луна:** чанки без vertex-маски воды. Без `_HasOcean=0` Unity даёт `COLOR.a=1` → зыбь по всему шару (~1–2 m ход). `CelestialTerrainRuntime.HasOcean` выкл. зыбь. Мыс не трогать.

---

## Дальше (не в тестовом билде)

Полировка берега, отдельные озёрные высоты, настоящий объём/прозрачность воды — после стабильного LOD. Шум/трипланар суши не полировать (заглушка до C24).
