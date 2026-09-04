# Что попадёт на GitHub (следующий коммит)

GitHub **уже подключён**: `origin` → `https://github.com/CoffeeCat45/test1.git`, ветка `develop` (сейчас `origin/develop` = `0.0.7`).

Коммит **этим промптом не делается**. Датасеты, карты и картинки остаются только на диске (`.gitignore`).

Лимит GitHub: **100 МБ на файл**. Без LFS туда нельзя класть GEBCO/LDEM `.raw`.

---

## Не попадёт (gitignore + локальные архивы)

| Что | Зачем на ПК |
|-----|-------------|
| `Build/` (~9 ГБ) | Собранный `.exe` |
| `Library/` (~3 ГБ) | Кеш Unity |
| `Не используемые/` (~7 ГБ) | Старый RSS/ReStock/TIFF |
| `файлы луны/` (~405 МБ) | Сырьё LROC |
| `StreamingAssets/Planet/` (~8 ГБ) | Высоты Земли/Луны `.raw` / `.bin` |
| `StreamingAssets/MapMode/` (~132 МБ) | PNG карт и облаков |
| `Content/.../RSS-Terrain/` (~416 МБ) | Splat DDS |
| `Content/.../RSS-Textures/` (~149 МБ) | Earth/Moon color+NRM DDS |
| `Assets/Texture2D/` (~197 МБ) | Скайбокс Galaxy и импорт |
| `Assets/Models/`, `Mesh/`, `AnimationClip/`, `Cubemap/`, `Sprite/` | Импорт ReStock / арт |
| `*.raw` `*.tif` `*.dds` | Любые такие файлы ещё где угодно |
| `_Recovery/`, `.plastic/`, `bin/`, `SimulationServer/publish/` | Мусор сборок |
| `SimulationConsole/` | Замороженная консоль, в Play не нужна |
| `Без названия.canvas` | Локальный черновик |

После клона **без этих папок Play на чужой машине не покажет землю/луну/текстуры деталей** — это ожидаемо.

`StreamingAssets/Ships/` **не** игнорируется (JSON чертежей, килобайты).

---

## Попадёт (код, настройки, доки)

Если в следующем промпте сделать `git add` по рабочему дереву с этим ignore:

- `context/` — база знаний
- `GITHUB_HEAVY_FILES.md`, `.gitignore`, `.cursorrules`
- `SpaceProgramUnity/SpaceProgram/Assets/Scripts/` — Core + UnityAdapter + EarthSandbox
- `Assets/Shaders/`, `Prefabs/`, `Materials/`, `Resources/` (без тяжёлых DDS)
- `Assets/Scenes/` (сцены Unity, не PNG-карты планет)
- `Assets/Scenery/` (~19 МБ, VAB)
- `ProjectSettings/`, `Packages/manifest.json`, `Settings/`
- `Ships/*.json`
- `Tools/` (скрипты, не GEBCO-тайлы)
- `SimulationServer/` исходники (без `publish/` и `bin/`)
- правки уже tracked-файлов (`Simulation.cs`, HUD, и т.д.)

Типичный размер такого коммита — **десятки МБ**, не гигабайты.

---

## Как проверить GitHub

Следующим промптом: коммит на `develop` и `git push origin develop`. Если push пройдёт — репозиторий живой. Сообщение можно короткое, без длинного описания.
