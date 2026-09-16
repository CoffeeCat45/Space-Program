# Space Program — Бортовой журнал

> **Версия:** 0.1.3 (2026-09-16) — что можно / что сырое: [[CHANGELOG.md]]
> Живой план и статусы кластеров. Обновлять после каждого значимого шага.
> Стартовый хаб: [[architecture/AI_CONTEXT.md|AI_CONTEXT]] · История: [[CHANGELOG.md|Полный Changelog]]
> Архитектурный канон: [[architecture/ARCHITECTURE_RULES.md]]

---

## Ближайший план (приоритет)

### Сейчас — Солнце / освещение 🟡

| # | Задача | Статус |
|---|--------|--------|
| 1 | Pad-спавны: Солнце в Core, Земля на орбите, SoI, спин; утро у Канаверала, прочие площадки в том же времени | 🟡 2026-08-22 |
| 2 | Flight light из sim-вектора Солнца, терминатор, ночь с контуром | 🟡 2026-09-12 skybox инерциальный; горизонт vs корабль (**G**); SoI: yaw/pitch+roll |
| 3 | Тень ракеты на площадке (URP cascades) + затмение (умбра) | 🟡 2026-08-16 |
| 4 | Проверка в Play: мыс / Марокко / Тибет / Greenwich, орбита, выход из SoI | ⬜ |
| 5 | Map (M): корабль на той же суше, что flight (спин глобуса = F Spin F, не identity) | ✅ 2026-08-17 |
| 6 | Pad старт: без LOD-тряски, диск солнца в небе | 🟡 2026-08-26 Moon sun: LOD-body spin, не Earth |
| 7 | Убрать LOD-freeze на паде; интерполяция спина планеты (sim×2) | ✅ 2026-08-17 |
| 8 | Spin+radial одним quaternion; map pause без Rescan runner; LOD без collapse при M | 🟡 2026-08-17 |
| 9 | Суточный спин: shader UV offset, transform планеты без вращения (LOD stable) | 🟡 2026-08-17 |
| 10 | Map: orbit focus, SoI canon, dev toggles (trail / сфера SoI) | 🟡 2026-09-11 смена SoI больше не крутит chase; InverseSpin без lerp Earth/Moon |

---

### Сейчас — pad launch / flight spawn 🟡

| # | Задача | Статус |
|---|--------|--------|
| 1 | Ракета на `SpawnPoint` площадки Cove (sim + visual, без телепорта при отрыве) | 🟡 2026-09-14 посадка: локальная высота меша/ядра; Landed без выравнивания в радиус; выталкивание вместо pin центра |
| 2 | Камера на **визуальный центр** корабля, не COM/пустую точку над pad | 🟡 2026-09-16 GFX: «Небо в металле» выкл по умолчанию (космос/небо в блеске деталей); 2026-09-12 два рига: горизонт (пад) и корабль (**G** / GFX); SoI без срыва roll; CAM DIAG / F7; рамка скорости Surface/Orbit/Target у компаса; crash 18 m/s; Dev «не взрываться»; мёртвый двигатель без тяги/выхлопа |
| 3 | VAB PartTree: не применять sim-COM offset от flat-layout (Z) к Y-up иерархии | 🟡 |
| 4 | Очистка VAB-артефактов без `UnloadSceneAsync` последней сцены | ✅ |

**Проверка:** Play → «С мыса» → `test` · ракета на решётке pad · камера на корпус · HUD mass ~10450 kg · нет `Block_*` мусора.

**.exe = тот же контент, что Editor:** сборка `Space Program → Build Windows Player`. GEBCO-высоты + LocalChunks PNG; Earth splat из RSS-Terrain если pack на диске. После сборки в корне репо: `ShareBuild/` + `ShareBuild.zip` без карт (скинуть другу); полный `Build/` остаётся у тебя. Max LOD **не** поднимать автоматом до 15 на паде (L15 = просадка FPS и прыжки высот). Слайдер Terrain Graphics; эталон пада `capL=8`. Слайд −Y = physics altitude, не interpolated COM.

**GitHub:** код без датасетов (`https://github.com/CoffeeCat45/Space-Program.git`). Коммиты: `CoffeeCat45@users.noreply.github.com` (GH007). Карты/`.raw`/DDS только на этом ПК. `SimulationConsole` не в git. Графика/HUD/debug/dev — `settings.json` рядом с проектом или exe. ShareBuild копирует этот файл к другу (HUD `hiddenFields` и debug `hiddenOverlays` с диска Editor, не пустой RAM-store). GFX: режим окна, MSAA, пост-AA; SMAA-качество только при SMAA. Пост-AA применяется к FlightCamera при входе в полёт. Внизу GFX: экран загрузки LOD (выкл по умолчанию). Карты в уже собранном `Build/` без пересборки: **Space Program → Update Heavy Build Data**.

---

### В работе — Earth Sandbox LOD (Flight mode) 🟡

> Поведение: [[systems/LOD.md]] · Эталон старта 🔒: [[systems/LOD_BASELINE_PAD_START.md]] · Канон координат: [[systems/work_terrain.md]]  
> Сцена: `Assets/Scenes/Earth_Sandbox_Test.unity` · **M** = Map ↔ Flight

| Компонент | Статус |
|-----------|--------|
| Cube-sphere quadtree (6 граней, LOD 0–6) | 🟡 sandbox |
| Ground splat: пустыня + берег + макушки. **Заглушка** — не полировать (шум/трипланар/биом-карта = C24) | 🟡 freeze |
| Океан: шельф по GEBCO, зыбь по широте (пассаты/вестерли). Сфера выкл. | 🟡 2026-08-15 |
| Map mode: origin = **orbit focus**; flight origin = ship COM | 🟡 2026-08-18 |
| PNG-shell vs LOD: без двойного глобуса (смена на ~76 км) | 🟡 2026-08-15 |
| Dual-frame A/D+тяга: Core SoI-центр, Unity корабль в нуле. Surface ENU (не FromTo). InverseSpin nlerp. Mesh Frame north = visual −Z. **🔒** [[systems/PLANET_SPIN.md]] · [[systems/work_terrain.md]] §3.1 | 🔒 2026-08-23 |
| Flight clouds **v3** (2026-08-21): FBM + yzx, без RotateY-сдвига, Cull полусфер, ~150–190 FPS | 🟡 4 режима: simple / experimental / NASA PNG / NASA PNG+ (`clouds-alpha.png`) |
| Flight atmosphere: круглый лимб без треугольников икосферы | 🟡 2026-08-16; map-зум; opacity slider 2026-08-21 |
| GEBCO mmap + flight chunk meshes | 🟡 sandbox |
| Terrain Graphics panel | 🟡 sandbox; Max LOD 1→N снова грузит чанки |
| Restricted quadtree (LOD gap ≤ 1) | 🟡 sandbox; 2026-08-28: **эталон старта** [[systems/LOD_BASELINE_PAD_START.md]] 🔒; журнал [[systems/LOD_FAILURES.md]] |
| View-cone unload + horizon merge | 🟡 2026-08-28: bootstrap не freeze на паузе и не кончается на L2 |
| Edge stitching / skirts | 🟡 2026-09-12: pad-кольца Земля+Луна; правила UV/RING общие ([[systems/LOD.md]] §5.2.1), не хаки тел |
| Интеграция в main game (Cove / pad launch) | 🟡 snapshot 2026-08-13: кэш, без destroy backside, L0 shell off |

---

### Сейчас — Луна 🟡

| # | Задача | Статус |
|---|--------|--------|
| 1 | Ближняя орбита (внутри SoI Земли), спин = tidal lock, траектория на карте | 🟡 2026-08-25 SMA 4.5 Mm (потом 1:10 = 38.44 Mm) |
| 2 | Спавн на Луне (меню / Scenarios) для проверки LOD | 🟡 2026-08-25 near side; утро к Солнцу + фаза π |
| 3 | LOD как у Земли: макро LROC + LDEM; ближний пол = RSS-Terrain Moon splat | 🟡 2026-08-26 LDEM 64 ppd; PNG 4K/8K; close-up Low/Mid/High/Steep |
| 4 | Cove-площадка на Луне (как на мысе) | 🟡 2026-08-25 pad = signed LDEM (не пьедестал над maria) |
| 5 | Луна: без «воды», звёзды, метка, Земля с облаками | 🟡 2026-09-07 SoI Солнца: Луна на карте + облака Земли |
| 6 | Реальный 1:10 SMA + LOD swap Earth↔Moon mid-flight | 🟡 2026-09-10 L2 ~48 км, L0 72, PNG 75; force-split до ~2 км; coarsen при timewarp |

---

### Следующий крупный рубеж — C24 (проектирование)

GPU planet engine: compute shaders + texture atlases, quadtree cube-sphere, floating origin.  
Текущий MVP до C24: icosphere + flatten у полюса (`BodyView.ApplyLaunchSiteFlatten`).

Детали — в [[CHANGELOG.md]] (2026-07-13+ terrain) и [[systems/EARTH_TERRAIN_NOTES.md]].

---

## Статусы кластеров C1–C23

Статусы: ✅ закрыт · 🟡 MVP / в работе · ⬜ не начат · 🔒 freeze (только bugfix)

| Кластер | Тема | Статус |
|---------|------|--------|
| C1–C6 | Sandbox 2D, HUD, манёвры, rendezvous, preview, Core refactor | ✅ |
| C7 | Стадии, Δv (2D) | ✅ |
| C8 | Миграция Core → 3D | ✅ |
| C9–C13 | Мир, посадка, атмосфера, аэро | ✅ |
| C14-play | Игровой цикл | ✅ |
| C14-visual | 3D feel, KSP graphics bridge | ✅ |
| C14 | VAB legacy (стадии) | 🟡 🔒 |
| C15 | Стыковка legacy | 🟡 🔒 |
| C16 | RCS / SAS legacy | 🟡 🔒; 2026-09-16 Dev SAS (**0**): hold следует за рулём, не телепорт к углу включения |
| C17 | 3D-блоки, CoM, инерция | ✅ |
| C17-play | Playtest на блочной ракете | ✅ |
| C18 | Per-block контакты, debris | 🟡 (18.6 polish); 2026-09-12 crash +50%, тяга только с живых двигателей |
| C19 | WorldSnapshot / QuickSave F5/F9 | ✅ |
| C20 | Lockstep MP MVP | ✅ (2026-07-12) |
| C21 | VAB + ReStock — см. [[systems/VAB_DEVELOPMENT.md]] | 🟡 2026-09-14 свои OBJ+PBR → Custom Part Importer; exe: `VabScene`+`VABlvl3` |
| C22 | Mod API | ⬜ |
| C23 | Миссии / экономика | ⬜ |

Промпты и детали кластеров — в [[CHANGELOG.md]] и тематических заметках (`systems/`).

---

## Глобальный бэклог

### MP polish (после C20 ✅)

- [ ] **MP-F1** — микротелепорты на орбитах при lockstep catch-up
- [ ] **MP-F2** — высокое RAM/GPU в build
- [ ] **MP-F3** — аудит утечек памяти (long-session)

### Прочее

- [ ] Regression preview = execution (Unity Play mode)
- [x] Локализация HUD → C1
- [x] MP-B1…B3, Dock-B1, C20.6 smoke-test — закрыты (см. [[CHANGELOG.md]])

### Оценка объёма

| Блок | Статус |
|------|--------|
| C1–C20 (ядро + MP MVP) | ✅ закрыто 2026-07-12 |
| MP-F1…F4 polish | открыт |
| C21–C23 (VAB, mod API, миссии) | в работе / ⬜ |

---

## Ссылки по темам

| Тема | Файл |
|------|------|
| Бинды клавиш | [[systems/CONTROLS.md]] |
| Map / orbit focus / SoI | [[systems/MAP_MODE_ORBIT_SOI.md]] |
| VAB runtime | [[systems/VAB_DEVELOPMENT.md]] |
| ReStock import | [[systems/RESTOCK_PIPELINE.md]] |
| Архитектура / код-индекс | [[architecture/PROJECT_LAYOUT.md]] |
| Работа с AI | [[architecture/WORKING_WITH_USER.md]] |
