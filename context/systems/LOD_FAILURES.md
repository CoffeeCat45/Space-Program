# LOD — что ломается (живой разбор F8)

> Канон и архитектура: [[LOD.md]]. **Эталон рабочего старта 🔒 (не редактировать):** [[LOD_BASELINE_PAD_START.md]]. Сюда — **симптомы, гейты, тупики**. После каждого реального фикса обновлять таблицу «сейчас».  
> Проверка: Unity Play, **F8** в Console. Правила — UV/горизонт/RING на любом теле ([[LOD.md]] §5.2.1). Мыс не ломать ради Луны: это другая точка грани, не «другая планета».

Как прислать баг: F8 + скрин Scene (wireframe) + Game. Overlay врёт меньше, чем «кажется, дыра» (чёрный шар сбоку = Земля, не LOD).

---

## Сейчас (2026-09-12)

| Симптом | F8 | Почему | Статус |
|---------|-----|--------|--------|
| Луна pad: finest и +1 дёргаются (и на паузе) | после прогрева `refine=1` `unfinished=1` `skipIdle=0`; `lastFlap: merge F4 L7 0.50,0.50 \| split F4 L7 0.50,0.50` | RING-merge схлопывал **сам** `parentL7` под кораблём (sibling L8 `cap=7`, `IsViewerFinestQuadMember` мимо), затем тот же тик split обратно | 🟡 2026-09-12 RING не merge узел с кораблём в поддереве; догон RING-exempt только пока `V` ниже maxL |
| Луна pad: не грузятся, но на одном уровне | `parentL7` 4×L8 ок; `L1q 7/8/8/7` затем `8/8/8/7`; `lastFlap split L6/L7 0.50,0.48 \| merge` того же UV | Догон режет чужой L1 до L8; RING/span merge в том же тике схлопывает. Потом idle-skip — плиток нет | 🟡 2026-09-12 внутренние L6–L8 у 0.5,0.5 не RING-merge; skipIdle пока L1q не 8/8/8/8 |
| Луна pad не дробится, `chunks=24`, `viewerSpan=86850m` | `L2 V gate=CRACK U+nL1`, соседи `L1 SPLIT-OK sse` | `HoldNonViewerSplits` стопил **всех** не-V, пока V хочет split. V ждёт L1, L1 в hold → тупик. Catch-up не закрывал gap=1 (`L2` vs `L1` — не `gap>1`) | 🟡 фикс: hold только при ≥75% капа; catch-up при CRACK до Refresh |
| Земля / мыс: дёрганье колец (Луна ок) | `uv=0.390,0.159` `viewerSpan=4975m`; `lastFlap split L5 0.4,0.16 / L3 0.0,0.0 \| merge` того же тика | Мыс не центр грани (один L1). L5 кузены пада **в диске** (на Луне те L5 за HORIZON). L7 кольца догоняли их, RING схлопывал дальний ребёнок cap=0 | 🟡 2026-09-12 после maxL догон только от листа `V`; RING-merge только если все дети сверх капа |
| `needL=14/16`, `capL=8` | pad не 64 m | Слайдер Max LOD. Moon L8 ≈ 1.3 km, Earth L8 ≈ 5 km | не баг |
| `uv=0.500,0.500` неровные кольца | F8 ancestry ровный L8, Scene одна четверть мелкая | UV half-open: `V` в одном L1. Три других L1 **касаются** угла. Sibling-balance был только parent-contains-V. PERIPH 0.45×horizon резал чужие четверти при 535 чанках. Камера **не** split. | 🟡 Touch-угол + in-disk 2×2 + PERIPH 0.85×h. F8: `L1q maxL=a/b/c/d` |
| `V BUDGET` при `639/640`, соседи `L+` | `gate=BUDGET` | Диск съел слоты; merge не трогал in-disk | 🟡 merge капа + hold дальних SSE только при капе |
| `BOOT 180/180`, 31 чанк | `SPLIT-OK pri` | Пауза стопила bootstrap | ✅ |
| `gap=1` на паде при полном L9 2×2 | L2 `HORIZON` в ancestry | Ложный gap, крал split-бюджет | ✅ in-disk only |
| Max LOD **9**: дыры / плоский пол под ракетой; Max LOD **8** ок | `chunks=642/640`, `L1q=9/9/9/7`, `parentL7 [3] L8 gate=BUDGET`, рядом `L8+` / `L9 MAX`; L8: `355/640`, `L1q=8/8/8/8` | Async mesh: несколько `Split()` в тике все проходили `count+4≤640`. Touch + in-disk balance сверлили **четыре** L1 до L9 → кап, pad 2×2 неполный, T-junction | 🟡 rent в `Split()`; starve только если нет 4 слотов или V ещё хочет split при ≥75% |
| Луна ~1 km: квадраты / `L7 CRACK`, Земля «норм» | `chunks=483/640` `padMix=1` `starve=1` `L1q=7/6/8/8` `parentL6 [1] CRACK V-nL6` | Starve по `padMix` при свободных слотах стопил catch-up. Не битый LDEM: 64 ppd ≈ 470 м/пикс, серый splat | 🟡 starve без `padMix`; overlay/сетка скрыты до F8 / GFX |
| Луна ~4.4 km: под ракетой глаже, сбоку кратеры; `parentL5` все `L6+` | `chunks=487/640` `maxL=7` `parentL6` 4×L7 MAX | SSE + догон до того же LOD + sibling-balance каждого 2×2 = 16×L7 | 🟡 кольца от maxL; `gate=RING` |
| Луна pad: 3 тёмных L7 «островов», мерцание слева сверху на взлёте | `L1q 7/6/7/6` `parentL6 L7 V CRACK` к L6, `BOOT 95/180` `padMix=1` | 4 L1 сходятся в UV 0.5,0.5. Ближайшая точка = угол под ракетой → slant≈высота, SSE/touch режут **три чужих L1 в иглы L7**. На взлёте горизонт/span листа прыгает — иглы split/merge | 🟡 угол ≠ ребро: slant/RING от центра; pad-SSE только Contains; догон CRACK у V до того же LOD |
| Луна pad, четверть SE не грузится | `L1q 8/6/8/8` `gap=1 sib` `parentL0 [1] k=C` `parentL7 [1] L8 V k=E` | Pad на центре грани. Догон по 4 рёбрам бьёт SW/NE; **SE только угол**. Проба «5% угла L8» не стреляла: корабль часто на ребре/`k=E`, не в вершине. `CRACK …H` — лимб ел бюджет догона | 🟡 проба если `uv` у 0.5,0.5 (не угол листа); диагональ первой; лимб не split |
| Луна pad: чанки не грузятся, `chunks=17`, `viewerSpan=86850m` | `BOOT 0/180` `L2 V CRACK V-nL1` SW `RING cap=1` `L1q 1/2/2/2` | Высота капа = уровень V (2) → сосед L1 `cap=1`. Догон режет L1→L2, RING-merge в начале следующего тика схлопывает. `gap=0` (отставание на 1 — не gap&gt;1) | 🟡 RING не блокирует crack-соседа; merge колец выкл, пока `V` в CRACK |
| Луна pad L8, SE на L7 | `L1q 8/7/8/8` `gap=1 sib` `BOOT 102/180` `parentL7` 4×L8 | Диагональный L1: догон до −1 (L7). RING-merge схлопывал L8 (same-level ≠ coarser). `sib` = L6 на своём RING cap vs L8 | 🟡 диагональ до L_V; merge не трогает catch-up-соседа V; sib не считает RING-cap |
| **exe серый void**, pad+цилиндр, HUD `(пусто)` | F8 `chunks=61` `M` на L1–L7 `capL=7` | Дерево жило. Player: `shader_feature` `_SANDBOX_VERTEX_PAINT` снят, PNG не биндился → серые плиты; `Shader.Find` мог гасить quadtree. HUD `(пусто)` = нет очереди мешей | ✅ 2026-08-31 `multi_compile` + Resources `.mat` + fallback |
| **exe Земля magenta**, Луна с высотами ок | F8 `chunks=61` `capL=7` `needL=16` | Луна: LocalChunks+splat. Земля GEBCO: SandboxGlobe+vertex-paint → pink. Высоты GEBCO в меше были, шейдер нет | ✅ 2026-08-31 Земля = LocalChunks + geographic PNG |
| **exe сплошная вода**, pad, нет суши/рельефа | F8 `chunks=61` `capL=7` | Высоты меша = 0: sampler игнорировал SPEL `.bin`. vertexColor.a=1 → ocean. Луна mmap ок | ✅ 2026-08-31 fallback `.bin` + player mmap raw |
| **exe Земля белая/без текстур**, `gate=MAX` L7 | `capL=7` `needL=16` `viewerSpan≈10 km` | PlayerPrefs MaxLod=7 (exe ≠ Editor). GEBCO-only не грузил RSS splat (Луна грузила) | 🟡 splat при GEBCO; **не** авто-MaxLod 15 |
| **Пад телепорт вверх-вниз, не взлетает, FPS↓** | `capL=15` | Авто-MaxLod 15: очередь мешей + GEBCO под кораблём. Слайд −Y с interpolated COM + фильтр quat на паде | ✅ 2026-08-31 откат слайда/фильтра; V2 15→8 |

---

## Как читать F8

```
BOOT 0/180  chunks=24/640  gap=0  unfinished=1  viewerSpan=86850m
maxL=8 capL=8 needL=14     ← capL=слайдер, needL=для 64 m, maxL=высота
parentL1 [1] L2 V gate=CRACK U+nL1<L2   ← не split, пока сосед U+ не L2
parentL0 [0] L1 gate=SPLIT-OK sse       ← этот сосед должен делиться
```

| Поле | Смысл |
|------|--------|
| `V` | лист, в котором UV корабля (ровно один, half-open) |
| `n=` | геоцентрический угол **ближайшей** точки чанка до надира |
| `c=` | угол до **центра** чанка |
| `k=C` / `k=E` | ближайшая точка — вершина (угол грани) или ребро |
| `cap=` | кольцевой потолок LOD (на каждой строке 2×2) |
| `gap=1 U+nL6` | ребро листа `V`: сосед грубее больше чем на 1; `D nL#` = диагональный L1 на центре грани; `sib` = грубый сиблинг в ancestry |
| `horizon` | `acos(R/(R+h))+margin`. Дальше — за кривизной, split нельзя (кроме pad 2×2 parent≥2) |
| `viewerSpan` | span листа с `V`. На паде при `capL=8`: Луна ~1.3 km, не 64 m |
| `gap=1` | сосед **на диске** грубее чем на 1 LOD. Не путать с CRACK (сосед грубее на 1, split запрещён) |
| `padMix=1` | 2×2 под кораблём (parent≥L2): и лист, и `L+` (L8 рядом с L9) |
| `starve=1` | нет 4 слотов, или ≥75% капа и V хочет split |
| `clamp=1` | ≥75% капа или нет слотов — запасной hold дальних SSE (живой, не прошлый тик) |
| `BOOT 0/180` | кадры кончились, pad всё ещё грубее finest — bootstrap не снят |
| `flap s=/m=` | split/merge за последний LOD-тик. После прогрева оба 0. Не ноль = кольцо качается |
| `lastFlap` | до 8 ключей `split/merge F# L# u,v` этого тика. Вставить в чат вместе с F8 |

### `gate=`

| gate | Значение | Что делать коду / игроку |
|------|----------|-------------------------|
| `SPLIT-OK sse/pri/sib` | хочет split | должен уйти за кадры, иначе hold/budget/catch-up сломан |
| `CRACK U+nL#<L#` | сосед грубее — restricted QT | **сосед** должен split (catch-up). Если сосед тоже `SPLIT-OK` и чанки не растут — тупик |
| `HORIZON n>h` | ближайшая точка за лимбом | ок для дальних L2 на face-center pad |
| `BUDGET` | `chunks+4>640` и узел **хочет** split | merge дальних / не жрать слоты SSE |
| `HOLD-V` | прошлый тик: нет 4 слотов, или ≥75% капа и V хочет split | дальние SSE стоп; crack-сосед и pad-sibling всё равно split |
| `MAX` | `level>=maxL` | слайдер / высота |
| `RING cap=` | сосед дальше кольца +1 | ок: finest только 2×2 с `V` |
| `PERIPH` | ≥75% капа и далеко по углу | дальние не сверлить |
| `WAIT-MESH` / `NEED-MESH` | нет меша до split | bootstrap sync на диске |

`HOLD-V` в overlay — флаг **прошлого** LateUpdate (`LastTickHeldNonViewerSplits`). В момент F8 hold уже снят. `padMix=1` — в 2×2 под кораблём (parent≥L2) есть и лист, и `L+`. `starve=1` — нет слотов или V ещё split при ≥75% капа (не из‑за одного `padMix`).

---

## Тупики (не повторять)

1. **V=CRACK, сосед=SPLIT-OK, chunks не растут** — запрещено стопить соседа, который должен догнать V. Hold только при капе; catch-up при CRACK **до** Refresh.
2. **Hold всех не-V пока V хочет split** — на старте 24 чанка V всегда хочет split → дерево замирает на L2.
3. **Catch-up только при `gap>1`** — `L_n` vs `L_{n-1}` это CRACK, но `gap=0`. Нужен catch-up и по CRACK.
4. **Игнор горизонта на всей L0 грани** — хлопок split/merge. Pad 2×2 только parent≥2.
5. **Moon-only хак / flatten океана на Луне** — Cape и облака Земли не трогать.
6. **`needL` как баг Луны** — это 64 m; слайдер `capL` режет.
7. **Несколько Split в одном тике без аренды GO** — `CanAllocate` смотрит живые объекты; async mesh откладывал Rent → `642/640`. Rent в `Split()`.
8. **Touch + in-disk balance всех четырёх L1 до maxL при капе** — L9 под кораблём смешанный (`BUDGET` рядом с `MAX`). Starve только без слотов / пока V хочет split.
9. **Starve по `padMix` при свободных слотах** — `L7 CRACK` к L6 не догоняется, квадраты на 1 km. `padMix` чинится split, не hold всего диска.
10. **Весь горизонт в finest при 640** — hitch рисует полосу L8, pad L7. Merge не трогал L1 с `V`. Clamp: кольцо 2.5× slant, не вся четверть грани.
11. **Sibling-balance каждого in-disk 2×2 + догон до того же LOD** — F8 под кораблём ровный L7 2×2, картинка с finer сбоку: кузены L6 тоже стали L7. Кольца +1; догон только gap&gt;1; balance только parent листа `V`.
12. **Угол грани = slant пада** — три L1 только касаются UV 0.5,0.5 → иглы L7. Угол: центр чанка; ребро: ближайшая точка.
13. **Ширина кольца в плитках maxL, пока V ещё L3** — все соседи `RING cap=0`, догон и merge едят друг друга, `chunks=25`. Ширина = текущий `viewerSpan`.
14. **Догон только 4 рёбер + «угол finest-листа»** — на `uv=0.5,0.5` диагональный L1 (`L1q 8/6/8/8`) никогда не семплится. Проба от близости к центру грани, не от 5% угла L8.
15. **Кап кольца от слайдера + span чанка** — Max LOD 14: L8 за километр `cap=13`, SSE сверлит горы, догон same-level по швам (`L1q 14/13/14/14`), 8 FPS при пустой очереди. Шаг = viewerSpan; высота = уровень V.
16. **RING cap=1 vs V CRACK** — высота капа = уровень V замораживает соседа на L1; merge колец откатывает догон. Не схлопывать кольца, пока V в CRACK ниже maxL.
17. **RING clamp при gap&gt;1** — сосед L5 `cap=5` рядом с L8: clamp + merge 2×2 = вечный `U+nL5`. Догон gap&gt;1 не clamp; не merge, если ребёнок — сосед V.

---

## Чеклист после правки

- Луна pad: за ~2 с `viewerSpan` с 86 km вниз (при `capL=8` ~1.3 km), не застревать `chunks=17` с `L2 V CRACK` и SW `RING cap=1`.
- Луна pad Max LOD **8**: `L1q` ровный, `chunks` ≪ 640, пол под ракетой без L8/L9 шва.
- Луна pad Max LOD **9**: `chunks≤640` (не 642), `parentL7` без `BUDGET` рядом с `L8+`/`L9`, F8 `padMix=0` после прогрева. Слайдер 8↔9 без дыр под кораблём.
- Мыс: тот же quadtree, океан/берег как были; F8 `F2` `uv≈0.39,0.16`, не `0.500,0.500`.
- Луна ~1 km, Max LOD 8: F8 `starve=0` при `chunks≪640`, нет вечного `L7 CRACK` к L6, `padMix` сходит. Overlay с экрана до F8.
- Луна pad face-center: не три тёмных квадрата в стороне от ракеты; на взлёте без мерцания игл слева сверху. F8 `L1q` не `8/6/8/8` и не язык `14/13/14/14`. Слайдер 8 для FPS; 14 = плитки 21 m × 65².
- Пауза на старте: `BOOT` не залипает `180/180`.
