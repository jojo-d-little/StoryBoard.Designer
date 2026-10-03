# Room object local image placement

Status: Architecture plan. The post-rotation bitmap-bounds model below supersedes the earlier pivot-based proposal. No implementation or sample-project corrections are included in this plan.

## Scope boundary and deferred API review

The public `IHostRuntimeGameDebugger.LoadRuntimeSnapshot` entry point is outside this placement effort. The thick Simulator uses `LoadGameRuntimeProject` and the clean-export loader, which enriches mapped image variants with bitmap dimensions. No production call sites for `LoadRuntimeSnapshot` were found in the repositories reviewed; current in-repository calls are tests. Add a separate follow-up to decide whether this diagnostic/replay API should remain, be narrowed, or be retired. Do not block the placement work on it.

## Goal

Make the image alignment editor, Room Designer, runtime render placement, mutations, and Web Portal renderer show the same result for scaled rectangular images with transparent margins, local image rotation, nonzero X/Y offsets, room rotation, movement, and mutation. Establish one transform contract, implement it once in shared placement math, verify every consumer, then manually correct affected sample-project assets in the Designer. Do not add an old-data compatibility path.

## Reproduction inputs

Keep both existing stress cases available:

- **Workshop Supply Closet rug:** 13 × 8 cell footprint; source bitmap 1536 × 1024; authored base scale 0.43; local image rotation −180°; offsets −67, −52. The bitmap has substantial transparent padding. It demonstrates that editor offsets must be applied after local image rotation and must not reverse direction when the image rotates.
- **Docks Elevator Foyer elevator:** 10 × 10 footprint at (600, 80), 400 × 400 bitmap, base scale 1, local rotation 180°, offsets (0, 0), room rotation 0°. Existing screenshots and object paths are recorded in the historical notes below. It catches renderer-origin compensation and scale/dimension mismatches.

These are diagnostic fixtures. Do not alter their authored values until the transform behavior is implemented and verified; then use the Designer for any sample-project corrections.

## Working transform contract

Use distinct coordinate frames and apply transforms in this order:

1. Resolve the selected full bitmap and apply its authored base scale. Use the full bitmap rectangle, including transparent pixels.
2. Apply the selected variant's local image rotation.
3. Compute the axis-aligned bounds after both base scaling and local image rotation have been applied to the full bitmap. Translate those bounds so their top-left is `(0, 0)`. This normalized bitmap-bounds top-left is the placement anchor: at zero X/Y offset, pin it exactly to the footprint top-left. This makes the bitmap's post-rotation bounds independent of whichever internal pivot a rendering API uses.
4. Apply the authored local X/Y offsets in footprint-local pixels from that pinned anchor. Zero offsets put the normalized bitmap bounds' top-left exactly at the footprint's top-left. Positive X moves right; positive Y moves down. Local image rotation does not rotate these offsets. Base image scale does not scale them.
5. Apply the room/object position and room rotation to the footprint-relative image placement. The offset rotates with the object in the room. Physical footprint rotation and collision/occupancy remain based on the physical footprint, not on the image's rotated bounds.

The renderer may use any internal pivot if its output is compensated to match this contract. The visible artwork is intentionally not auto-cropped: transparent margins are included in the bitmap bounds, and the authored offsets provide the final visual alignment.

The final draw angle may be the sum of local image and room rotations, but placement math must retain the two angles separately. Their translation effects differ: local image rotation precedes the offset; room rotation carries the already-offset image with the object. Do not rotate offsets by the local image angle or use the combined angle to rebase the physical footprint.

## Ownership and expected changes

| Area | Current behavior | Expected work |
|---|---|---|
| Large Object Alignment Editor | `LargeObjectVisualGuideDialog` rotates offsets and uses a center-origin WPF transform. | Use the shared transform result. Show the full rotated bitmap bounds and footprint so zero-offset placement and offset directions are visible. |
| Room Designer | `RoomDesignerPreviewObjectViewModel` calls `RuntimeRenderablePlacementMath`; the XAML positions the image and rotates it from a separate binding. | Render from the same shared result as the alignment editor. Remove duplicated correction assumptions and verify thumbnail dimensions/aspect ratio match the selected full bitmap. |
| Runtime render projection | `GameCommandProcessorService.BuildRenderableImage` computes final renderable X/Y and rotation via the shared helper. | Make this the runtime consumer of the canonical transform. Ensure each runtime load path provides selected-variant full bitmap dimensions; do not silently substitute cell dimensions when a rotated-bounds calculation requires the real size. |
| Runtime mutations | `SessionRuntimeScopeMutationGateway` changes logical object position/rotation and uses physical footprints for move/rotation rules. | Keep grid movement, collision, occupancy, and logical mutation semantics unchanged unless a concrete mismatch is found. Ensure the render projection recomputes canonical image placement after mutations and that visual transition endpoints use the same resolved draw coordinates. |
| Web Portal / Pixi | The adapter passes renderable X/Y, rotation, and scale. Pixi rotates the Sprite from a root container and applies scale layers with pivots. | First verify the host's resolved placement against Pixi's actual transform stack. Keep one owner for bounds normalization: prefer the shared runtime calculation, with Pixi applying the supplied transform faithfully. Change Pixi only if its current root/scale behavior cannot represent the contract. Do not add a second independent AABB/offset formula. |

Relevant implementation locations:

- Designer guide: `StoryboardDesigner.App/Views/LargeObjectVisualGuideDialog.xaml.cs` and `.xaml`
- Room Designer: `StoryboardDesigner.App/Features/RoomDesigner/ViewModels/RoomDesignerPreviewObjectViewModel.cs` and `Controls/RoomDesignerOverlayPreviewControl.xaml`
- Shared/runtime placement: `Storyboard.GameEngine/GameServices/Spatial/RuntimeRenderablePlacementMath.cs` and `GameServices/Commands/GameCommandProcessorService.cs`
- Mutations: `Storyboard.GameEngine/GameServices/Commands/SessionRuntimeScopeMutationGateway.cs`
- Portal mapping and drawing: `Storyboard.WebPortal/src/gameRenderer/adapters/mapHostSessionScene.ts` and `src/gameRenderer/pixi/PixiGameRenderer.ts`

## Implementation checklist

### 1. Implement the shared transform foundation

- [x] Encode the agreed contract as shared placement math and a precisely defined renderer-placement result.
- [x] Use selected full-bitmap dimensions, including transparent margins; verify every runtime load path supplies them.
- [x] Add expected-coordinate tests for the rug and elevator: local rotation, room rotation, combined rotations, zero/nonzero offsets, rectangular bounds, and base scale.
- [x] Verify local image rotation does not rotate offsets, while room rotation carries offsets with the object.
- [x] Keep physical footprint geometry separate from rendered bitmap bounds.

**Done when:** the math tests prove that the normalized bitmap-bounds top-left pins to the footprint top-left at zero offset and that offsets behave as agreed.

### 2. Update Designer previews

- [ ] Make the Large Object Alignment Editor use the shared result; remove `RotateOffset`-based positioning.
- [ ] Make the Room Designer use the same result and full bitmap dimensions.
- [ ] Verify bitmap-boundary overlays, footprint overlays, zero-offset placement, and positive/negative offset directions.

**Done when:** both Designer views agree for the rug and elevator cases.

### 3. Update runtime placement and mutation flow

- [ ] Use the shared result to build renderable image state after startup, variant selection, movement, and rotation.
- [ ] Keep movement, collision, occupancy, and logical mutation semantics based on the physical footprint.
- [ ] Trace mutation responses and visual transition endpoints; use resolved render positions where pixels are animated.
- [ ] Do not silently substitute cell dimensions when the selected bitmap's actual dimensions are required.

**Done when:** runtime snapshots and mutation transitions match the Designer placement.

### 4. Verify and adapt Pixi

- [ ] Trace resolved X/Y, rotation, and scale through the host adapter to the Pixi root and scale layers.
- [ ] Check bitmap bounds, room rotation, base and situational scale, and movement/rotation animation endpoints.
- [ ] Change Pixi only if it cannot faithfully render the shared result; do not add a second bounds calculation.

**Done when:** Pixi shows the same placement as runtime for both stress assets.

### 5. Catalog and manually correct sample projects

- [ ] Scan sample projects for nonzero local rotations and offsets, rectangular bitmaps/footprints, and large transparent margins.
- [ ] Produce a reviewable catalog; do not auto-rewrite saved objects.
- [ ] Correct selected samples manually in the Designer and record which objects were corrected or intentionally retained.

**Done when:** affected sample objects have an explicit, reviewable keep-or-correct decision.

### 6. Close-out verification

- [ ] Run targeted shared-math, Designer, runtime mutation/projection, and Pixi checks.
- [ ] Recheck the rug and elevator through all four areas.
- [ ] Confirm no compatibility branch for the old placement behavior was added.

**Done when:** all four areas agree and reviewed sample-project corrections are complete.
## Acceptance criteria

- The same authored bitmap, scale, local rotation, offsets, footprint, and room rotation produce matching bounds in the alignment editor, Room Designer, runtime host renderable state, and Pixi.
- With zero offsets, the full scaled/local-rotated bitmap bounds begin at the footprint-local origin before room placement/rotation.
- Local image rotation never reverses the meaning of X/Y offsets; room rotation carries them with the object.
- Transparent margins remain part of the bitmap bounds; users can align visible art with the offsets.
- Grid occupancy and collision continue using physical footprint dimensions only.
- Mutations and their visual transitions use the same resolved placement as an initial render.
- Sample-project corrections are explicit Designer edits after the implementation, not an automatic compatibility migration.

## Historical review notes (before the new contract)

The original investigation found a Docks Elevator Foyer displacement for a 400 × 400 image with a 10 × 10 footprint and local rotation 180°. It reported different Designer and runtime placement corrections, traced to footprint rebasing by combined room/local rotation and runtime use of cell-size fallback dimensions. The old proposal used center-pivot compensation and stated that portal code was unnecessary. That proposal is superseded by the post-rotation full-bitmap-bounds contract above; the portal assumption remains to be verified against Pixi's actual transform stack.

The earlier checkpoint recorded passing GameEngine placement/image-header tests, Designer preview/door-parity tests, and WebPortal scene-mapping tests. Those results predate this contract and do not verify the new behavior. The earlier work left Docks content unchanged and did not include the Workshop rug stress case.

## Preserved prior checkpoint record (superseded by this plan)

This section preserves the working-copy investigation notes that predated the architecture plan. They are useful evidence about the earlier implementation and its tests; their pivot-based transform proposal and renderer conclusion do not define the new contract.

### Earlier implementation checkpoint

The earlier status recorded that the Designer and runtime placement views had been compared and a source fix had been tested against local GameEngine package `1.0.12-local.20261002`. Publishing the package and rebuilding the running applications were separate release steps. Docks content was unchanged at that checkpoint.

### Earlier code review findings

- The appearance guides rotated the sprite around its center and used native image pixels at scale 1. The room preview rotated around its top-left corner and used rendered dimensions when computing a corrective position.
- `RuntimeRenderablePlacementMath.ComputeIconLocalOffset` combined local center-pivot compensation with footprint translation selected by room rotation plus local rotation. The elevator case added `(400, 400)` twice in Designer. Its early return when combined rotations equal 0° was also flagged because differing pivots could still require a position adjustment.
- Runtime called the same helper but supplied grid-cell size times scale as rendered image width and height. For a 40-pixel grid and 400-pixel image, Designer and runtime therefore calculated different offsets. The portal mapped the runtime draw position and rotated the actual texture around its top-left corner; its visible result had not been checked in the portal at that checkpoint.
- Earlier fixtures covered room-rotated doors and noncardinal local rotation, but not the large sprite with cardinal local rotation. The Designer comparison snapshot printed the same shared value on both sides with constant zero delta; door parity diagnostics reported deltas without asserting visual agreement.

### Earlier Docks screenshot calculations

The authored object was `StoryBoard.Game.TheDocks/src/GameObject/46698d7c24ef4c9285f6b7ca6d6af748.object.json`; its runtime export had the same local-180° setting. The elevator was at `(600, 80)`, with a 10 × 10 footprint and 400 × 400 PNG.

The old Designer offset was `(800, 800)`, yielding draw position `(1400, 880)` and visible bounds `(1000, 480)`–`(1400, 880)`. The old runtime offset was `(440, 440)`, yielding draw position `(1040, 520)` and visible bounds `(640, 120)`–`(1040, 520)`. The then-expected offset was `(400, 400)`, draw position `(1000, 480)`, and visible bounds `(600, 80)`–`(1000, 480)`.

That investigation attributed the discrepancies to two issues in the old path:

1. Footprint rebasing used room rotation plus local image rotation, although the local rotation does not rotate the physical footprint.
2. Runtime pivot compensation used grid-cell dimensions `(40 × 40)` instead of selected-image dimensions `(400 × 400)`, leaving a one-cell visual shift in the portal.

### Superseded pivot-based formula

The former proposal used `D = A + B(G) + R(G + L) O + R(G) (C - R(L) C)`, with footprint rebasing based on room rotation and center compensation based on the image center. It also stated that portal code would not need to change. The current plan replaces this formula with full-bitmap rotated-AABB normalization followed by offsets; the Pixi integration must be verified rather than assumed.

The earlier runtime dimension path read native dimensions from the raster file header during clean-export loading and carried them on the selected variant in memory, without changing export JSON or host contracts. The earlier review separately flagged that this enrichment did not cover every runtime snapshot load path and that missing dimensions could silently fall back to cell size. The new implementation must verify dimension availability across all supported entry points.

### Earlier verification results

These results were reported for the earlier implementation and do not verify the new contract:

- GameEngine placement, normalization, and image-header tests: 20 passed; existing movement and rotation tests: 37 passed.
- Designer preview and door-parity tests against local GameEngine package `1.0.12-local.20261002`: 22 passed. The four Workshop Tutorial Atrium door draw deltas were asserted to be zero.
- WebPortal scene-mapping tests: 21 passed. The separate Pixi appearance test file could not run cleanly because JSDOM had no canvas implementation; it contained no changed code at that checkpoint.
- The running Designer Debug process held its DLL open, so verification used a Release build. Refreshing the running app and publishing the GameEngine package remained release steps.

The earlier historical checklist proposed a two-way North Hallway → Elevator Foyer route to make the room reachable in the portal. That content step was not part of the placement fix. It is not an implementation instruction for this plan.




