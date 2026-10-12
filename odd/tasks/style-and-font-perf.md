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
- [ ] T2 — Font lookup without per-run lock/normalization (route: delegated, writer trigger: registry + helpers)

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

## Progress

- Engram mirror `odd/style-and-font-perf/tasks`: pending (Engram tools unavailable this session).

## Next step

T2.
