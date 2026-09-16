# ReStock import pipeline (C21.4)

Импорт 3D-деталей из мода ReStock (KSP .mu) для VAB — только Editor, не трогает SimulationCore.

**Свои модели (не KSP):** не этот пайплайн. Unity **Space Program → Parts → Custom Part Importer** + опционально `Tools/Blender/export_unity_custom_part.py`. См. [[systems/VAB_DEVELOPMENT.md]].

Исходники `.mu` и `Pipeline/` — в `Не используемые/` (не в git). В Play нужны префабы `Assets/Prefabs/Parts`. Пути `ReStock/Assets/` и `Pipeline/` ниже — как до архива; вернуть папки из архива, если снова печь импорт.

Связанные заметки: [[systems/VAB_DEVELOPMENT.md]], [[architecture/PROJECT_LAYOUT.md]].

---

## Обзор

### Структура файлов (v10, актуальная)

```
ReStock/Assets/
  Command/restock-mk1-pod.mu
  Command/restock-cupola-1.mu
  FuelTank/restock-fueltank-0625-1.mu
  FuelTank/restock-fueltank-125-1.mu
  Engine/restock-engine-terrier-1.mu
  Engine/restock-engine-reliant-2.mu

Pipeline/Blender/
  export_pilot_parts.py          ← batch export (BATCH_PARTS, manifest)
  restock_mu_to_fbx.py           ← import .mu, UV, material split helpers

SpaceProgramUnity/SpaceProgram/Assets/
  Models/Parts/
    Meshes/
      Command/
        restock-mk1-pod/
          restock-mk1-pod.obj
          restock-mk1-pod.manifest.json
        restock-cupola-1/
          restock-cupola-1.obj              ← cupola + crewed-endcap (joined)
          restock-cupola-1.manifest.json
      FuelTank/
        restock-fueltank-0625-1/
          restock-fueltank-0625-1.obj
          restock-fueltank-0625-1.manifest.json
        restock-fueltank-125-1/
          restock-fueltank-125-1.obj
          restock-fueltank-125-1.manifest.json
      Engine/
        restock-engine-terrier-1/
          Engine.obj, EngineCore909.obj, Frame_2.obj, Frame_4.obj, Frame_MLI.obj
          restock-engine-terrier-1.manifest.json
        restock-engine-reliant-2/
          LV_T30_Boat.obj, LV_T30.obj, fairingWhite.obj, … (все варианты mesh)
          restock-engine-reliant-2.manifest.json
    Textures/
      Command/*.png
      FuelTank/*.png
      Engine/*.png
  Materials/Parts/
    restock-mk1-pod-1.mat
    restock-engine-125-revised-1.mat
    …                             ← плоский список .mat по имени текстуры
  Prefabs/Parts/
    restock-mk1-pod.prefab
    restock-engine-terrier-1.prefab
    …                             ← batch prefabs (pilot batch в export_pilot_parts.py)

  Scripts/UnityAdapter/Editor/
    ReStockImportPipeline.cs      ← reimport, материалы, prefab build, UV postprocessor
    ReStockBatchParts.cs          ← загрузка из Pipeline/Parts/*.partimport.json + manifest + merge manifest
    ReStockPilotEditorMenus.cs    ← меню ReStock 8/9/10
```

**Правило путей в коде:** всегда `Meshes/{Category}/{partId}/…` — и для single-, и для split-деталей (`GetPartFolderAssetPath` в `ReStockBatchParts.cs`). Flat-файлы в корне `Command/` / `FuelTank/` / `Engine/` **не используются** (legacy удаляются при export).

**Не использовать:** `Meshes/**/Materials/` — пустые stub от импортёра; рабочие материалы только в `Assets/Materials/Parts/`.

---

### Логика пайплайна

#### 1. Blender (`export_pilot_parts.py`)

Для каждой записи в `BATCH_PARTS` `(partId, category, delete_paint, keep_meshes, force_split, default_enabled)`:

1. Импорт `.mu` через `io_object_mu` (`restock_mu_to_fbx.py`)
2. Опционально: удалить paint-варианты, collider/flag mesh
3. `keep_meshes` — опционально вырезать лишние mesh до export (cupola: только `25mCupola 1`); reliant экспортирует **все** mesh в папку, видимость — в Unity через `EnabledMeshes`
4. `prepare_meshes_for_unity_export` — схлопнуть UV в один слой `UVMap`
5. `bake_mesh_scales` — apply scale
6. `split_mesh_objects_by_material` — multi-material mesh → `Name__materialKey` (если >1 material slot); `build_mesh_plan` сохраняет `Frame 2__…` (не отбрасывает после split)
7. **`MODEL_ATTACHMENTS`** (cupola): второй `.mu` + KSP cfg transform → pivot на группу mesh → bake → join; cupola OBJ с `export_materials=True`
8. `export_blender_textures_as_png` → `Textures/{Category}/`
9. Экспорт OBJ в `Meshes/{Category}/{partId}/`:
   - **single** (`force_split=false`): один `{partId}.obj`
   - **split** (`force_split=true`): по одному OBJ на mesh (`Engine.obj`, …)
10. `write_manifest` → `{partId}.manifest.json`
11. `delete_legacy_flat_exports` — убрать старые flat `.obj` из корня категории

#### 2. `manifest.json`

```json
{
  "partId": "restock-engine-terrier-1",
  "category": "Engine",
  "splitSubmeshes": true,
  "meshes": [
    { "name": "Engine", "role": "primary", "defaultEnabled": true },
    { "name": "Frame 2", "role": "primary", "defaultEnabled": true }
  ]
}
```

Unity `MergeManifest()` читает mesh-список; `EnabledMeshes` + `LockEnabledMeshes` в `ReStockBatchParts.cs` **перебивают** `defaultEnabled` из manifest для ручной настройки (reliant: только `LV_T30_Boat`).

#### 3. Unity — **ReStock → 9** (`ForceReimportPilotAndRebuild`)

1. Reimport всех PNG в `Textures/`
2. Для каждой детали из `ReStockBatchParts.Definitions`:
   - **split:** reimport каждый `{partId}/*.obj`
   - **single:** reimport `{partId}/{partId}.obj`
3. `RebuildPartMaterials()` — группировка PNG → `.mat` в `Materials/Parts/` (PNG > DDS, алиасы слотов KSP)
4. `BuildPilotPartPrefabs()`:
   - **single:** load root OBJ → `ApplySimpleVabPartSetup` → save prefab
   - **split:** instantiate каждый submesh OBJ → parent → `ApplySimpleVabPartSetup` (disable по `EnabledMeshes`) → save prefab
5. `OnPostprocessModel`: `FlipPilotMeshUvV = true`; лог `ReStock UV: …` (диагностика)

**Назначение материалов на mesh:** `PilotMeshToMaterial` + `GetMeshMaterialOverride` (в т.ч. суффикс `__materialKey` после material split в Blender). Для **container prefabs** — `VabContainerPrefabSetup` + `ResolvePilotMeshMaterialKey` (имя GO, иерархия, путь вложенного OBJ-префаба `Engine.obj` → `Engine`, суффикс `Frame 2__…`). **Terrier:** `Frame 2`/`Frame 4` → `revised-1` (серый металл), `Frame_MLI` + фольга после split → `additional-3`. Legacy-копирование материалов для split-деталей отключено (в flat-префабе Frame тоже был `additional-3`).

#### 4. Сцена VAB

После rebuild: **удалить** старые экземпляры → перетащить префабы из `Assets/Prefabs/Parts/`.

---

### Правила импорта (зафиксировано)

| Правило | Детали |
| ------- | ------ |
| Формат mesh | **OBJ + PNG**, не FBX/GLB (ломает UV-атласы KSP) |
| Одна папка = одна деталь | `Meshes/{Category}/{partId}/` |
| Multi-mesh в одном .mu | `force_split=true` → отдельные OBJ |
| Multi-material на одном mesh | Blender split → `Mesh__restock-engine-125-revised-1.obj` |
| **Cupola дно** | В KSP второй MODEL `restock-crewed-endcap-1`; см. справочник ниже |
| **Коллайдеры** `collider_*` | Физика KSP; в export удаляются, не попадают в OBJ |
| UV | flip V при импорте; warning `FBXImporter inconsistent result` на .obj — **норма**, не баг |
| Текстуры | `restock-engine-125-1` (KSP slot) → `restock-engine-125-revised-1` (PNG/mat) |

---

### pilot batch (см. BATCH_PARTS в export_pilot_parts.py) (проверено v10)

| `partId` | Категория | Тип export | Видимые mesh | Статус |
| -------- | --------- | ---------- | ------------ | ------ |
| `restock-mk1-pod` | Command | single `{partId}.obj` | Mk1PodDark | ✅ |
| `restock-fueltank-0625-1` | FuelTank | single | Tank0625 | ✅ |
| `restock-fueltank-125-1` | FuelTank | single | Tank800_A | ✅ |
| `restock-engine-terrier-1` | Engine | split ×5 | все 5 | ✅ |
| `restock-cupola-1` | Command | single + **endcap** `restock-crewed-endcap-1` (как в KSP cfg) | `25mCupola 1` | ✅ |
| `restock-engine-reliant-2` | Engine | split ×10 | только `LV_T30_Boat` | ✅ MVP |

**Ручная разметка** (`ReStockBatchParts.cs`): `EnabledMeshes`, `LockEnabledMeshes`, `PrimaryMaterialOverride`; маппинг mesh→mat в `ReStockImportPipeline.cs` (`PilotMeshToMaterial`, `GetMeshMaterialOverride`).

| Mesh | Material |
| ---- | -------- |
| `Engine`, `LV_T30`, `LV_T30_Boat` | `restock-engine-125-revised-1` |
| `EngineCore909`, glow | `restock-engine-125-revised-2` |
| `Frame 2/4/MLI` | `restock-engine-125-additional-3` |
| `fairingWhite` / `fairingOrange` | shroud-125-1 / -2 |
| `Tank0625` / `Tank800_A` | fueltank-0625-1 / fueltank-125-1 |
| `25mCupola 1` / cupola body | `restock-cupola-lab-1` |
| endcap (`25mCapBlack`, `CommonHatchRound`, …) | `restock-endcap-crewed-1` / `restock-crewed-hatch-1` |
| `*__restock-*` (material split) | имя после `__` |

---

### Рабочий цикл (чеклист)

1. Blender 3.6+ → Run `Pipeline/Blender/export_pilot_parts.py`
2. Unity → **Space Program → ReStock → 9** (лог: `batch rebuild v14`)
3. Консоль: `materials … missingAlbedo=0`, `built N batch prefabs`, `setup … -> mesh->material` без `MISSING`
4. VabScene: заменить экземпляры из `Prefabs/Parts/`

---

### C21.4 — Справочник: импорт ReStock (как работает, что знать, типовые проблемы)

**Для кого:** экспорт из Blender, настройка материалов в Unity, отладка «съехавших» UV/текстур/мешей.

#### Что нужно до старта

| Требование | Где / зачем |
| ---------- | ----------- |
| **Blender 3.6+** | `Pipeline/Blender/export_pilot_parts.py` |
| **Аддон `io_object_mu`** (taniwha) | Импорт `.mu`; в prefs: KSP GameData = `ReStock/` (или полный GameData) |
| **Полные ReStock Assets** | `.mu` + текстуры; для cupola нужен `Command/restock-crewed-endcap-1.mu` (отдельный файл, не внутри cupola `.mu`) |
| **Unity Editor** | Меню `Space Program → ReStock → 9` |
| **Не трогать** `SimulationCore` | Пайплайн только Editor + `Assets/` |

**Золотое правило:** в сцену VAB кладём **префабы** из `Assets/Prefabs/Parts/`, не сырые OBJ из `Meshes/`.

---

#### Как устроен импорт (сквозной поток)

```
ReStock/Assets/{Category}/{partId}.mu
  │
  ├─ Blender: export_pilot_parts.py
  │     ├─ импорт .mu (io_object_mu)
  │     ├─ очистка: collider, flag, paint-варианты
  │     ├─ UV → один слой; bake scale
  │     ├─ [опционально] MODEL_ATTACHMENTS (cupola + endcap)
  │     ├─ [опционально] split по material / split по mesh
  │     ├─ PNG → Assets/Models/Parts/Textures/{Category}/
  │     └─ OBJ + manifest.json → Assets/Models/Parts/Meshes/{Category}/{partId}/
  │
  └─ Unity: ReStock → 9
        ├─ reimport OBJ/PNG
        ├─ PNG → .mat в Assets/Materials/Parts/
        ├─ назначение материалов по слотам / mesh-имени
        └─ prefab → Assets/Prefabs/Parts/{partId}.prefab
```

**Два режима export:**

| Режим | Когда | Результат в папке детали |
| ----- | ----- | ------------------------ |
| **single** | pod, tanks, cupola | один `{partId}.obj` |
| **split** | terrier, reliant | несколько `{MeshName}.obj` + manifest |

Для split Unity собирает **один prefab** из нескольких OBJ-детей; видимость submesh — через `EnabledMeshes` в `ReStockBatchParts.cs`.

---

#### Форматы: что работает и что нет

| Формат | Статус | Почему |
| ------ | ------ | ------ |
| **OBJ + PNG** | ✅ рабочий | UV-атласы KSP сохраняются; flip V в Unity (`FlipPilotMeshUvV`) |
| **FBX / GLB** | ❌ не использовать | «мозаика» UV, неверные material slots |
| **Материалы в `Meshes/**/Materials/`** | ❌ игнорировать | Пустые stub от импортёра Unity |
| **Материалы в `Assets/Materials/Parts/`** | ✅ канон | Создаёт `RebuildPartMaterials()` из PNG |

---

#### KSP `.mu` → Blender: координаты и дочерние MODEL

Аддон `io_object_mu` конвертирует Unity LHS → Blender RHS: вектор `(x, y, z)` Unity становится **`(x, z, y)`** в Blender (`mu.py`).

**Следствие для MODEL-блоков из `.cfg`:**

| KSP cfg (Y-up, как в патчах ReStock) | Blender (после io_object_mu) |
| ------------------------------------ | ---------------------------- |
| `position = 0, -0.4, 0` | `(0, 0, -0.4)` — смещение по **Z** |
| `scale = 1, -1, 1` (flip стека) | для endcap cupola: **`(1, -1, 1)`** — flip по **Y** (ось люка в mesh), не `(1, 1, -1)` |
| `rotation = 0, 0, 0` | градусы → радианы, swap Y/Z как у position |

**Cupola — как в игре, не «всё в одном .mu»:**

В `ReStock/Patches/Command/restock-command-pods.cfg` дно закрывается **вторым MODEL**:

```cfg
MODEL { model = ReStock/Assets/Command/restock-cupola-1 }
MODEL {
  model = ReStock/Assets/Command/restock-crewed-endcap-1
  position = 0.0, -0.4, 0.0
  scale = 1,-1,1
}
```

В export это `MODEL_ATTACHMENTS` в `export_pilot_parts.py`:

1. Импорт cupola → mesh `25mCupola 1`
2. Импорт endcap → 3 mesh (крышка, рамка, люк)
3. Transform **одним pivot** на всю группу (не на каждый mesh отдельно!)
4. Bake location/rotation/scale в вершины
5. Join → один OBJ с **несколькими material slots**
6. `export_materials=True` → `.mtl` для Unity

**Коллайдеры** `collider_base` / `collider_top` — физика KSP, **не визуал**; в export удаляются.

---

#### Материалы и текстуры

**Один KSP `.mu` ≠ одна текстура.** Типичные слои:

| Слой в mesh | Пример | Действие |
| ----------- | ------ | -------- |
| Основной корпус `-1` | `restock-mk1-pod-1` | материал `…-1.mat` |
| Окна / панели `-2` | `restock-mk1-pod-2` | отдельный `.mat` |
| Paint-варианты | `Mk1PodWhite/Dark` | **скрыть** в export / Unity |
| Flag | `Flag` | **скрыть** (в KSP рисуется в рантайме) |

**Назначение в Unity** (по приоритету):

1. Имя material slot из OBJ/MTL → `ResolveMaterialForSourceSlot`
2. Алиасы в `MaterialSlotAliases` (KSP slot → ReStock PNG)
3. Имя mesh → `PilotMeshToMaterial` / `GetMeshMaterialOverride`
4. Fallback `-1` для partId

**Cupola / endcap — отдельные материалы:**

| KSP material / mesh | Unity `.mat` |
| ------------------- | ------------ |
| cupola body | `restock-cupola-lab-1` |
| `restock-endcap-crewed-1`, `25mCapBlack`, `25mCapFoil` | `restock-endcap-crewed-1` |
| `restock-crewed-hatch-1`, `CommonHatchRound` | `restock-crewed-hatch-1` |

PNG должны попасть в `Textures/Command/` при Blender export и быть в `PilotTextureSourcePatterns` для копирования в `Materials/Parts/`.

**Multi-material на одном mesh** (reliant `LV_T30_Boat`): Blender делает `split_mesh_objects_by_material` → OBJ `Mesh__materialKey`; Unity читает суффикс после `__`.

---

#### Типовые проблемы и решения

| Симптом | Вероятная причина | Решение |
| ------- | ----------------- | ------- |
| Серая деталь, `MISSING` в логе setup | Нет `.mat` или albedo PNG | ReStock → 9; проверить `missingAlbedo=0`; PNG в `Textures/{Category}/` |
| Текстура «мозаикой» / разбитые UV | Импорт через FBX/GLB | Только OBJ; пересобрать из Blender |
| Вся деталь одной неправильной текстурой | Один material на multi-slot mesh; или fallback на `-1` | Blender: split по material; cupola: `export_materials=True`; проверить алиасы слотов |
| Mk1 pod — серая каша, лишние mesh в Hierarchy | Paint-варианты (`Mk1PodWhite`…) поверх основного | Export с `delete_paint`; в Unity renderer disabled |
| Terrier Frame — не тот металл | Неверный mesh→mat | `Frame 2/4` → `revised-1`, `Frame_MLI` → `additional-3` |
| Reliant — мозаика на корпусе | Multi-material без split | Re-export после material split; только `LV_T30_Boat` enabled |
| Cupola — дно открыто | Нет endcap `.mu` или не экспортирован attachment | Проверить `restock-crewed-endcap-1.mu`; лог Blender: `attached … endcap`, `joined N mesh` |
| Cupola — крышка не там / не перевёрнута | Transform на каждый mesh; неверная ось flip | Pivot на группу; position KSP Y → Blender Z; flip endcap `(1,-1,1)` по Y |
| Cupola — люк с текстурой корпуса | Join без material export | `export_materials=True`; материалы endcap в `PilotMaterialNames` |
| `Importer(FBXImporter) inconsistent result` на `.obj` | Побочный эффект UV flip | **Норма**, не баг — игнорировать |
| Prefab старый после reimport | Экземпляр в сцене не обновился | Удалить из сцены → перетащить prefab заново из `Prefabs/Parts/` |
| `built N prefabs` < 6 | Нет OBJ или manifest | Blender export; путь `Meshes/{Category}/{partId}/` |
| `missingMaterial=86` при полном ReStock FBX | Нет PNG для не-pilot деталей | Ожидаемо для full batch; важны pilot batch + `missingAlbedo=0` |
| Материалы в `Meshes/**/Materials/` | Stub от импортёра | Не использовать; удаляются cleanup-меню; рабочие — `Materials/Parts/` |
| Ноды стыковки не совпадают с mesh | Угаданные bounds | **VAB → Regenerate Pilot Part Nodes**; ноды из `.partdef.json` |

---

#### Диагностика: что смотреть в логах

**Blender (`export_pilot_parts.py`):**

```
[export_batch_parts] attached 3 mesh(es) from restock-crewed-endcap-1.mu
[restock_mu_to_fbx] KSP MODEL transform cfg pos=(0.0, -0.4, 0.0) ... -> blender pos=(0.0, 0.0, -0.4) scale=(1.0, -1.0, 1.0)
[export_batch_parts] joined 4 mesh(es) -> 25mCupola 1
[export_batch_parts] DONE converted=6 failed=0
```

**Unity (ReStock → 9):**

```
ReStock: batch rebuild v14 (...)
ReStock: materials ... missingAlbedo=0
ReStock: built N batch prefabs
ReStock: setup restock-cupola-1 -> ...->restock-cupola-lab-1 / restock-endcap-crewed-1 / restock-crewed-hatch-1
```

Если `failed>0` в Blender или `MISSING` / `missingAlbedo>0` в Unity — чинить до проверки в VAB.

---

#### Ключевые файлы (куда лезть при правках)

| Задача | Файл |
| ------ | ---- |
| Список pilot-деталей, split, enabled mesh | `ReStockBatchParts.cs` |
| Меню reimport, материалы, prefab build | `ReStockImportPipeline.cs` |
| Batch export, MODEL_ATTACHMENTS | `Pipeline/Blender/export_pilot_parts.py` |
| UV, material split, KSP→Blender transform | `Pipeline/Blender/restock_mu_to_fbx.py` |
| KSP патч cupola + endcap | `ReStock/Patches/Command/restock-command-pods.cfg` |

**Добавление новой pilot-детали:** `BATCH_PARTS` в `export_pilot_parts.py` → Blender export → `Pipeline/Parts/*.partimport.json` → ReStock → **9** → prefab в сцену.

**VAB runtime (C21.5):** см. [[systems/VAB_DEVELOPMENT.md]] — не дублировать здесь.

**Промпт ReStock (чат):**

```
@architecture/AI_CONTEXT.md @roadmap/PROGRESS.md
C21.4 ReStock pipeline. Unity Editor + export_pilot_parts.py. Не трогать SimulationCore.
делай
```

