# Table cell content loss

## Objective

Table cells must render everything their inner layout produces: rounded/stroked boxes and links.

## Problem / why

Found while attempting allocation fix F4 (`odd/tasks/allocation-fixes.md`): `TableLayoutEngine`
lays out each cell with its own `BlockLayoutEngine`, then copies the cell primitives into the
table output (~195-199, and a second copy loop ~347). The copy rebuilds each primitive and drops:

- `RectPrimitive.BorderRadius`, `Stroke`, `StrokeWidth` (rounded/bordered boxes inside cells)
- link annotations (`<a href>` inside a cell is not clickable)
- page-break primitives (semantics inside a table row to be decided by the table's own paging)

## Scope

- `HtmlToPdfDotNet.Library/Models/Layout/TableLayoutEngine.cs`
- `HtmlToPdfDotNet.Library/Models/Layout/RenderPrimitives.cs` (internal offset helper only, no public API change)
- `HtmlToPdfDotNet.Library/Models/Layout/LayoutResult.cs` if annotations need a merge helper
- Tests under `HtmlToPdfDotNet.Tests/Layout` and `HtmlToPdfDotNet.Tests/Integration`

## Constraints

- Intentional output change only for cells containing the dropped content; all other snapshots unchanged.
- No public API break (`PublicApiValidationTests`).
- Prefer shifting cell primitives in place (also removes the per-primitive copy, ~0.32 MB on table-report).
  Refuted by measurement, see Results: copies are kept.

## Tasks

- [x] T1 — Preserve all cell primitives and link annotations (route: delegated, writer trigger: engine + primitives + tests)
  - RED observed (6 failing): rounded box in cell lost `BorderRadius` (expected 4.5, actual 0); link in cell
    and link in cell on a later page produced no annotation; repeated thead lost the rounded box radius/stroke,
    the underline (`BorderLinePrimitive` only on page 0 of 4) and the link annotation (none on any page).
  - GREEN: `dotnet test HtmlToPdfDotNet.Tests` → Passed 561/561; no snapshot changed.
  - Commit: pending (Next step).

## Acceptance criteria

- RED then GREEN tests: link in a `<td>` yields a link annotation at the cell's position;
  a rounded/bordered box inside a cell keeps `BorderRadius`/`Stroke`/`StrokeWidth`.
- Page-break handling inside cells decided and documented with a test.
- Full suite green; snapshot changes, if any, explained.

## Checks

- `dotnet test HtmlToPdfDotNet.Tests`
- `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*RunLayout*' --job short`

## Decisions

- **Copy, do not shift in place.** `RenderPrimitive` gains an internal `CopyRelocated(dx, dy, page)`
  (memberwise clone + `private protected virtual Translate`, coordinates field-backed so public
  `get; init;` API is unchanged). Memberwise copy preserves every property, including future ones.
  In-place relocation saved ~290 KB on table-report but was ~3 ms (~15%) slower: the retained
  primitives were allocated early during each cell layout, interleaved with garbage, and got
  promoted (Gen1 656 → 968, Gen2 312 → 375 per 1000 ops). Copies restore the baseline GC profile.
- **Repeated thead rows** were also dropping images, border lines (underlines) and annotations from the
  cached layout; the cache now holds full copies plus annotations and emits fresh copies per page.
- **Page breaks inside a cell are dropped.** The table owns pagination and never splits a row: every
  cell primitive is placed on the row's page. `PageBreakPrimitive` is only a marker consumed by
  `BlockLayoutEngine.SplitBoxIntoPages`; a cell-level marker would make an enclosing bordered box split
  at a page boundary that does not exist. Pinned by `TableLayout_PageBreakInsideCell_IsNotEmittedByTheTable`.
  Known pre-existing limitation (out of scope): cell content taller than a page, or forced to a new inner
  page, overlaps because inner Y restarts at 0 on the inner page.

## Results

`dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*RunLayout*' --job short`, same session:

| Input | Before Mean | Before Alloc | In-place (rejected) Mean | In-place Alloc | After Mean | After Alloc |
|---|---|---|---|---|---|---|
| table-report | 17,536 / 17,891 us | 12,823.37 KB | 21,933 / 20,927 us | 12,533 KB | 17,995 us | 12,823.41 KB |
| flex-layout | 6,702 / 7,064 us | 3,910.52 KB | — | — | 7,241 us | 3,910.52 KB |
| small | 55.7 / 46.2 us | 75.15 KB | — | — | 49.0 us | 75.15 KB |

Allocation unchanged (+0.04 KB for table-report); mean within ShortRun noise.

## Progress

- Engram mirror `odd/table-cell-content-loss/tasks`: pending (Engram tools unavailable this session).

## Next step

Commit.
