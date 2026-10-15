# Traversal-aware room spawn points — Designer handoff

## Scope and implementation

Implemented in the Designer repository against `Storyboard.Contracts 1.0.15-local.20261015.t002602798`. Package references remain floating; neither Contracts nor GameEngine source was changed.

- Room models now hold zero or more spawn points with default status, traversal connection identity, coordinates, optional render Z order, and optional facing. A default spawn is optional; the Room Designer render-order panel and Room Settings dialog both let producers opt in or remove it.
- Missing traversal arrivals are created when a room editor opens and before project save or Runtime export. Cardinal and diagonal arrivals start near their entry wall/corner, up/down arrivals start at the room center, and generated points start at the top of the room draw order. Existing positions and point identities are retained.
- Room Designer displays compact, centered white direction letters on traversal markers from the current room toward the source room; hover text identifies the connected rooms. Markers can be dragged. The selected marker supports coordinate, facing, and draw-order editing using the room object list.
- Project authoring save/load and Runtime export preserve spawn point fields. Legacy room sidecars without the plural `spawnPoints` field migrate an old singular `spawnPoint` when present; when neither field exists, the room has no spawn point. Previously generated defaults recorded in the Designer metadata sidecar are removed during load. Room templates export only an explicitly opted-in default point, with no traversal ID.
- Designer-only generated-position baselines are stored in `DesignerMetadata/RoomSpawnPointGeneration.json`. On a later save/open, unchanged generated points are recalculated after room-size or traversal-direction changes; coordinate edits clear that marker and remain fixed.
- Runtime directed room links now include the stable traversal ID. Bidirectional links share the ID; one-way exports contain only the permitted directed link.
- Area validation checks that there is at most one default, plus unknown IDs, wrong rooms, invalid one-way direction, duplicates, and missing incoming traversal points. It runs as an Area candidate in normal Designer validation and before save/Runtime export.

## Changed files

- `StoryboardDesigner.App/Models/Rooms/Room.cs`
- `StoryboardDesigner.App/Models/Rooms/RoomSpawnPoint.cs`
- `StoryboardDesigner.App/Services/RoomSpawnPointAuthoringService.cs`
- Project-side `DesignerMetadata/RoomSpawnPointGeneration.json` serialization in `JsonExportService` (generated per authored project)
- `StoryboardDesigner.App/Services/JsonExportService.cs`
- `StoryboardDesigner.App/Features/RoomDesigner/ViewModels/RoomDesignerTabViewModel.cs`
- `StoryboardDesigner.App/Features/RoomDesigner/ViewModels/RoomDesignerRenderOrderItemViewModel.cs`
- `StoryboardDesigner.App/Features/RoomDesigner/Controls/RoomDesignerOverlayPreviewControl.xaml`
- `StoryboardDesigner.App/Features/RoomDesigner/Controls/RoomDesignerOverlayPreviewControl.xaml.cs`
- `StoryboardDesigner.App/Features/RoomDesigner/Views/RoomDesignerWorkspaceView.xaml`
- `StoryboardDesigner.App/Views/RoomSettingsDialog.xaml`
- `StoryboardDesigner.App/Views/RoomSettingsDialog.xaml.cs`
- `StoryboardDesigner.App/ViewModels/MainWindowViewModel.ProjectExplorer.cs`
- `StoryboardDesigner.App/ViewModels/MainWindowViewModel.cs`
- `StoryboardDesigner.App/ViewModels/MainWindowViewModel.FileCommands.cs`
- `StoryboardDesigner.App/Validation/Rules/Project/RoomSpawnPointTraversalIntegrityRule.cs`
- `StoryboardDesigner.App.Tests/RoomSpawnPointTraversalTests.cs`
- `StoryboardDesigner.App.Tests/RoomDesignerTabViewModelSpawnPointTests.cs`
- `StoryboardDesigner.App.Tests/SpatialAudioAuthoringPersistenceTests.cs` (spawn-point assertion updated for the new contract shape)
- Runtime export snapshot baselines under `StoryboardDesigner.App.Tests/Snapshots/RuntimeExportV1/`

## Validation and test results

- Validation runs from `MainWindowViewModel.FileCommands.CreateValidationRuleRegistry` for Area candidates and from `JsonExportService.BuildTraversalValidationIssues` before save and Runtime export.
- Build: `dotnet build StoryboardDesigner.App/StoryboardDesigner.App.csproj --no-restore` succeeded.
- Focused spawn-point, editor, and persistence tests after the optional-default revision: 23 passed.
- Legacy project load now maps a missing spawn-point field to an empty collection; an old singular point still migrates as an opted-in default.
- Full Designer test suite: last run reported 957 passed and 21 failed (978 total). Runtime sample tests fail against the available GameEngine package’s older `IHostRuntimeCommandProcessorClient.GetCurrentPresentation` signature and older Runtime sample payloads. Five unrelated WPF/window tests also fail in the full-suite run; the traversal editor test passed when run alone.

## Representative Runtime export

For a bidirectional traversal `T` from room `A` to `B`, the Area links use the same ID with direction carried by endpoints:

```json
"links": [
  { "fromRoomId": "A", "toRoomId": "B", "traversalConnectionId": "T" },
  { "fromRoomId": "B", "toRoomId": "A", "traversalConnectionId": "T" }
]
```

Room `B` may export an opted-in default point and its traversal arrival independently:

```json
"spawnPoints": [
  { "x": 400, "y": 300, "isDefault": true, "renderZOrder": 1001 },
  { "x": 91, "y": 101, "isDefault": false, "traversalConnectionId": "T", "renderZOrder": 10, "facingOverride": "North" }
]
```

## Remaining integration needs

- GameEngine needs a package refresh that understands `RuntimeRoomDto.SpawnPoints`, traversal-associated arrivals, and required `RuntimeRoomLinkDto.TraversalConnectionId`. Existing checked-in Runtime sample exports need regeneration with those fields.
- No Designer implementation follow-up remains for generated-position updates; the baseline marker is stored in a Designer-owned sidecar so Contracts remains unchanged.
- GameEngine integration and the checked-in Runtime sample refresh remain outstanding.
