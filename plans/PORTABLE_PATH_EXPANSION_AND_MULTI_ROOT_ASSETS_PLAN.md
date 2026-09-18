# Portable Path Expansion and Multi-Root Asset Sources Handoff Plan

Last updated: 2026-09-18  
Status: Stage 02 complete; Stage 03 pending
Purpose: replace inconsistent path portability behavior with one diagnosable `%ENV_VAR%` path-expression standard, enable multiple named asset roots, and create a locked cross-repository delivery cadence.

## Local Execution Status

Stage 01 shared portability is complete. Stage 02 Designer integration is complete in the Designer workspace using `Storyboard.Foundation 0.1.1`; its handoff records the implementation, test results, and boundary notes. Stage 03 GameEngine/GameHost integration has not started. Architecture-owned setup and authoring documentation remains pending in the Architecture workspace.

## Problem Statement

The development environment was made more portable by introducing environment variables, but support is inconsistent:

1. GameHost runtime-registration and static-client paths use `%NAME%` expansion at several independent call sites.
2. Designer-authored images, sound references, and preview images use the separate `ASSETROOT:/...` syntax, resolved only against `STORYBOARD_ASSET_SOURCE_ROOT`.
3. Designer persistence can only normalize against one asset root.
4. Several externally configurable JSON-path entry points do not expand embedded `%NAME%` expressions at all, while missing variables can silently fall through as relative paths where expansion is applied directly.

The outcome is one portable-path grammar across the system, deterministic multi-root asset authoring, clear diagnostics, and one shared implementation seam for every external/configured path boundary that opts into expansion.

## Non-Goals

1. Do not make every internal derived path (for example, a runtime-export child file found relative to its already-resolved root) an environment-variable expression.
2. Do not place machine-specific absolute source paths into runtime exports; Designer continues staging source assets into runtime-relative `assets/...` outputs.
3. Do not introduce a required per-project asset-root configuration file in the first rollout; roots are discovered from the process environment by convention.
4. Do not remove legacy `ASSETROOT:/` read compatibility before checked-in data and supported migration paths are complete.
5. Do not make WebPortal or Simulator resolve authoring-time source asset paths when they consume only runtime-export-relative assets.

## Current Baseline (Observed)

| Area | Current behavior | Gap |
| --- | --- | --- |
| System setup docs | Documents `STORYBOARD_DEVELOPMENT_ROOT`, `STORYBOARD_SAMPLE_PROJECTS_ROOT`, `STORYBOARD_WEBPORTAL_ROOT`, and `STORYBOARD_ASSET_SOURCE_ROOT`. | The setup doc has an obsolete location reference and no grammar/error/catalog policy. |
| GameHost static client | `StaticClientMountingExtensions.ResolveFolderPath` directly calls `Environment.ExpandEnvironmentVariables`. | No common unresolved-token diagnostics or shared test contract. |
| Runtime game registration | `GameManager`, `JsonRuntimeGameDiscoveryProvider`, and `RuntimeHostAssetManagementClient` separately expand `runtimeProjectPath`. | Logic is duplicated; catalog-path and provider-path routes are not uniformly expanded. |
| Discovery and identity provider JSON paths | `RuntimeGameDiscoveryProviderFactory` and `RuntimeIdentityProviderFactory` resolve absolute/relative paths but do not expand embedded expressions. | `%STORYBOARD_...%` cannot be used consistently in these settings. |
| Designer source assets | `AssetSourcePathResolver` recognizes `ASSETROOT:/` and one root; dialogs normalize selected assets back to that token. | A special persistence grammar and one-root limit. |
| Designer export | Image/sound stagers use `AssetSourcePathResolver`, then copy bytes into runtime `assets/...` and record the authored source string in manifests. | Must preserve source token identity while resolving the physical source safely. |
| Checked-in authored data | The inventory contains 213 legacy `ASSETROOT:/` source references and at least four non-tokenized absolute references: two game preview images in ChessDemo/WorkshopTutorial and one full/gray/normal map set in MapDemo1. | Stage 02 must cover every asset-valued authoring field; Stage 06 migration must include both legacy tokens and eligible absolute paths. |
| Runtime consumers | GameEngine/Host resolve runtime registrations and runtime-relative exported assets. WebPortal and Simulator receive/export runtime assets rather than source-root paths. | They need verification, not source-asset-root ownership. |

Important existing locations include:

1. `StoryBoard.Architecture/docs/environment-variables-setup.md`
2. `StoryBoard.Architecture/docs/asset-source-root-configuration.md`
3. `StoryBoard.Designer/StoryboardDesigner.App/Services/AssetSourcePathResolver.cs`
4. `StoryBoard.Designer/StoryboardDesigner.App/Services/DesignerImagePathResolver.cs`
5. `StoryBoard.Designer/StoryboardDesigner.App/Services/JsonExportService/CleanExportImageAssetStager.cs`
6. `StoryBoard.Designer/StoryboardDesigner.App/Services/JsonExportService/CleanExportSoundAssetStager.cs`
7. `StoryBoard.GameEngine/Storyboard.GameEngine/GameManager/GameManager.cs`
8. `StoryBoard.GameEngine/Storyboard.GameEngine/GameServices/Discovery/RuntimeGameDiscoveryProviderFactory.cs`
9. `StoryBoard.GameEngine/Storyboard.GameEngine/GameServices/Identity/RuntimeIdentityProviderFactory.cs`
10. `StoryBoard.GameEngine/Storyboard.GameHost/Infrastructure/StaticClientMountingExtensions.cs`

## Proposed Shape

### 1. Canonical portable-path expression

Persisted or configured portable paths use Windows-style environment-variable tokens:

```text
%STORYBOARD_SAMPLE_PROJECTS_ROOT%/WorkshopTutorial/GameRuntimeJson/WorkshopTutorial.sbr.runtime.json
%STORYBOARD_ASSET_SOURCE_ROOT%/PlaceHolderImages/DarkDoor.png
%STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN%/HotelRoom/VicHotel_DoorClosed.png
```

Forward slashes after the expanded root are the canonical serialized separator. The resolver accepts either separator when reading and normalizes only after resolution. Absolute paths, file URIs, and project-relative paths remain supported where they are supported today.

The shared resolver must:

1. Parse `%NAME%` tokens before rooted/relative-path handling.
2. Fail explicitly for malformed or unresolved tokens; it must never reinterpret `%MISSING%/x` as a relative path.
3. Return a structured result containing raw expression, expanded value, normalized physical path when applicable, base-directory use, and diagnostic code/message.
4. Preserve the caller's policy: whether absolute paths, file URIs, and relative paths are permitted and which base directory applies.
5. Be the only implementation allowed to call `Environment.ExpandEnvironmentVariables` for product path expressions.

The resolver should be the first shipped capability in a small dependency-neutral, packable `Storyboard.Foundation` library owned in the Contracts repository, under a `Storyboard.Foundation.Paths` namespace. It is a utility package, not a runtime DTO/schema change; this avoids putting product-path behavior into the Designer and prevents GameEngine from depending on the Designer.

### 2. Asset-root naming and persistence convention

The existing primary variable remains supported:

```text
STORYBOARD_ASSET_SOURCE_ROOT
```

Additional roots follow this exact family:

```text
STORYBOARD_ASSET_SOURCE_ROOT_<NAME>
```

`<NAME>` is uppercase letters, digits, and underscores, starts with a letter, and describes the collection rather than a drive or machine. Examples: `STORYBOARD_ASSET_SOURCE_ROOT_SHARED`, `STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN`, and `STORYBOARD_ASSET_SOURCE_ROOT_LICENSED_AUDIO`.

Designer enumerates the primary variable plus the suffixed family from the process environment. When an author selects an absolute file, it:

1. Normalizes every configured root to a full directory path.
2. Finds all roots containing the selected file.
3. Persists the path using the deepest matching root, for example `%STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN%/HotelRoom/door.png`.
4. Treats equal-depth matches as ambiguous and asks the author to choose the root; it never silently picks based on environment enumeration order.
5. Leaves a source outside every configured root unchanged (current backward-compatible behavior), while showing a portability warning and offering no fabricated token.

Designer will accept and resolve any valid `%NAME%` path expression that is present in authored data. It only auto-generates tokens from the asset-root family above.

### 3. Legacy migration policy

Stage 02 stops writing `ASSETROOT:/` immediately after the new resolver is available. It continues reading the token as an alias for `%STORYBOARD_ASSET_SOURCE_ROOT%/` and records an actionable warning when it is encountered. Existing absolute and relative references remain readable.

Stage 06 is a separately approved retirement decision. It migrates checked-in authored samples/starter projects and can remove legacy read support only after external-project migration guidance, diagnostics, and version/package adoption evidence are accepted. The first delivery must not break existing projects merely because they still contain `ASSETROOT:/`.

### 4. Supported environment-variable catalog

The first published catalog contains:

| Variable | Scope | Status |
| --- | --- | --- |
| `STORYBOARD_DEVELOPMENT_ROOT` | local source-workspace root; development registrations only | Existing, documented |
| `STORYBOARD_SAMPLE_PROJECTS_ROOT` | sample-project collection root | Existing, documented |
| `STORYBOARD_WEBPORTAL_ROOT` | static WebPortal distribution directory served by GameHost | Existing, documented |
| `STORYBOARD_ASSET_SOURCE_ROOT` | primary authoring-time asset source root | Existing, canonicalized |
| `STORYBOARD_ASSET_SOURCE_ROOT_<NAME>` | additional named authoring-time asset roots | New |
| `STORYBOARD_RUNTIME_GAME_DISCOVERY_JSON_PATH` | explicit GameHost discovery-registration file override | Existing runtime override; expression expansion becomes standardized |
| `STORYBOARD_RUNTIME_IDENTITY_JSON_PATH` | explicit identity JSON store override | Existing runtime override; expression expansion becomes standardized |

No new broad “project root” or “runtime output root” variable is proposed until a concrete configuration boundary needs it. A new variable must have an owner, a documented consumer, a lifetime (development-only versus deployable), and a test.

### 5. User-facing environment setup documentation (required)

The implementation is not complete until a developer can configure the system without reading source code or inferring variable names from JSON. Update these user-facing documents as part of the staged rollout:

1. `StoryBoard.Architecture/docs/environment-variables-setup.md` becomes the single setup index. For every supported variable it must state:
   - exact name and whether it is required, optional, development-only, or deployment-specific;
   - which subsystem reads it and why;
   - the path/value shape expected;
   - persistent and current-process PowerShell examples;
   - a minimal local-development configuration and an expanded multi-root example;
   - restart/process-scope expectations after `setx`.
2. `StoryBoard.Architecture/docs/asset-source-root-configuration.md` becomes the authoring guide. It must explain:
   - canonical persisted form `%STORYBOARD_ASSET_SOURCE_ROOT...%/relative/path`;
   - the primary root and `STORYBOARD_ASSET_SOURCE_ROOT_<NAME>` naming convention;
   - how Designer selects the deepest matching root and handles a tie;
   - what occurs when a selected asset is outside every configured root;
   - image, sound, preview, and map-path coverage;
   - legacy `ASSETROOT:/` read compatibility and its migration status.
3. Add a concise `Storyboard.Foundation.Paths` reference document covering `%NAME%` grammar, allowed/unsupported forms, unresolved-variable diagnostics, relative-base policy, and caller ownership. It is a technical reference, not the primary setup guide.
4. Update GameHost configuration documentation/examples to show `%STORYBOARD_WEBPORTAL_ROOT%`, runtime registration, discovery JSON, and identity JSON use where applicable.

Documentation requirements:

1. Never show a machine-specific path as the canonical value; use a clearly marked illustrative value only.
2. Include a copy/paste-ready two-root example, such as `STORYBOARD_ASSET_SOURCE_ROOT` plus `STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN`.
3. Include troubleshooting for unset variables, stale processes, unresolved tokens, tied root mappings, and assets outside configured roots.
4. Keep a variable's documentation beside its owner and link to the setup index rather than duplicating conflicting catalogs.

### 6. Foundation fast followers (deferred, individually gated)

`Storyboard.Foundation` is one intentionally small low-level package, not a path-only package. This workstream ships only `Storyboard.Foundation.Paths`. The following candidates are recorded as fast followers to consider one at a time after the portability rollout is accepted; none is authorized by this plan's implementation stages.

| Candidate namespace | Repeated behavior observed | Proposed narrow responsibility | Entry gate |
| --- | --- | --- | --- |
| `Storyboard.Foundation.Content` | Designer image/sound export staging and source-image naming, plus GameEngine Host asset handling, each compute SHA-256 and lowercase hex strings. | Hash byte arrays/streams and produce one canonical lowercase-hex representation. Domain-specific hash-to-GUID identity remains local. | Inventory all consumers and lock compatibility of hash casing, stream ownership, and truncation before adoption. |
| `Storyboard.Foundation.Files` | Designer independently sanitizes project names, source-image names, export stems, and export path segments using `Path.GetInvalidFileNameChars()`. | Validate/sanitize one filesystem name segment. Callers retain fallback names, collision policy, and user-facing validation. | Decide reserved-name, whitespace, maximum-length, and cross-platform policy; prove no existing artifact-name breakage. |
| `Storyboard.Foundation.Json` | GameEngine repeatedly creates permissive config `JsonSerializerOptions` and read/deserialize/fallback flows; Designer repeats JSON file reading for settings, catalogs, manifests, and project helpers. | Named JSON option profiles and result-based file read/deserialization; never silently impose fallback or error policy. | Separate user-preference, host-config, runtime-export, and authoring-document error policies before extracting shared mechanics. |

Fast-follower cadence:

1. Close and publish the path-portability slice first.
2. Open one focused plan for exactly one candidate, with its own owner, consumer inventory, compatibility decision, validation suite, and handoffs.
3. Do not add a candidate merely because it is convenient while working in another slice.
4. Keep `Storyboard.Foundation` free of Designer, GameEngine, WPF, ASP.NET, domain-model, and product-contract dependencies.

## Initial Scope Breakdown

### A) Shared portability seam

Create and package the resolver, its explicit error/result contract, test matrix, and variable catalog documentation.

Estimated effort: Medium.

### B) Designer authoring and export

Replace the single-root token resolver, add multi-root normalization and ambiguity behavior, preserve legacy reads, and validate image/sound/preview/export flows.

Estimated effort: High.

### C) GameEngine and GameHost configuration boundaries

Replace direct expansion and audit every externally configurable path entry point. Apply the common resolver to registration catalog paths, provider JSON paths, registration runtime-project paths, and static-client mounts.

Estimated effort: Medium.

### D) Consumer verification, migration, and closeout

Verify WebPortal/Simulator source-root non-ownership, migrate controlled project fixtures after compatibility coverage exists, and run end-to-end portability tests from relocated temporary roots.

Estimated effort: Medium.

## Structured Delivery Order and Session Handoffs (Locked)

1. Stage 01: Shared Portability Contract and Package
2. Stage 02: Designer Multi-Root Authoring and Export
3. Stage 03: GameEngine and GameHost Path-Boundary Integration
4. Stage 04: Simulator Consumer Verification
5. Stage 05: WebPortal Consumer Verification
6. Stage 06: Legacy Asset Token Migration and Retirement Decision
7. Stage 07: Cross-System Regression Hardening and Closeout

Each included stage has a placeholder handoff below and must complete it before the next included stage begins.

## Stage Inclusion Matrix

| Stage | Inclusion | Reason | Boundary override |
| --- | --- | --- | --- |
| 01 Contracts and shared runtime mapping | Required | New dependency-neutral portability package is the common seam. | Stage is utility/package work rather than schema/codegen work. |
| 02 Designer authoring UX | Required | Owns authored source-asset resolution, persistence, and export staging. | None. |
| 03 GameEngine runtime integration | Required | Owns configured runtime paths and GameHost mounting. | Includes GameHost configuration integration. |
| 04 Simulator host parity | Required (verification-only) | Confirm Simulator cache/client paths do not resolve authoring-source expressions immediately after its GameEngine/Host producer work. | Plan-specific order override: Simulator follows Stage 03 because both are one ownership area; no Simulator code changes expected. |
| 05 Host/WebPortal runtime consumption | Required (verification-only) | Confirm WebPortal receives runtime-relative assets and needs no authoring-root resolver. | Plan-specific order override: WebPortal follows the Engine/Simulator ownership unit; no WebPortal code changes expected. |
| 06 Contract retirement | Optional | `ASSETROOT:/` is a data-format compatibility alias, not a DTO change; retirement needs separate approval after migration evidence. | Data migration may include Samples, TheDocks, and Designer starter projects. |
| 07 Regression hardening and closeout | Required | Cross-repository package/version and relocated-root coverage are essential. | None. |

## Stage Definitions

### Stage 01: Shared Portability Contract and Package

Goal: create `Storyboard.Foundation`, ship only its `Paths` capability, publish its versioned package, and document the single expression/error contract.

Codebase context:

1. Primary owner: `StoryBoard.Contracts`.
2. First targets: new Foundation project, `Paths` namespace, unit tests, package/readme metadata, environment-variable catalog docs, and the technical path-expression reference.
3. Preserve: no DTO/schema field change; no `ASSETROOT:/` retirement; compatible packages remain consumable during staged rollout.
4. Required tests: token expansion, unresolved/malformed token rejection, absolute/relative/base resolution, URI policy, nested-root containment helpers, deterministic root ordering helpers. Do not implement Content, Files, or Json fast followers in this stage.
5. Allowed read scope: `StoryBoard.Architecture/docs/**`, `StoryBoard.Designer/**`, `StoryBoard.GameEngine/**`, and package/version docs.
6. Allowed edit scope: `StoryBoard.Contracts/**`, `StoryBoard.Architecture/docs/**`, this plan, and Stage 01 handoff.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_01_SHARED_PORTABILITY_HANDOFF.md`.

### Stage 02: Designer Multi-Root Authoring and Export — Complete

Goal: resolve standard expressions, write the named root token form, preserve legacy reads, and ensure preview/validation/image/sound staging all share that behavior.

Codebase context:

1. Primary owner: `StoryBoard.Designer`.
2. First targets: `AssetSourcePathResolver`, image resolver, image/sound stagers, authoring dialogs, health/validation paths, every asset-valued authoring field (including game previews and full/gray/normal map variants), focused tests, and the author-facing asset-root setup guide.
3. Preserve: runtime exports remain `assets/...`; stored manifest `sourcePaths` retains the authored expression; out-of-root absolute paths are not silently rewritten.
4. Required tests: primary/named/multiple/nested/tied roots; image, sound, preview, game presentation, full/gray/normal map persistence; old token read; missing token diagnostic; export from two roots.
5. Allowed read scope: Stage 01 package/handoff, Architecture docs, current Designer tests, GameEngine runtime-output contracts.
6. Allowed edit scope: `StoryBoard.Designer/**`, `StoryBoard.Architecture/docs/**`, and Stage 02 handoff.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_02_DESIGNER_HANDOFF.md`.

### Stage 03: GameEngine and GameHost Path-Boundary Integration

Goal: replace direct expansion with the shared resolver at every external/configured path boundary and add diagnostics/tests for invalid configuration.

Codebase context:

1. Primary owner: `StoryBoard.GameEngine`.
2. First targets: `GameManager`, `RuntimeHostAssetManagementClient`, `JsonRuntimeGameDiscoveryProvider`, discovery/identity provider factories, `StaticClientMountingExtensions`, configuration tests, package references, and GameHost configuration/setup examples.
3. Preserve: runtime-export child assets remain constrained under the resolved runtime root; no source asset root is required by runtime clients.
4. Required tests: `%STORYBOARD_*%` registrations; discovery and identity JSON paths; configured catalog path; static mount; missing variable produces the correct safe failure/disabled mount diagnostic; relative fallback after successful expansion.
5. Allowed read scope: Stage 01 handoff, Stage 02 handoff (runtime-export invariants), Architecture docs, existing GameHost/GameEngine tests.
6. Allowed edit scope: `StoryBoard.GameEngine/**`, `StoryBoard.Architecture/docs/**`, and Stage 03 handoff.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_03_GAMEENGINE_HOST_HANDOFF.md`.

### Stage 04: Simulator Consumer Verification

Goal: establish that Simulator uses host/runtime-export assets and does not resolve authoring source paths while the GameEngine/Host integration context is still active.

Codebase context:

1. Primary owner: `StoryBoard.GameEngine/Storyboard.Simulator*` for verification only.
2. First targets: asset cache/discovery client tests and simulator startup path handling.
3. Preserve: simulator cache locations and explicit command-line project paths remain separate concerns from source-root expressions.
4. Required tests: simulator receives assets staged from two distinct source roots through Host; no source-root expression or physical source path reaches simulator.
5. Allowed read scope: Stage 03 handoff, Simulator tests, Host asset contracts.
6. Allowed edit scope: `StoryBoard.GameEngine/Storyboard.Simulator*/**` only if verification exposes a consumer defect; otherwise Stage 04 handoff only.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_04_SIMULATOR_HANDOFF.md`.

### Stage 05: WebPortal Consumer Verification

Goal: establish evidence that WebPortal consumes host-served/runtime-relative assets only and does not need authoring-source expression support.

Codebase context:

1. Primary owner: `StoryBoard.WebPortal` for verification only.
2. First targets: portal asset URL construction and integration tests/configuration documentation.
3. Preserve: the only portal filesystem reference remains the GameHost-owned static mount resolved in Stage 03.
4. Required tests: host-mounted portal launches from `%STORYBOARD_WEBPORTAL_ROOT%`; runtime asset URLs remain valid after source roots change.
5. Allowed read scope: Stage 03-04 handoffs, `StoryBoard.WebPortal/**`, host transport/asset contracts.
6. Allowed edit scope: `StoryBoard.WebPortal/**` only if verification exposes a consumer defect; otherwise Stage 05 handoff only.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_05_WEBPORTAL_HANDOFF.md`.

### Stage 06: Legacy Asset Token Migration and Retirement Decision

Goal: decide and, only if approved, execute checked-in data migration and legacy token retirement.

Entry criteria: Stages 01-05 pass; all supported readers write canonical `%STORYBOARD_ASSET_SOURCE_ROOT...%/` references; migration report identifies every `ASSETROOT:/` and eligible non-token absolute asset reference in authored project data and starter content.

Codebase context:

1. Owners: Designer for migration tooling/compatibility, SampleProjects/TheDocks/Designer starter content for data, Contracts only if documentation/package retirement needs it.
2. First targets: migration command or scripted reviewed conversion, `StoryBoard.SampleProjects/**`, `StoryBoard.Game.TheDocks/**`, and `StoryBoard.Designer/StoryboardDesigner.App/StarterProjects/**`.
3. Preserve: generated runtime exports are regenerated only from migrated authored sources; do not perform a blind text replacement.
4. Required tests: opening/saving/publishing migrated projects under primary and named roots; external legacy fixture behavior until read support is actually removed.
5. Allowed read scope: repository-wide for migration inventory and all prior handoffs.
6. Allowed edit scope: explicitly approved project-data folders, `StoryBoard.Designer/**`, Architecture docs, and Stage 06 handoff.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_06_LEGACY_MIGRATION_HANDOFF.md`.

### Stage 07: Cross-System Regression Hardening and Closeout

Goal: run the system-level portability matrix, resolve only approved stabilization defects, and complete closure evidence.

Codebase context:

1. Owners: all affected repositories, with edits restricted to regressions uncovered by the approved plan.
2. Required matrix: relocated development root; relocated sample root; relocated WebPortal distribution root; primary asset root; two named asset roots; unset/misspelled variable; legacy token fixture while supported.
3. Required tests: package consumer/build tests, Designer focused and export tests, GameEngine/GameHost path tests, WebPortal integration, Simulator host-asset path, and representative end-to-end publication/session launch.
4. Allowed read scope: repository-wide for validation.
5. Allowed edit scope: focused tests/baselines/docs and Stage 07 handoff; implementation edits require a documented defect addendum.

Primary output: `plans/active/handovers/PORTABLE_PATH_EXPANSION_AND_MULTI_ROOT_ASSETS_PLAN/PORTABLE_PATH_EXPANSION_07_CLOSEOUT_HANDOFF.md`.

## Risk Register

1. Environment expansion is platform/process-sensitive. Mitigation: own token parsing and unresolved-token diagnostics, avoid fallthrough, and test at process scope.
2. Two asset variables may target the same directory. Mitigation: force explicit author choice for equal-depth matches.
3. Nested roots can produce an incorrect broad token. Mitigation: choose the deepest containing root and test it.
4. Existing `ASSETROOT:/` data is widespread across samples, starter content, and exports. Mitigation: read compatibility first, controlled migration second, retirement only by approval.
5. Package-version drift can leave Designer and GameEngine on different resolver behavior. Mitigation: publish/consume the shared package in Stage 01 and verify versions in Stage 07.
6. Broad expansion could permit unintended filesystem access. Mitigation: retain each caller's existing base/root containment rules; source expression expansion does not relax runtime asset traversal protections.

## Validation Gates

1. Unit tests for the portability package, including no-unresolved-token fallback.
2. Designer tests covering persistence, previews, validation, and image/sound export from at least two roots.
3. GameEngine/GameHost tests covering every configurable path boundary listed in Stage 03.
4. A launch test using moved temporary roots rather than workspace-drive assumptions.
5. WebPortal and Simulator verification that runtime-relative asset flow remains unchanged.
6. A repository-wide `ASSETROOT:/` inventory before and after any approved Stage 06 migration.

## Completion Acceptance Criteria

1. Newly authored in-root assets persist as `%STORYBOARD_ASSET_SOURCE_ROOT...%/relative/path`, never `ASSETROOT:/`.
2. At least primary plus two named asset roots work for image, sound, and preview authoring/export.
3. Every approved external/configured path boundary uses the shared resolver and reports unresolved variables clearly.
4. Runtime exports and host/simulator/WebPortal asset delivery remain source-root independent.
5. The environment-variable setup index, asset-authoring guide, Foundation path-expression reference, and GameHost configuration examples describe the actual deployed behavior and include a two-root copy/paste setup example.
6. Legacy token support/migration status is explicit; it is neither silently broken nor silently retained without a future decision.

## Final Closeout Checklist

1. All required stage handoffs are complete.
2. Package versions consumed by Designer and GameEngine are recorded.
3. Validation matrix outcomes and intentionally skipped non-owner edits are recorded.
4. Any deferred legacy retirement is listed as an active follow-up with acceptance criteria.
5. Archive this plan and its entire handoff set together only after completion.
