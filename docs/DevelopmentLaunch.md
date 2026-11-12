# Run a project in WebPortal

Save the project, then choose **Run Development WebPortal**. Designer validates and saves the authored project, exports its runtime files, starts a development GameHost, and opens its WebPortal client. Choose **Enable sound** or **Continue with sound disabled** in the Portal startup prompt before the session begins.

Use **Tools → Development WebPortal Setup** to set the development username, GameHost executable, and built WebPortal folder containing `index.html`. The username defaults to `dev`. Environment overrides are `STORYBOARD_GAMEHOST_EXECUTABLE_PATH` and `STORYBOARD_WEBPORTAL_ROOT`.

After an edit, choose **Run Development WebPortal** again. Each successful launch uses a fresh Host and session with the latest export. Designer stops its previous Host and removes its temporary registration. Closing Designer also stops its managed Host. Runtime output folders containing spaces are supported; see [Runtime export destination](RuntimeExportDestination.md) for destination settings.

If validation, export, Host readiness, or browser startup fails, check the warning and Designer output console. Correct the reported project or setup issue and run again.

Designer no longer offers **Run Simulator** or **Simulator Setup**. Existing replay fields and the old executable preference remain compatible saved data; they do not start replay or a WPF runtime. Existing recordings and preference files require no migration.

For launch regression checks, see [Deep UI smoke setup](../StoryboardDesigner.App/DEEP_UI_TESTS.md).
