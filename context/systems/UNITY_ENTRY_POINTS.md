# Ключевые точки входа Unity

Самые важные файлы `UnityAdapter` — не полный список, а ориентиры для навигации в коде.

Связанные заметки: [[architecture/PROJECT_LAYOUT.md]], [[systems/RESTOCK_PIPELINE.md]], [[systems/CONTROLS.md]], [[systems/MAP_MODE_ORBIT_SOI.md]].

---

## SimulationRunner

`SpaceProgramUnity\SpaceProgram\Assets\Scripts\UnityAdapter\MonoBehaviours\SimulationRunner.cs`

- главный адаптер между `SimulationCore` и Unity
- `double fixedDt` / `timeScale`, аккумулятор шага, lockstep MP
- thrust, манёвры, target, auto-burn, warp
- QuickSave/Load (**F5** / **F9**)
- **M** — map mode; `GetRenderOrigin()` / `SetOrbitFocusBody()` / `orbitFocusBodyName`
- setup камеры / HUD / preview / `SyncVisualSubsystemScales()`

---

## OrbitPreviewView

`SpaceProgramUnity\SpaceProgram\Assets\Scripts\UnityAdapter\Rendering\OrbitPreviewView.cs`

- линии орбит (ship + фоновые celestial в map), hover, RMB-меню манёвров
- maneuver node, event markers, ghost target, Pe/Ap focus body
- `OrbitLineVisualUtility` — pixel-width overlay lines

---

## Map / SoI / dev views

| Компонент | Файл | Роль |
|-----------|------|------|
| `SphereOfInfluenceView` | `Rendering/SphereOfInfluenceView.cs` | SoI wireframe (dev toggle, map only) |
| `SunVisual` | `Rendering/SunVisual.cs` | Directional sun light + flight billboard |
| `ShipTrailView` | `Rendering/ShipTrailView.cs` | След корабля (dev, скрыт в map) |
| `DevSettings` | `DevSettings.cs` | PlayerPrefs cheats / visuals |
| `DevSettingsHud` | `MonoBehaviours/DevSettingsHud.cs` | GUI «Developer settings» |
| `MapMarkerPlacement` | `Rendering/MapMarkerPlacement.cs` | Pin маркеров на радиале планеты |
| `WorldVisualBridge` | `Rendering/WorldVisualBridge.cs` | Flight 1:1 / Map ×0.001, dual render context |

Детали map focus и SoI — [[systems/MAP_MODE_ORBIT_SOI.md]].

---

## BodyView + FlightPartVisualFactory

`BodyView.cs` — визуал Planet/Moon/Ship, vectors, exhaust, map/flight transforms.

Корабль: сначала `FlightPartVisualFactory.TryCreatePartVisual` (prefab из `PartDatabase` / `RuntimePartCatalog`), fallback `BlockMeshFactory` (primitives).

`FlightPartVisualFactory.cs` — instantiate prefab по `partTemplateId` (Editor: AssetDatabase; `.exe`: baked Resources catalog).

`DebrisView.cs` — визуал debris / jettison (C18).

---

## SimulationHud

`SpaceProgramUnity\SpaceProgram\Assets\Scripts\UnityAdapter\MonoBehaviours\SimulationHud.cs`

- секции Flight / Orbit / Target / Events / Control / Maneuver / Details
- RU/EN через `Localization`

---

## VAB

`VabEditorController.cs` — runtime 3D-редактор. См. [[systems/VAB_DEVELOPMENT.md]].

`CameraController.cs` — flight camera (не VAB).

Океан: [[systems/OCEAN.md]]. Атмосфера и облака: [[systems/ATMOSPHERE_CLOUDS.md]].
