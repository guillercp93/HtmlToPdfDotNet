# Flex review fixes

## Objective

Fix all 9 informational findings from the reliability review of `c23c19e`
(lineage `review-c510e91456112bd2`) before starting performance work.

## Problem / why

The flexbox commit introduced layout bugs (zero-width `flex: auto` items,
overlapping content after wrapped rows and multi-page boxes) and weak tests.
A benchmark baseline must run on correct layout.

## Scope

- `HtmlToPdfDotNet.Library/Commons/CssValueParser.cs`
- `HtmlToPdfDotNet.Library/Models/Layout/FlexLayoutEngine.cs`
- `HtmlToPdfDotNet.Library/Models/Layout/BlockLayoutEngine.cs`
- Matching tests under `HtmlToPdfDotNet.Tests/Css` and `HtmlToPdfDotNet.Tests/Layout`

## Constraints

- Test-first: RED before each fix, then GREEN, full suite green at task close.
- No behavior changes outside the findings.
- Delivery strategy: `ask-on-risk`. Forecast: ~500 authored changed lines across 3 commits.

## Tasks

- [x] T1 — CSS parser (route: delegated, writer trigger: parser + tests)
  - R3-flex-auto-collapse: `flex: auto` → 1 1 auto, `initial` → 0 1 auto,
    `none` → 0 0 auto, single length (`flex: 200px`) → 1 1 200px.
  - R3-negative-grow: negative `flex-grow` / `flex-shrink` fall back to defaults.
- [ ] T2 — Flex layout (route: delegated, writer trigger: engine + tests)
  - R3-wrap-trailing-gap: container end includes `(lines-1)*gap`; page-break
    checks account for line start Y.
  - R3-align-content-noop: free cross space uses the container's definite height.
  - R3-auto-basis-measure: auto-basis measurement includes box/rect primitives;
    regression test for non-grow items.
  - R3-weak-shrink-tests: assert item X positions / widths.
- [ ] T3 — Block layout (route: delegated, writer trigger: engine + tests)
  - R3-multipage-box-bottom: cursor advances past bottom padding/border after a split.
  - R3-split-only-two-pages: emit one box part per spanned page.
  - R3-line-height-untested: assert line heights / baseline with CSS `line-height`.

## Acceptance criteria

- Each finding has a test that fails before its fix and passes after.
- `dotnet test HtmlToPdfDotNet.Tests` fully green after each task.

## Checks

- `dotnet test HtmlToPdfDotNet.Tests`

## Progress

- Engram mirror `odd/flex-review-fixes/tasks`: pending (Engram tools unavailable this session).

## Next step

T2. T1 done in commit 2256531 (456/456 tests green; RED observed: 15 new cases failed before fix). Known limit: `flex: 50%` basis resolves to 0 because CssLength has no percent mode.
