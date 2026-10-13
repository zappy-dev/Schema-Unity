# What the generator writes, and from what

Read when a generated name surprises you, a Publish fails on an id or a column name, or a column has an
unusual type. The generator is `CSharpSchemeStorageFormat.Serialize` in the package
(`Core/Serialization/CSharpSchemeStorageFormat.cs`; in a project it is under `Library/PackageCache/com.devzappy.schema.unity@*/`).
Everything here is **read** at commit `e9089e0` unless it says otherwise.

## One `.g.cs` per table

From the table's Manifest row: the file goes to `CSharpExportPath`, in namespace `CSharpNamespace` (none if
blank), and has an `Ids` class only when `CSharpGenerateIds` is true and the table has an identifier column.

| Emitted | From |
|---|---|
| `class XScheme : SchemeWrapper<XEntry>` with `SCHEMA_NAME`, `Get()`, `GetEntry(object id)` | the table's name, spaces removed, PascalCased |
| `class XEntry : EntryWrapper`, one property per column | columns whose `ShouldPublish` is true |
| `/// <summary>` on a property | the column's tooltip; empty gives `Gets the value of 'X'.` |
| `Ids.NAME = "value"` | each value of the identifier column |

Both classes are `partial`. The whole file is rewritten on every Publish.

## Property names and types

A property is the column name with spaces removed and PascalCased; `_`, `-` and `.` also split words, so
`Fuel_Burn` becomes `FuelBurn` and `ID` stays `ID`. Two columns that PascalCase alike do not compile.

| Column type (name in the editor) | C# property type | Read as |
|---|---|---|
| Text (shown as `String`), FilePath, Folder | `System.String` | `GetDataAsString`, `""` when absent |
| Integer | `System.Int32` | `GetDataAsInt`, `0` when absent |
| Float | `System.Single` | `GetDataAsFloat`, `0` when absent |
| Boolean | `System.Boolean` | `GetDataAsBool`, `false` when absent |
| Color (plugin type) | `UnityEngine.Color` | `GetDataAsColor("X").Result` |
| Unity Asset (plugin type) | the asset's Unity type | `GetDataAsObject<T>("X").Result`, through `UnityAssetLinker` |
| Reference | the referenced table's `YEntry` | `YScheme.GetEntry(<id read>).Result` |
| List | `System.Collections.Generic.List<T>` | `GetDataAsList<T>` |

The docs say "Number" for what the editor offers as Integer and Float. `.Result` on a failed result throws: a
Color cell that is not a `Color`, or a Reference that is blank, dangling, or to a table that is not loaded
(see [[column-types]]).

## Ids

One `const string` per identifier value, named from the value with ` ` and `-` turned to `_`, `#` to `POUND`, commas
dropped, then upper-cased: `Port_Harbor` becomes `PORT_HARBOR`. A name that is still not a C# identifier
(starts with a digit, holds `.` or `/`) makes Publish fail with `Cannot generate identifier for ...`; rename the
row, or turn `CSharpGenerateIds` off for the table. Rows appear in table order.

## Where to look in the package

| To learn | Read |
|---|---|
| what a type emits | `Core/Data/Type/<Type>.cs`: `GetDataMethod`, `CSDataType`, `IsValidValue` |
| colour and asset columns | `Runtime/Type/ColorDataType.cs`, `UnityAssetDataType.cs`, `UnityAssetLinker.cs` |
| what Publish does | `Editor/UI/SchemaEditorWindow.Publish.cs`, `PublishScheme` |
| how rows are looked up | `Core/Schemes/SchemeWrapper.cs`, `GetEntryById` |
