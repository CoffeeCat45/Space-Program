# LOD — эталон старта на паде (🔒 не редактировать)

> **НЕ РЕДАКТИРОВАТЬ.** Рабочий вариант на 2026-08-28.  
> Пользователь: куча проблем ещё есть, но **на старте LOD прорисовывается нормально**. Смещение площадки / полёт / кольца — **отдельная задача позже**, не «улучшать» этот снимок по пути.  
> Канон: общий cube-sphere quadtree, без хаков «только Луна». Мыс (океан / Cape) не ломать.  
> Живой разбор поломок: [[LOD_FAILURES.md]]. Архитектура: [[LOD.md]].

Если F8 на лунном паде (Max LOD **8**) перестал совпадать с блоком ниже — это регрессия, откатывать split/merge/догон/RING, а не писать новый алгоритм.

---

## Сценарий эталона

| Поле | Значение |
|------|----------|
| Тело | Луна, пад на **центре грани** |
| Focus | `F4` `uv=0,500,0,500` `alt≈3,7` |
| Слайдер | `maxL=8` `capL=8` (`needL=14` — цель 64 m, **не баг**) |
| Горизонт | `≈1,88°` |
| Проверка | Unity Play, **F8** в Console |

Мыс: `uv≈0.39,0.16`, не `0.500,0.500`. Диагональный догон на мысе **не** стреляет.

---

## Эталонный F8 (2026-08-28, после прогрева)

```
LOD DIAG  F8=показать+лог  Shift+F8=скрыть  TICK  splitBud=4  meshBud=2  skipIdle=0  refine=0
chunks=117/640 pendingWork=0 lastTickPending=1 unfinished=0 gap=0 padMix=0 starve=0 clamp=0 incomplete=0 syncDisk=0 hardBP=0
focus F4 uv=0,500,0,500 alt=3,7 horizon=1,88° maxL=8 capL=8 needL=14 viewerSpan=1357m
F4 L1q maxL=8/8/8/8 minL=2/2/2/2 (SW/SE/NW/NE; even max=min = rings)
async=0 inFlight=0 noMesh=0
V=viewer M=mesh n=nearest c=center k=C corner/E edge cap=RING max
F4 parentL0 2x2:
 [0] L1+ M span=173700 n=0,00° c=28,60 k=E cap=7 cover=1 gate=branch
 [1] L1+ M span=173700 n=0,00° c=28,60 k=C cap=7 cover=1 gate=branch
 [2] L1+ V M span=173700 n=0,00° c=28,60 k=E cap=8 cover=1 gate=branch
 [3] L1+ M span=173700 n=0,00° c=28,60 k=C cap=7 cover=1 gate=branch
F4 parentL1 2x2:
 [0] L2 M span=86850 n=20,70° c=33,26 k=C cap=0 gate=HORIZON n=20,70>h=1,88
 [1] L2+ V M span=86850 n=0,00° c=14,32 k=E cap=8 cover=1 gate=branch
 [2] L2 M span=86850 n=28,60° c=42,54 k=C cap=0 gate=HORIZON n=28,60>h=1,88
 [3] L2 M span=86850 n=20,70° c=33,26 k=E cap=0 gate=HORIZON n=20,70>h=1,88
F4 parentL2 2x2:
 [0] L3 M span=43425 n=10,18° c=16,15 k=C cap=0 gate=HORIZON n=10,18>h=1,88
 [1] L3+ V M span=43425 n=0,00° c=7,16 k=E cap=8 cover=1 gate=branch
 [2] L3 M span=43425 n=14,32° c=21,48 k=C cap=0 gate=HORIZON n=14,32>h=1,88
 [3] L3 M span=43425 n=10,18° c=16,15 k=E cap=0 gate=HORIZON n=10,18>h=1,88
F4 parentL3 2x2:
 [0] L4 M span=21712 n=5,07° c=8,02 k=C cap=0 gate=HORIZON n=5,07>h=1,88
 [1] L4+ V M span=21712 n=0,00° c=3,58 k=E cap=8 cover=1 gate=branch
 [2] L4 M span=21712 n=7,16° c=10,74 k=C cap=0 gate=HORIZON n=7,16>h=1,88
 [3] L4 M span=21712 n=5,07° c=8,02 k=E cap=0 gate=HORIZON n=5,07>h=1,88
F4 parentL4 2x2:
 [0] L5 M span=10856 n=2,53° c=4,01 k=C cap=3 gate=HORIZON n=2,53>h=1,88
 [1] L5+ V M span=10856 n=0,00° c=1,79 k=E cap=8 cover=1 gate=branch
 [2] L5 M span=10856 n=3,58° c=5,37 k=C cap=1 gate=HORIZON n=3,58>h=1,88
 [3] L5 M span=10856 n=2,53° c=4,01 k=E cap=3 gate=HORIZON n=3,58>h=1,88
F4 parentL5 2x2:
 [0] L6 M span=5428 n=1,27° c=2,00 k=C cap=6 gate=RING cap=6
 [1] L6+ V M span=5428 n=0,00° c=0,90 k=E cap=8 cover=1 gate=branch
 [2] L6 M span=5428 n=1,79° c=2,69 k=C cap=5 gate=RING cap=5
 [3] L6 M span=5428 n=1,27° c=2,00 k=E cap=6 gate=RING cap=6
F4 parentL6 2x2:
 [0] L7 M span=2714 n=0,63° c=1,00 k=C cap=7 gate=RING cap=7
 [1] L7+ V M span=2714 n=0,00° c=0,45 k=E cap=8 cover=1 gate=branch
 [2] L7 M span=2714 n=0,90° c=1,34 k=C cap=6 gate=RING cap=6
 [3] L7 M span=2714 n=0,63° c=1,00 k=E cap=7 gate=RING cap=7
F4 parentL7 2x2:
 [0] L8 M span=1357 n=0,32° c=0,50 k=C cap=7 gate=MAX
 [1] L8 V M span=1357 n=0,00° c=0,22 k=E cap=8 gate=MAX
 [2] L8 M span=1357 n=0,45° c=0,67 k=C cap=7 gate=MAX
 [3] L8 M span=1357 n=0,32° c=0,50 k=E cap=7 gate=MAX
```

`lastTickPending=1` при `refine=0` / `gap=0` в этом снимке **не** считать поломкой старта.

---

## Что считать «нормально на старте»

Обязательно после прогрева (не `BOOT`, не `chunks=17`, не `viewerSpan=86850`):

- `viewerSpan≈1357m` при `capL=8` (Луна 1:10)
- `L1q maxL=8/8/8/8` (SW/SE/NW/NE) — все четыре L1 дотягивают finest у пада
- `minL=2` — лимб (`HORIZON n=20°>h=1.88`), не дыра под ракетой
- `parentL7` — четыре L8, один с `V`
- `gap=0` `unfinished=0` `padMix=0` `starve=0`
- `chunks` порядка **100–150 / 640**, не 17 и не 640
- `refine=0` после прогрева

Красные регрессии (уже чинили, не возвращать):

- `L1q 8/6/8/8` или `8/7/8/8` или `8/8/8/5` — четверть SE/NE не догнала
- `chunks=17` `RING cap=1` `L2 V CRACK`
- `L1q 14/13/14/14` при слайдере 14 — язык finest в одну сторону
- три иглы L7 / SSE от угла грани как slant пада

---

## Код этого варианта (не «упрощать» без новой задачи)

| Место | Зачем в эталоне |
|-------|-----------------|
| `PlanetPqsLodRules.cs` | кольца, диагональная проба у `uv≈0.5,0.5`, clamp догона, RING vs CRACK |
| `PlanetQuadtreeNode.cs` / `PlanetQuadtreeLod` | догон, `IsViewerCatchUpNeighbor`, merge колец, F8 |

Инварианты, без которых этот F8 ломается:

1. Шаг RING = **viewerSpan**, высота капа = min(слайдер, уровень листа `V`), не span дальнего чанка и не слайдер 14 на весь диск.
2. Пока `V` в CRACK ниже maxL — **не** RING-merge (догон L1→L2 иначе откатывается).
3. Сосед по ребру, из‑за которого `V` в CRACK — RING **не** стопит split.
4. Догон при gap&gt;1 **не** clamp в RING cap.
5. На центре грани: диагональный L1 догоняет **до LOD корабля**; merge **не** схлопывает лист, который бьёт проба догона `V` (даже на том же LOD).
6. `sib` / gap не считает лимб и листья, уже сидящие на своём RING cap.
7. Угол чанка ≠ ребро: slant для угла от **центра**, pad-SSE только `Contains`.
8. Камера / look **не** split/merge. Dual-frame 🔒.

---

## Сознательно не в этом freeze

Позже, отдельным чатом: смещение площадки, полёт, «идеальные» кольца +1 по всей сфере, FPS на Max LOD 14, видимая сетка чанков, `needL` vs слайдер.

Не чинить «заодно» ради этих пунктов, ломая эталон старта.
