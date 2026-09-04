# Space Program — AI Context

Стартовый хаб для AI. Детали — в связанных заметках; **не дублировать** их здесь.

---

## Что это за проект

`Space Program` — космическая игра-песочница с упором на реалистичную орбитальную механику. По духу вдохновлена `Kerbal Space Program`, но архитектурно это отдельный проект со своим симуляционным ядром, детерминированной логикой и полным контролем над физикой.

**Главная идея:**

- `SimulationCore` = мозг, вся физика и состояние мира
- `Unity` = только визуализация, ввод, камера, UI

Цель — понятная, предсказуемая, математически обоснованная симуляция: орбиты, переходы, гравитация, манёвры, SoI, rendezvous.

**Уже есть:** блочные корабли, snapshot save/load, lockstep MP MVP (C20), VAB + ReStock prefabs, Earth terrain sandbox (LOD в работе).

**В перспективе:** перенос Core в другой движок (ультра далёкая преспектива), mod API, миссии/экономика/наука, GPU planet engine (C24).

---

## Roadmap и текущий фокус

**Актуальный фокус и задачи СМОТРИ СТРОГО В** [[roadmap/PROGRESS.md]].

Журнал может отставать от кода — при расхождении сверять с репозиторием и Play mode.

`SimulationConsole` — заморожен. Разработка и проверка — только Unity Play mode.

---

## Карта знаний (architecture)

| Тема | Заметка |
|------|---------|
| Архитектурный канон (double, timestep, Core) | [[ARCHITECTURE_RULES]] |
| Работа с пользователем | [[WORKING_WITH_USER]] |
| Структура репо и слои | [[PROJECT_LAYOUT]] |
| Физика: ожидания и ограничения | [[PHYSICS_EXPECTATIONS]] |
| Cube-sphere / Mesh Frame | [[CONVENTIONS]] |

## Карта знаний (systems / roadmap)

| Тема | Заметка |
|------|---------|
| Бортовой журнал (может отставать) | [[roadmap/PROGRESS]] |
| Changelog | [[roadmap/CHANGELOG]] |
| ReStock import (Editor) | [[systems/RESTOCK_PIPELINE]] |
| Unity: Runner, views, HUD | [[systems/UNITY_ENTRY_POINTS]] |
| Terrain канон (Mesh Frame) | [[systems/work_terrain]] |
| Сутки Земли, полёт vs карта | [[systems/PLANET_SPIN]] |
| LOD runtime | [[systems/LOD]] |
| LOD: эталон старта на паде 🔒 | [[systems/LOD_BASELINE_PAD_START]] |
| LOD: F8 / что ломается | [[systems/LOD_FAILURES]] |
| Океан (чанки, не сфера) | [[systems/OCEAN]] |
| Атмосфера и облака (визуал) | [[systems/ATMOSPHERE_CLOUDS]] |
| NASA/GEBCO заметки | [[systems/EARTH_TERRAIN_NOTES]] |
| Бинды | [[systems/CONTROLS]] |
| Map / orbit focus / SoI | [[systems/MAP_MODE_ORBIT_SOI]] |
