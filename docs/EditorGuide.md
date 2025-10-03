## Unity Editor Guide

### Open the tools
- `Tools > Scheme Editor`: Main data authoring UI.
- `Tools > Scheme Debugger`: Inspect loaded state and debug diagnostics.

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

### Tips
- Keep identifiers stable to avoid breaking references in code.
- Use namespaces in Manifest to organize generated C#.
- For large datasets, the editor provides virtualized table rendering for performance.


