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
- What happens, unless the project says otherwise (see below):
  - A filtered copy of the scheme is written to `Assets/Plugins/Schema/Resources/<SchemeName>.json` (only attributes with `ShouldPublish`).
  - Strongly-typed C# wrappers are generated to the `CSharpExportPath` (default `Assets/Scripts/Schemes`).
  - Then every publish step the project declares runs (see below).
- After publishing, Unity assets are refreshed automatically.
- A publish never leaves a row out. If a value is not valid for its column, the publish fails and the message names the scheme, row and column.
- Publish All sorts the schemes so that referenced ones come first. If schemes reference each other in a cycle, it stops and names them.

### Making Publish do what your project does
A project does not have to publish the way Schema publishes. It can add a step of its own to a publish, and switch
either built-in output off, so that Publish writes exactly what the project wants it to: a game can bake an authored
table straight into a ScriptableObject of its own and never ship a Resources JSON.

**A publish step.** Write a class that derives from `SchemaPublishStep` (or implements `ISchemaPublishStep`), in
`Schema.Core.Publish`. Nothing registers it: every such type in the project is found and run, once per scheme being
published, after the built-in outputs.

```csharp
using Schema.Core;
using Schema.Core.Publish;

public sealed class TuningBaker : SchemaPublishStep
{
    // Only this table. Leave it out to be called for every scheme.
    protected override string SchemeName => "MachineGuns";

    // Steps run from the lowest Order to the highest; the default is 0.
    public override int Order => 0;

    protected override SchemaResult PublishScheme(SchemaPublishRequest request)
    {
        // request.Scheme is the table as authored; request.Scheme.CreatePublishedCopy(request.Context)
        // is the filtered copy publishing would have written.
        // ... write TankTuning.asset ...
        return SchemaResult.Pass("Baked TankTuning.asset");
    }
}
```

A publish runs in the editor, so a step that writes assets belongs in an `Editor` folder (or an editor-only
assembly). `Schema.Core`, where the step types live, is auto-referenced, so an asmdef needs nothing added to it.

A step needs a public parameterless constructor. A step that returns a failed result, or throws, fails the publish
and is named in the message, so a bake that did not happen is never reported as published. A step that cannot be
built (abstract, generic, or wanting constructor arguments) is skipped with a warning naming it, and the rest of the
publish carries on.

**The switches.** **Project Settings > Schema Publish** has one toggle per built-in output:
- **Resources JSON** — write the published table to the scheme's publish target.
- **Generated C#** — generate the strongly-typed wrappers.

Both are on by default, so a project that does nothing keeps the behaviour it had. The same page lists the publish
steps found in the project, and warns when Publish would do nothing at all (both outputs off and no steps). The
setting is kept in the project's content folder as `Content/PublishSettings.json`, beside the Manifest, so it
travels with the project and is committed with it rather than being a per-machine preference. A settings file that
cannot be read fails the publish and names itself, rather than quietly writing files the project had switched off.

### Changing a column's type
- The conversion is all-or-nothing. If any cell cannot convert, nothing changes and the message lists the rows (by identifier) and values that failed.
- A blank cell stays blank in a Reference column (when it allows empty references), and becomes the type's default color in a Color column. It does not become the first row of the referenced scheme.

### Sending feedback
The Schema Editor can file a report about itself with a local [repairkit](https://github.com/zappy-dev/repairkit) daemon. It needs Unity 2021.2 or newer; on older Unity versions the code is not compiled and nothing appears.
- **The daemon.** For reports about Schema-Unity itself the daemon is this repository's own: `tools/repairkit.sh start`, socket `~/.schema-unity/repairkit/repairkit.sock`. Setup, the gate and launchd: [RepairKit-Daemon.md](RepairKit-Daemon.md).
- **Switch it on, once per machine.** With the daemon's socket in its usual place, `~/.schema-unity/repairkit/repairkit.sock`, the path is filled in for you and there is nothing to paste. For a socket anywhere else, open **Edit > Preferences > Schema Feedback** (or the dialog below) and give the path of the daemon's Unix socket, a file ending in `.sock`; a leading `~` is expanded for you, so a path copied out of the docs works as it reads. The path is kept in this machine's EditorPrefs, so your project gains no files and nothing to commit. With no path set, **Send** explains this and sends nothing; clearing the field switches feedback off and keeps it off.
- **Send a report.** Press **Feedback** at the end of the Schema Editor toolbar, or choose **Tools > Schema > Send Feedback...**. The Schema Editor window is captured first, then a small dialog opens: write what is wrong (required), keep or untick the screenshot, tick **Include the open scheme's data** if the report needs the table, and press **Send** (Cmd/Ctrl+Enter). The dialog says "Sent. Report ..." with the report's id, or says in plain words what went wrong.
- **What goes with it.** `source: schema-editor`; the host project's path and name, the Unity version, the package version and git commit, the Manifest and Content paths, the open scheme and the cell that last had focus, and the last 50 Console lines that mention Schema (since the last domain reload). The daemon starts a repair for each report; a repair that passes the gate is merged into the package's `main` on that machine, and nothing is pushed.
- **If the daemon is not running**, the report and its screenshot are kept in `~/.schema-unity/feedback-outbox/` (outside every project) and sent on your next Send, or when the Editor next loads. A screenshot over 2 MB is shrunk and sent as JPEG.
- **Capture notes.** The screenshot is rendered from the window's own view, so other windows on top of it never appear. The window is asked to draw itself just before the picture is taken. If it cannot be taken, or what comes back is not a picture of the window (black, all but black, or noise), the dialog says so and the report is sent without it.

### Tips
- Keep identifiers stable to avoid breaking references in code.
- Use namespaces in Manifest to organize generated C#.
- For large datasets, the editor provides virtualized table rendering for performance.


