# Room object local image placement checklist

Use this as the short development driver. The agreed transform contract, code locations, acceptance criteria, and historical findings are in `Room_Object_Local_Image_Placement_Review.md`.

## Scope note

The public `LoadRuntimeSnapshot` debugger API is out of scope for this placement effort. The thick Simulator loads projects through `LoadGameRuntimeProject`, which uses the clean-export loader and enriches selected image variants with bitmap dimensions. No production call sites for `LoadRuntimeSnapshot` were found in the repositories reviewed; current in-repository calls are tests. Track a separate follow-up to assess whether that API should remain, be narrowed, or be retired. Do not block this effort on the snapshot API.

## Ordered steps

### 1. Implement the shared placement math

- [x] Encode the agreed order: scale the full bitmap, rotate locally, normalize its full-bitmap bounds, pin that bounds top-left to the footprint top-left at zero offset, apply X/Y offsets, then apply room placement/rotation.
- [x] Define the helper's output as the renderer placement needed to produce that visual result. Use exact dimensions for the selected full bitmap, including transparent margins.
- [x] Add expected-coordinate tests for the rug and elevator, covering local rotation, room rotation, combined rotations, rectangular dimensions, scale, and positive/negative offsets.
- [x] Ensure local rotation does not rotate the offsets; room rotation carries the offsets with the object. Keep physical footprint geometry separate.

**Done when:** shared-math tests prove the contract for both stress assets and the listed transform cases.

### 2. Update the Designer previews

- [x] Make the Large Object Alignment Editor use the shared placement result.
- [x] Make the Room Designer use the same result and full bitmap dimensions.
- [x] Verify zero-offset bounds and X/Y directions in both previews.

**Done when:** both Designer views agree for the rug and elevator cases.

### 3. Wire runtime placement and mutations

- [ ] Use the shared result to build renderable state after startup, image-variant selection, movement, and rotation.
- [ ] Keep movement, collision, occupancy, and mutation rules based on the physical footprint.
- [ ] Check visual transition endpoints use resolved renderable positions.

**Done when:** runtime snapshots and mutation results match the Designer placement.

### 4. Verify the Pixi renderer

- [ ] Trace resolved position, rotation, and scale through the host adapter to Pixi transforms.
- [ ] Check bitmap bounds, room rotation, scale layers, and movement/rotation animations.
- [ ] Change Pixi only if it cannot render the shared placement result; do not add a second bounds calculation.

**Done when:** Pixi matches runtime placement for both stress assets.

### 5. Catalog and correct sample projects

- [ ] Catalog sample objects whose rotations, offsets, bitmap shapes, or transparent margins may affect placement.
- [ ] Review the catalog; do not automatically rewrite saved values.
- [ ] Correct selected objects manually in the Designer and record the edits.

**Done when:** affected sample objects have a reviewable keep-or-correct decision and manual corrections.

### 6. Close out

- [ ] Run shared-math, Designer, runtime-mutation, and Pixi checks.
- [ ] Recheck the rug and elevator through all four areas.
- [ ] Confirm no compatibility path for old placement behavior was added.

**Done when:** all four areas agree and sample-project corrections are complete.
