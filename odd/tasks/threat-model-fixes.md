# Threat model fixes (v1.1.0)

Objective: fix every confirmed issue from the recon threat model evaluation, one commit per issue, then PR to Master and publish v1.1.0 to NuGet.
Branch: `develop` (joins open PR #13 to `Master`). Delivery: `single-pr`, non-breaking minor release. Test runner: `dotnet test HtmlToPdfDotNet.Tests`.

## Specs

- **S1 Link schemes.** Only `http`, `https` and `mailto` link targets become PDF link annotations; any other scheme (`javascript:`, `file://`, `smb://`, ...) or a scheme-less href produces no annotation, and the link text still renders.
- **S2 Path leak.** A missing local image must not reveal the resolved server path in the exception message; report the original `src` only.
- **S3 BasePath opt-in** (user decision: "Opt-in, v1.1.0"). New `ConversionOptions.RequireBasePathForLocalFiles` (default `false`, current behavior unchanged). When `true` and `BasePath` is not set, a local image `src` throws `UnauthorizedAccessException`; `data:` URIs keep working.
- **S4 Document limits.** New optional limits on `ConversionOptions`: maximum HTML length and maximum element nesting depth. Documents above a limit are rejected with an exception before layout. Defaults: length unlimited; depth 256, which may reject very deep documents that convert today on large stacks (decision recorded in L5).
- **S5 Image bombs.** PNG width/height must be positive and bounded, and zlib decompression must stop at the size the header implies, so a small malicious PNG cannot exhaust memory or overflow `int` arithmetic.
- **S6 Example rate limiting.** The Example app limits `/report/pdf` per client and answers HTTP 429 above the limit.
- **S7 Docs and version.** README documents the new options and security behavior; library version 1.1.0.
- **S9 Flex nesting cost.** Nested flex containers must lay out in time roughly linear in nesting depth (no exponential blow-up), with identical output for existing flex documents.
- **S10 Filesystem-aware containment** (user-reported finding "Case-insensitive lexical containment allows image path traversal on case-sensitive filesystems"). Image reads and `WritePdfFile` writes share one containment check that resolves symbolic links and compares with host case rules; `../Assets/x.png` from base `assets` and links pointing outside the base are rejected.
- **S11 Pinned release actions** (user-reported finding "Mutable GitHub Actions can taint the package published by the release workflow"). Every action in all workflows is pinned to a full commit SHA with a version comment.
- **S12 Pinned MCP package** (user-reported finding: project MCP config launches `npx -y @aikidosec/mcp@latest`). Both `.mcp.json` and `opencode.jsonc` launch an exact, reviewed version.
- **S13 Scoped OIDC token** (user-reported finding "Job-wide OIDC permission exposes the NuGet publishing identity to pre-login build code"). Restore, build, test and pack run in a job without `id-token`; only a separate publish job that runs no repository code gets `id-token: write`, and it verifies the transferred packages before login.
- **S8 Delivery.** "create PR to Master and launch new publish in nuget": PR to `Master`, then tag `v1.1.0` to trigger `release.yml`. Merge and tag only after explicit user confirmation.

Out of scope (noted in evaluation, not security fixes): JPEG metadata stripping; a broken `<img>` aborting the whole conversion.

## Tasks

| ID | Specs | Route | Status | Commit |
|----|-------|-------|--------|--------|
| T1 | S1 | inline | done | 9442799 |
| T2 | S2 | inline | done | 594bcbd |
| T3 | S3 | inline | done | ced79e9 |
| T4 | S4 | inline | done | 0e374bf |
| T5 | S5 | inline | done | 55e9708 |
| T6 | S6 | inline | done | fe5ddc5 |
| T7 | S7 | inline | done | bdace4f (amended) |
| T9 | S9 | inline | done | eeb339b |
| T10 | S10 | inline | done | (this commit) |
| T11 | S11 | inline | done | (this commit) |
| T12 | S12 | inline | done | (this commit) |
| T13 | S13 | inline | done | (this commit) |
| T8 | S8 | inline | pending | |

Route evidence: units are small (< 100 lines each) and T3/T4 share `ConversionOptions.cs`, so no Writer trigger applies; all inline.

## Log

- L1 (2026-11-01, user): "Create a plan for fix each issue. Later each issue fixed is a commit. Later create PR to Master and launch new publish in nuget"
- L2 (2026-11-01, user decision on S3): "Opt-in, v1.1.0"
- L3: Verified before planning: href copied unchanged (`BlockLayoutEngine.cs:511-537`) and written as `/URI` (`PdfDocumentWriter.cs:133`); `ImageLoader.cs:203` falls back to the current directory; `ImageLoader.cs:43` puts the resolved path in the message; `PngDecoder.cs:32-58` trusts header dimensions and decompresses without bound. Deep-nesting stack overflow not yet measured (T4).
- L4: Engram mirror pending: the Engram MCP tools are not available in this session.
- L5 (T4): Measured stack overflow: 8 MB stack crashes between 5,000 and 10,000 nested divs; 1 MB stack (Windows default) at 1,000-1,500 divs or 750 nested tables; IIS threads use 256 KB. Default `MaxNestingDepth` set to 256, which rejects documents deeper than 256 that convert today on large stacks (accepted trade-off; `null` disables).
- L6 (T4, new finding, not fixed): nested flex containers cost grows exponentially (20 levels 55 ms, 40 levels 1.5 s, 80 levels > 60 s) because `FlexLayoutEngine` lays out each item up to three times (lines 254, 309, 485). The depth limit does not cover it. Needs a decision: fix (memoize measurement) or defer.
- L7 (T4, new finding, upstream): HtmlAgilityPack parse time is quadratic in nesting depth (5,000 levels 0.1 s, 20,000 levels 3 s). Mitigation documented: set `MaxHtmlLength` for untrusted input.
- L8 (T3, new finding, not fixed): header/footer templates are laid out without `BasePath`, so their local images resolve against the current directory even when `BasePath` is set. With `RequireBasePathForLocalFiles` and no `BasePath` they are rejected (tested).
- L9 (T3): test fixture `SecurityTests.CreateMinimalPng()` writes an undecodable PNG; the new test uses the valid `_png64` image instead.
- L10 (T6): verified by running the Example app: 10 x 200 then 429 on `/report/pdf`; `/report` unaffected. T1-T5 each observed RED before GREEN; full suite 601 passed.
- L11 (2026-11-01, user decision on L6): "Fix flex first" (fix nested flex cost before merging PR #13 and publishing v1.1.0).
- L12 (2026-11-01, user): "wait, I have other issues" followed by the case-insensitive containment finding. Confirmed on Linux with RED tests: case-variant sibling read and write, and directory/file symlinks escaping BasePath (5 failing). Fixed with `PathContainment`; 611 tests pass.
- L13 (2026-11-01, user): mutable-actions finding. Confirmed: checkout@v4 and setup-dotnet@v4 ran in the publish job. Pinned checkout v4.4.0 (11d5960), setup-dotnet v4.3.1 (67a3573), codeql-action v4.39.1 (9b32540); each equals the current v4 tag, so CI behavior is unchanged. YAML validated; workflows run first in CI on push. Job split (build vs publish) not done: optional hardening.
- L14 (2026-11-01, user): MCP `@latest` finding. Pinned `@aikidosec/mcp@1.0.33` in both configs; that version ships `npm-shrinkwrap.json`, so its dependencies install with locked integrity, and npm versions are immutable. Upgrades now require an explicit edit.
- L15 (2026-11-01): verifiers. S9 PASS (eeb339b): 0 mismatches in 400 parallel conversions, output equal to baseline after normalising dates and image names, 20 levels 55 ms vs 3,191 ms baseline. S10 B1 recheck PASS (70b2b96): all five link-then-`..` escapes rejected; 614 tests pass. Baseline advisories, not fixed: a flex container as the first child of `<body>` renders nothing; links inside flex items are dropped.
- L16 (2026-11-01, user): job-wide OIDC finding. Confirmed: `id-token: write` covered restore/build/test/pack. Split `release.yml` into `build` (contents: read) and `publish` (id-token: write only, no checkout, SHA-256 of the packages checked against the build job output). Workflow filename unchanged so the Trusted Publishing policy still matches. No `environment:` added, because a nuget.org policy without one may stop matching; recommended to the user as a follow-up. YAML validated and checksum/version shell steps dry-run locally; first real run is the v1.1.0 tag.
- L17 (2026-11-01, user): "There are comments in the PR" (Aikido review on #14; #13 had been squash-merged before the threat-model commits, so they moved to #14). Both confirmed and fixed: (1) PNG limit checked only the pixel product, so 67,108,864x1 passed although the README promised 8192 x 8192; added `PngDecoder.MaxDimension` per-dimension check, RED observed with valid image data (8193x1, 1x8193). (2) Example rate limiter partitioned on the proxy address behind a reverse proxy; added `UseForwardedHeaders` (X-Forwarded-For trusted from loopback only) before `UseRateLimiter`. Verified in the running app: client A 10 x 200 then 429, client B still 200. Suite 617 passed.
