# VAB — блочная сборка (C21)

3D-редактор ракет, чертежи, runtime-сборка. ReStock import — [[RESTOCK_PIPELINE.md]].

Связанные заметки: [[architecture/PROJECT_LAYOUT.md]], [[systems/CONTROLS.md]].

---

## C21 — VAB на блочной сборке · 🟡

**Зачем:** 3D VAB-редактор: дерево блоков из `ShipBlock`, сохранение чертежей на диск, spawn на мысе.

**Каноничная карта папок (Unity `Assets/` + внешнее хранилище):**

```
SpaceProgramUnity/SpaceProgram/Ships/              ← JSON-чертежи ракет (.json), вне Assets
SpaceProgramUnity/SpaceProgram/Assets/
├── Scenes/                              ← VabScene (хост), PlaybackScene, MenuScene; ангар — `Scenery/KSC/Interiors3D/VABlvl3`
├── Models/
│   ├── Environments/                    ← 3D-меши ангаров / зданий (импорт FBX/OBJ)
│   └── Parts/
│       ├── Meshes/                      ← ReStock: одна папка на деталь {Category}/{partId}/*.obj
│       ├── Textures/                    ← ReStock PNG / DDS
│       └── Custom/                      ← свои модели: {Category}/{partId}/*.obj + PNG
├── Materials/Parts/                     ← Lit .mat (не Meshes/**/Materials/)
│   └── Custom/                          ← URP Lit для своих деталей
├── Prefabs/
│   ├── UI/                              ← Sidebar, панель сохранения, список кораблей
│   └── Parts/                           ← container prefabs (ReStock и свои)
├── Resources/RuntimePartCatalog.asset   ← печётся при Build: префабы для .exe
└── StreamingAssets/Ships/               ← legacy `.ship` + копия JSON для .exe
```

**Файлы (архитектура данных C21):**

- `SimulationCore/Entities/VesselBlueprint.cs` — DTO: `BlockSnapshot`, `VesselBlueprint`, JSON serialize/deserialize (без Unity)
- `UnityAdapter/Vab/VabFileSystem.cs` — `SaveVehicle` / `LoadVehicle` / `GetSavedVehicleNames` → `SpaceProgramUnity/SpaceProgram/Ships/*.json`


| #    | Подзадача                                                    | Статус |
| ---- | ------------------------------------------------------------ | ------ |
| 21.0 | Карта папок + `VesselBlueprint` + `VabFileSystem` (JSON)   | ✅      |
| 21.1 | Snap attach nodes (Core `VabSnapSolver`, порог 0.2 м)       | 🟡 MVP |
| 21.2 | Live CoM / Δv preview в редакторе                            | ⬜      |
| 21.3 | Export blueprint → `.ship` v2 / Core spawn на мысе           | ⬜      |
| 21.4 | **ReStock import pipeline** (Blender → Unity OBJ + материалы + префабы) | ✅ MVP (v10, 2026-07-15) |
| 21.5 | **VAB 3D-редактор runtime** (UI, hold/drag, detach, камера) | 🟡 MVP (2026-07-15); container prefabs + snap + terrier materials ✅ (2026-07-17) |

#### C21.5 — VAB 3D-редактор (runtime)

**Зачем:** KSP-like сборка в ангаре: деталь на курсоре, snap по `AttachmentNode`, отрыв ветки, орбитальная камера. **Core** хранит дерево `BlockSnapshot`; **Unity** — префабы и ввод.

**Файлы:**

| Слой | Файл | Роль |
| ---- | ---- | ---- |
| Core | `SimulationCore/Vab/VabSnapSolver.cs` | Поиск snap между нодами (без Unity) |
| Core | `SimulationCore/Vab/VabEditSession.cs` | Удержание детали/ветки, commit/cancel/delete в blueprint |
| Adapter | `UnityAdapter/Vab/VabEditorController.cs` | UI-сайдбар, raycast, ghost, detach, save/load |
| Adapter | `UnityAdapter/Vab/VabCameraController.cs` | Орбитальная камера вокруг focus point |
| Adapter | `UnityAdapter/Vab/VabRotationGizmo.cs` | Кольца + вращение при удержании |
| Adapter | `UnityAdapter/Vab/VabColliderUtility.cs` | Вкл/выкл коллайдеров на ghost |
| Core | `SimulationCore/Entities/PartNodeLibrary.cs` | Жёсткие координаты нод pilot ReStock (метры, part-local) |
| Adapter | `UnityAdapter/Vab/PartCatalog.cs` | Автоскан container prefabs `Prefabs/Parts/{Command,FuelTank,Engine}/`; flat legacy prefabs скрыты |
| Adapter | `UnityAdapter/Vab/PartPrefabBuilder.cs` | Чтение authored `AttachmentNodeMarker` на container prefab |
| Adapter | `UnityAdapter/Editor/VabContainerPrefabSetup.cs` | Меню **Apply Materials to Container Prefabs** |
| Adapter | `UnityAdapter/Vab/PartThumbnailUi.cs` | Двухслойные иконки каталога (KSP-фон + foreground PNG) |
| Adapter | `UnityAdapter/Editor/VabPartThumbnailStudio.cs` | Рендер иконок в изолированной пустой сцене |
| Adapter | `UnityAdapter/Editor/VabPartThumbnailGenerator.cs` | Меню batch-генерации `@thumbs` |
| Adapter | `UnityAdapter/Vab/VabPartCatalogTooltip.cs` | KSP-style hover-панель детали |
| Adapter | `UnityAdapter/Play/GameSessionBootstrap.cs` | Меню → VAB, показ/скрытие ангара |

**Сделано (MVP 2026-07-15):**

- UI-сайдбар: категории **К / Б / Д**, каталог pilot-частей из `PartCatalog`, Save/Load/Clear
- Клик в UI → ghost на курсоре; коллайдеры ghost **выключены**
- Snap 0.2 м; зелёная подсветка при примагничивании
- **Ноды стыковки pilot-деталей** — `PartNodeLibrary` + обновлённые `.partdef.json` (bounds.y/2, не угадывание по mesh); меню **Space Program → VAB → Regenerate Pilot Part Nodes**
- **Node + Surface attach** — `VabSnapSolverAdapter.ExecuteSnapResolution`: торцевые ноды (0.2 м) или боковое крепление по нормали меша (`attachedToNodeIndex = -2`)
- **Радиальная симметрия** — клавиша **X**: 1 → 2 → 3 → 4 → 6 → 8; при surface attach — призраки-клоны + commit N блоков в blueprint
- Первая деталь — площадка сборки над полом (`VabSceneEnvironment.buildOriginHeightAboveFloor`, по умолчанию **7 м**); ghost двигается по площадке до фиксации
- **Escape** (руки пустые) — главное меню. Камера меню — `FlightCamera` на `SimulationSession` (не камера ангара: иначе Display 1 пустой).
- Delete — удалить ветку под курсором или удерживаемый блок; Delete над сайдбаром — удалить удерживаемое
- Камера: **ПКМ** орбита, колёсико zoom (**экспоненциальный**, 2–80 м), Shift+**ПКМ** — пан по высоте
- Чертежи → `SpaceProgramUnity/SpaceProgram/Ships/*.json` через `VabFileSystem`

**Сделано (2026-07-17):**

- **Container prefabs** — ручные префабы с `Node_Top`/`Node_Bottom` в Editor; 3 pilot-детали в каталоге
- **Stack snap** — вертикальная сборка pod→tank→tank→engine; слой `AttachmentNode`, owned markers, post-placement refresh
- **Terrier materials** — правильные текстуры на сабмешах (`revised-1` / `revised-2` / `additional-3`); резолвер по nested OBJ path

**Сделано (2026-07-18):**

- **Иконки каталога (KSP-style)** — editor pipeline `VabPartThumbnailStudio` (пустая временная сцена, URP, layer 31) → foreground PNG в `Assets/Models/Parts/Icons/@thumbs/`; серый двухтоновый фон в UI (`PartThumbnailUi`); меню **Generate Missing / Regenerate All / Repair Broken**; hover-панель с превью
- **Скругление ячеек каталога** — `PartThumbnailRoundedMask` (mask + border-radius на превью в сетке и tooltip)

**Не сделано (следующие шаги C21):**

- Mirror-симметрия (radial x2…x8 через **X** уже есть — см. [[systems/CONTROLS.md]])
- Staging stack в UI
- Live CoM / Δv preview (21.2)
- Export blueprint → `.ship` v2 (21.3)

**Промпт для чата:**

```
@architecture/AI_CONTEXT.md @roadmap/PROGRESS.md
C21.5 VAB editor — polish snap / симметрия / staging. Unity + SimulationCore/Vab.
делай
```

---

### Свои модели (не KSP) → prefab

Не путать с ReStock `.mu`. Исходники: `.obj` (Y-up, метры) + PNG цвет / металлы / roughness.

1. Blender (опционально): `Tools/Blender/export_unity_custom_part.py` — OBJ под Unity и копии карт `{name}_albedo/_metallic/_roughness.png`.
2. Unity Edit mode: **Space Program → Parts → Custom Part Importer** — указать OBJ (PNG рядом подхватываются), категорию, массу/тягу → **Собрать prefab**.
3. Результат: `Assets/Prefabs/Parts/{Category}/{partId}/{partId}.prefab` + `.partdef.json`. Каталог VAB подхватывает сам.
4. Ноды `Node_Top` / `Node_Bottom` после первой сборки можно подвинуть в Prefab Mode; повторный импорт их не сбрасывает (галка в окне).
5. **Общий атлас:** одинаковые PNG (по содержимому) → один `.mat` в `Materials/Parts/Custom/`. Короткий список деталей в имени через `+`; если не влезает — имя файла атласа, детали в Asset Labels и `custom-part-materials.json`.

Папка `Assets/Models/Parts/Custom/` — без KSP UV-flip (он только у ReStock `Meshes/`).

**Промпт C21.4 (ReStock):**

```
@architecture/AI_CONTEXT.md @roadmap/PROGRESS.md
C21.4 ReStock pipeline v10. Unity Editor + export_pilot_parts.py.
Не трогать SimulationCore.
делай
```

---

