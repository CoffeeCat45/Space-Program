# Структура проекта

Карта репозитория и краткий обзор слоёв `SimulationCore` и `UnityAdapter`.

Связанные заметки: [[ARCHITECTURE_RULES.md]], [[systems/UNITY_ENTRY_POINTS.md]], [[systems/RESTOCK_PIPELINE.md]].

---

## Текущая структура

Корень репозитория:

- `D:\Projects\games\Space Program`

| Путь | Роль |
|------|------|
| `context\` | База знаний (Obsidian) |
| `SpaceProgramUnity\SpaceProgram\` | Unity-проект (сцены, ассеты, `Assets/Scripts/`) |
| `SpaceProgramUnity\SpaceProgram\Assets\Scripts\SimulationCore\` | Физика и состояние мира, **без Unity** |
| `SpaceProgramUnity\SpaceProgram\Assets\Scripts\UnityAdapter\` | Визуал, ввод, VAB, flight views |
| `SpaceProgramUnity\SpaceProgram\Assets\Scripts\EarthSandbox\` | Изолированная песочница terrain/LOD (`Earth_Sandbox_Test`) |
| `Tools\` | Утилиты (например GEBCO stitcher), не рантайм игры |
| `SimulationServer\` | Lockstep-сервер (исходники; `publish/` не в git) |
| `SimulationConsole\` | ⚠️ **заморожен**, **не в git** — не использовать для тестов |
| `Не используемые\` | Архив: `ReStock/`, `Pipeline/`, старый RSS/TIFF — игра не читает |
| `файлы луны\` | Сырьё LROC/TIFF; в игре png + `moon_elevation.raw` |
| `Build\` | Локальный Windows-плеер с картами, не в git |
| `ShareBuild\` | Копия плеера без GEBCO/PNG/RSS (после Build Windows Player), не в git |

Unity-проект живёт **внутри** репозитория. GitHub — **код и доки без датасетов** (карты/высоты/DDS только на этом ПК). См. `GITHUB_HEAVY_FILES.md`.

**ReStock / Pipeline:** исходники `.mu` и Blender-скрипты — в `Не используемые\ReStock` и `Не используемые\Pipeline`. В игре уже `Assets/Prefabs/Parts`. Повторный импорт — [[systems/RESTOCK_PIPELINE.md]].

**Ships (чертежи VAB):** рядом с Unity root; в `.exe` ещё копия в StreamingAssets:

- Editor: `SpaceProgramUnity\SpaceProgram\Ships\*.json`
- Логика: `VabFileSystem.GameDirectory` = родитель `Assets/` (`Application.dataPath/../`); плеер также читает `StreamingAssets/Ships`
- Сборка: `PlayerContentBuildProcessor` копирует JSON к exe и в `StreamingAssets`; префабы — `RuntimePartCatalog` в Resources. После Windows Build — `ShareBuildExporter` кладёт лёгкую копию в `ShareBuild/` (без карт, с Editor `settings.json`). Обновить карты в уже собранном `Build/` без нового BuildPipeline: **Space Program → Update Heavy Build Data**.
- RSS DDS (`Assets/Content/Environment/…`) в Editor читаются с диска; в плеер копируются в `StreamingAssets/Content/` при Build. **Не в git.**

**Настройки (графика / HUD / разработчик):** `settings.json` в `VabFileSystem.GameDirectory` (рядом с `Assets/` в Editor, рядом с `.exe` в плеере). Первый запуск копирует старые PlayerPrefs в файл. F5 и `Ships/` — отдельные файлы. Режим окна (оконный / полный экран / безрамочный) — поле `graphics.displayMode`. Экран загрузки LOD — `play.showLodLoadingScreen`. Экспорт ShareBuild копирует этот файл как есть (HUD с диска; если HUD уже загружен в Editor — сначала flush).

---

## SimulationCore (кратко)

Ядро — **3D**, `SimVector3` / `SimQuaternion`, **`double`**, без Unity.

Главные типы: `Body`, `Vessel`, `Simulation`, `SimulationScenario`, `VesselBlueprint`, `WorldSnapshot`.

Корабль — **блочный**: `ShipBlock`, динамический CoM, `RigidBodyInertia`, ступени, топливо, Δv, RCS/SAS, аэро, посадка, per-block контакты.

Интегратор: Velocity Verlet (`Integrator.cs`); гравитация Ньютона (`Gravity.cs` / `IGravityModel`).

QuickSave/Load: `WorldSnapshot` в `Simulation` + `ShipPersistence` (**F5** / **F9** в `SimulationRunner`).

Полный перечень систем — [[roadmap/PROGRESS.md]] (журнал может отставать; сверять с кодом).

---

## UnityAdapter (кратко)

- рендер, орбиты, HUD, ввод — `SimulationRunner`
- камера follow — `CameraController`
- QuickSave/Load — runner (**F5** / **F9**, New Input System `keyboard.f5Key` / `f9Key`)

**Визуал корабля:**

1. **Префабы ReStock** — `FlightPartVisualFactory` + `PartDatabase` / `RuntimePartCatalog` (`Assets/Prefabs/Parts/…`) в flight и VAB; `.exe` грузит baked catalog
2. **Fallback** — `BlockMeshFactory` (Unity primitives: cylinder/cube) если prefab не найден

Ключевые view: `BodyView`, `OrbitPreviewView`, `DebrisView`, `SimulationHud`, `SphereOfInfluenceView`, `SunVisual`.

Map / SoI / dev toggles — [[systems/MAP_MODE_ORBIT_SOI.md]].

**VAB:**

- сцена Unity: хост `VabScene` + аддитивно ангар `VABlvl3` (`Assets/Scenery/KSC/Interiors3D/`)
- редактор: `VabEditorController`, каталог `PartCatalog` / `PartVisualAdapter`
- чертежи: `VesselBlueprint` (Core) + `VabFileSystem` → `Ships/*.json`
- сессия редактирования в Core: `VabEditSession`

**ReStock import (Editor, C21.4):** см. [[systems/RESTOCK_PIPELINE.md]] → префабы в `Assets/Prefabs/Parts/`.

**Terrain / LOD (активная разработка):** песочница `Earth_Sandbox_Test`, канон координат — [[systems/work_terrain.md]], runtime — [[systems/LOD.md]].

Бинды и секции HUD — [[systems/CONTROLS.md]] и фактический код.

Точки входа — [[systems/UNITY_ENTRY_POINTS.md]].

---

## Карта созданных сущностей (индекс кода)

Снимок основных типов и файлов. При расхождении верить репозиторию.
### Интерфейсы


| Сущность               | Файл                                 | Назначение                                              |
| ---------------------- | ------------------------------------ | ------------------------------------------------------- |
| `IGravityModel`        | `Physics/IGravityModel.cs`           | Порт расчёта ускорения от гравитации                    |
| `ISimulationStateView` | `Simulation/ISimulationStateView.cs` | Read-only снимок полёта для HUD (без мутации симуляции) |




### SimulationCore — Math


| Сущность     | Тип    | Файл                 | Назначение                                                          |
| ------------ | ------ | -------------------- | ------------------------------------------------------------------- |
| `SimVector3`    | struct | `Math/SimVector3.cs`    | 3D-вектор на `double`: add, subtract, dot, cross, length, normalize |
| `SimQuaternion` | struct | `Math/SimQuaternion.cs` | Ориентация корабля (3D; MoonFlyby — z=0 для паритета)              |




### SimulationCore — Entities


| Сущность | Тип          | Файл                 | Назначение                                                                             |
| -------- | ------------ | -------------------- | -------------------------------------------------------------------------------------- |
| `Body`   | class        | `Entities/Body.cs`   | Небесное тело: mu, radius, `isRootBody` (Sun), `sphereOfInfluenceRadius` (finite children) |
| `Vessel`          | class : Body | `Entities/Vessel.cs`          | Корабль: heading, ступени, тяга, `rcsFuel`, масса, Δv                                  |
| `Stage`           | class        | `Entities/Stage.cs`           | Ступень: dryMass, fuel, thrust, расход                                                 |
| `BlockSnapshot`   | class        | `Entities/VesselBlueprint.cs` | Одна деталь в VAB-чертеже: id, шаблон, parent, pose, attach node                        |
| `VesselBlueprint` | class        | `Entities/VesselBlueprint.cs` | Файл ракеты: `vesselName` + `List<BlockSnapshot>`; JSON без Unity                       |
| `VabSnapSolver`   | static class | `Vab/VabSnapSolver.cs`        | Snap math между `AttachmentNode` (порог 0.2 м), без Unity                              |
| `VabEditSession`  | class        | `Vab/VabEditSession.cs`       | Runtime-сессия VAB: удержание детали/ветки, commit blueprint                             |


### SimulationCore — World


| Сущность | Тип | Файл | Назначение |
| -------- | --- | ---- | ---------- |
| `CelestialBody` | static class | `World/CelestialBody.cs` | Канон 1:10: Earth/Moon/Sun; Earth SoI 14×R; `Sun.IsRootBody` |
| `SimulationWorldBuilder` | static class | `World/SimulationWorldBuilder.cs` | Сборка bodies, Laplace SoI, `isRootBody` на Sun |


### SimulationCore — Physics


| Сущность                     | Тип                      | Файл                                    | Назначение                                                                                |
| ---------------------------- | ------------------------ | --------------------------------------- | ----------------------------------------------------------------------------------------- |
| `Gravity`                    | static class             | `Physics/Gravity.cs`                    | Фасад над `NewtonianGravityModel`: `CalculateAcceleration`, `CalculateCircularOrbitSpeed` |
| `IGravityModel`              | interface                | `Physics/IGravityModel.cs`              | `CalculateAcceleration` для тела от списка влияющих тел                                   |
| `NewtonianGravityModel`      | class : IGravityModel    | `Physics/NewtonianGravityModel.cs`      | Ньютоновская гравитация                                                                   |
| `Integrator`                 | static class             | `Physics/Integrator.cs`                 | Velocity Verlet: `StepPosition`, `StepVelocity`                                           |
| `AtmosphereProfile`          | class                    | `Physics/AtmosphereProfile.cs`          | ρ₀, scale height, top altitude на теле                                                    |
| `IAtmosphereModel`           | interface                | `Physics/IAtmosphereModel.cs`           | Порт плотности и drag (моды)                                                              |
| `ExponentialAtmosphereModel` | class : IAtmosphereModel | `Physics/ExponentialAtmosphereModel.cs` | ρ(h)=ρ₀·(e^{-h/H}−e^{-h_max/H}), drag + torque                                            |
| `AerodynamicProperties`      | struct                   | `Physics/AerodynamicProperties.cs`      | Cd, A_forward/side/top, CoP offset                                                        |
| `Aerodynamics`               | static class             | `Physics/Aerodynamics.cs`               | Эффективная площадь и yaw-момент                                                          |




### SimulationCore — Simulation


| Сущность                        | Тип                          | Файл                                 | Назначение                                                                 |
| ------------------------------- | ---------------------------- | ------------------------------------ | -------------------------------------------------------------------------- |
| `SimulationScenario`            | enum                         | `Simulation/SimulationScenario.cs`   | Игровые: `PlanetSurfaceStart`, `PlanetMoonShip`, `SolarTransfer`, `DockingApproach`; legacy: `MoonFlyby`, `InclinedOrbit`, … |
| `Simulation`                    | class : ISimulationStateView | `Simulation/Simulation.cs`           | Главный класс: Update, SoI, orbit focus helpers, preview                   |
| `OrbitalMechanics`              | static class                 | `Simulation/OrbitalMechanics.cs`     | Орбитальная математика: eccentricity, peri/apo, orbit path, directions     |
| `SimulationPreview`             | class                        | `Simulation/SimulationPreview.cs`    | Preview orbit/finite burn, CA sampling, SoI entry, drift compare           |
| `ISimulationStateView`          | interface                    | `Simulation/ISimulationStateView.cs` | Read-only порт для HUD                                                     |
| `BodyState`                     | struct                       | `Simulation/Simulation.cs`           | Снимок состояния тела для view                                             |
| `ClosestApproachInfo`           | struct                       | `Simulation/Simulation.cs`           | CA: distance, time, positions, relativeSpeed, SoI entry flag               |
| `SphereOfInfluenceEntryPreview` | struct                       | `Simulation/Simulation.cs`           | Preview входа в SoI луны: time, position, path points                      |


**Ключевые public-методы** `Simulation` **(группы):**

- Управление: `SetShipThrust`, `ApplyShipDeltaV`, `Update`, `CreateCopy`
- Орбита / энергия: eccentricity, semi-major axis, peri/apoapsis, orbit type, bound/unbound, escape speed, angular momentum, specific energy
- Направления: prograde, retrograde, radial in/out (текущие и at-time)
- Preview: orbit path, finite burn path, circularize delta-v, maneuver preview
- Target / CA: `GetClosestApproachToBody`, relative velocity/speed/direction
- SoI: active primary switching, `IsRootBody`, `TryGetFiniteSphereOfInfluenceRadius`, `TryGetMapFocusSphereOfInfluence`, entry preview
- Mass / fuel: `GetShipMass`, `GetShipFuelMass`, `GetShipAvailableDeltaV`, burn duration estimate



### SimulationConsole

Локальная папка, **gitignore**. Не запускать, не коммитить.

| Сущность       | Тип    | Файл         | Назначение                                             |
| -------------- | ------ | ------------ | ------------------------------------------------------ |
| `Program`      | class  | `Program.cs` | Main, ASCII-визуализация (архив) |




### UnityAdapter — MonoBehaviours


| Сущность               | Тип           | Файл                                 | Назначение                                                                                                        |
| ---------------------- | ------------- | ------------------------------------ | ----------------------------------------------------------------------------------------------------------------- |
| `ShipOrientationMode`  | enum          | `Simulation/ShipOrientationMode.cs`  | Manual, Prograde, Retrograde, Maneuver, RadialPlus, RadialMinus, TargetAlignment                                  |
| `SimulationRunner`     | MonoBehaviour | `MonoBehaviours/SimulationRunner.cs` | Адаптер ядра ↔ Unity: время, thrust, манёвры, warp, auto-burn, target; `SimulationState` → `ISimulationStateView` |
| `SimulationPilotInput` | class         | `Input/SimulationPilotInput.cs`      | Клавиатурные бинды: корабль, RCS I/K/J/L, манёвры, время, цель                                                    |
| `CameraController`     | MonoBehaviour | `MonoBehaviours/CameraController.cs` | Follow / zoom / focus камеры                                                                                      |
| `SimulationHud`        | MonoBehaviour | `MonoBehaviours/SimulationHud.cs`    | HUD: Flight, Orbit, Target, Events, Control, Maneuver, Details                                                    |




### UnityAdapter — Rendering / View


| Сущность                | Тип           | Файл                                 | Назначение                                                                      |
| ----------------------- | ------------- | ------------------------------------ | ------------------------------------------------------------------------------- |
| `BodyView`              | static class  | `Rendering/BodyView.cs`              | Planet/Moon/Ship; prefabs через `FlightPartVisualFactory`, fallback `BlockMeshFactory`; vectors, exhaust |
| `DebrisView`            | MonoBehaviour | `Rendering/DebrisView.cs`            | Визуал debris / jettisoned blocks (C18)                                                                  |
| `FlightPartVisualFactory` | static class | `Rendering/FlightPartVisualFactory.cs` | Instantiate prefab из `PartDatabase` для flight/VAB blocks              |
| `WorldVisualBridge`     | static class  | `Rendering/WorldVisualBridge.cs`   | KSP dual-mode: Flight 1:1 / Map ×0.001, Scaled Space R=50 m, clip planes, `RenderContext`                        |
| `LaunchPadView`         | MonoBehaviour | `Rendering/LaunchPadView.cs`         | Визуал пусковой площадки на мысе (только Flight mode)                                                            |
| `OrbitPreviewView`      | MonoBehaviour | `Rendering/OrbitPreviewView.cs`      | Орбиты, maneuver node, event markers, ghost target, RMB-меню, drag узла         |
| `SunVisual`             | MonoBehaviour | `Rendering/SunVisual.cs`             | Визуальное солнце (не физическое тело)                                          |
| `StarfieldBackground`   | MonoBehaviour | `Rendering/StarfieldBackground.cs`   | Звёздный фон                                                                    |
| `ShipTrailView`         | MonoBehaviour | `Rendering/ShipTrailView.cs`         | След корабля                                                                    |
| `SphereOfInfluenceView` | MonoBehaviour | `Rendering/SphereOfInfluenceView.cs` | SoI wireframe rings (map, dev toggle)                                         |
| `OrbitLineVisualUtility` | static class | `Rendering/OrbitLineVisualUtility.cs` | Pixel-width orbit/SoI overlay lines, OrbitOverlay layer                      |
| `MapMarkerPlacement`    | static class  | `Rendering/MapMarkerPlacement.cs`    | Map pin markers on planet radial                                             |
| `ScreenSpaceBillboardScaler` | MonoBehaviour | `Rendering/ScreenSpaceBillboardScaler.cs` | Billboard markers (map pin mode)                                    |
| `AtmosphereView`        | MonoBehaviour | `Rendering/AtmosphereView.cs`        | Кольцо верхней границы атмосферы                                                |




### UnityAdapter — VAB / ReStock


| Сущность                  | Тип           | Файл                                              | Назначение                                                                 |
| ------------------------- | ------------- | ------------------------------------------------- | -------------------------------------------------------------------------- |
| `VabFileSystem`           | static class  | `Vab/VabFileSystem.cs`                            | Чтение/запись чертежей в `SpaceProgramUnity/SpaceProgram/Ships/*.json`; список имён для UI         |
| `VabEditorController`     | MonoBehaviour | `Vab/VabEditorController.cs`                      | VAB UI, ghost/drag, snap, detach ветки, save/load                           |
| `VabCameraController`     | MonoBehaviour | `Vab/VabCameraController.cs`                      | Орбитальная камера VAB: focus, RMB, zoom, Shift-pan (roadmap 1.1)          |
| `VabFocusPlane`           | static        | `Vab/VabFocusPlane.cs`                            | Виртуальная плоскость сборки на глубине фокуса (roadmap 1.2)               |
| `VabFocusPlaneCursor`     | MonoBehaviour | `Vab/VabFocusPlaneCursor.cs`                    | Маркер пересечения луча с focus plane (этап 1)                             |
| `VabRootAnchor`           | static        | `Vab/VabRootAnchor.cs`                            | Фиксированный якорь root `(0, 7, 0)` относительно VesselRoot (roadmap 2.1) |
| `VabSelectionHighlighter` | MonoBehaviour | `Vab/VabSelectionHighlighter.cs`                  | Контурная подсветка выбранной детали в VAB                                 |
| `VabRotationGizmo`        | MonoBehaviour | `Vab/VabRotationGizmo.cs`                         | Кольца вращения; Q/W/E/A/S/D при удержании детали                          |
| `VabColliderUtility`      | static class  | `Vab/VabColliderUtility.cs`                       | Коллайдеры для raycast + вкл/выкл на ghost                                 |
| `PartCatalog`             | static class  | `Vab/PartCatalog.cs`                              | Скан `Assets/Prefabs/Parts/`, заполнение `PartDatabase`                    |
| `VabSceneEnvironment`     | MonoBehaviour | `Vab/VabSceneEnvironment.cs`                      | Маркер ангара; высота/точка spawn первой детали                            |
| `VabSceneCleanup`         | Editor static | `Editor/VabSceneCleanup.cs`                       | Меню удаления missing scripts с KSP-пропов                                 |
| `ReStockImportPipeline`   | Editor static | `Editor/ReStockImportPipeline.cs`               | Reimport OBJ, rebuild `.mat`, сборка префабов, UV flip, material mapping   |
| `ReStockBatchParts`       | static class  | `Editor/ReStockBatchParts.cs`                     | Part definitions из архива `Не используемые/Pipeline/Parts` + manifest                            |
| `ReStockPilotEditorMenus` | Editor static | `Editor/ReStockPilotEditorMenus.cs`               | Меню **ReStock → 8/9/10**                                                  |
| `ReStockModelImportPostprocessor` | class | `Editor/ReStockImportPipeline.cs` (nested) | `AssetPostprocessor`: UV flip, import settings для pilot OBJ               |


### UnityAdapter — Прочее


| Сущность                | Тип          | Файл              | Назначение           |
| ----------------------- | ------------ | ----------------- | -------------------- |
| `Localization`          | static class | `Localization.cs` | EN/RU строки для HUD |
| `DevSettings`           | static class | `DevSettings.cs` | PlayerPrefs: fuel, thrust, visuals, trail, SoI sphere |
| `DevSettingsHud`        | MonoBehaviour | `MonoBehaviours/DevSettingsHud.cs` | In-game developer settings GUI |
| `Localization.Language` | enum         | `Localization.cs` | English, Russian     |




### Unity Tutorial (шаблон, не часть игры)


| Сущность       | Тип              | Файл                                                 |
| -------------- | ---------------- | ---------------------------------------------------- |
| `Readme`       | ScriptableObject | `Assets/TutorialInfo/Scripts/Readme.cs`              |
| `ReadmeEditor` | Editor           | `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs` |


