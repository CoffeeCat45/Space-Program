# Математические конвенции

Координаты Земли и cube-sphere — критичные правила, нарушение ломает terrain/LOD и map mode.

Полный канон UV/GEBCO/Mesh Frame: [[systems/work_terrain.md]].

---

## 1. Конвенция двух слоёв (из work_terrain)

- **SimulationCore / диск** (`earth_elevation.raw`, `earth_geographic.png`) → чистый канон (запад = −X, `u = geographicU`).
- **Unity visual** (меши планеты, маркеры, ракеты на сцене) → Mesh Frame: `mesh.x = -sim.x` только для **позиций**; UV из sim direction без flip.
- **Flight A/D / тяга:** не крутить знаки в Core и не упрощать поворот планеты до одного `FromTo(mesh→+Y)` — [[systems/work_terrain.md]] §3.1.

---

## 2. Закон синхронизации проекций куб-сферы

- **Критично:** перевод 3D → локальные UV грани куба и обратно — только **Philip Nowell** (`SpherifyCubePoint` / `PlanetCubeSphereCoords`).
- **Запрещено:** гномоническая проекция (`dir / dot(dir, normal)`) в поиске чанка под кораблём (`ChunkContainsViewerSurface`, `IsDirectionInsideFaceUvBounds`) — на углах граней ошибка до ~6.7° (десятки км на масштабе 1:10).
- Split/merge, D_min, force-split и защита от merge — только Nowell для вектора фокуса.

История отладки inverse: [[history/lod_problem.md]].
