## Quickstart

### Prerequisites
- Unity 2021.3+ (LTS recommended)
- .NET SDK 8 (optional; for building and running tests)

### Install via Unity Package Manager (no download required)
1) Open your Unity project
2) Open `Window > Package Manager`
3) Click the `+` button (top-left) and choose `Add package from git URL...`
4) Paste this URL and click Add:
   `https://github.com/zappy-dev/Schema-Unity.git?path=/SchemaPlayground/Packages/com.devzappy.schema.unity#main`
5) Wait for the package to install. The Schema editor tools will be available under `Tools > Schema Editor`.

### Try it now (sample project)
If you cloned the repo:
- Open `SchemaPlayground/` in Unity
- Load `Assets/Scenes/SampleScene.unity`
- Open `Tools > Schema Editor` and inspect `Manifest`, `Entities`, and `Quests`
- Press Play; see `Assets/Scripts/Player2DController.cs` using Schema at runtime

### 1) Clone and open
```bash
git clone https://github.com/zappy-dev/Schema-Unity.git
cd Schema-Unity
```
Open `SchemaPlayground/` in Unity.

### 2) Explore the editor
- Open `Tools > Schema Editor`.
- Select the `Manifest` scheme to see how other schemes (e.g., `Entities`, `Quests`) are registered.

### 3) Edit data
- Add or modify entries in a scheme (rows) and attributes (columns).
- Use the attribute tooltips and types to guide valid inputs.

### 4) Publish and generate code
- In the editor, publish your scheme to Resources and generate C#.
- Generated C# appears under the configured `CSharpExportPath` (default: `Assets/Scripts/Schemes`).
- Published data is written under `Assets/Plugins/Schema/Resources/`.

### 5) Use at runtime
Initialize Schema **once** in your game startup:
```csharp
using Schema.Runtime;

void Awake()
{
    // Initialize Schema once your game starts, and check for any errors
    var init = SchemaRuntime.Initialize();
    if (init.Failed) Debug.LogError(init.Message);
}
```
Then query data via generated wrappers, for example:
```csharp
// Example: Manifest
var manifestRes = ExampleProject.Schemes.ManifestScheme.Get();
if (manifestRes.Try(out var manifest))
{
    var entryRes = ExampleProject.Schemes.ManifestScheme.GetEntry(ExampleProject.Schemes.ManifestScheme.Ids.ENTITIES);
    if (entryRes.Try(out var entry))
    {
        Debug.Log($"Entities path: {entry.FilePath}");
    }
}
```

### Next steps
- Read [Key Concepts](KeyConcepts.md).
- See [Editor Guide](EditorGuide.md) for workflows.
- See [Runtime Guide](RuntimeGuide.md) for loading and querying data.


