# Style map and font lookup performance

## Objective

Remove the two remaining hot spots in `RunLayout` that live in our code, without changing output.

## Problem / why

Re-profile after `5af8471` + `f626ba3` (EventPipe, ShortRun, throttled CPU):

| Hot spot | table-report | flex-layout |
|---|---|---|
| `StyleResolver.cs:124/143`: `Dictionary<HtmlNode, ComputedStyle>` insert, identity hash slow path (`RuntimeHelpers.GetHashCodeWorker`) | ~24–29% | ~53% |
| `FontRegistry.cs:163-176` via `Helpers.cs:113` (`MakeRun`): `lock(_sync)` per run lands in `Monitor.Enter_Slowpath`; plus `ToLowerInvariant().Trim()` and alias enumerator per run | ~20% | ~1% |

Sampling bias inflates these helper frames; judge each change by same-session benchmark before/after.

## Scope

- T1: `StyleResolver` and every consumer of its node → style map (layout engines, `PdfGenerator`, tests).
- T2: `FontRegistry`, `Helpers.MakeRun`, `StandardFontMetrics` font-name normalization.

Out of scope: HtmlAgilityPack parse cost (`AppendChild` list growth), `CleanHtml` LINQ.

## Constraints

- Output byte-identical: full test suite incl. snapshots stays green.
- `FontRegistry` thread-safety contract preserved (check how it is shared: static/singleton/DI).
- Delivery strategy: `ask-on-risk`. Forecast: ~400 authored changed lines across 2 commits.

## Tasks

- [x] T1 — Style map without identity hashing (route: delegated, writer trigger: resolver + consumers) — **refuted**, no library change; evidence in `## Results — T1`
- [x] T2 — Font lookup without per-run lock/normalization (route: delegated, writer trigger: registry + helpers) — **refuted**, no library change; evidence in `## Results — T2`

## Acceptance criteria

- Tests green after each task.
- Same-session `RunLayout` benchmark shows lower Mean and/or Allocated on table-report and flex-layout.

## Checks

- `dotnet test HtmlToPdfDotNet.Tests`
- `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*RunLayout*' --job short` before and after, same session

## Results — T1

Verdict: **refuted**. Identity hashing of fresh `HtmlNode`s is cheap; the profile frames were sampling bias. No library edit.

Microbenchmark `HtmlToPdfDotNet.Benchmarks/StyleMapBenchmarks.cs` (`--filter '*StyleMap*' --job short`, parse inside each benchmark, `ParseOnly` baseline; table-report = 9,267 nodes, flex-layout = 3,382):

| Method | flex-layout Mean | Gen2/1k | table-report Mean | Gen2/1k |
|---|---:|---:|---:|---:|
| ParseOnly (baseline) | 1.624 ms | 0 | 4.221 ms | 23 |
| ParseAndHashOnly (`RuntimeHelpers.GetHashCode` per node) | 1.814 ms | 0 | 4.573 ms | 23 |
| ParseAndDictionary (what `Resolve` does) | 3.484 ms | 82 | 6.542 ms | 242 |
| ParseAndPresizedDictionary | 3.713 ms | 78 | 7.335 ms | 187 |
| ParseAndOrderedStore (hash-free `List<KeyValuePair<HtmlNode, object>>`) | 1.495 ms | 0 | 7.504 ms | 187 |

- Identity hashing costs 0.19 ms (flex) / 0.35 ms (table): well under the 1 ms threshold.
- The ~2 ms dictionary delta is GC (Gen2 count jumps), not hashing. Pre-sizing does not help, and a hash-free ordered store is just as slow on table-report (its backing array also crosses the LOH threshold), so replacing the map would not remove it.
- Stopwatch probe without parse (median of 150 runs): dictionary insert over plain walk ≈ 0.36 ms (table) / 0.06 ms (flex); `ReferenceEqualityComparer` and `ConditionalWeakTable` are no better.
- `HtmlNode` does not override `GetHashCode`/`Equals` (checked by reflection on HtmlAgilityPack 1.12.4).

`RunLayout` reference (`--filter '*RunLayout*' --job short`, same session, throttled CPU, no library change so no "after"):

| Input | Mean | Allocated |
|---|---:|---:|
| small | 47.09 µs | 78.77 KB |
| table-report | 23,649 µs | 15,202 KB |
| flex-layout | 7,919 µs (±34 ms error, noisy) | 4,559 KB |

## Results — T2

Verdict: **refuted**. The whole per-run font resolution costs well under the 1 ms threshold; the `Monitor.Enter_Slowpath` frames were sampling bias. No library edit.

Microbenchmark `HtmlToPdfDotNet.Benchmarks/FontLookupBenchmarks.cs` (`--filter '*FontLookup*' --job short`). N = 3,432 lookups per invocation = text primitives emitted by table-report's `RunLayout`; cross-check from the input: 2,400 `<td>` + 400 `<span>` + 400 `<strong>` + 7 `<th>` + 1 `<h1>` ≈ 3,200 runs, so N is a fair upper estimate. Families are as they reach `MakeRun` after `StyleResolver.NormalizeFontFamily` (first family, unquoted, generics mapped): `Helvetica`, `Times-Roman`, `Courier`, `Georgia`, mixed bold/regular.

| Method (3,432 calls) | Mean | Allocated |
|---|---:|---:|
| `Helpers.MakeRun` with default (empty) `ConversionOptions.Fonts` — the real path | 452.95 µs | 1,115.39 KB |
| `FontRegistry.TryResolve` alone, empty registry (lock per alias + `ToLowerInvariant().Trim()` + alias iterator) | 265.62 µs | 707.84 KB |
| `StandardFontMetrics.Resolve` alone (fallback) | 72.24 µs | 139.43 KB |
| `FontRegistry.TryResolve` hit on registered DejaVu Sans | 280.76 µs | 482.63 KB |

- Full `MakeRun` cost for table-report ≈ 0.45 ms ≈ 1.9% of the ~23.6 ms `RunLayout`; the registry lookup is ≈ 0.27 ms (≈ 1.1%). Even a lock-free cache would save at most ~0.25 ms.
- Allocation is the only notable signal: ~206 B per `TryResolve` (lowercased string + iterator), ≈ 0.7 MB of table-report's ~15.2 MB (≈ 4.6%). Not worth a cache plus invalidation logic on its own; revisit only if GC pressure becomes the target.
- `_sync` is a private `object` used only in `lock` statements (never hashed or exposed), so there is no evident reason for a real slow path; the profiler frames are attributed to the inlined helper, consistent with T1's sampling bias.
- `RunLayout` reference: see `## Results — T1` (no library change, so no "after").

## Progress

- Engram mirror `odd/style-and-font-perf/tasks`: pending (Engram tools unavailable this session).

## Next step

Both targets refuted by microbenchmark; re-profile with a non-sampling method (or pick the next target from allocation data) before further library changes.
