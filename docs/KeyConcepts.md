## Key Concepts

### Manifest
The `Manifest` scheme lists all other schemes and their settings (publish targets, C# export path, namespace, and whether to generate IDs). It is also published to Resources and used by the runtime to discover and load schemes.

Key fields:
- `SchemeName` (identifier)
- `FilePath` (staging file path under `Content/`)
- `PublishTarget`, `CSharpExportPath`, `CSharpNamespace`, `CSharpGenerateIds`

### Scheme
A table-like definition with a unique `SchemeName`, a set of `Attributes` (columns), and `Entries` (rows). A scheme can optionally designate one attribute as the identifier.

### Attribute
A column definition with a `DataType`, optional identifier flag, and `ShouldPublish`. Common types include:
- Text, Number, Boolean
- FilePath, Folder
- Color (Unity runtime plugin - hex color codes like #FF0000 or #FF0000AA)
- Custom types can be added in code

### Entry
A row of data in a scheme. At publish time, only attributes with `ShouldPublish` are included in the runtime data and code generation.

### Publishing
Publishing writes a sanitized version of a scheme to `Assets/Plugins/Schema/Resources/` and generates C# wrappers into `Assets/Scripts/Schemes/` (configurable via Manifest). The runtime loads from `Resources` using `SchemaRuntime.Initialize()`.

### Storage and File Systems
Schema abstracts IO via an `IFileSystem`. In-editor it uses a local filesystem; at runtime it reads from Unity `Resources` via `TextAssetResourcesFileSystem`.


