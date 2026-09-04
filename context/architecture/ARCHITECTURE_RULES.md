# Архитектурные правила

Неизменяемый канон проекта: `double`, детерминизм, fixed timestep, независимость `SimulationCore`.

Связанные заметки: [[AI_CONTEXT.md]], [[PROJECT_LAYOUT.md]], [[PHYSICS_EXPECTATIONS.md]].

---

## Жёсткие ограничения

- не ломать `SimulationCore`
- `SimulationCore` независим от `Unity` (нет `UnityEngine`, `MonoBehaviour` в Core)
- `Unity` — только визуализация, ввод, камера, HUD
- никакой `Unity Physics` для основной физики
- расчёты на `double` (см. ниже)
- фиксированный шаг времени (`fixed timestep`)
- код простой, без лишней архитектурной сложности
- желательно сохранять детерминизм (lockstep MP, replay, snapshot)

---

## Точность типов данных (обязательно)

**В `SimulationCore` — только `double`:**

- позиции, скорости, ускорения (`SimVector3`)
- гравитационные параметры (`mu`, радиусы, SoI)
- время: `dt`, накопитель шага, орбитальные интервалы
- промежуточные величины орбитальной механики

**Запрещено в Core:**

- `float` для физики
- неявное `double → float → double` в траектории
- мировое состояние в `float` «для экономии»

**В `UnityAdapter`:**

- `float` только на границе: `Transform`, `LineRenderer`, UI, ввод, `worldScale`
- в Core — `double`; из Core — `float` только для отрисовки
- `SimulationRunner.fixedDt` и `timeScale` — **`double`** (по умолчанию `0.02`, `1.0`)

**Иерархия SoI (`SimulationScenario.SolarTransfer` и др.):**

- дочерние тела интегрируются **относительно родителя** с μ родителя
- `r_rel` / `v_rel` — состояние родителя **на начало шага**; сборка с родителем **после** его интеграции
- не смешивать старую позицию ребёнка с уже обновлённой позицией родителя в одном шаге

**Масштаб / моды:**

- метры/AU в Core — по-прежнему `double`
- floating origin в рендере не заменяет точность в Core

**Flight, два центра (🔒 2026-08-15):** Core — центр SoI, летит ракета. Unity — корабль в нуле, уезжает планета. Не инвертировать A/D/тягу в Core. Формула поворота планеты — [[systems/work_terrain.md]] §3.1 и снимок [[systems/PLANET_SPIN.md]].

---

## Что важно не сломать

- независимость `SimulationCore`
- fixed timestep: `double fixedDt` в runner → `Simulation.Update(double dt)`
- double в Core и для шага времени в адаптере
- Velocity Verlet + patched conics SoI
- target / maneuver node / preview / warp pipeline
- `WorldSnapshot` / QuickSave (F5/F9)
- lockstep input buffer и checksum (C20, когда MP включён)
- terrain UV/координаты — [[systems/work_terrain.md]], [[CONVENTIONS.md]] (не ломать при правках flight/planet)

---

## Статус репозитория (слои)

| Слой | Путь | Роль | Unity |
|------|------|------|-------|
| **SimulationCore** | `Assets/Scripts/SimulationCore/` | Физика, орбитальная математика, состояние мира | **Нет** |
| **SimulationConsole** | `SimulationConsole/` | ⚠️ **ЗАМОРОЖЕН** | **Нет** |
| **UnityAdapter** | `Assets/Scripts/UnityAdapter/` | Визуал, ввод, камера, HUD, VAB | **Да** |
| **EarthSandbox** | `Assets/Scripts/EarthSandbox/` | Изолированная terrain/LOD песочница | **Да** |

### Текущее состояние (сверять с кодом)

- **Мозг / глаза:** физика в Core; Unity — `SimulationRunner` + views.
- **3D Core:** `SimVector3` / `SimQuaternion`, **`double`**; MoonFlyby — z=0 для паритета.
- **Fixed timestep:** `SimulationRunner.fixedDt` и `timeScale` — **`double`** (default `0.02`, `1.0`); аккумулятор + sub-stepping до burn/warp.
- **Интегратор:** Velocity Verlet (`Integrator.cs`); гравитация — `IGravityModel` / `NewtonianGravityModel`.
- **SoI:** patched conics; pad и `SolarTransfer` — Солнце в Core (Земля относительно Солнца); визуал `SunVisual`.
- **Интерфейсы:** `IGravityModel`, `ISimulationStateView`; `Simulation` implements view port.
- **Точки входа:** Unity Play mode + `SimulationRunner`; консоль не использовать.

Подробная карта файлов — [[PROJECT_LAYOUT.md]].
