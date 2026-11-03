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

### Tips
- Keep identifiers stable to avoid breaking references in code.
- Use namespaces in Manifest to organize generated C#.
- For large datasets, the editor provides virtualized table rendering for performance.


