# Deep UI Smoke Tests (Quick Run)

This project includes an opt-in deep UI smoke test for periodic simulator workflow coverage.

## What is opt-in

Deep UI tests are skipped by default.

The opt-in gate is controlled by this environment variable:

- `STORYBOARD_DESIGNER_RUN_DEEP_UI=1`

## Run default smoke suite (deep test skipped)

From repository root:

```powershell
dotnet test .\StoryboardDesigner.App.SmokeTests\StoryboardDesigner.App.SmokeTests.csproj -p:RestoreIgnoreFailedSources=true
```

## Run only deep UI test

From repository root (PowerShell):

```powershell
$env:STORYBOARD_DESIGNER_RUN_DEEP_UI='1'
dotnet test .\StoryboardDesigner.App.SmokeTests\StoryboardDesigner.App.SmokeTests.csproj --filter "FullyQualifiedName~DeepUiTraversalDoorWorkflowTests" -p:RestoreIgnoreFailedSources=true
Remove-Item Env:STORYBOARD_DESIGNER_RUN_DEEP_UI
```

From repository root (cmd.exe):

```bat
set STORYBOARD_DESIGNER_RUN_DEEP_UI=1
dotnet test .\StoryboardDesigner.App.SmokeTests\StoryboardDesigner.App.SmokeTests.csproj --filter "FullyQualifiedName~DeepUiTraversalDoorWorkflowTests" -p:RestoreIgnoreFailedSources=true
set STORYBOARD_DESIGNER_RUN_DEEP_UI=
```

## Expected behavior

- Default smoke run: deep test shows as skipped.
- Opt-in filtered run: deep test executes.

## Troubleshooting quick checks

- Verify app builds:

```powershell
dotnet build .\StoryboardDesigner.slnx -p:RestoreIgnoreFailedSources=true
```

- If deep test does not run, confirm the environment variable is set in the same shell session used to run `dotnet test`.
