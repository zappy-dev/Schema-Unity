## FAQ & Troubleshooting

### Which Unity versions are supported?
Unity 2021.3 LTS and newer are recommended.

### Do I need the .NET SDK?
Only if you want to build the core libraries or run tests outside Unity. Unity usage alone does not require the .NET SDK.

### Where are my generated C# files?
Under the `CSharpExportPath` configured in the Manifest (default `Assets/Scripts/Schemes`).

### Where is runtime data stored?
Published JSON is placed under `Assets/Plugins/Schema/Resources/`. The runtime loads from `Resources` using `SchemaRuntime`.

### How do I add a new scheme?
Create a new entry in the `Manifest` and a corresponding content file under `Content/`. Then open the editor to define attributes and entries.

### I changed an attribute name; code no longer compiles
Generated C# reflects attribute names. If you rename, re-publish to regenerate wrappers and update usages in your code.

### Can I add custom data types?
Yes. Implement new data types in `Schema.Core.Data` and update editor behaviors as needed. You can also register types via the Plugins Data Type mechanism by calling `DataType.AddPluginType(...)`. The Unity Runtime demonstrates this with a `UnityAssetDataType` plugin that adds a Unity Asset data type (e.g., for Images/Textures) and registers it on load.

### How can I contact support?
Email support@devzappy.com.


