# Lighting Designer Implementation Handover

## Purpose

This is the focused implementation brief for adding lighting authoring to the
Designer. The canonical decisions remain in
[Lighting_Data_Contract_Designer_Handover.md](Lighting_Data_Contract_Designer_Handover.md).
The schemas in `Storyboard.Shared.Contracts/RuntimeContracts/Schemas` are the
contract source of truth.

The Designer work should author and persist lighting data. It should not emit
Host-resolved room coordinates or reconstruct Runtime-effective state.

## Current contract status

The following schema work is complete:

- Project-level point-light defaults.
- Room-level ambient lighting.
- Phase page/chapter ambient overlays.
- Object-authored point lights with local pixel offsets.
- Object light-occlusion settings.
- Runtime effective/per-instance lighting fields.
- Host resolved lighting fields and staged TypeScript generation.

The WebPortal does not consume generated TypeScript contracts yet.

## Designer contract surface

### Project authoring root

`ProjectAuthoringRootDto` now supports optional:

```text
pointLightDefaults: ProjectPointLightDefaultsDto?
```

Schema:

[ProjectPointLightDefaultsDto_contract.schema.json](../Storyboard.Shared.Contracts/RuntimeContracts/Schemas/DtoContracts/Designer/ProjectPointLightDefaultsDto_contract.schema.json)

These are Designer-controlled project fallbacks for individual point lights.
They are not room-level settings and are not expected to change during play.
If a project does not define them, the Lighting package defaults remain the
final fallback.

Supported defaults are:

- `radiusPx`
- `intensityScale`
- `color`
- `outerColor`
- `gradientExponent`
- `lightHeightCells` (authored light elevation offset; Runtime combines it with object elevation and height)
- `swayAmountPx`
- `swayHz`
- `swayDirectionDeg`
- `flickerAmount`
- `flickerHz`
- `flickerStyle`

### Room

`RoomDto` now supports optional:

```text
ambientLighting: ProjectRoomAmbientLightingDto?
```

Schema:

[ProjectRoomAmbientLightingDto_contract.schema.json](../Storyboard.Shared.Contracts/RuntimeContracts/Schemas/DtoContracts/Designer/ProjectRoomAmbientLightingDto_contract.schema.json)

The room owns the base ambient setting. If no room ambient value is authored,
Runtime must treat the base intensity as `1` (fully lit). The room ambient
object contains only:

- `ambient`: normalized `0..1` intensity.
- `ambientColor`: six-digit `#RRGGBB` color.

Room canvas width and height remain the existing room/project geometry fields.
Cell size remains project-wide and is not part of room lighting authoring.

### Phase nodes

`PhaseNodeDto` now supports optional:

```text
phaseAmbientOverlay: ProjectPhaseAmbientOverlayDto?
```

Schema:

[ProjectPhaseAmbientOverlayDto_contract.schema.json](../Storyboard.Shared.Contracts/RuntimeContracts/Schemas/DtoContracts/Designer/ProjectPhaseAmbientOverlayDto_contract.schema.json)

The overlay is authored at the phase-page level. Runtime uses the chapter
overlay when the page does not define one. It contains:

- `ambientScale`: normalized `0..2` multiplier.
- `color`: optional six-digit `#RRGGBB` color.
- `colorMode`: `Override` or `Blend`.
- `colorBlend`: normalized `0..1`, used for `Blend`.

Runtime composes the effective room state as:

```text
effectiveAmbient = clamp(roomAmbient * phaseAmbientScale, 0, 1)
```

The initial implementation does not modify individual point-light intensity
when the phase overlay changes.

### Game object authoring

`ProjectGameObjectDto` now supports the common object fields:

```text
pointLight: ProjectPointLightDto?
lightOcclusion: ProjectLightOcclusionDto?
isPointLightEnabledDefaultValue: bool
```

Schemas:

- [ProjectPointLightDto_contract.schema.json](../Storyboard.Shared.Contracts/RuntimeContracts/Schemas/DtoContracts/Designer/ProjectPointLightDto_contract.schema.json)
- [ProjectLightOcclusionDto_contract.schema.json](../Storyboard.Shared.Contracts/RuntimeContracts/Schemas/DtoContracts/Designer/ProjectLightOcclusionDto_contract.schema.json)

There is at most one object-owned point light in this version. Multiple lights
per object and standalone room lights are deferred.

The point-light definition contains:

- `offsetXPx`, `offsetYPx`: local pixel offset from the top-left footprint
  cell corner.
- `radiusPx`, `intensityScale`, `color`, `outerColor`, and `gradientExponent`.
- `directionDeg` and `coneAngleDeg`.
- `lightHeightCells` (authored light elevation offset in cells).
- `motionMode`, `phase`, sway fields, and flicker fields.

The local offset is not a sprite offset and is not an absolute room coordinate.
It is relative to the object’s logical top-left footprint cell. The light
follows the object when the object moves, stacks, or rotates. Runtime/Host owns
the later conversion to absolute room-space pixels.

For vertical resolution, Designer stores the authored light elevation offset,
not the final room-space light height. Runtime calculates the effective light
height as:

```text
heightInRoom + objectHeightUnits + lightElevationOffset
```

When `lightHeightCells` is omitted, the project point-light default supplies
the offset fallback.

Light occlusion contains:

```text
strength: normalized 0..1
```

For a solid object with height, Runtime should default the effective occlusion
to full strength (`1`). The Designer must allow the user to override that
value, including reducing it to zero.

## Recommended Designer UX

Lighting should be authored alongside the existing object appearance workflow:

1. Keep light placement in the existing footprint/image/alignment workspace.
2. Present `offsetXPx` and `offsetYPx` as the light’s position within the
   object footprint.
3. Provide a secondary lighting-details dialog for radius, colors, intensity,
   cone, elevation, sway, and flicker settings.
4. Keep project point-light defaults in project settings.
5. Keep room ambient settings in room settings.
6. Keep phase ambient overlays in the phase page/chapter settings.

The placement UI should make the logical origin explicit: the top-left cell of
the object footprint, not the sprite anchor or image bounds.

## Validation and default behavior

Designer validation should enforce:

- Colors are six-digit `#RRGGBB` strings.
- Normalized ambient, flicker, blend, and occlusion values remain in `0..1`.
- Phase ambient scale remains in `0..2`.
- Pixel offsets and pixel radii use pixel units.
- Cell-based heights use cell units.
- Angles use degrees.
- Frequencies use Hertz.
- Cone angles remain in `1..360` degrees.
- Negative radius, height, frequency, sway amount, or gradient values are
  rejected.
- `Blend` color mode uses `colorBlend`; `Override` does not require it.
- Occlusion is meaningful with the object’s existing footprint/height model.

Lighting is additive and optional. Existing project files and ordinary objects
must remain valid without any lighting fields. No migration should be required,
and the Designer should not synthesize point lights for objects that have none.

## Runtime handoff expectations

Designer export must preserve authored values without resolving them into Host
coordinates. Runtime is responsible for:

- Applying project defaults to omitted point-light properties.
- Applying page-over-chapter phase overlay fallback.
- Resolving room ambient effective state.
- Materializing copied objects from known authored bases.
- Maintaining independent per-instance state.
- Treating a disabled instance light as omitted from Host output later.

The Runtime contract types that receive the authored data are:

- `RuntimePointLightDto`
- `RuntimePointLightDefaultsDto`
- `RuntimeRoomAmbientLightingDto`
- `RuntimePhaseAmbientOverlayDto`
- `RuntimeLightOcclusionDto`
- `RuntimeGameObjectDto.effectivePointLight`
- `RuntimeGameObjectDto.effectiveLightOcclusion`
- `RuntimeGameObjectDto.isPointLightEnabled`

The Designer task should not implement Host DTO projection, absolute room
coordinate calculation, delta emission, or WebPortal rendering.

## Acceptance fixtures

Before handing work to the Runtime/engine session, add or verify fixtures for:

- A project with project-level point-light defaults.
- A project with no lighting settings.
- A room with ambient lighting.
- A room with no ambient lighting, falling back to intensity `1`.
- A phase page overlay.
- A page inheriting its chapter overlay.
- A point-light object with local pixel offset.
- A solid object with default full occlusion.
- An object with an overridden occlusion strength.
- An object with lighting omitted entirely.
- A copied object that preserves authored lighting data.
- Round-trip Designer serialization with older projects that contain no
  lighting fields.

## Completion criteria

Designer work is ready for Runtime integration when:

- The authoring UI can create, edit, and remove all Designer lighting fields.
- Saved Designer JSON round-trips without losing omitted versus explicit data.
- Existing projects load unchanged without migration.
- Validation and defaults match this document and the schemas.
- Exported Designer DTOs contain local offsets, not Host absolute coordinates.
- Fixtures cover the cases above.
