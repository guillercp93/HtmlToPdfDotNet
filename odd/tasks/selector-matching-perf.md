# Selector matching performance

## Objective

Cut style-resolution time and allocations in `RunLayout` without changing output.

## Problem / why

Profiling `RunLayout` on `table-report` (see `odd/tasks/performance-baseline.md`):
style resolution is ~67% of samples, ~58% of samples sit in GC polls.

- `StyleResolver.cs:190` + `CssSelectorMatcher.cs:111-130`: per element, every rule is
  tested, a new `(rule, score)` list is built, sorted via LINQ `OrderBy`, copied via `ToList` (~48%).
- `CssSelectorMatcher.cs:53-59, 88-95`: class checks `Split` the class attribute and run
  LINQ `Any` per rule per ancestor (~7%).

## Scope

- `HtmlToPdfDotNet.Library/Models/Styles/CssSelectorMatcher.cs`
- `HtmlToPdfDotNet.Library/Models/Styles/StyleResolver.cs`
- `HtmlToPdfDotNet.Library/Models/Styles/CssRule.cs` (only if the index needs selector metadata)
- Tests under `HtmlToPdfDotNet.Tests/Css`

Out of scope: `Dictionary<HtmlNode, ComputedStyle>` hashing, `FontRegistry` locking (later candidates).

## Constraints

- Output must stay byte-identical: snapshot/E2E tests are the safety net.
- Cascade order preserved: specificity ascending, then source order (stable).
- Delivery strategy: `ask-on-risk`. Forecast: ~350 authored changed lines across 2 commits.

## Tasks

- [x] T1 — Allocation-free matching (route: delegated, writer trigger: matcher + resolver + tests)
  - Characterization tests first: cascade order (specificity, then source order), multiple
    classes, extra whitespace in `class`, descendant selectors, case handling.
  - Replace `Split` + LINQ `Any` with span-based class token scan.
  - Replace per-element `List` + `OrderBy` + `ToList` with a reused buffer and a stable sort.
- [~] T2 — Rule index by id / class / tag — DROPPED (no measured gain) (route: delegated, writer trigger: resolver + index + tests)
  - Bucket rules by the key of the selector's last part; per element test only candidate buckets
    plus universal rules; merge preserving cascade order.

## Acceptance criteria

- All existing tests green, including snapshots.
- `RunLayout` on `table-report`: lower Mean and Allocated than baseline (31.4 ms / 31,261 KB);
  numbers recorded per task.

## Checks

- `dotnet test HtmlToPdfDotNet.Tests`
- `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*' --job short`
  (compare against a same-session baseline run, CPU throttling makes absolute numbers drift)

## Results — T1

Same-session `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*RunLayout*' --job short`
(baseline run before any library edit, then after T1).

| Input        | Mean before  | Mean after   | Allocated before | Allocated after |
|------------- |-------------:|-------------:|-----------------:|----------------:|
| table-report | 40,603.89 us | 24,845.10 us |      31,261.23 KB |     15,196.40 KB |
| flex-layout  | 14,753.01 us |  8,148.64 us |      18,989.98 KB |      4,553.07 KB |
| small        |     52.60 us |     50.07 us |          84.50 KB |         76.92 KB |

`table-report` Mean is noisy (StdDev 5.1 ms before / 3.1 ms after, short job); Allocated is deterministic.

Evidence:

- Characterization tests added to `HtmlToPdfDotNet.Tests/Css/CssStyleSheetParserTests.cs`
  (`CssSelectorMatcherTests`, `StyleResolverCascadeTests`); all passed on the original code before edits.
- `dotnet test HtmlToPdfDotNet.Tests`: 506 passed, 0 failed (before and after the change).
- Class match: span tokenization on the space character (same as the previous `Split(' ')`),
  `OrdinalIgnoreCase`; no `Split`, LINQ or lambdas in the matching path.
- Cascade: `CssSelectorMatcher.CollectMatchingRules` fills a reused buffer and sorts by
  (specificity, source order). `GetMatchingRules` stays public and delegates to it.
- Thread safety: the buffer is created once per `StyleResolver.Resolve()` call and passed down the
  recursion (not an instance field), since `StyleResolver` is a public type that callers could share.
- Not committed by the writer.

## Results — T2 (dropped)

Same-session `--filter '*RunLayout*' --job short`, T1 code vs T1 + rule index:

| Input        | Mean T1      | Mean T2      | Allocated T1 | Allocated T2 |
|------------- |-------------:|-------------:|-------------:|-------------:|
| table-report | 24,219.18 us | 23,466.83 us | 15,196.30 KB | 15,202.15 KB |
| flex-layout  |  7,843.58 us |  8,233.58 us |  4,553.02 KB |  4,558.86 KB |
| small        |     47.64 us |     46.93 us |     76.92 KB |     78.77 KB |

Differences are within noise. With ~24 rules, brute-force matching is cheap after T1.
Decision (user): keep only T1. The index (513 tests green) is kept in
`git stash` as "T2 rule index by id/class/tag (no measured gain on benchmarks)";
revisit only if a large-stylesheet benchmark shows selector matching as a hot spot.

## Progress

- Engram mirror `odd/selector-matching-perf/tasks`: pending (Engram tools unavailable this session).

## Next step

Run the full benchmark suite and choose the next target.
