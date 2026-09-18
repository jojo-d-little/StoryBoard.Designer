# Portable Path Expansion — Stage 02 Designer Handoff

Status: Complete  
Stage: 02 of 07  
Date: 2026-09-18  
Owner Session: StoryBoard.Designer workspace

## Opening Prompt (Use To Start This Stage)

Execute only Stage 02 of `plans/active/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN.md`, starting from the completed Stage 01 handoff. Replace Designer's single-root persistence behavior with canonical `%STORYBOARD_ASSET_SOURCE_ROOT...%/` expressions, use deepest-root selection and explicit tie handling, retain `ASSETROOT:/` reads, and cover image, sound, preview, validation, and export. Do not change GameEngine/Host or migrate project data.

## Stage Boundary Allowlist Snapshot

Allowed read: Stage 01 handoff/package, Architecture docs, Designer tests, GameEngine runtime-output contracts.  
Allowed edit: `StoryBoard.Designer/**`, `StoryBoard.Architecture/docs/**`, this handoff.

## Scope Completed

Stage 02 is complete within the Designer boundary. Designer now consumes `Storyboard.Foundation 0.1.1` and uses its `Storyboard.Foundation.Paths` resolver for configured and authored path expressions.

1. Canonical writes use `%STORYBOARD_ASSET_SOURCE_ROOT...%/relative/path` with forward-slash serialization.
2. The primary root and valid `STORYBOARD_ASSET_SOURCE_ROOT_<NAME>` variables are discovered from the process environment.
3. Nested roots use the deepest containing root.
4. Equal-depth roots are reported as ambiguous and are not selected by environment enumeration order; callers may provide an explicit root choice.
5. Legacy `ASSETROOT:/` expressions remain readable as aliases for `%STORYBOARD_ASSET_SOURCE_ROOT%/` and are canonicalized when a value is normalized for persistence.
6. Unresolved and malformed `%NAME%` expressions fail through the Foundation result contract and never fall through as relative paths.
7. Image preview/validation, image export, sound export, game presentation, room image, object image, and map image persistence continue through the shared Designer resolver seam.
8. Runtime exports remain under runtime-relative `assets/...` paths, while manifest `sourcePaths` preserve the authored expression.

## Files Changed

1. `StoryboardDesigner.App/StoryboardDesigner.App.csproj`
2. `StoryboardDesigner.App/Services/AssetSourcePathResolver.cs`
3. `StoryboardDesigner.App.Tests/AssetSourcePathResolverTests.cs`
4. `StoryboardDesigner.App.Tests/JsonExportServiceRuntimeExportTests.cs`
5. This handoff.

## Contract/Interface Impact

Authored string values gain canonical `%...%` persistence; runtime output remains runtime-relative.

No DTO/schema changes were made. An internal normalization result exposes portability, ambiguity, and candidate root names to future dialog-level root-choice UX.

## Validation Commands Executed

1. `dotnet restore StoryboardDesigner.App.Tests/StoryboardDesigner.App.Tests.csproj --configfile NuGet.Config`
2. `dotnet build StoryboardDesigner.App.Tests/StoryboardDesigner.App.Tests.csproj --configuration Release --no-restore --property:UseSharedCompilation=false`
3. `dotnet test StoryboardDesigner.App.Tests/StoryboardDesigner.App.Tests.csproj --configuration Release --no-build --no-restore --logger "console;verbosity=minimal"`

## Test Results

Build succeeded with zero warnings and zero errors. The full Designer test suite passed: 874 passed, 0 failed, 0 skipped.

Focused coverage includes primary, named, nested, tied, unresolved, legacy, image, sound, preview, and runtime export cases, including export from primary and named roots.

## Behavioral Notes

New writes must not emit `ASSETROOT:/`.

Existing legacy values remain readable. An out-of-root absolute path remains unchanged and therefore intentionally non-portable. A tied root match remains unchanged unless the caller supplies an explicit root variable name.

## Known Issues/Risks

1. Out-of-root assets remain valid but non-portable until deliberately relocated or configured.
2. Current dialog call sites use the safe default that refuses ambiguous normalization; a future UX pass can surface `CandidateRootVariableNames` as an explicit root-choice dialog.
3. Architecture-wide environment setup and asset-authoring documentation remain owned by the Architecture workspace and were not edited from this Designer workspace.

## Boundary Compliance Report

Out-of-scope reads: None.  
Out-of-scope edits: None.  
Exceptions: None.

Package consumed: `Storyboard.Foundation 0.1.1`.

## Explicit Next-Stage Start Checklist

1. Stage 03 must consume the same `Storyboard.Foundation 0.1.1` package contract.
2. Runtime invariants are confirmed: manifests retain authored source expressions and staged runtime assets stay relative.
3. Stage 03 should preserve the Designer source-root/runtime-export separation.
