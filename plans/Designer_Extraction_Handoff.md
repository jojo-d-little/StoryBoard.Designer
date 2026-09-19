# Designer Extraction Handoff Plan

## Objective

Move Designer onto published package dependencies while preserving the current Designer → GameEngine coupling. Designer should consume contracts and GameEngine as packages, not source projects.

## Intended dependency direction

```text
Storyboard.Contracts ─┐
                     ├→ Designer
GameEngine package ──┘
```

Designer may depend on both packages. Neither package may depend on Designer.

## Prerequisites

- [ ] Contracts package is published and restoreable from GitHub Packages.
- [ ] GameEngine repository has been extracted and publishes a private package.
- [ ] Designer’s current contracts and GameEngine project references are inventoried.
- [ ] Existing Designer tests and build scripts have a known baseline.

## Scope

- [ ] Replace the local contracts project reference with `Storyboard.Contracts`.
- [ ] Replace the local GameEngine project reference with the GameEngine package.
- [ ] Change Designer-generated contract references to `Storyboard.Shared.DesignerContracts`.
- [ ] Remove local copies under `StoryboardDesigner.App/Services/DesignerJsonContracts` after package-backed compilation succeeds.
- [ ] Remove Designer-local schema/codegen promotion steps that are now contract-owned.
- [ ] Preserve current Designer → GameEngine APIs and coupling.
- [ ] Add package restore/build validation from a clean checkout.

## Namespace expectations

- Runtime contracts: `Storyboard.Shared.RuntimeContracts.*`
- Save-game contracts: `Storyboard.Shared.SaveGameStateContracts.*`
- Host contracts: `Storyboard.Shared.HostContracts.*`
- Designer contracts: `Storyboard.Shared.DesignerContracts`
- Transport metadata: `Storyboard.Shared.HostContracts.Transport`

## Explicitly defer

- [ ] Do not introduce a service boundary between Designer and GameEngine yet.
- [ ] Do not redesign GameEngine interfaces solely to make Designer independent.
- [ ] Do not remove coupling that is required for behavioral parity.

## Acceptance criteria

- [ ] Designer builds with package references only.
- [ ] Designer tests pass without local contract source or codegen output.
- [ ] No local Designer generated DTO copies remain.
- [ ] Existing Designer behavior and serialization snapshots remain stable.
- [ ] CI restores both packages from GitHub Packages.
- [ ] Remaining Designer → GameEngine coupling is documented for the later decoupling effort.

## Asset-root path convention

Designer persisted asset references use the canonical environment-token form:

```text
%STORYBOARD_ASSET_SOURCE_ROOT%/path/to/asset.png
```

Named roots use the same form, for example `%STORYBOARD_ASSET_SOURCE_ROOT_VICTORIAN%/...`.
The legacy `ASSETROOT:/...` alias was intentionally retired after the controlled project migration and must not be introduced in new Designer projects or documentation.

## Later decoupling handoff

After package migration is stable, inventory the coupling by category:

- direct type references,
- shared mutable state,
- callbacks/events,
- filesystem/configuration assumptions,
- test-only dependencies.

Use that inventory to decide whether the future boundary should be interfaces, an adapter layer, a process/service boundary, or a separate orchestration package.
