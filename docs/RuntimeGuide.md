## Runtime Guide

### Initialization
Call once during startup (e.g., in a bootstrap MonoBehaviour):
```csharp
using Schema.Runtime;

void Awake()
{
    var init = SchemaRuntime.Initialize();
    if (init.Failed)
    {
        UnityEngine.Debug.LogError(init.Message);
    }
}
```

This configures Schema to use a Resources-backed filesystem and loads the `Manifest` and listed schemes.

### Listening for manifest changes and reloading data
The sample `Player2DController` subscribes to manifest updates and reloads entries after initialization or when the manifest changes:

```csharp
using ExampleProject.Schemes;
using Schema.Runtime;
using UnityEngine;

public class Player2DController : MonoBehaviour
{
    [SerializeField]
    private EntitiesEntry playerEntry;

    void Start()
    {
        // Subscribe to manifest changes and initialize Schema
        Schema.Core.Schema.ManifestUpdated += OnManifestUpdated;
        var loadRes = SchemaRuntime.Initialize();
        if (loadRes.Failed)
        {
            Debug.LogError(loadRes.Message);
        }
    }

    private void OnManifestUpdated()
    {
        if (!EntitiesScheme.GetEntry(EntitiesScheme.Ids.PLAYER).Try(out var player, out var err))
        {
            Debug.LogError(err.Message);
            return;
        }
        playerEntry = player;
    }
}
```

This ensures runtime data is refreshed both on initial load and whenever the manifest is updated.

### Accessing data
Use generated wrappers for type-safe access:
```csharp
var res = ExampleProject.Schemes.EntitiesScheme.Get();
if (res.Try(out var entities))
{
    // By identifier
    var entryRes = ExampleProject.Schemes.EntitiesScheme.GetEntry(ExampleProject.Schemes.EntitiesScheme.Ids.PLAYER);
    if (entryRes.Try(out var player))
    {
        UnityEngine.Debug.Log($"Player HP: {player.HitPoints}");
    }
}
```

### Updating content
At runtime, content is read-only from Resources. Update data via the editor, republish, and rebuild the player.

### Troubleshooting
- Ensure `Manifest.json` is present under `Assets/Plugins/Schema/Resources/`.
- If wrappers are missing, re-run Publish and check `CSharpExportPath` in Manifest.
- Conflicting namespaces or invalid identifiers will cause codegen errors; fix names in the editor.


