# Portable Path Expansion — Stage 01 Shared Portability Handoff

Status: Complete  
Stage: 01 of 07  
Date: 2026-09-18  
Owner Session: Storyboard.Foundation workspace

## Opening Prompt (Use To Start This Stage)

Execute only Stage 01 of `plans/active/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN.md`. In this workspace, create and test the dependency-neutral `Storyboard.Foundation` package, shipping only its `Paths` capability and documented `%NAME%` expression/error contract. Do not implement the deferred Content, Files, or Json fast followers; do not edit Designer, GameEngine, project data, or legacy-token behavior. Record package version, test results, boundary compliance, and the exact Stage 02 adoption steps here.

## Stage Boundary Allowlist Snapshot

Allowed read: `StoryBoard.Architecture/docs/**`, `StoryBoard.Designer/**`, `StoryBoard.GameEngine/**`, package/version documentation.  
Allowed edit: `Storyboard.Foundation/**`, `StoryBoard.Architecture/docs/**`, main plan, this handoff.

## Scope Completed

Created the Foundation package with only the `Storyboard.Foundation.Paths` namespace:

1. Strict `%NAME%` token parsing and process-environment expansion.
2. Structured resolution results containing raw expression, expanded expression, physical path, base directory, diagnostic code, and message.
3. Explicit policies for absolute paths, relative paths, file URIs, and required relative bases.
4. Deterministic asset-root ordering and containment matching, including deepest-root-first behavior.
5. Dependency-free executable test project covering the Stage 01 matrix.
6. Package README, environment-variable catalog, and technical path-expression reference.

No Content, Files, or Json fast follower capability was added.

## Files Changed

1. `Storyboard.Foundation.sln`
2. `src/Storyboard.Foundation/Storyboard.Foundation.csproj`
3. `src/Storyboard.Foundation/Paths/**`
4. `src/Storyboard.Foundation/README.md`
5. `tests/Storyboard.Foundation.Tests/**`
6. `docs/paths-reference.md`
7. `docs/environment-variable-catalog.md`
8. `NuGet.Config`
9. `.gitignore`
10. This handoff and the Stage 01 ownership correction in the main plan.

## Contract/Interface Impact

Package API only; no DTO/schema change. Public entry points are `PathExpressionResolver`, `PathExpressionOptions`, `PathExpressionResolutionResult`, `PathExpressionDiagnosticCode`, `AssetRoot`, `AssetRootMatch`, and `AssetRootMatcher` under `Storyboard.Foundation.Paths`.

## Validation Commands Executed

1. `dotnet restore src/Storyboard.Foundation/Storyboard.Foundation.csproj --configfile NuGet.Config`
2. `dotnet restore tests/Storyboard.Foundation.Tests/Storyboard.Foundation.Tests.csproj --configfile NuGet.Config`
3. `dotnet build tests/Storyboard.Foundation.Tests/Storyboard.Foundation.Tests.csproj --configuration Release --no-restore --property:UseSharedCompilation=false`
4. `dotnet run --project tests/Storyboard.Foundation.Tests/Storyboard.Foundation.Tests.csproj --configuration Release --no-build`
5. `dotnet pack src/Storyboard.Foundation/Storyboard.Foundation.csproj --configuration Release --no-restore --property:UseSharedCompilation=false --output artifacts`

## Test Results

All 7 tests passed:

1. Token expansion and relative resolution.
2. Unresolved-token rejection without relative fallback.
3. Malformed-token rejection.
4. Absolute and relative policy enforcement.
5. File URI policy.
6. Deepest containing root selection.
7. Deterministic equal-depth root ordering.

The Foundation workspace subsequently advanced the package to `0.1.1`; Stage 02 consumes `Storyboard.Foundation 0.1.1`.

## Behavioral Notes

1. Windows drive-letter paths are classified as paths, not URI schemes.
2. Undefined variables return `UnresolvedVariable` and no physical path.
3. Equal-depth asset-root matches are returned in deterministic variable-name order; callers must treat the tie as ambiguous.
4. `ASSETROOT:/` compatibility remains entirely outside this package and unchanged for Stage 02 adoption.
5. `NuGet.Config` clears external feeds because this package has no third-party dependencies and the empty workspace must restore offline.

## Known Issues/Risks

1. `ASSETROOT:/` remains supported by Designer after Stage 02 adopts the package; retirement remains deferred to Stage 06.
2. The handoff path in the plan still says `plans/active/...`, while this workspace currently stores the plan under `plans/...`; this is documentation-only and does not affect the package.
3. The SDK solution-level build returned failure without diagnostics in this environment; both project-level builds pass with shared compilation disabled, and the executable test run plus package generation pass.

## Boundary Compliance Report

Out-of-scope reads: None.  
Out-of-scope edits: None.  
Exceptions: None.  
Deferred capabilities: Content, Files, and Json were not implemented. Designer, GameEngine, project data, and legacy-token behavior were not edited.

## Explicit Next-Stage Start Checklist

1. Consume local/package version `Storyboard.Foundation 0.1.1`.
2. Preserve legacy `ASSETROOT:/` read compatibility.
3. Start Designer work with multi-root resolver tests before dialog changes.
4. Use `PathExpressionResolver.Resolve` for source resolution and `AssetRootMatcher.FindContainingRoots` for root selection.
5. Treat equal-depth root matches as an explicit author choice, never as enumeration-order selection.
