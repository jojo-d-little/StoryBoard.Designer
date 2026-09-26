# Lighting Designer Implementation Checklist

This is the short execution checklist for implementing Designer-side lighting.
Use the [Lighting Designer Implementation Handover](Lighting_Designer_Implementation_Handover.md)
for field definitions and the [canonical contract handover](../../StoryBoard.Contracts/plans/Lighting_Data_Contract_Designer_Handover.md)
for cross-application decisions.

The implementation is complete when Designer can author, validate, save, reload,
and export lighting without producing Runtime-effective values or Host room
coordinates.

## 1. Lock the dependency baseline

- [x] Update `Storyboard.Contracts` to the approved package version; use `1.0.6` exactly for this effort unless a floating range is intentional.
- [x] Restore and build before adding feature code.
- [x] Confirm Designer uses `Storyboard.Shared.DesignerContracts` from the package.
- [x] Do not copy contract DTOs or schemas into the Designer repository.

**Checkpoint:** the application builds against the package containing the lighting DTOs.

## 2. Add the Designer authoring model

Add nullable/optional authoring state to the existing Designer models:

- [x] `ProjectModel`: project-level point-light defaults.
- [x] `Room`: room-level ambient lighting.
- [x] `PhaseNode`: optional ambient overlay for page/chapter use.
- [x] `GameObject`: optional point light, optional light occlusion, and default light enablement.
- [x] Reuse the existing object footprint, spatial type, height, orientation, and grid settings. Do not create a second footprint model.
- [x] Keep point-light offsets local pixels from the top-left footprint cell. Never store absolute room coordinates in Designer.
- [x] Ensure object/template/instance-copy operations preserve authored lighting data.
- [x] Keep lighting absent by default; ordinary objects must not receive synthesized lights.

**Checkpoint:** the in-memory model can represent every Designer field while existing projects still load with lighting absent.

## 3. Wire every serialization path

Update both directions in `JsonExportService` and any related sidecar handling:

- [x] Project root: point-light defaults.
- [x] Room and room-template files: ambient lighting.
- [x] Phase and phase-sidecar files: ambient overlays.
- [x] Object, template, base-object, contained-object, and room-instance files: point light, occlusion, and enabled default.
- [x] Runtime export/projection: pass authored lighting into the appropriate Runtime DTOs without resolving it into Host state.
- [x] Preserve omission versus explicit values on save/reload. `null` means absent; explicit `0`, `1`, colors, and `false` must remain meaningful.
- [x] Confirm older project JSON with no lighting fields round-trips without migration or field synthesis.

**Checkpoint:** a lighting-rich project survives Designer save → close → reload → save without losing fields or changing coordinate meaning.

## 4. Add validation before UI polish

Implement validation using the shared schema constraints plus cross-field rules:

- [x] Colors: six-digit `#RRGGBB`.
- [x] Ambient, flicker, blend, and occlusion: `0..1`.
- [x] Phase ambient scale: `0..2`.
- [x] Cone angle: `1..360` degrees.
- [x] Reject negative radius, height, frequency, sway, and gradient values.
- [x] Enforce units: pixels, cells, degrees, and Hertz.
- [x] Validate `Blend`/`Override` color behavior and `colorBlend` usage.
- [x] Require occlusion to be meaningful with the existing footprint/height model.
- [x] Restrict phase overlay authoring to the intended page/chapter levels and document page-over-chapter fallback.

**Checkpoint:** invalid lighting cannot be saved, and valid boundary values survive round-trip tests.

## 5. Build the authoring UI in ownership order

Implement the smallest UI surface for each owner:

- [x] Project settings: point-light defaults.
- [x] Room settings: ambient intensity and color.
- [x] Phase settings: ambient scale, color mode, color, and blend amount.
- [x] Object appearance/alignment workspace: enable point-light authoring and place the light relative to the clearly labeled top-left footprint cell.
- [x] Object lighting-details dialog: radius, intensity, colors, cone, height, sway, and flicker.
- [x] Object occlusion controls: strength and clear/remove behavior.
- [x] Provide explicit create, edit, and remove actions for every optional lighting object.
- [x] Make clear that sprite anchor, image bounds, and image offsets do not define light placement.

**Checkpoint:** a user can author and remove each supported lighting field without editing JSON manually.

## 6. Add focused fixtures and tests

Cover the full Designer persistence path with fixtures for:

- [ ] Project defaults and a project with no lighting.
- [ ] Room ambient and a room with ambient omitted, defaulting at Runtime to `1`.
- [ ] Page overlay, chapter fallback, override color, and blend color.
- [ ] Point-light object with local pixel offset.
- [ ] Solid object with default full occlusion.
- [ ] Explicit occlusion override including zero.
- [ ] Lighting omitted entirely.
- [ ] Copied/template object preserving authored lighting.
- [ ] Explicit disabled default light.
- [ ] Older project load/save with no lighting fields.
- [ ] Invalid ranges, colors, units, and incompatible combinations.

Also verify that exported Designer/runtime payloads contain local offsets and
authored values only—not absolute room `x`/`y`, effective inherited values, or
Host DTOs.

**Checkpoint:** focused tests pass and serialization snapshots demonstrate backward compatibility.

## 7. Final integration gate

- [ ] Review the implementation against the completion criteria in the Designer handoff.
- [ ] Run `dotnet restore`, `dotnet build`, and `dotnet test` from a clean package state.
- [ ] Confirm no local contract DTO/schema copies were introduced.
- [ ] Confirm no WebPortal rendering, Host projection, delta emission, or absolute-coordinate logic was added to Designer.
- [ ] Record any deferred UI decisions or contract questions in the handoff before passing work to Runtime.

## Keep these boundaries firm

- One optional object-owned point light for this effort; no standalone room lights or multiple lights per object.
- Project defaults are not room settings.
- Existing footprint/height data remains canonical.
- Runtime owns inheritance and per-instance effective state.
- Host owns local-offset-to-room-coordinate resolution.
- WebPortal owns current-frame rendering arrays.
