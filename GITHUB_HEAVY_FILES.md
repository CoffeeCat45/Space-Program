# GitHub vs диск

Origin: `https://github.com/CoffeeCat45/Space-Program.git` (старый `test1.git` редиректит сюда). Ветка релиза `v0.1.0-alpha`; `develop` — код без датасетов.

## Commit / push (обязательно)

GitHub **GH007** режет пуш, если в коммите личный Gmail (global `user.email`). В этом репо всегда:

- **author / committer:** `CoffeeCat45 <CoffeeCat45@users.noreply.github.com>`
- **remote:** `origin` = `https://github.com/CoffeeCat45/Space-Program.git` (не `test1.git`)
- локальный `user.name` / `user.email` уже выставлены **только в этом репо**; global не трогать
- если агент коммитит не из корня репо или Cursor подставляет другой email — перед `git commit` задать env `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_COMMITTER_NAME`, `GIT_COMMITTER_EMAIL` на те же значения
- не пушить, пока `git log -1 --format=%ae` не noreply; иначе снова GH007

Лимит GitHub: **100 МБ на файл**. Без LFS туда нельзя класть GEBCO/LDEM `.raw`.

После клона **без локальных карт Play не нарисует землю/луну/splat** — это ожидаемо.

`StreamingAssets/Ships/` **не** игнорируется (JSON чертежей, килобайты).

---

## Не в git (только этот ПК)

| Что | Зачем на ПК |
|-----|-------------|
| `Build/` (~9 ГБ) | Собранный `.exe` с картами. Обновить только карты: меню **Space Program → Update Heavy Build Data** (не входит в основной Build) |
| `ShareBuild/` + `ShareBuild.zip` | Тот же плеер **без** Planet/MapMode/RSS + копия Editor `settings.json` — скинуть другу |
| `Library/` (~3 ГБ) | Кеш Unity |
| `Не используемые/` (~7 ГБ) | Старый RSS/ReStock/TIFF, `Pipeline/` |
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

---

## На GitHub (код, настройки, доки)

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
