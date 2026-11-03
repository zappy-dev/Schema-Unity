## Unity Editor Guide

### Open the tools
- `Tools > Schema Editor`: Main data authoring UI.
- `Tools > Schema Debugger`: Inspect loaded state and debug diagnostics.

### Editing schemes
1. Select a scheme from the explorer.
2. Add/Edit attributes (columns). Mark one attribute as the Identifier if needed.
3. Add/Edit entries (rows). Use appropriate data types; tooltips help describe intent.

### Watching and reloading
- The editor listens for changes to the `Manifest` and reloads affected tables when changes are detected.

### Publishing
- Choose a scheme and publish.
- What happens:
  - A filtered copy of the scheme is written to `Assets/Plugins/Schema/Resources/<SchemeName>.json` (only attributes with `ShouldPublish`).
  - Strongly-typed C# wrappers are generated to the `CSharpExportPath` (default `Assets/Scripts/Schemes`).
- After publishing, Unity assets are refreshed automatically.
- A publish never leaves a row out. If a value is not valid for its column, the publish fails and the message names the scheme, row and column.
- Publish All sorts the schemes so that referenced ones come first. If schemes reference each other in a cycle, it stops and names them.

### Changing a column's type
- The conversion is all-or-nothing. If any cell cannot convert, nothing changes and the message lists the rows (by identifier) and values that failed.
- A blank cell stays blank in a Reference column (when it allows empty references), and becomes the type's default color in a Color column. It does not become the first row of the referenced scheme.

### Sending feedback
The Schema Editor can file a report about itself with a local [repairkit](https://github.com/zappy-dev/repairkit) daemon. It needs Unity 2021.2 or newer; on older Unity versions the code is not compiled and nothing appears.
- **Switch it on, once per machine.** Open **Edit > Preferences > Schema Feedback** (or the dialog below) and paste the path of the daemon's Unix socket, a file ending in `.sock`. The path is kept in this machine's EditorPrefs, so your project gains no files and nothing to commit. With no path set, **Send** explains this and sends nothing.
- **Send a report.** Press **Feedback** at the end of the Schema Editor toolbar, or choose **Tools > Schema > Send Feedback...**. The Schema Editor window is captured first, then a small dialog opens: write what is wrong (required), keep or untick the screenshot, tick **Include the open scheme's data** if the report needs the table, and press **Send** (Cmd/Ctrl+Enter). The dialog says "Sent. Report ..." with the report's id, or says in plain words what went wrong.
- **What goes with it.** `source: schema-editor`; the host project's path and name, the Unity version, the package version and git commit, the Manifest and Content paths, the open scheme and the cell that last had focus, and the last 50 Console lines that mention Schema (since the last domain reload). Reports are filed as "report only": the daemon holds any repair for review.
- **If the daemon is not running**, the report and its screenshot are kept in `~/.schema-unity/feedback-outbox/` (outside every project) and sent on your next Send, or when the Editor next loads. A screenshot over 2 MB is shrunk and sent as JPEG.
- **Capture notes.** The screenshot is rendered from the window's own view, so other windows on top of it never appear. If it cannot be taken (the window is not open, or the capture comes out black) the dialog says so and the report is sent without it.

### Tips
- Keep identifiers stable to avoid breaking references in code.
- Use namespaces in Manifest to organize generated C#.
- For large datasets, the editor provides virtualized table rendering for performance.


