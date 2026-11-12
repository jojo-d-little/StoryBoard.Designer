# Runtime export destination

Projects may set the optional `runtimeExportDirectory` authoring property to choose where Designer writes runtime output. The expression is resolved relative to the folder containing the `.sbe.json` project file, independent of the current working directory. Relative paths are preferred; Foundation path expressions also support absolute paths and `%NAME%` environment variables.

An empty or missing value keeps the existing `GameRuntimeJson` folder. A configured value names the final output folder directly. Designer saves the portable expression in the authoring project and never writes it into runtime JSON or UI state.

The Global Settings dialog shows the expression and its resolved folder. The folder picker stores a relative expression when Windows can represent the selected folder relative to the project. Changing or clearing the setting changes future exports and does not move or delete prior output.

Configured output must be separate from the authoring project tree and from other projects' detected output folders. This protects source content and prevents export cleanup from touching another project's files. Source image paths continue to resolve from the authoring project; previews use source images first and the current export as fallback.
