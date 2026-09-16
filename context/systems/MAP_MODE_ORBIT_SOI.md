# Map mode, orbit focus, SoI

Карта орбит (**M**), heliocentric pad-сценарии, визуал SoI и dev-переключатели.

Связанные заметки: [[systems/CONTROLS.md]], [[systems/UNITY_ENTRY_POINTS.md]], [[architecture/PHYSICS_EXPECTATIONS.md]], [[architecture/CONVENTIONS.md]] (Mesh Frame), [[systems/work_terrain.md]] (dual-frame flight — **🔒 не трогать** §3.1).

---

## Map mode (Flight ↔ Map)

| Параметр | Flight | Map (**M**) |
|----------|--------|-------------|
| Position scale | `WorldVisualBridge.FlightPositionScale` (1:1 sim м) | `MapPositionScale` = **0.001** |
| Render origin | COM корабля (interpolated) | **Orbit focus body** (см. ниже) |
| Камера | follow корабль; identity / зенит +Y (как Scene; skybox инерциальный) | по умолчанию follow корабль; dbl-click — осмотр body |
| След корабля | опционально (dev) | скрыт |
| Orbit overlay | current/planned ship orbit | + фоновые орбиты планет/Луны |

Переключение: **M** в `SimulationRunner` → `SyncVisualSubsystemScales()`, `OrbitOverlayCamera`, layer masks, `RestoreShipAsFlightFocus` при выходе из map (камера не остаётся на Planet). Позы полёта и карты **не общие**: yaw/pitch/zoom первого лица не сбрасываются на дефолт сценария и chase `(52,14,-6)` при каждом M; карта помнит свой ракурс. **Первый M** (и повтор без dbl-click) — камера и origin на **корабль**, identity (не радиал SoI): камера не едет за планетой, пока корабль летит. **Повторный вход в M** после dbl-click восстанавливает этот body (`lastMapCameraFocusBodyName`). **F** снова на корабль. Flight LOD-дерево **не** схлопывать на вход в M (только скрыть чанки); иначе hitch на секунды.

Preview CA и вход в SoI — **разные кадры** (HUD не дожимает SoI в том же кадре). Иначе spike 6–10 с и 0 FPS, M не отвисает.

---

## Orbit focus (dbl-click)

**Двойной ЛКМ** на теле в map (`CameraController` → `SimulationRunner.StabilizeMapCameraOnFocusBody`):

- без dbl-click (первый **M**, **F**) origin и камера = **Ship**; identity (не радиал SoI), камера не едет за планетой;
- pick: сначала экранный диск тела (меньший диск побеждает, если Солнце на фоне Земли), иначе physics raycast;
- `orbitFocusBodyName` + pivot камеры = **то же тело** (осмотр глобуса); корабль — снова `BindMapCameraToShip`;
- `CameraController.StabilizeMapOrbitOnTarget` — орбита снаружи вокруг pivot;
- допустимые имена: `Ship`, vessel, `Planet`, `Moon`, `Sun`;
- `GetRenderOrigin()` в map = sim-позиция focus body;
- `OrbitPreviewView` — орбита focus-тела; **Pe/Ap корабля** в полёте (иконка-домик как в KSP: бирюзовый щит остриём на линию, буквы Pe/Ap; высота и T+ по наведению); тонкие фоновые орбиты остальных (`OrbitLineVisualUtility`: background **0.35 px**, focused **1.15 px**); текущая орбита корабля **светло-серая**;
- при focus на celestial — фоновая линия того же body скрыта (нет двойной орбиты Земли);
- орбита **корабля** на карте остаётся, пока он в полёте (не только при focus на Ship);
- Overlay-камера **не сбрасывает depth**. Линии — `OrbitLineOverlay`, **точки внутри сферы и за диском discard**. Occluder = визуальный диск **Planet и Moon**. Свою орбиту тела не рисуем, если смотрим на это тело **или** корабль в его SoI (орбита Земли не идёт сквозь Землю при старте с мыса).
- После выхода в SoI Солнца Луна на карте не должна исчезать: hibernate LOD оставляет `FlightPlanetSphere` на объекте, но рисуется `DistantCelestialGlobe` — масштаб по диаметру (не `×0.001` от baked R). Облака/лимб Земли в полёте с ~1.5 Mm — как дальний глобус с Луны ([[systems/ATMOSPHERE_CLOUDS.md]]).
- Выход из SoI Луны (LOD → distant globe): окклюдер метки/линий — **видимый** диск, не неактивный `PlanetMapSphere` с baked R. Иначе радиус ≈ R_Луны × диаметр глобуса, метка улетает, Unity: `Screen position out of view frustum`.
- Patched conics на карте (как в KSP): серая линия — оскулирующий коник **текущего** primary **до границы его SoI**, пока корабль ещё внутри пузыря. Оранжевый патч — коник **следующего** тела от точки выхода (Луна→Земля, Земля→Солнце). Маркер следующего SoI только если коник реально доходит до границы. Из SoI Луны следующий патч — Земля («SoI Земли»), не Солнце. Линии **не** рисуются сквозь диск Луны/Земли. Из SoI Земли — оранжевый гелиоцентрический патч вокруг Солнца («SoI Солнца»).
- Выход в SoI Солнца — **оскулирующий коник**, не 1500 шагов `CreateCopy`. Связанная орбита у Земли (Ap < SoI) **не** рисует «SoI Солнца». Уже в SoI Солнца серая орбита корабля и диск Солнца в **одних** sim-координатах (`MapHeliocentricVisualScale` = 1; корабль считается heliocentric, если primary = Sun).
- CA preview: если корабль уже внутри SoI цели — без 1500 шагов `CreateCopy`. На паузе карта не пересобирает preview, пока Δv не менялся.
- **Descent Preview Path** на карте выкл. (оранжевая линия при взлёте в атмосфере — это было «красное жало», не вектор тяги).
- Узел манёвра на карте выкл. (RMB не добавляет, **N** не ставит узел).
- Путь корабля обновляется **каждый кадр**; баллистическая дуга при небольшом pitch-over, не радиальная иголка до порога.
- Hover по линии (warp-to-here): **тот же коник, что на экране**, не 1000 шагов `CreateCopy`. `Camera.main` один раз за кадр. Круговая вокруг Солнца не должна проседать в 40 FPS из‑за hover.
- Орбита корабля всегда относительно **active SoI primary** (`BuildOrbitPath` / `TryGetOrbitApsides`): любой текущий и будущий шар SoI, без веток Earth/Moon. Вырожденный коник (почти радиальная скорость, крошечный \(p\), иголка, которая едва выходит над поверхностью) → радиальная двухтельная ломаная, путь не пустой. Замкнутая орбита, которая не бьёт поверхность → полный эллипс; суборбита, которая пересекает шар → дуга impact→Ap→impact.
- **В полёте** толстая серая линия + пины Pe/Ap **всегда траектория корабля vs active primary**, даже если focus на Луне/Земле. Орбита focused celestial остаётся тонким фоном (не подменяет путь корабля). Pe/Ap планеты/спутника — **только если это тело выбрано** (dbl-click), не на всех фоновых эллипсах сразу; кэш корабля не перезаписывают. На паде (`Landed`) оверлей корабля пустой; толстая линия = орбита focused celestial вокруг родителя.
- Pe/Ap: иконка-домик на линии (`OrbitEventBadge`). Только если высота над сферой **> 0** (суборбита: Pe под поверхностью не рисуется, остаётся Ap). Окно с высотой/T+ — **по наведению на иконку**, не на точку линии. На паде — Pe/Ap орбиты focused celestial, не корабля.
- CA / «Призрак Moon» на карте **выкл.**: цель по умолчанию = Moon, на лунном паде это «сближение с собой» (высота ≈ −R). Overlay не рисуется, пока target = active primary.
- Зелёный маркер корабля и Pe/Ap — OrbitOverlay, `ZTest Always`, но **тот же disk-occluder, что у линий** (якорь иконки за диском Planet/Moon → discard). На ближней стороне диска зелёная метка **поднимается** над PNG. Hover Pe/Ap не срабатывает, если значок за планетой.

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
- Pad-сценарии (мыс и др.) содержат Луну. С мыса Луна в flight на **истинной** дальности в **системе корабля** (far clip 8 Mm). Глобус под `FlightUniverseRoot`: localPosition = ship-frame − slide корня, иначе диск на ~высоте ближе физики (FP «падаю на Луну», карта ещё далеко). Kepler rails в 1.25×SoI Луны выкл. **на подлёте**; после выхода Moon→Earth (улетаем) рельсы Земли снова можно — иначе варп после смены SoI остаётся на 12 численных шагах. Очередь глобуса **3050**. SMA пока **4.5 Mm**; 1:10 = 38.44 Mm позже (тогда scaled-space камера).
- `SunVisual`: directional light из sim-направления на корабль; billboard диск в flight (цвет как карта); в map — только свет, диск у `BodyView`;
- Map: диск Солнца в **физической** sim-позиции (тот же origin, что гелиоцентрическая орбита корабля и Земли). Диск ×0.4 (`MapSunVisualRadiusScale`). Земля–Солнце в Core 44.3 Mm. Луна/LEO без отдельной растяжки.
- Pick Солнца: local radius **1.5** (диск), не огромный шар — иначе dbl-click Sun попадал в Planet.
- null-safety: `CanUseSimulationSun`, `SetSimulation(null)` при shutdown session.
- Сутки, отмена кручения в полёте, терминатор: [[systems/PLANET_SPIN.md]].

---

## Другие dev-переключатели (`DevSettings` + `DevSettingsHud`)

| Toggle | Default | Эффект |
|--------|---------|--------|
| Infinite fuel | Off | `SimulationCheatFlags` |
| No crash damage | Off | `SimulationCheatFlags.DisableCrashDamage` |
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
| Kepler-путь vs primary | `SimulationCore/Simulation/OrbitalMechanics.cs` |
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

1. «С мыса» → **M** → камера на корабль (не за диском Земли). dbl-click Earth → орбита Земли, origin на Земле, камера вокруг Земли. **F** → снова корабль.
2. dbl-click Sun → камера вокруг Солнца, орбита Земли вокруг Sun, без сферы SoI у Sun.
3. Developer settings → Сфера SoI On → dbl-click Earth → 3 кольца вокруг Земли.
4. **M** exit → камера на корабль, не на Planet.
5. С мыса → Луна: first-person диск на истинной дальности (как карта). HUD **Тело: Moon** только внутри SoI; до пузыря орбита остаётся земной. Орбита на карте — тонкая линия, **не** серая полоса на весь экран; за диском планеты не видна.
6. Взлёт с **любого** тела (мыс / Луна / следующий шар): **M** → **толстая** серая траектория корабля вокруг active primary + пины Pe/Ap (Pe на диске, если под поверхностью; Ap — отдали зум). У зелёной метки нет оранжевого вектора тяги. **M** снова → меши ракеты на месте. На паде линии корабля нет, пока `Landed`.

Открыто: полный playtest выхода из Earth SoI в heliocentric ([[roadmap/PROGRESS.md]] § Солнце #4).
