# Room object local image placement review

Status: Observation checkpoint. Designer multi-cell traversal authoring is available; the Docks route and portal comparison are pending. No placement or rotation change is approved by this note.

## Reproduction

In the Docks project, Elevator Foyer contains an Elevator object at (600, 80) with a 10 × 10 cell footprint. Its 400 × 400 image has scale 1, local rotation 180°, local offsets (0, 0), and room rotation 0°. The room preview draws the selection footprint over the elevator floor but draws the elevator image one 400-pixel footprint to the right and down. Changing to local rotation 0° and room rotation 180° visually aligns it in the Designer.

The authored object is `StoryBoard.Game.TheDocks/src/GameObject/46698d7c24ef4c9285f6b7ca6d6af748.object.json`; the matching runtime export has the same local-180° setting. Keep this saved configuration available for comparison. These paths are relative to the shared `C:/work/GIT` directory.

## Code review findings

- The appearance guides rotate the sprite around its center and use native image pixels at scale 1. The room preview rotates around its top-left corner and uses the sprite's actual rendered dimensions when computing a corrective position.
- `RuntimeRenderablePlacementMath.ComputeIconLocalOffset` combines local center-pivot compensation with a footprint translation selected by *room rotation + local rotation*. For this elevator it adds (400, 400) twice. After the top-left 180° image rotation, the visible sprite is displaced (400, 400). Its early return when combined rotations equal 0° also deserves review because different pivots can still require a position adjustment.
- Runtime calls the same helper but supplies `grid cell size × scale` as the rendered image width and height. For this 40-pixel-grid, 400-pixel image, Designer and runtime therefore calculate different offsets. The portal maps the runtime draw position and rotates the actual texture around its top-left corner. Its visible result has not yet been checked in the portal.
- The earlier top-left-anchor simplification established the appearance editor's local layout as authoritative and aimed for one shared placement model. Its regression fixtures covered room-rotated doors and noncardinal local rotation, but not this large sprite with cardinal local rotation. The current Designer comparison snapshot prints the same shared value as both sides with a constant zero delta; door parity diagnostics report deltas without asserting visual agreement.

## Observation before a change

In Docks, use Manage Traversals to add a two-way North Hallway → Elevator Foyer pair (North outbound, South return). Choose "No door (open passage)" on both sides if this route should have no door; door choices are optional and independent. Save, export, and reload the portal. Then capture the saved local-180° case in both the Designer and portal: footprint and sprite bounds, selected image dimensions, scale, local offset, local rotation, room rotation, normalized runtime anchor/draw/offset, and portal texture bounds. Compare it with the room-180°/local-0° workaround without changing the saved test case permanently.

After both views are understood, define one explicit transform contract for footprint anchor, native image size, scale, local offset axes, local pivot, room rotation, and final draw position. Make the appearance guide, room preview, runtime normalization, and portal consume equivalent geometry. Test local and room rotations separately and together, rectangular assets and footprints, nonzero offsets, scaling, and existing door placements. A one-line cardinal correction is not sufficient evidence for that change.

## Current decision

Do not change image placement math, pivots, export metadata, or Docks content as part of the traversal phase. Resume this review after the Designer and web portal comparison.
