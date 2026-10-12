# Performance baseline

## Objective

Measure runtime speed and memory of HTML → PDF conversion before optimizing,
so every later optimization is judged against recorded numbers.

## Problem / why

The repo has no benchmarks. Candidate hot spots seen in code (unmeasured):
selector matching per node × rule × ancestor (`StyleResolver.ApplyStylesheetRules`,
`CssSelectorMatcher.MatchDescendant`), LINQ allocations in the style path,
string interpolation per PDF operator (`ContentStreamBuilder`), and triple
buffer copy on stream build (`ContentStreamBuilder.Build`).

## Scope

- New project `HtmlToPdfDotNet.Benchmarks` (BenchmarkDotNet, net10.0, Release).
- Representative HTML inputs: small document, large table report, CSS-heavy flex layout.
- Per-stage benchmarks: full `Convert`, `RunLayout` (style + layout), and PDF writing.
- Recorded baseline results in this document.

## Constraints

- No library behavior changes in this feature.
- Benchmark artifacts (`BenchmarkDotNet.Artifacts/`) are not committed.
- Delivery strategy: `ask-on-risk`. Forecast: ~300 authored changed lines.

## Tasks

- [x] T1 — Benchmark project + inputs + benchmarks (route: delegated, writer trigger: new project, 3+ files)
  - Evidence: `dotnet build HtmlToPdfDotNet.slnx -c Release` → 0 errors, 0 warnings; `dotnet test HtmlToPdfDotNet.Tests` → 474 passed; `--job dry` run executed all 9 benchmarks.
- [x] T2 — Run baseline in Release, record results here (route: delegated, per-action run)
  - Evidence: default job run completed, 9 benchmarks, global total time 00:02:22; results below.

## Acceptance criteria

- `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*'` runs all benchmarks.
- Results include mean time and allocated memory per input and stage.
- Existing test suite still green.

## Checks

- `dotnet build HtmlToPdfDotNet.slnx -c Release`
- `dotnet test HtmlToPdfDotNet.Tests`

## Baseline results

Command: `dotnet run -c Release --project HtmlToPdfDotNet.Benchmarks -- --filter '*' --exporters markdown`
Job: BenchmarkDotNet `DefaultJob` (full default job; run took 00:02:22).
Inputs: `small.html` (1 page), `table-report.html` (400-row table, multi-page), `flex-layout.html` (12 sections × 6 nested flex cards).
`WritePdf` writes a layout precomputed once in `GlobalSetup` (without the page-margin shift done by `Convert`).

```

BenchmarkDotNet v0.15.8, Linux CachyOS
11th Gen Intel Core i7-11800H 2.30GHz (Max: 0.80GHz), 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v4


```
| Method    | Input        | Mean         | Error      | StdDev     | Gen0      | Gen1      | Gen2     | Allocated   |
|---------- |------------- |-------------:|-----------:|-----------:|----------:|----------:|---------:|------------:|
| **Convert**   | **flex-layout**  | **15,596.24 μs** | **298.546 μs** | **343.806 μs** | **1625.0000** |  **812.5000** | **406.2500** | **19245.79 KB** |
| RunLayout | flex-layout  | 14,829.19 μs | 260.894 μs | 244.040 μs | 1562.5000 |  875.0000 | 468.7500 | 18989.98 KB |
| WritePdf  | flex-layout  |    308.02 μs |   1.583 μs |   1.481 μs |   14.1602 |    1.9531 |        - |   179.54 KB |
| **Convert**   | **small**        |    **104.52 μs** |   **0.847 μs** |   **0.792 μs** |   **11.7188** |         **-** |        **-** |   **155.13 KB** |
| RunLayout | small        |     51.47 μs |   0.199 μs |   0.176 μs |    6.8359 |    0.4883 |        - |     84.5 KB |
| WritePdf  | small        |     50.04 μs |   0.194 μs |   0.172 μs |    4.3335 |    0.1831 |        - |    53.63 KB |
| **Convert**   | **table-report** | **39,155.40 μs** | **428.308 μs** | **379.683 μs** | **3333.3333** | **1500.0000** | **666.6667** | **34462.01 KB** |
| RunLayout | table-report | 31,447.22 μs | 588.095 μs | 491.086 μs | 2800.0000 | 1200.0000 | 400.0000 | 31261.15 KB |
| WritePdf  | table-report |  4,262.18 μs |  78.179 μs |  73.129 μs |  218.7500 |   39.0625 |        - |  2682.88 KB |

Observation: `RunLayout` accounts for most of `Convert` time and allocation on `table-report` (31.4 of 39.2 ms) and
`flex-layout` (14.8 of 15.6 ms); on `small`, `RunLayout` (51.5 μs) and `WritePdf` (50.0 μs) take about equal time.

## Progress

- Engram mirror `odd/performance-baseline/tasks`: pending (Engram tools unavailable this session).

## Next step

Review the baseline and choose the first optimization target.
