# Map mode, orbit focus, SoI

Карта орбит (**M**), heliocentric pad-сценарии, визуал SoI и dev-переключатели.

Связанные заметки: [[systems/CONTROLS.md]], [[systems/UNITY_ENTRY_POINTS.md]], [[architecture/PHYSICS_EXPECTATIONS.md]], [[architecture/CONVENTIONS.md]] (Mesh Frame), [[systems/work_terrain.md]] (dual-frame flight — **🔒 не трогать** §3.1).

---

## Map mode (Flight ↔ Map)

| Параметр | Flight | Map (**M**) |
|----------|--------|-------------|
| Position scale | `WorldVisualBridge.FlightPositionScale` (1:1 sim м) | `MapPositionScale` = **0.001** |
| Render origin | COM корабля (interpolated) | **Orbit focus body** (см. ниже) |
| Камера | follow корабль, LOD terrain | ortho «глобус», celestial markers |
| След корабля | опционально (dev) | скрыт |
| Orbit overlay | current/planned ship orbit | + фоновые орбиты планет/Луны |

Переключение: **M** в `SimulationRunner` → `SyncVisualSubsystemScales()`, `OrbitOverlayCamera`, layer masks, `RestoreShipAsFlightFocus` при выходе из map (камера не остаётся на Planet). **Повторный вход в M** восстанавливает последний dbl-click focus (`lastMapCameraFocusBodyName`), а не снова полушарие площадки. Flight LOD-дерево **не** схлопывать на вход в M (только скрыть чанки); иначе hitch на секунды.

Preview CA и вход в SoI — **разные кадры** (HUD не дожимает SoI в том же кадре). Иначе spike 6–10 с и 0 FPS, M не отвисает.

---

## Orbit focus (dbl-click)

**Двойной ЛКМ** на теле в map (`CameraController` → `SimulationRunner.StabilizeMapCameraOnFocusBody`):

- pick: сначала экранный диск тела (меньший диск побеждает, если Солнце на фоне Земли), иначе physics raycast;
- `orbitFocusBodyName` + pivot камеры = **то же тело** (раньше pivot всегда был Planet);
- `CameraController.StabilizeMapOrbitOnTarget` — орбита снаружи вокруг pivot;
- допустимые имена: `Ship`, vessel, `Planet`, `Moon`, `Sun`;
- `GetRenderOrigin()` в map = sim-позиция focus body;
- `OrbitPreviewView` — толстая орбита + Pe/Ap focus body; тонкие фоновые орбиты остальных (`OrbitLineVisualUtility`: background **0.35 px**, focused **1.15 px**);
- при focus на celestial — фоновая линия того же body скрыта (нет двойной орбиты Земли);
- при focus на celestial — орбита корабля в фоне скрыта.

Heliocentric map: dbl-click **Sun** → origin = Sun, камера крутится вокруг Солнца, видна орбита Земли вокруг Sun (`TryGetOrbitParentBody`).

---

## SoI в Core (patched conics, KSP/RSS)

### Корень — Солнце

- `CelestialBody.Sun.IsRootBody` = true;
- при создании world: `Body.isRootBody`, `sphereOfInfluenceRadius = 0`;
- **бесконечная SOI** — не Laplace, не рисуется сфера;
- вне конечных пузырей детей active primary = **Sun** (`ResolveActivePrimaryBody`, `GetActivePrimarySphereOfInfluenceRadius` → `PositiveInfinity`).

Канон в коде: `Simulation.IsRootBody`, `HasFiniteSphereOfInfluence`, `TryGetFiniteSphereOfInfluenceRadius`, `TryGetMapFocusSphereOfInfluence`.

### Конечные пузыри

| Body | Радиус SoI (pad / heliocentric) |
|------|----------------------------------|
| Earth (`Planet`) | **14×R** ≈ 8.92×10⁶ m (`CelestialBody.Earth.SphereOfInfluenceRadiusMultiplier`) |
| Moon | Laplace × multiplier в `SimulationWorldBuilder` |
| Sun | нет (root) |

Переходы: hysteresis + exit multiplier **1.04** (`SphereOfInfluenceExitMultiplier`). Интеграция с Солнцем: `UpdateSolarSystem()` — Земля/Луна relative to parent, корабль — SoI switching.

**Не планируется сейчас:** сфера Хилла / галактика (нет других звёздных систем).

---

## Визуал SoI (dev toggle)

`DevSettings.SoiSphereEnabled` (PlayerPrefs, **default Off**) → `SphereOfInfluenceView`:

- только **map mode**;
- 3 ортогональные кольца (wireframe), `OrbitLineVisualUtility.SoiSpherePixelWidth` (**0.65 px**), слой OrbitOverlay;
- body = orbit focus (`TryGetMapFocusSphereOfInfluence`): celestial → свой SoI; Ship → SoI **active primary**;
- центр каждый кадр: тот же Mesh Frame + live `GetRenderOrigin()`, что глобус (`SimToUnityMeshFramePosition`) — не сырой sim и не origin с момента focus;
- **Sun focus** → сфера не рисуется (корректно).

UI: **Developer settings** (кнопка справа снизу) → «Сфера SoI».

---

## Солнце и свет (flight)

- Core: Sun в heliocentric pad-сценариях, Земля на орбите вокруг Sun;
- Pad-сценарии (мыс и др.) содержат Луну. С мыса Луна в flight на **истинной** дальности в **системе корабля** (far clip 8 Mm). Глобус под `FlightUniverseRoot`: localPosition = ship-frame − slide корня, иначе диск на ~высоте ближе физики (FP «падаю на Луну», карта ещё далеко). Kepler rails в 1.25×SoI Луны выкл.; хорда через пузырь — смена primary. Очередь глобуса **3050**. SMA пока **4.5 Mm**; 1:10 = 38.44 Mm позже (тогда scaled-space камера).
- `SunVisual`: directional light из sim-направления на корабль; billboard диск в flight (цвет как карта); в map — только свет, диск у `BodyView`;
- Map: Солнце **визуально** в 4× дальше от Земли (`MapHeliocentricVisualScale`), диск ×0.4. Физика 44.3 Mm не меняется. Орбита Земли вокруг Солнца на карте растягивается так же (`cachedCurrentOrbitLayoutBodyName` — refresh линии с той же растяжкой). Луна/LEO без растяжки. Focus на Sun — без растяжки.
- Pick Солнца: local radius **1.5** (диск), не огромный шар — иначе dbl-click Sun попадал в Planet.
- null-safety: `CanUseSimulationSun`, `SetSimulation(null)` при shutdown session.
- Сутки, отмена кручения в полёте, терминатор: [[systems/PLANET_SPIN.md]].

---

## Другие dev-переключатели (`DevSettings` + `DevSettingsHud`)

| Toggle | Default | Эффект |
|--------|---------|--------|
| Infinite fuel | Off | `SimulationCheatFlags` |
| Thrust multiplier | ×1 | `SimulationCheatFlags` |
| Cloud / Atmosphere visuals | On | runtime на `FlightPlanetSphere` |
| Earth spin | On | cheat freeze spin |
| Ship trail | **On** | `ShipTrailView`, скрыт в map |
| SoI sphere | **Off** | см. выше |
| Earth map texture | **2K** | `earth_geographic.png` vs `earth_geographic_8k.png` (map + flight macro) |
| Moon map texture | **4K** | `moon_geographic.png` vs `moon_geographic_8k.png` (map + distant flight shell) |

PNG bake (Editor): **Space Program → Bake Earth Color Map 2K/8K (.png)**  
TIFF: `Earth5400x2700_geo.tif` → 2K PNG; `Earth21600x10800_geo.tif` → 8K PNG.

---

## Ключевые файлы

| Область | Путь |
|---------|------|
| Map toggle, focus, render origin | `UnityAdapter/MonoBehaviours/SimulationRunner.cs` |
| Орбиты map + Pe/Ap | `UnityAdapter/Rendering/OrbitPreviewView.cs` |
| Линии overlay | `UnityAdapter/Rendering/OrbitLineVisualUtility.cs` |
| SoI rings | `UnityAdapter/Rendering/SphereOfInfluenceView.cs` |
| Map markers | `MapMarkerPlacement.cs`, `ScreenSpaceBillboardScaler.cs`, `BodyView.cs` |
| SoI physics | `SimulationCore/Simulation/Simulation.cs` |
| World radii | `SimulationCore/World/CelestialBody.cs`, `SimulationWorldBuilder.cs` |
| Dev toggles | `UnityAdapter/DevSettings.cs`, `DevSettingsHud.cs` |
| Scale / frames | `UnityAdapter/Rendering/WorldVisualBridge.cs` |
| Sun light | `UnityAdapter/Rendering/SunVisual.cs` |
| Trail | `UnityAdapter/Rendering/ShipTrailView.cs` |

---

## Проверка в Play

1. «С мыса» → **M** → dbl-click Earth → орбита Земли, origin на Земле, камера вокруг Земли.
2. dbl-click Sun → камера вокруг Солнца, орбита Земли вокруг Sun, без сферы SoI у Sun.
3. Developer settings → Сфера SoI On → dbl-click Earth → 3 кольца вокруг Земли.
4. **M** exit → камера на корабль, не на Planet.
5. С мыса → Луна: first-person диск на истинной дальности (как карта). HUD **Тело: Moon** только внутри SoI; до пузыря орбита остаётся земной. Толстая белая полоса на карте — баг линии overlay, не позиции корабля.

Открыто: полный playtest выхода из Earth SoI в heliocentric ([[roadmap/PROGRESS.md]] § Солнце #4).
