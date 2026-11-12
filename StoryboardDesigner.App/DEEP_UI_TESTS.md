# Deep UI Smoke Tests (Quick Run)

The smoke suite checks the surviving WebPortal launch/setup UI and includes an opt-in real producer/Host/Portal regression. See [launch instructions](../docs/DevelopmentLaunch.md).

## What is opt-in

Deep UI tests are skipped by default.

The opt-in gate is controlled by this environment variable:

- `STORYBOARD_DESIGNER_RUN_DEEP_UI=1`

The real launch test needs a built GameHost, built WebPortal distribution, Node.js, and an existing Playwright installation with Chromium. It reads the sibling Debug Host output, WebPortal `dist`, and WebPortal `node_modules/playwright` by default. Override those locations with `STORYBOARD_GAMEHOST_EXECUTABLE_PATH`, `STORYBOARD_WEBPORTAL_ROOT`, and `STORYBOARD_DESIGNER_PLAYWRIGHT_MODULE`. The test does not install packages or build/edit sibling source.

The test copies the Host to a temporary fixture and isolates Designer preferences. It edits real Designer models, invokes `RunDevelopmentCommand` to save/validate/export, launches the actual Host, and loads its launch URI in an isolated Chromium browser. It selects **Continue with sound disabled**, lets Portal create the session, sends `look`, and requires the latest authored echo message in the rendered output. Both fresh and legacy-replay projects launch twice. Checks cover paths with spaces, game/session identity, served assets, unchanged durable registration, and old Host/registration cleanup. The browser adapter is test-only; the application continues to use the system browser.

Screenshots and session identities are saved under `StoryboardDesigner.App.SmokeTests/TestResults/retirement-browser/<run-id>`. The test output records fixture paths and artifact hashes. Missing dependencies or failed scenarios fail the opt-in test; they are never counted as a successful skipped run.

## Run default smoke suite (deep test skipped)

From repository root:

```powershell
dotnet build .\StoryboardDesigner.App\StoryboardDesigner.App.csproj
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

For the focused launch/setup UI checks:

```powershell
dotnet test .\StoryboardDesigner.App.SmokeTests\StoryboardDesigner.App.SmokeTests.csproj --filter "FullyQualifiedName~Bite2GameSimulatorTabInteractionSmokeTests|FullyQualifiedName~Bite3SimulatorHappyPathSmokeTests" -p:RestoreIgnoreFailedSources=true
```

The historical class names above are retained for filter compatibility. Their assertions verify actual menu absence, setup dialog reachability, and the unsaved-project WebPortal warning.

## Troubleshooting quick checks

- Verify app builds:

```powershell
dotnet build .\StoryboardDesigner.slnx -p:RestoreIgnoreFailedSources=true
```

- If deep test does not run, confirm the environment variable is set in the same shell session used to run `dotnet test`.
- If a previously restored smoke output has a missing-method error against Contracts, run `dotnet restore .\StoryboardDesigner.App.SmokeTests\StoryboardDesigner.App.SmokeTests.csproj --force-evaluate` and rebuild. Preserve all floating package ranges and prerelease selection.
