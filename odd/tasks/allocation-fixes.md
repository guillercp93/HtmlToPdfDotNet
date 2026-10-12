# Allocation fixes

## Objective

Cut per-conversion allocations in `RunLayout` with six cheap, output-preserving fixes.

## Problem / why

Allocation profile (EventPipe allocation ticks with stacks, stage sums within 186 B of total):
`table-report` allocates 15.57 MB per `RunLayout`, `flex-layout` 4.67 MB. CPU sampling misled us
twice; allocation data is measured. GC pressure also limits throughput under concurrent conversions.

## Scope

| ID | Site | Measured (table-report) |
|---|---|---|
| F1 | `CssValueParser.ParseEdges` (~70-71): `Split` + `Array.ConvertAll` + capturing lambda | ~0.86 MB |
| F2 | `StyleResolver.ApplyStylesheetRules` (~207): boxed enumerator over `IReadOnlyDictionary` `CssRule.Declarations` | ~0.67 MB |
| F3 | `Helpers.CleanHtml` (~201): `Where().ToArray()` + `new string` | ~0.3–0.6 MB |
| F4 | `TableLayoutEngine` (~199): copies every `TextPrimitive` from a discarded cell layout | ~0.32 MB |
| F5 | `Helpers.ShiftAll` (~216): LINQ rebuild of the primitive list | ~0.28 MB |
| F6 | `FontRegistry.TryResolve` (~172-175): `FaceKey` record class per lookup | ~0.27 MB |

Out of scope: HtmlAgilityPack parse, shared `ComputedStyle`, per-cell engine creation.

## Constraints

- Output byte-identical: full suite incl. snapshots green.
- No public API break (`PublicApiValidationTests`); `CssRule.Declarations` keeps its public type.
- Each fix measured by BenchmarkDotNet `Allocated` (deterministic) before/after, same session.
- Delivery: 3 work-unit commits — CSS (F1, F2), text helpers (F3, F5), layout/fonts (F4, F6).
  Forecast ~350 authored changed lines. Strategy `ask-on-risk`.

## Tasks

- [x] T1 — F1 + F2 (route: delegated, one writer for all six; parent commits per group).
  Evidence: suite 529/529 green after each fix; RunLayout table-report 15202 → 13761 KB.
- [x] T2 — F3 + F5. Evidence: suite 546/546 green after each fix; table-report 13761 → 13176 KB.
- [x] T3 — F6 done, F4 skipped (see Results). Evidence: suite 554/554 green; table-report 13176 → 12823 KB.

## Acceptance criteria

- Tests green; `Allocated` for `RunLayout` drops on table-report and flex-layout; no Mean regression beyond noise.

## Checks

- `dotnet test HtmlToPdfDotNet.Tests`
- `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*RunLayout*' --job short`

## Progress

- Engram mirror `odd/allocation-fixes/tasks`: pending (Engram tools unavailable this session).

## Results

BenchmarkDotNet `--job short`, same session. Allocated is deterministic; Mean is noisy under `short`.

Baseline (`*RunLayout*` + `*Convert*`):

| Method | Input | Mean | Allocated |
|---|---|---:|---:|
| RunLayout | table-report | 22,810.57 us | 15202.15 KB |
| RunLayout | flex-layout | 7,923.68 us | 4558.80 KB |
| RunLayout | small | 49.94 us | 78.77 KB |
| Convert | table-report | 28,468.76 us | 18402.68 KB |
| Convert | flex-layout | 11,242.28 us | 4768.60 KB |
| Convert | small | 99.86 us | 149.39 KB |

Per group (`*RunLayout*`):

| Step | table-report Mean / Alloc | flex-layout Mean / Alloc | small Mean / Alloc |
|---|---|---|---|
| Baseline | 22,810.57 us / 15202.15 KB | 7,923.68 us / 4558.80 KB | 49.94 us / 78.77 KB |
| + F1 F2 | 22,167.97 us / 13761.04 KB | 7,447.89 us / 4070.32 KB | 50.19 us / 78.44 KB |
| + F3 F5 | 17,660.63 us / 13176.46 KB | 7,344.96 us / 3932.07 KB | 62.43 us / 76.46 KB |
| + F6 | 19,451.11 us / 12823.37 KB | 7,019.51 us / 3910.53 KB | 47.99 us / 75.15 KB |

Final (`*RunLayout*` + `*Convert*`):

| Method | Input | Mean | Allocated | Alloc delta |
|---|---|---:|---:|---:|
| RunLayout | table-report | 17,654.63 us | 12823.37 KB | -15.6% |
| RunLayout | flex-layout | 7,127.55 us | 3910.52 KB | -14.2% |
| RunLayout | small | 48.26 us | 75.15 KB | -4.6% |
| Convert | table-report | 26,944.69 us | 16024.10 KB | -12.9% |
| Convert | flex-layout | 9,269.63 us | 4120.71 KB | -13.6% |
| Convert | small | 101.48 us | 145.78 KB | -2.4% |

F4 skipped: `TextPrimitive` (and the other primitives) are public sealed classes with `init`-only
`X`/`Y`. Shifting in place needs a mutator, which means either a public API change or an internal
offset method with explicit backing fields in `RenderPrimitives.cs` (outside this task's edit surface).
A `with`-style copy would still allocate. Note: the current copy also drops `RectPrimitive`
`BorderRadius`/`Stroke*`, link annotations and page breaks from cell content, so an in-place shift
would change output unless those differences are preserved deliberately.

Follow-ups noticed (not done): `TryResolve` still allocates a `FamilyAliases` iterator and a
lowered/trimmed family string per call; `ParseLength` allocates its units array per call.

## Next step

Commit per group.
