# Журнал изменений

История значимых шагов проекта. Живой план и статусы — [[roadmap/PROGRESS.md]].

---
| Дата       | Шаг                                    | Результат                                                                                                                                                                                                        |
| ---------- | -------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-06-28 | Первичный аудит                        | Создан PROGRESS.md, просканировано 20 `.cs` файлов исходного кода                                                                                                                                                |
| 2026-06-28 | Локализация HUD                        | Полный перевод SimulationHud через Localization; консоль помечена замороженной                                                                                                                                   |
| 2026-07-07 | HUD cleanup (шаг 1)                    | Убраны дубли Pe/Ap/CA/узел/ориентация; Events = только «Далее»; блок клавиш за `H`; burn start в next event                                                                                                      |
| 2026-07-07 | Maneuver UX: burn start marker         | Оранжевый маркер «Burn» на орбите; burn start = узел − burn/2; подсветка next event                                                                                                                              |
| 2026-07-07 | Rendezvous: closure + ghost            | HUD: скорость сближения/удаления; ghost target: имя, d и v при CA                                                                                                                                                |
| 2026-07-07 | Maneuver UX: Enter + меню              | Enter далеко от burn → warp; ПКМ-меню локализовано, разделено «есть узел / нет»                                                                                                                                  |
| 2026-07-07 | Roadmap по кластерам C1–C8             | Кластеры для новых чатов, промпты, гибрид «кластер / подзадача»                                                                                                                                                  |
| 2026-07-07 | C2: next event + warp + auto-burn      | `NextOrbitalEventInfo` в Runner; HUD/сцена синхронизированы; warp стоп на Pe/Ap/CA/SoI/Burn; auto-burn по Δv; Pg/Rg/R+/R− с T+ burn start                                                                        |
| 2026-07-07 | C3: rendezvous polish                  | Линия корабль→цель; ghost = масштаб/цвет/ориентация тела; HUD rel v компактно; warp V с lead time и auto slowdown                                                                                                |
| 2026-07-07 | C4: drift diagnostics (4.1–4.2)        | `SampleFiniteBurnPreviewWorldPath`, `CompareBurnExecution`; жёлтый preview burn overlay; HUD «Дрейф»; trail vs preview                                                                                           |
| 2026-07-07 | C5: orbit UX polish                    | Единый формат маркеров (имя/Alt/T+), hover с active primary, камера F=корабль + zoom∝distance, легенда trail vs orbit                                                                                            |
| 2026-07-07 | C4: preview=execution fixes            | Burn start sync в diagnostics; `ExecuteFiniteBurnPreview`; circularize с post-burn SoI; `ExpectsBurnDrift`                                                                                                       |
| 2026-07-07 | C6: SimulationCore refactor            | `IGravityModel`, `OrbitalMechanics`, `SimulationPreview`, `ISimulationStateView`; `SimulationPilotInput`; HUD на read-only порту; `Simulation.cs` ~1415 строк                                                    |
| 2026-07-07 | Roadmap C7→C13                         | Старый C7 разбит: корабль, мир, посадка, атмосфера v1/v2, polish; C13 = игра                                                                                                                                     |
| 2026-07-07 | Roadmap 3D + C1–C21                    | C8 = миграция 3D; C9–C13 = KSP в 3D; C14–C20 = VAB, MP, моды; C21 = игра                                                                                                                                         |
| 2026-07-07 | C7: стадии в 2D                        | `Stage`, heading в `Vessel`; 2 ступени MoonFlyby; **T** = jettison; Δv по Циолковскому; HUD ступень/отделение                                                                                                    |
| 2026-07-07 | C8: миграция Core → 3D                 | `Vector3`/`Quaternion`; `OrbitalMechanics` 3D; `Vector2` удалён; adapter perspective camera; MoonFlyby XY-паритет                                                                                                |
| 2026-07-08 | C10: посадка на сферу                  | `FlightStatus`, контакт с поверхностью, пороги мягкой посадки, liftoff, HUD; сценарий `PlanetSurfaceStart`; масштаб мира ×2                                                                                      |
| 2026-07-09 | C11: атмосфера v1                      | `AtmosphereProfile`, `IAtmosphereModel`, экспоненциальный drag; Cd/area на `Stage`; HUD ρ/drag/q; `AtmosphereView`                                                                                               |
| 2026-07-09 | C11: аэро-архитектура                  | Сглаженная ρ на границе; A_fwd/side/top; CoP→torque; TimeWarp clamp в атмосфере                                                                                                                                  |
| 2026-07-09 | C12: аэро v2                           | α + lift/drag от α; SAS prograde в атмосфере; preview спуска; drift спуска; предупреждения q                                                                                                                     |
| 2026-07-09 | C13: полировка посадки                 | Трение на поверхности, `CheckCrash`/`RespawnVessel`; HUD v/surf/glide; маркер Impact; сценарий `PlanetAtmosphericEntry`; `reentryIntensity` (q×Mach); фиксы переторможения и вакуума                             |
| 2026-07-09 | C14: VAB MVP (стадии)                  | `StageData`, `List<StageData>` в `Vessel`, `RecalculateMassAndDeltaV`, `StageSeparation` + debris on rails, JSON `.ship`, `ShipConfigurationHud`                                                                 |
| 2026-07-09 | Roadmap: фаза 4½ playability           | **C14-play** (игровой цикл), **C14-visual** (3D feel); приоритет play перед VAB/C15; перекрёстные ссылки C14.5/C17 → C14-play                                                                                    |
| 2026-07-09 | C14-play: игровой цикл                 | Spawn с `.ship` (StreamingAssets), меню сценариев (M), mission hints HUD, R respawn + restart, дефолт «С мыса» + PlayerPrefs                                                                                     |
| 2026-07-09 | C14-visual: 3D feel                    | Планета ×10 на мысе, `LaunchPadView`, `InclinedOrbit`, камера по сценарию                                                                                                                                        |
| 2026-07-09 | InclinedOrbit: TWR tuning              | С мыса на крупной планете; бустер с cap ~5.5 TWR, орбитер ~1.15 TWR на НО                                                                                                                                        |
| 2026-07-10 | C15: стыковка MVP                      | Core: `MergeVessels`, `DockingConfig`, порт-к-порту, `IsDocked`, merge ступеней; сценарий `DockingApproach`; визуал станции + docked partner; sync offset носа из `BodyView`                                     |
| 2026-07-10 | C16: RCS + SAS MVP                     | Core: `rcsFuel`, `ApplyRcsThrust`, PID SAS, `RadialPlus`/`RadialMinus`/`TargetAlignment`; ввод **I/K/J/L**; язык HUD → кнопки **RU/EN** (без **L**)                                                              |
| 2026-07-10 | C16.3: SAS на порт стыковки            | **5** → `TargetAlignment`; вектор порт-к-порту (`GetDockingPortPosition`); HUD «На порт цели»; проверка в `DockingApproach` (Tab + 5)                                                                            |
| 2026-07-10 | Roadmap: архитектурный pivot C17+      | C17 = 3D-блоки/CoM/инерция; C18 = per-block разрушение; C19 = WorldSnapshot; C20 = lockstep MP; C21–C23 = VAB/mod/миссии; C14–C16 🔒 freeze; старый C17 (launch pad) → C14-visual/C23 polish                     |
| 2026-07-10 | **C17 ✅** блочное тело                 | Core: `ShipBlock`, динамический CoM, `RigidBodyInertia`, RCS thrusters τ=r×F, aero CoP; Unity: `BlockMeshFactory`, `BodyView` assembly, camera follow CoM, HUD **B** + маркер CoM                                |
| 2026-07-10 | **C17-play ✅** сырой playtest          | `.ship` v2 + `default_launch_v2.ship`; τ двигателя off-CoM; staging impulse; `DebrisView` block mesh; миссия «С мыса» на блочной ракете                                                                          |
| 2026-07-10 | **C18 🟡** per-block разрушение        | Core: `v_contact` (double), `structuralStrength`, поглощение удара, clamp ω; `FixedQueue<int>` → Unity; debris только при jettison; `ExplosionEffects` (блок / ступень / crash)                                  |
| 2026-07-10 | **C18.4 ✅** docking port blocks        | `DockingPort` block, port↔port capture (`v+ω×r`), snap merge, HUD «Зазор портов», VFX стыковки; `DockingApproach` с портами на корабле и станции                                                                 |
| 2026-07-10 | **C19 ✅** WorldSnapshot / QuickSave    | `WorldSnapshot` + `VesselSnapshotData`; binary `SPSN` v1 + debug JSON; `CaptureSnapshot`/`LoadSnapshot`, `worldRevision`; F5/F9 `quicksave.bin`; HUD по центру; perf: убран JSON `Debug.Log` на F5               |
| 2026-07-10 | **C20 🟡** lockstep transport + server | `SimulationServer/` UDP 50 Hz; `NetworkInputFrame`/`NetworkTickPacket` в Core; `NetworkTransportClient`; desync → pause + `desync_local_tick_X.bin` + HUD; детерминированный debris ID; `ApplyNetworkInputFrame` |
| 2026-07-11 | **C20:** Sync-on-drift | `VesselCompactState` в `NetworkTickPacket`; сервер шлёт `correctionStates`; клиент при checksum mismatch подтягивает `position`/`velocity`, без `HandleDesync` |
| 2026-07-11 | **C20:** checksum lockstep v2 | `CalculateStateChecksum`: убраны `currentTick`, время, quaternions, `fuelMass`; CoM/velocity округляются до 1 см; блоки — только `blockId` + `isDestroyed` |
| 2026-07-11 | **C14-play:** сценарии + Inspector   | Меню **M** и Inspector: только 4 сценария (`PlanetSurfaceStart`, `PlanetMoonShip`, `SolarTransfer`, `DockingApproach`); фикс — PlayerPrefs больше не перетирает Inspector в Editor; `NormalizePlayableScenario`, `OnValidate` при смене в Play; локализация названий |
| 2026-07-11 | **C20:** lockstep стабильность       | ≥6748 тиков без crash; tolerant checksum + sync-on-drift; instant network pause; per-vessel `ApplyNetworkInputFrameForVessel` (слоты id 1/2, `IsServerContext`, idle-skip, `networkVesselOrientation`) |
| 2026-07-11 | **C20:** блокеры MP (открыты)         | **MP-B1** магнетизм станции при W/I/K игрока 1; **MP-B2** SAS HUD мигает (сеть vs локальные 1–6); **MP-B3** тяга против velocity увеличивает \|v\| вместо торможения — требует отладки знака сил в Core thrust/RCS |
| 2026-07-11 | **Core SP:** изоляция физики судов   | Корень «магнетизма» — офлайн Core, не lockstep: `AdvanceIndependentVesselPhysics`, `VesselControlState`, `ResolveVacuumVesselPairContact`; пассивная станция без thrust/RCS; `AuditOfflineDockingPropulsionLeakage` |
| 2026-07-11 | **C15/C18:** стыковка DockingApproach | Hull-repulsion отключён при aligned port approach (`DockingHullExclusionPortSeparation`); `maxDistance` 0.20 м; merge при зазоре ≤0.20; SP play-test ✅ |
| 2026-07-11 | **C20:** MP-B1…B3 закрыты (SP)       | Магнетизм, SAS HUD, знак тяги — проверено в офлайн Play Mode |
| 2026-07-12 | **C20 ✅** MP MVP закрыт              | 2 игрока: build-клиент + хост-сервер; `DockingApproach`, совместный полёт; ≥11480 lockstep-тиков; вылетов по тикам нет; главное меню (без автозапуска), MP HUD справа |
| 2026-07-12 | **C20:** perf / RAM в build           | `RenderBodies` вне lockstep-цикла; `ClearBodyObjects` + opaque config panel; краш D3D12 `DEVICE_REMOVED` при перегрузе GPU — в работе (MP-F2/F3) |
| 2026-07-12 | **MP-F1…F4** (будущее)                | Микротелепорты на орбитах; RAM/GPU; утечки; TCP/periodic snapshot для join |
| 2026-07-13 | **C14-visual:** KSP graphics bridge   | Двухрежимный рендер Flight/Map (**M**): `WorldVisualBridge`, `RenderContext`; корабль 1:1 @ origin; карта ×0.001 + billboard-маркеры; камера near/far 0.1/10k vs 1/100M |
| 2026-07-13 | **C14-visual:** Scaled Space planet   | Полёт: icosphere R=50 m; позиция `-up×R×(1+h/R_earth)` вместо `h×0.001`; Moon/Sun только на карте; `SunVisual` flight = directional only |
| 2026-07-13 | **C14-visual:** scale / debug fix     | `CelestialVisuals.localScale = 1`; icosphere unit radius → `localScale = 50` (не 100); `[DEBUG GRAPHICS BRIDGE]` в HUD (pos/scale/distance) |
| 2026-07-13 | **C14-play:** Cape / surface launch   | Pad normal +Y; spawn `(0, R, 0)`; surface launch TWR ~2.35; thrust on pad до liftoff; `LandingConfig` / `SimulationWorldConfig` glue |
| 2026-07-13 | **C21.0:** VAB data layout            | Карта папок `Assets/Scenes`, `Models/{Environments,Parts}`, `Prefabs/{UI,Parts}`, внешняя `[repo]/Ships/`; `VesselBlueprint` + `VabFileSystem` |
| 2026-07-15 | **C21.4 🟡** ReStock import pipeline  | Blender `export_pilot_parts.py` → OBJ+PNG+manifest; Unity `ReStockImportPipeline` + `ReStockBatchParts`; 6 pilot-деталей; terrier/cupola/reliant split; материалы в `Assets/Materials/Parts/`; меню ReStock 8–10 |
| 2026-07-15 | **C21.4:** UV / формат                | FBX/GLB отвергнуты (мозаика UV); рабочий путь OBJ+PNG; `FlipPilotMeshUvV`; алиасы KSP material slots → ReStock texture names |
| 2026-07-15 | **C21.4:** folder layout v10           | Все меши: `Meshes/{Category}/{partId}/`; Blender + `ReStockBatchParts` унифицированы; flat-файлы в корне категории удалены |
| 2026-07-15 | **C21.4 ✅** pilot batch validated     | ReStock → 9 v10: 6 prefabs, `missingAlbedo=0`, terrier/reliant/pod/tanks/cupola — визуально ок в VabScene; документация структуры и логики в PROGRESS C21.4 |
| 2026-07-15 | **C21.5 🟡** VAB 3D-редактор runtime   | `VabEditorController` + UI-сайдбар; `VabEditSession`/`VabSnapSolver` в Core; hold/drag, snap 0.2 м, detach ветки, Delete; `VabCameraController` (ПКМ/zoom/Shift-pan/dbl-click); Q/W/E/A/S/D; Escape — отмена удержания; spawn площадка; `PROGRESS.md` — отдельная секция **Управление → VAB-редактор** |
| 2026-07-16 | **C21.5:** pilot attachment nodes      | `PartNodeLibrary` (м); `PartCatalog` приоритет библиотеки над `.partdef`; обновлены 6 `.partdef.json`; меню **Regenerate Pilot Part Nodes**; terrier top @ 0.28 м, tank125 ±0.93 м, mk1 pod bottom −0.55 м |
| 2026-07-16 | **C21.5:** VAB KSP-like placement/camera | Камера фиксируется при удержании детали; глубина по focus-plane; snap после выравнивания нод; pivot на attach node; surface snap только на боковых гранях |
| 2026-07-17 | **C21.5 ✅** container prefabs + snap | Ручные prefabs `Command/`/`FuelTank/`/`Engine/` с `Node_Top`/`Node_Bottom`; `PartPrefabBuilder` читает authored-ноды; fix snap (слой AttachmentNode, owned markers, stack fallback); 3 pilot-детали в каталоге |
| 2026-07-17 | **C21.4/21.5:** terrier materials | `Frame 2`/`Frame 4` → `revised-1`, `Frame_MLI` → `additional-3`; резолвер по пути nested OBJ; Blender `mesh_matches_keep_name` для material-split; меню **Apply Materials to Container Prefabs** |
| 2026-07-25 | **Earth terrain:** RSS-Terrain LOD + текстуры (сессия) | Квадродерево LOD чанков (cube-sphere), интеграция RSS-Terrain DDS для Земли (высоты, макро-фото, детальные текстуры), биом по высоте+цвету, площадка Cove. Подробности, найденные баги и открытые вопросы — **отдельный файл `EARTH_TERRAIN_NOTES.md`**, не дублируется здесь |
| 2026-07-25 | **Earth terrain:** переворот DDS-карт | `EarthHeight.dds`/`EarthColor.dds`/`Earth_NRM.dds` хранят строку 0 как южный полюс (не северный, как везде в коде) — тихо читали данные из зеркальной точки планеты; исправлено в `DdsTextureLoader` (`LoadWorldColorMap`/`LoadWorldNormalMap`, переворот DXT-блоков) + убран Editor `AssetDatabase` обход, который скрытно обходил фикс |
| 2026-07-25 | **Earth terrain:** LOD split/merge дедлок и осцилляция | Синхронизация соседей квадродерева форсировала split через полную `ShouldSplit()`, что либо навечно застревало (сосед не хочет — цепочка блокирована), либо осциллировало split↔merge каждый кадр (5 FPS, дрожащая текстура); фикс — приоритетный чанк игрока обходит проверку трещин напрямую, форс соседей остался «мягким» |
| 2026-07-25 | **Earth terrain:** площадка Cove + свет | `LaunchPadView` теперь берёт высоту палубы из реальных `Renderer.bounds` меша (не только ручного `SpawnPoint`, который мог рассинхронизироваться в «грязном» префабе); `LaunchSiteTerrain.Refresh()` вызывается до создания `Simulation`, чтобы высота площадки не бралась устаревшей/нулевой; `DynamicGI.UpdateEnvironment()` добавлен после назначения скайбокса (баг яркости при первом старте) — требует финальной проверки, см. `EARTH_TERRAIN_NOTES.md` §8 |
| 2026-07-25 | **Earth terrain:** мип-уровни мировых карт + баланс биома | Флип-фикс (см. выше) грузил `EarthColor`/`Earth_NRM` без мип-уровней → алиасинг («зубчатые» береговые линии на дальних/наклонных ракурсах); теперь распаковка в RGBA32 + downsample до 2048px + честная генерация мипов. Вес реального цвета карты в смешивании биома поднят 0.45→0.6 (всё ещё «одна текстура травы, другой оттенок»). `VisualHeightScale` временно откатан 0.3→0.2 (локальный разброс высот у мыса начал превышать запас flatten-радиуса). Добавлен лог `LaunchSiteTerrain: raw height=...` для диагностики провала под площадку. Подробности — `EARTH_TERRAIN_NOTES.md` §8.1–8.4 |
| 2026-08-04 | **Earth Sandbox LOD:** документация + панель настроек | Сцена `Earth_Sandbox_Test`: cube-sphere quadtree, Flight/Map (**M**), Terrain Graphics (LOD distance, load speed, max LOD, view cone), restricted quadtree, view-cone unload, mesh cache fix. **Описание текущего поведения:** [`LOD.md`](LOD.md) |
| 2026-08-12 | **Map mode orbit markers:** дедуп + perf | Убран per-frame `ShowMarker`/TextMesh rebuild; при манёвре скрыты current Pe/Ap и planned Pe/Ap у узла; компактный Node; burn start не дублирует узел; `ScreenSpaceBillboardScaler` — порог обновления characterSize; occlusion pass пропускает billboard presentation |
| 2026-08-12 | **Map mode «Узел» vs Pe/Ap** | Узел = точка манёвра; Pe/Ap скрывается у узла (current+planned); при Δv=0 узел без строк burn/Δv, с высотой |
| 2026-08-13 | **Flight LOD:** edge/backside twitch | Не уничтожать меши за камерой (только merge в Dead); один waiter на ключ; cull по nearest-зоне, не по дальнему углу чанка |
| 2026-08-13 | **Flight LOD snapshot + L0 overlap** | Рабочая сборка в `systems/LOD.md` §11. L0 face shell снимается после split (`Face4_L0_U0_V0` не кроет pad); LineRenderer не пишет NaN в transform |
| 2026-08-14 | **Flight splat: sand/grass/snow** | Шейдер берёт Low/Mid/High по цвету NASA, не красит всё травой. PNG-фото только с высоты. Influence Repeat. |
| 2026-08-14 | **Flight splat: coast / peaks / tile scale** | Песок: пустыни + берег морей (не озёра). Снег: широта + макушки. NASA от высоты камеры. Тайлы 200/800 real-m × (R/6371000) — готово к 1:1. |
| 2026-08-14 | **Scene view: ground looked transparent** | Dynamic clip от AABB планеты резал near plane. Scene clip = flight 1 m / 2500 km. DepthOnly pass + clamp UV mip. |
| 2026-08-14 | **Flight splat: square tile rows** | Снег Тибета показывал сетку тайлов (1-tap stochastic + UV по lon/lat). Два масштаба + поворот решётки. |
| 2026-08-15 | **Flight ocean: LOD chunks** | Убрана глобальная сфера воды. Океан на мешах чанков (GEBCO≤0 → высота 0, `vertex.a`). Шейдер opaque. Озёра по NASA. Физика без изменений. |
| 2026-08-15 | **Map/flight visual restore** | Map origin = центр планеты. Планета в полёте всегда по радиалу корабля (без lock 30 km / 15 km / 800 m). |
| 2026-08-15 | **Flight: PNG vs LOD overlap** | PNG-shell и GEBCO-чанки больше не рисуются вместе (раньше 76–102 км). Смена на 76 км, обратно на спуске ≤64 км. |
| 2026-08-15 | **Flight: dual-frame restore** | Физика: центр SoI, летит ракета. Визуал: корабль в нуле, планета FromTo(mesh radial→+Y). Откат инверсии A/D в Core и heading-match. |
| 2026-08-15 | **Flight: A/D vs Mesh Frame** | Планета: FromTo(mesh→sim) × тот же flight-frame, что у корабля. A/D/тяга в Core без инверсии. |
| 2026-08-15 | **Canon freeze: dual-frame A/D** | Запрет ломать §3.1 `work_terrain.md`: не инвертировать Core yaw, не упрощать поворот планеты. |
| 2026-08-15 | **Flight clouds** | Два лёгких слоя (1.8 / 7.5 km): процедурный шум, 3-tap fake volume, ветер. На планете (dual-frame). С орбиты в полёте гаснут. |
| 2026-08-15 | **Flight atmosphere** | Гладкая сфера R+70 км на планете (dual-frame, без LOD/GEBCO). Rim scattering + терминатор по главному свету. Карта: лимб на ScaledSpace. |
| 2026-08-16 | **Docs: ocean / atm+clouds** | `context/systems/OCEAN.md`, `ATMOSPHERE_CLOUDS.md` — канон тестового билда. |
| 2026-08-16 | **Clouds: warped FBM** | Worley давал редкие кружки. Снова domain-warp FBM: неровные массы, медленный осевой ветер. |
| 2026-08-16 | **Clouds: puffs, slow wind** | Не «полотно с дырками»: weather×Worley. Ветер = медленный поворот вокруг оси (без воронки на полюсе). Паттерн не кипит. |
| 2026-08-16 | **Map mode: atm limb** | Усиленное сине-оранжевое свечение атмосферы на глобусе (масштаб 0.001 гасил лимб). |
| 2026-08-16 | **Map mode: full sky** | На карте skybox всегда с полным Млечным Путём, независимо от высоты ракеты. |
| 2026-08-16 | **Clouds map/flight sync** | Один noise field + `simulationTime` для ветра; map больше не меняет scale/coverage и не отключает high layer. |
| 2026-08-16 | **Pad spawn: fix reset** | Node_Bottom → SpawnPoint применяется **каждый кадр после layout** (`UpdateBlockTransforms` сбрасывал offset). Убран одноразовый LateUpdate-align. |
| 2026-08-16 | **Pad spawn: Node_Bottom** | Визуальный спавн на Cove: нижний `Node_Bottom` PartTree → `SpawnPoint` (не `bounds.min.y` по миру). Fallback — lowest renderer вдоль оси корабля. |
| 2026-08-17 | **Pad shake root fix** | Убран LOD-freeze на паде (Земля не грузилась). Интерполяция spinAngle между physics-шагами; map mode без per-frame RefreshMapModeCelestialTransforms. |
| 2026-08-17 | **Pad shake + sun disk** | Стабильный радиал на паде; LOD freeze на земле; камера после поворота планеты; диск солнца ship→sun, яркий Unlit. |
| 2026-08-17 | **Dev: clouds/atm off** | В настройках разработчика под тягой: полное выключение визуала облаков и атмосферы (PlayerPrefs). Физика без изменений. |
| 2026-08-17 | **Map spawn vs spin** | Глобус карты больше не identity: `GetMapPlanetMeshFrameSpinRotation` (R=F Spin F). Маркер/орбиты остаются Mesh Frame инерциальных позиций — Флорида под кораблём без телепорта. |
| 2026-08-17 | **Sun visual fix** | Поворот планеты: `shipFrame × spin × FromTo(mesh→bodyFixed)` (без east-twist/тряски). Карта: один диск BodyView. Полёт: крупнее unlit-солнце, утро а не зенит. |
| 2026-08-17 | **Pad/map shake fix** | Радиал и spin из одного interpolated quaternion; пауза — без вращения планеты; map occlusion без GetComponentsInChildren(runner); выход из карты — LOD bootstrap без CollapseLodTree. |
| 2026-08-17 | **Sun pad: shake + map dots** | На паде co-rotate ориентация с поверхностью; landed — без орбит overlay; spin без interpolation на земле. Две точки на карте = фейковая орбита от скорости вращения. |
| 2026-08-17 | **Flight spin = shader UV** | Суточное вращение через `_PlanetSpinLongitudeRadians` в шейдерах; transform планеты без спина (стабильный LOD). θ из `spinAngleRadians` в Core. |
| 2026-08-17 | **Shader spin fix + rename** | PlanetSpinCommon: spin как аргумент функции; Kerbin* shaders → EarthPlanet* / PlanetClouds; KspSkybox → GalaxySkybox. |
| 2026-08-18 | **Pad spin split** | На паде: transform зафиксирован, UV spin только shell/облака; LOD chunks без UV spin. Liftoff: mesh lock до 30 km, radial без lerp COM (орбита Солнца). |
| 2026-08-18 | **Map orbit focus** | Dbl-click в map → orbit focus; origin map = focus body; орбита/Pe/Ap выбранного; Earth SoI = 14×R; pick collider Sun/Moon. |
| 2026-08-18 | **SoI sphere dev toggle** | DevSettings «Сфера SoI»: 3 кольца orbit-overlay вокруг SOI orbit-focus body (map M); `TryGetMapFocusSphereOfInfluence` в Core. |
| 2026-08-18 | **Sun root SOI canon** | `CelestialBody.Sun.IsRootBody`, `Body.isRootBody`, `IsRootBody` / `TryGetFiniteSphereOfInfluenceRadius` в Simulation. |
| 2026-08-18 | **Context: map/SoI** | Новая [[systems/MAP_MODE_ORBIT_SOI.md]]; обновлены INDEX, AI_CONTEXT, UNITY_ENTRY_POINTS, PHYSICS_EXPECTATIONS, PROJECT_LAYOUT, CONTROLS, PROGRESS. |
| 2026-08-18 | **Earth map 2K/8K dev toggle** | `earth_geographic.png` / `earth_geographic_8k.png`; DevSettings; baker 2K/8K из TIFF 5400/21600. |
| 2026-08-15 | **Clouds restored** | Новый шейдер `SpaceProgram/PlanetClouds`, геометрия как у атмосферы (меш в метрах, scale 1). Без isSupported. |
| 2026-08-14 | **Static ocean** | Сфера на 0 m (не сплющивать GEBCO). Клип по NASA-воде, волны в шейдере. Озёра на чанках. Приводнение если GEBCO&lt;0. |
| 2026-08-18 | **Earth map 2K/8K bind fix** | Переключатель 2K/8K перепривязывает `_ColorMap`/`_PlanetColorMap`; map mode всегда биндит текстуру; LOD bootstrap не сбрасывается при altitude shell; clamp по `maxTextureSize`. |
| 2026-08-18 | **2K/8K toggle LOD fix** | Toggle перезагружает только earth_geographic (RSS DDS не уничтожаются); rebind splats на чанках; shell PNG только при map/altitude shell; LOD bootstrap после toggle. |
| 2026-08-18 | **Pad ascent 30 km jump** | Плавный blend pad-lock → ship-radial mesh (1.5–30 km) и spin (22–40 km); без телепорта Земли на 30 km. |
| 2026-08-18 | **Map→flight LOD restore** | После map mode: sync LOD focus, RefreshLodLookAhead, скрыть map sphere; bootstrap до sync focus; fallback если face не найден. |
| 2026-08-18 | **Pad ascent 2 km void** | LOD focus = ship radial до 30 km (не inverse transform при blend); visual blend с 8 km; watchdog если без меша. |
| 2026-08-18 | **Pad LOD/visual resync** | LOD inverse transform на паде/до 22 km; visual blend с 22 km; sim-radial LOD только в blend; invalidate pad meshes после map. |
| 2026-08-18 | **30 km PNG macro align** | LOD focus всегда inverse transform (без sim-radial 22–35 km); shell/cloud shader spin только до 22 km; map mode сбрасывает shell spin до mapModeActive. |
| 2026-08-18 | **23 km drift + map pick** | Pad-lock/blend и macro PNG с 30 km (не 22); dbl-click Planet в map → snap камеры снаружи глобуса + min orbit. |
| 2026-08-18 | **PNG macro canon restore** | Откат split spin (2026-08-17): flight = `GetFlightPlanetLocalRotation` + shader spin 0; как map mode. |
| 2026-08-18 | **Pad shake revert** | На паде снова pad-lock transform + spin в shell/облаках; полный spin на transform только выше 30 km (blend). |
| 2026-08-18 | **Ascent 30–38 km band** | Pad-lock до 38 km; один blend 38–52 km; shader spin плавно → transform; macro PNG с 38 km. |
| 2026-08-18 | **Coast LOD squares** | 22–41 km: min max LOD 5 + cap split merge; shader macro ocean mask на побережье. |
| 2026-08-18 | **38 km spin twist** | 38–52 km только radial blend (без суточного spin); spin на transform 52–66 km; shader spin до 52 km. |
| 2026-08-18 | **25–38 km LOD + drift** | Pad-lock до 52 km (без radial band на 38 km); LOD floor 7 / coarsen до 32 km; flight камера не держит focus Planet. |
| 2026-08-18 | **Pad LOD ocean regression** | Откат LOD focus через spin-rotated sim radial — inverse transform при pad-lock. |
| 2026-08-18 | **Spin handoff 76 km** | Transform spin только когда PNG shell заменяет chunks; 38–52 km radial без spin; dev телепорт. |
| 2026-08-19 | **LOD + 38 km drift** | Плавный LOD без floor на 16 km; pad-lock до 85 km; macro с 52 km; shell/chunk/spin разведены. |
| 2026-08-19 | **Shell/chunk handoff** | PNG shell и скрытие chunks на одной высоте (~76 km); macro fade до 70 km; collapse LOD при handoff. |
| 2026-08-19 | **Spin by flight state** | Pad-lock только Landed; в полёте meshFrame без spin на transform; без altitude blend на 85 km. |
| 2026-08-19 | **Reference frame B (этап 1)** | Handoff 20 km: surface locked + shader spin / inertial transform из Core; [[history/planet_visual_reference_frames.md]]. |
| 2026-08-19 | **Map exit + скачки 20 км** | Выход из карты: земля сразу под ракету, камера без пролёта сквозь планету. LOD смотрит вперёд на ~28 км по курсу. |
| 2026-08-19 | **Spin jerks 100 km** | Flight: nlerp InverseSpin(r) пар + lerp угла спина; quaternion sign-continuity; LOD focus/velocity в body-fixed. |
| 2026-08-19 | **Канон суток** | Снимок как работает кручение/терминатор: [[systems/PLANET_SPIN.md]]. |
| 2026-08-19 | **Дальний глобус** | Far clip больше не съедает PNG: scale как Scaled Space; 2K с 1,5 млн м, грубая сетка с 5 млн. |
| 2026-08-19 | **Pad spawn** | Снова нижний `Node_Bottom` → pad `SpawnPoint` после layout/pad transform. Pad-lock altitude = высота SpawnPoint, не 0. |
| 2026-08-19 | **Map scale** | Map больше не сбрасывает глобус в scale 1: камера была в центре Земли. Scale = `MapPositionScale`. |
| 2026-08-20 | **Облака v2** | Lambert+fake volume, rim fade, dual-alpha, климат зоны, Coriolis warp, Perlin/Voronoi FBM, параллакс ветра low/high. |
| 2026-08-20 | **Cloud coverage tune** | Мягче климат-маска (без завесы на экватор/север), ниже coverage/density, прозрачнее «просветы». |
| 2026-08-20 | **GFX panel toggle** | Terrain Graphics скрыта по умолчанию; кнопка **GFX** над CoM, работает и в map mode (дублёр настроек графики). |
| 2026-08-21 | **Clouds v3 canon** | Принят текущий вид: [[systems/ATMOSPHERE_CLOUDS.md]]. Без параллельной смазки, coverage 0.44/0.48, Cull, subdiv≤4. |
| 2026-08-21 | **Map atmo zoom** | Лимб пропадал при зуме: камера заходила внутрь оболочки. Min orbit R+115км×1.12; map всегда exterior. |
| 2026-08-21 | **Low cloud wind** | Нижний слой `_WindLon` 0.00028 → 0.000238 (~15% медленнее); верхний 0.0024 без изменений. |
| 2026-08-21 | **Cloud hole edges** | Круглые просветы: weather на том же warp, шире smoothstep; softness 0.20/0.22. Без новых сэмплов. |
| 2026-08-21 | **Cloud coverage/freq UI** | GFX + VAB: густота и частота рядом с Cloud detail. PlayerPrefs, live. |
| 2026-08-21 | **Night cloud alpha** | Ночь просвечивала: тёмный RGB при том же альфа. Ночью альфа выше, цвет силуэта чуть светлее. |
| 2026-08-21 | **HUD visibility** | Настройки → Настройки HUD: галочки по строкам бокового списка. Раскладка пока слева. |
| 2026-08-21 | **HUD dev section** | В каталог HUD: раздел «Настройки разработчика» — строки DEBUG GRAPHICS BRIDGE. |
| 2026-08-21 | **HUD background** | Настройки HUD: прозрачный / слабое / сильное затемнение фона списка. |
| 2026-08-21 | **HUD panel fit** | Фон HUD по высоте текста, углы скруглены (~10 px). |
| 2026-08-21 | **Atmosphere opacity** | Dev settings: ползунок видимости атмосферы (0–100%), PlayerPrefs. |
| 2026-08-22 | **GFX drag + clusters** | Terrain Graphics можно двигать. Ползунок скоплений облаков (0 = ровно, 1 = массы и дыры). |
| 2026-08-22 | **Max LOD unstick** | Слайдер 1→8 не грузил чанки: гистерезис высоты держал потолок. Ceiling сразу, подъём без спуска. |
| 2026-08-22 | **Cloud renderer UI** | Настройки: Простые оболочки / Экспериментальный. Густота-частота-скопления только у простых; значения не сбрасываются. |
| 2026-08-22 | **Clouds experimental** | Второй шейдер: вихри/потоки/ячейки для map. Без RotateY-шва и без тяжёлого Voronoi. |
| 2026-08-22 | **Exp clouds satellite** | Эксп. шейдер: маска+эрозия вместо мрамора. Резкие массы/дыры, без водяных разводов. FPS-оболочка, не NASA/объём. |
| 2026-08-22 | **Exp cloud checkerboard** | Шахматка в белых кучах: fake-volume на решётке ValueNoise. Свет на weather-scale, шум с поворотом домена. |
| 2026-08-22 | **Exp clouds mix** | Крупные массы с просветами (не Гренландия). Мелкие — отдельный billow, grain. |
| 2026-08-22 | **Exp clouds empty sky** | Coverage был порогом альфы и вырезал массы. Теперь coverage = размер скоплений. |
| 2026-08-22 | **Exp satellite pass** | Хвост края + эрозия каймы. Самозатенение по толщине. Grain только вокруг масс. |
| 2026-08-22 | **NASA PNG clouds** | Третий рендер: equirect `StreamingAssets/MapMode/earth_clouds.png`, geographic UV, одна оболочка, без ветра. |
| 2026-08-22 | **NASA clouds white globe** | PNG: белый RGB + маска в A. Luma-floor делал smoothstep=1 по всей сфере. |
| 2026-08-22 | **NASA clouds alpha bake** | matteason white-on-transparent: покрытие из A (или luma), без mip-тумана. Daily-файл сейчас почти пустой. |
| 2026-08-22 | **NASA clouds-alpha.png** | Live map `StreamingAssets/MapMode/clouds-alpha.png`. Покрытие из серого, не из пустого daily `earth_clouds.png`. |
| 2026-08-22 | **NASA live gray floor** | Live PNG: серый ≥128 (белый глобус). Покрытие из альфы, где есть просветы материков. |
| 2026-08-22 | **NASA clouds polar pinch** | Equirect на полюсе: глушение atan2/U-grad + LOD-размытие шапки. География не сдвигается. |
| 2026-08-22 | **NASA clouds same UV as Earth** | Облака семплят vertex `geographicUv` как `earth_geographic.png`: sim UV, без `1-U`. |
| 2026-08-22 | **NASA clouds date-line seam** | Interpolated UV0 через ±180° давал зигзаг. Сэмпл попиксельно из sim atan2, wrap-grad. |
| 2026-08-22 | **NASA PNG+ one-way wind** | Убран dual-sample/`sin` UV (мыло и телепорт). Один сэмпл, ветер только на восток, high без сдвига 40°. |
| 2026-08-22 | **NASA PNG+ prompt canon** | В `ATMOSPHERE_CLOUDS`: dual-sample = мыло; якорь для чата без EVE-двоения. |
| 2026-08-22 | **NASA PNG+ slower + phase** | Ветер 0.00085/0.0019. Low старт с фазой 2.4 рад, чтобы не копировать high. |
| 2026-08-22 | **NASA PNG+ FP wind** | Ветер ещё ×2.5: 0.00034/0.00076 — с первого лица не несутся. |
| 2026-08-22 | **NASA PNG+ wind tune** | Low 0.00025 / high 0.00065 рад/с. |
| 2026-08-22 | **NASA hide Cloud detail** | PNG и PNG+: ползунок Cloud detail скрыт (GFX + VAB). |
| 2026-08-23 | **Perf 1% low FPS** | Оверлей: `1%` за ~300 кадров. Средний FPS больше не прячет рывки. |
| 2026-08-23 | **LOD SSE + ship disk** | Split по screen-space error; кольца от корабля; не merge за камерой; idle skip тика. |
| 2026-08-23 | **LOD start warmup** | Старт: pad 3 split/кадр, не весь диск×12. Потом 3 с по 4. |
| 2026-08-23 | **LOD 1% hitch cap** | Merge≤2, mesh upload≤2/кадр, без обхода пустой очереди, sim не ×2 после hitch. |
| 2026-08-23 | **LOD shared mesh + GO pool** | Убран `Instantiate`/`Destroy` меша на split/merge. Чанки из пула. |
| 2026-08-23 | **Flight Mesh Frame north** | Грунт N/S был 180° от носа (x-flip chirality). Visual Z, навбол по +Y. Core тягу не трогал. |
| 2026-08-25 | **Map sun distance** | На карте Солнце ×4 дальше от Земли, диск ×0.4. Орбита Земля–Солнце тоже. Физика без изменений. Полёт: тот же жёлтый диск. |
| 2026-08-25 | **Map Earth orbit + Sun pick** | Refresh орбиты с той же heliocentric растяжкой. Pick Солнца 1.5, не 70 — dbl-click больше не фокусит Землю. |
| 2026-08-25 | **Map camera focus** | Pivot больше не всегда Земля. `StabilizeMapCameraOnFocusBody` + экранный pick. Dbl-click Sun крутит камеру вокруг Солнца. |
| 2026-08-25 | **LOD parent holes** | Шелл пока потомки без меша; L0 hide только <30 км; merge без дыры. |
| 2026-08-25 | **Луна MVP** | Toy SMA 4.5 Mm внутри SoI Земли; tidal lock; пад 0°N 0°E; тот же LOD (LROC 4K + LDEM 5760×2880). С мыса Луна видна на карте и в полёте. 1:10 SMA — позже. |
| 2026-08-25 | **Луна: pad + PNG** | Cove на Луне (радиус = Moon.R, flatten). Чанки `_BaseMap` LROC, не vertex-paint. Earth/Moon PNG раздельно — карта Земли не синий диск. Дальняя Земля — lat/lon глобус, far-clip не режет диск. |
| 2026-08-25 | **Луна: вода/звёзды/Земля** | Maria больше не океан (не flatten к 0). Skybox полный на Луне. Метка корабля: радиус окклюзии = LOD-тело. Дальняя Земля ближе (120 км) + NASA clouds-alpha. |
| 2026-08-26 | **Луна: не Earth PNG** | С пада шелл/чанки больше не красятся `earth_geographic`. Цель рельефа — NASA `ldem_64.tif` (64 ppd). |
| 2026-08-26 | **Луна: 4K/8K PNG** | DevSettings как у Земли: `moon_geographic.png` / `_8k.png`. Map + distant first-person. Нет 8K — fallback на 4K. |
| 2026-08-26 | **Луна: 8K toggle** | В MapMode не было `moon_geographic_8k.png` (LROC 8K лежал в `файлы луны`) — переключатель молча оставался на 4K. Файл скопирован, rebind чанков/шелла. |
| 2026-08-26 | **Луна: LDEM 64 ppd** | `ldem_64.tif` → `moon_elevation.raw` 23040×11520 Int16 м (−9.1…+10.8 км). Runtime mmap; TIFF в `MapMode`. |
| 2026-08-26 | **LOD после посадки** | После hop шелл/collapse оставляли orbital altitude → max LOD 0, чанки не грузились. Snap + bootstrap. Луна ~21 км, Земля ~76 км. |
| 2026-08-26 | **Луна: солнце на паде** | Flight sun брал спин Земли × body-fixed Луны → диск на горизонте; после отрыва — зенит. Зенит = спин LOD-тела. |
| 2026-08-26 | **LOD coverage / seams** | Общий quadtree: шелл пока прямые дети не рисуют (не L0-release и не «любой потомок»). Crack через грань куба. Skirts, ребро без clamp, радиальные нормали. Horizon-merge не держит бюджет. |
| 2026-08-26 | **Earth mountains / skirts** | Span-fraction skirts на L0–L3 давали стены в км (иглы, чёрные дыры). Рёбра без slope-clamp — иглы на берегах. Skirts только LOD≥4, cap 200 м; clamp снова на всём гриде. |
| 2026-08-26 | **Горы с дырками** | 2D-clamp на рёбрах расходился у соседей (небо в щелях). Юбки flip по центру патча на пике → Culled / гигантский треугольник. Рёбра: 1D-clamp из raw (совпадают). Юбки: обход CCW без 3D-flip. Cap 400 м. |
| 2026-08-26 | **Луна: ближний splat** | Чанки у площадки — RSS-Terrain Moon Low/Mid/High/Steep (как трава/песок на Земле), макро LROC. Смесь по LDEM/уклону, без земных биомов. Нет DDS — fallback PNG. Мыс не тронут. |
| 2026-08-26 | **Splat UV wrap** | Wrap пола был `[0, 16384)` — шов на 0°N 0°E, серый крест с пада Луны (mip от скачка 16 км). Wrap центрирован; кэш чанков v10. |
| 2026-08-26 | **LOD swap без рывка** | Split/merge: грубый и мелкий больше не рисуются вместе. Дети скрыты, пока нет всех 4 мешей; затем кадр смены. Merge не destroy шелл. Общий quadtree. |
| 2026-08-26 | **LOD hysteresis** | Merge сравнивал span детей (½) с порогом split → кольцо сразу merge. Теперь span узла vs `target × 0.72`. Рент чанка не включает renderer до coverage. |
| 2026-08-26 | **LOD parent off** | Родитель не рисуется, когда 4 потомка уже могут. Coverage каждый LateUpdate, не только LOD-тик. |
| 2026-08-26 | **LOD Play дыра** | Split без шелла + hide детей → чёрный квадрат / одна четверть. Шелл до split; детей не гасить без меша родителя; idle skip ждёт 4 сиблингов. Кнопка песочницы убрана из меню. |
| 2026-08-27 | **LOD Play старт = карта** | Bootstrap висел, пока pad не 64 m (restricted QT так не доходит) — только грань, L3–L4. Теперь тот же RefreshLodLookAhead, что после M; 45 кадров и warmup. |
| 2026-08-27 | **Play LOD kick после позы** | Build() был до transform планеты; bootstrap не кормил очередь как выход из M. `PrimeFlightTerrainLod` после `ApplyFlightUniverseVisualNow`; LateUpdate без развилки. |
| 2026-08-27 | **LOD 2×2 vs горизонт** | Диагональ пада на Луне (~8 км горизонт) не мешилась — дыра и мигание шелла (сетка плыла, pad нет). 2×2 вокруг корабля не режет горизонт. |
| 2026-08-27 | **LOD хлопок откат** | Игнор горизонта на всём face + force-split сиблингов → split/merge каждый тик. Только finest 2×2; nearest по углам чанка. |
| 2026-08-27 | **LOD 2×2 sibling-balance** | Play: 2 четверти, каждый M/сценарий +1. Песочница: если один ребёнок 2×2 split — остальные тоже (parent≥2, не грань). Сетка debug в local space. |
| 2026-08-27 | **LOD кольца вокруг корабля** | Сверлило одну клетку (бюджет вглубь + SSE от центра + горизонт по углам). Теперь ближайшая точка, 1 split/лист/тик, догон соседа без отсечения горизонтом, merge с crack. |
| 2026-08-27 | **LOD холодный Play vs сценарий** | Перезагрузка «лечила», потому что кэш мешей. Sync-меш диска на bootstrap; idle не freeze пока соседи хотят split; bootstrap ждёт диск, не 45 кадров. |
| 2026-08-27 | **LOD DIAG overlay** | Левый верх: фаза + 2×2 + `gate=` (HORIZON/CRACK/SSE/WAIT-MESH). F8 в Console, Shift+F8 скрыть. |
| 2026-08-27 | **LOD четверть пада** | F8: Play=reload, `unfinished=1` при `SPLIT-OK sib` — merge по farthest схлопывал кольцо. Fully-beyond = nearest; sibling-balance на pad 2×2 без горизонта. |
| 2026-08-27 | **LOD hitch 640/пауза** | Догон соседей за кадр забивал 640 (`BUDGET` на паде, spike ~5 с). Теперь pad сначала, догон ≤2/тик, merge дальних при капе; пауза не тикает LOD. |
| 2026-08-27 | **LOD дыры на швах** | С высоты щели L/L+1: меши из кэша не сшивались. Ребро мелкой плитки кладётся на грубого соседа. Дальний SSE при 75% капа — `PERIPH`. |
| 2026-08-28 | **LOD старт L2 / дыры на капе** | Пауза стопила bootstrap (31 чанк, `SPLIT-OK pri`). Теперь bootstrap+догон на паузе; не выходим с грубым pad; merge сверх maxL без лимита 2/тик. |
| 2026-08-28 | **LOD V не под ракетой / BUDGET** | Face-center: L2 за лимбом давали ложный `gap=1`. На высоте диск съедал 640, `V gate=BUDGET` при L+ соседях. Merge капа не трогал in-disk; пока pad хочет split — дальние SSE hold. |
| 2026-08-28 | **LOD CRACK-тупик + журнал** | `Hold-V` стопил L1 при `L2 V CRACK` (Луна 24 чанка, мыс L6). Hold только ≥75% капа; catch-up по CRACK до Refresh. Живой разбор F8: `context/systems/LOD_FAILURES.md`. |
| 2026-08-28 | **LOD кольца на 4-угле** | Pad в центре грани: `V` в одном L1, три других касаются угла — мелко сверлилась одна четверть. Sibling-balance всего in-disk 2×2 (parent≥2); touch-угол как pad; PERIPH 0.85×horizon. Взгляд камеры по-прежнему не split. F8: `L1q maxL=`. |
| 2026-08-28 | **LOD 9 рвёт pad / 642 чанков** | Слайдер 9: `chunks=642/640`, `L8 BUDGET` рядом с `L9` под ракетой; 8 — ровно. Rent в `Split()`; при ≥75% капа чужие L1 не сверлятся в finest, пока pad 2×2 смешанный. |
| 2026-08-28 | **Луна квадраты / скрыть LOD DIAG** | На 1 km `starve` из‑за `padMix` при 483/640 стопил catch-up (`L7 CRACK`). Starve только без слотов или пока V split. Overlay и сетка чанков скрыты (F8 / GFX). LDEM 64 ppd не «битый файл». |
| 2026-08-28 | **LOD полоса вместо кольца на 640** | Слайдер 8 на 4.7 км: `639/640`, hitch/L1 с кораблём красили полосу finer сбоку. При ≥75% капа finest только в 2.5× slant от корабля; F8 `clamp=` / `minL=`. |
| 2026-08-28 | **LOD кольца +1 под ракетой** | SSE и sibling-balance держали пятно одного LOD (~16×L7). Кап по углу: finest 2×2 с кораблём, сосед −1; догон только gap&gt;1; F8 `gate=RING`. |
| 2026-08-28 | **LOD RING cap=1 заморозил pad** | Кап кольца от листа L2: соседи не split, `chunks=9`, `L2 V CRACK`. Кап от maxL; догон снова закрывает трещину. |
| 2026-08-28 | **LOD три иглы L7 на центре грани** | Четыре L1 сходятся в UV 0.5,0.5; ближайшая точка = ракета → SSE резал три чужих квадранта в тёмные квадраты, на взлёте мерцали. Угол: slant от центра; pad-SSE только Contains; догон CRACK у V до того же LOD. |
| 2026-08-28 | **LOD RING cap=0 заморозил L3** | Ширина кольца как у плитки maxL (~0.45°) при корабле на L3: соседи cap=0, догон и merge в цикле, `chunks=25`. Ширина = текущий viewerSpan. |
| 2026-08-28 | **LOD cap=0 у L5 рядом с L8** | Шаг кольца в плитках L8: сиблинги L5 получали cap=0, Scene «не всё рядом». Шаг = max(viewerSpan, span чанка). F8: `c=` `k=` `cap=` все уровни; padMix только finest 2×2. |
| 2026-08-28 | **LOD четверть SE на центре грани** | `L1q 8/6/8/8`: догон не видел диагональный L1. Проба если корабль у UV 0.5,0.5 (не 5% угла L8); лимб не ест 2 split/тик. |
| 2026-08-28 | **LOD язык + 8 FPS при Max 14** | Кап кольца от слайдера и span чанка сверлил горы до L13. Шаг = viewerSpan; догон не same-level после max; merge cap=0 после прогрева. |
| 2026-08-28 | **LOD freeze 17 чанков RING cap=1** | `L2 V CRACK` к SW `RING cap=1`: догон и merge колец в цикле. Crack-сосед не RING; merge колец пока V в CRACK выкл. |
| 2026-08-28 | **LOD NE L5 при pad L8** | `L1q 7/7/8/5` `U+nL5`: RING clamp + merge 2×2 откатывали догон. Gap&gt;1 не clamp; не merge если ребёнок — сосед V. `D nL7` ложный. |
| 2026-08-28 | **LOD SE L7 при pad L8** | `L1q 8/7/8/8`: диагональ ловила −1, merge откатывал L8. Догон диагонали до LOD корабля; merge не трогает этот лист. |
| 2026-08-28 | **LOD эталон старта на паде 🔒** | F8 `L1q 8/8/8/8` `parentL7` 4×L8 `viewerSpan=1357m`. Снимок: `context/systems/LOD_BASELINE_PAD_START.md` — не править «улучшая» кольца. |
| 2026-08-30 | **Луна: пол «дышит» как волны** | Нет маски океана → `COLOR.a=1` → зыбь Earth-шейдера по всем чанкам. `_HasOcean=0`, высота волн 0. Мыс не тронут. |
| 2026-08-30 | **VAB Esc: No cameras rendering** | Меню гасило камеру ангара, а `FlightCamera` ещё не было. `EnsureMenuCamera` на сессии; UI VAB камеру не трогает. |
| 2026-08-30 | **Швы дальних чанков** | Юбки только с L4 — L1–L3 без заслона щели. Юбки с L1, cap 400 м; L0 без юбки. Stitch — радиальные нормали. |
| 2026-08-30 | **Мыс→Луна: миниатюра + SoI swap** | Луну с Земли не тянуть на 120 км (диск перед облаками). Вход в SoI Луны: LOD/чанки на Луну, Земля — дальний шар. SMA 4.5 Mm. |
| 2026-08-30 | **Flight WASD знаки** | W/S и A/D были против KSP: Core `−pitch`/`−yaw` + те же знаки с клавиш. Раскладка на вводе; Core 🔒. W → восток, D → юг. |
| 2026-08-30 | **Dev SAS: Q/E быстрее** | Режим 0: крен ×4 к ручной скорости, WASD как были. Обычный SAS не тронут. |
| 2026-08-30 | **Варп ×5 = ×1000** | Потолок 12×20 мс (`sim×12`, 240 ms/f). Rails не брали гиперболу и SAS. Hyperbola Kepler, крупные шаги на побережье, LOD >×4 не тикает. |
| 2026-08-30 | **Map SoI + freeze M** | Кольца SoI: сырой sim + старый origin → мимо глобуса. Mesh Frame + live origin. M больше не `CollapseLodTree` (spike ~3 с). |
| 2026-08-30 | **Луна с мыса в flight** | Clip-only диск был за дневным небом (queue 2000 vs атмосфера 3000, альфа ~0.98). Дальний глобус 3050; 120 км не возвращали. |
| 2026-08-30 | **SAS без двигателя** | Побережье = Kepler rails, ориентация не тикала. Prograde/hold крутятся на rails (варп — подшаги ≤ 1 с). |
| 2026-08-30 | **Map 0 FPS / spike 6–10 с** | CA и SoI preview в одном кадре (1500×0.2 с) + повтор после hitch. Разнесены; копия без SAS/Δv; backoff после длинного кадра. |
| 2026-08-30 | **Dev SAS snap к старому hold** | lastMode не сбрасывался вне SAS → повторный **0** мгновенно крутил к прошлой позе (камера/мир). Hold берётся при входе в режим. |
| 2026-08-30 | **Мыс→Луна: визуал ≠ SoI** | Луна была child `FlightUniverseRoot` (−Y на высоту) → диск у корабля при HUD 2 Mm. local = ship-frame − slide. Map M помнит последний focus. |
| 2026-08-30 | **SoI freeze 1–2 с** | Destroy PQS + Unload GEBCO/LDEM + Prime в одном кадре. Hibernate дерева, кэш высот/splat, стрим как KSP; варп → ×1. |
| 2026-08-31 | **Player shaders + catalog bake** | Always Included не содержал SpaceProgram/*. Resources `PlayerIncludedShaders` + автопечь `RuntimePartCatalog` если ассета нет. |
| 2026-08-31 | **Архив неиспользуемых папок** | В `Не используемые/` (gitignore): RSS PluginData, Parallax, RSS_Terrain, MapMode TIFF, ReStock, Pipeline. Игра: GEBCO + png + RSS-Terrain Earth/Moon. |
| 2026-08-31 | **exe: серый pad без «LOD»** | F8 дерево жило (`chunks=61`), HUD `(пусто)` = пустая очередь. `shader_feature` vertex-paint вырезался; PNG не биндился. `multi_compile` + Resources `.mat` + fallback, quadtree не гасится. |
| 2026-08-31 | **exe: Земля magenta, Луна ок** | Луна = `EarthPlanetLocalChunks`. Земля GEBCO-only шла в `SandboxEarthGlobe` + vertex-paint → error shader. Земля теперь тот же LocalChunks + `earth_geographic` (высоты GEBCO). |
| 2026-08-31 | **exe: сплошная вода, нет суши** | Меши не читали SPEL `.bin`, только mmap. В плеере дефолт был `.bin` → высота 0 → ocean mask. Луна mmap. Fallback на `.bin` + в exe всегда mmap raw если файл есть. |
| 2026-08-31 | **exe LOD/текстуры + тряска после старта** | F8 `capL=7` = PlayerPrefs MaxLod (не Editor). GEBCO не грузил Earth splat. После отрыва −Y слайд с physics altitude vs interpolated COM; float-quat × R_земли. Миграция MaxLod&lt;10→15, splat при GEBCO, тот же COM для слайда. |
| 2026-08-31 | **Пад: телепорт Y + FPS** | MaxLod 15 на старте сверлил чанки (GEBCO pop). Interpolated COM и фильтр quat двигали вселенную на паде — pin не отпускал. Откат слайда/фильтра; V2 15→8; splat оставлен. |
| 2026-08-31 | **gitignore: дампы** | В ignore: `файлы луны`, `.obsidian`, `.plastic`, `_Recovery`, `SimulationServer/publish`. `Не используемые/` и `Build/` уже были. |
| 2026-09-04 | **Сводка тяжёлых файлов** | `GITHUB_HEAVY_FILES.md`: GEBCO/LDEM raw не влезут на GitHub без LFS; Build и архивы уже в ignore. |
| 2026-09-04 | **gitignore: датасеты** | Planet/MapMode/RSS DDS/Texture2D/Models не в git. На GitHub — код и доки. |
| 2026-09-04 | **gitignore: SimulationConsole** | Консоль не в GitHub; папка на диске остаётся. |

