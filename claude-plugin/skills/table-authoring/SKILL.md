---
name: table-authoring
description: Change a table managed by the Schema package (com.devzappy.schema.unity: staging files under Content/, a Manifest, generated *Scheme.g.cs wrappers) and keep its copies in step -- add a column, row or whole new table, publish it, and use the new value from game code. Use when a feature needs a new tunable number, flag or row that designers will edit in a table, when asked to add or edit rows or columns in the table editor or Content/*.json, when a constant or property for a row or column is "missing" or the compiler says an Entry or Ids class "does not contain a definition" (CS1061) after the table was saved, when a *Scheme.g.cs or Assets/Plugins/Schema/Resources file appears in a diff or someone wants to edit it, when a new table works in the editor but not in a build, or when asked what the Manifest, Publish or UnityAssetLinker.asset is. Not for Unity Localization string tables, SQL or other databases, or config that has no Manifest.
metadata:
  kind: runbook
---

# Changing a Schema table

**A table is data in three copies, and game code reads only the third, as generated C#.** You change the
first, Publish writes the other two, and all three are committed. Nobody hand-edits the second or third:
the next Publish rewrites them (read).

Describes package `com.devzappy.schema.unity` 0.3.x, checked at 0.3.1 plus commit `e9089e0`.
**ran** = executed against that commit by a project using Schema (2026-10-12); **read** = read in the package
source at that commit. Problems named here are open at that pin: check the package changelog first.
Background: [Key Concepts](https://github.com/zappy-dev/Schema-Unity/blob/v0.3.1/docs/KeyConcepts.md),
[Editor Guide](https://github.com/zappy-dev/Schema-Unity/blob/v0.3.1/docs/EditorGuide.md).

| Copy | Default place | Written by |
|---|---|---|
| staging | `Content/<Table>.json`, the Manifest row's `FilePath` | the editor's Save; a hand edit is read on load |
| published | `Assets/Plugins/Schema/Resources/<Table>.json`, only columns with `ShouldPublish` | Publish (read) |
| generated | `<CSharpExportPath>/<Table>Scheme.g.cs`, default `Assets/Scripts/Schemes`, in `CSharpNamespace` | Publish (read) |

These are defaults. Read the project's staging Manifest (`Content/Manifest.json`) first: one row per table
with `FilePath`, `PublishTarget`, `CSharpExportPath`, `CSharpNamespace`, `CSharpGenerateIds`.

**Publishing is done in the editor.** 0.3.x has no publish command; `Schema.CLI` only lists and views tables
(read). Without Unity open you can edit the staging file and say what is left to publish; you cannot
regenerate anything. If the project has its own headless export script, use that instead and follow its skill.

## Add a column or a row

1. **Edit the staging copy**: in `Tools > Schema Editor`, or by hand in `Content/<Table>.json`. For a column, copy
   the block of an existing column of the same type and change `AttributeName`, tooltip and default; the
   tooltip becomes the property's doc comment. A row you add by hand shows up only after the next Publish (ran).
   A column missing from a row's data reads as `0`, `""` or `false`, never an error (ran), so rows need not carry it.
2. **Publish the table** from the editor. It validates the table first and stops on a bad cell (read), then
   writes the published JSON and the `.g.cs`, and creates `UnityAssetLinker.asset` if there is none (read).
3. **Check the diff**: for that table the staging file, the published JSON and the `.g.cs` changed, and
   nothing else (plus `UnityAssetLinker.asset` and its `.meta` the first time). The `.g.cs` is rewritten whole
   on every Publish, so a line that moved which you did not expect means the table changed (read).
4. **Only now write the code that uses it.** Before the Publish the property or `Ids` constant does not exist:
   `error CS1061: 'ShipsEntry' does not contain a definition for 'FuelBurn'`. In a headless
   `-executeMethod` run that error aborts the run (ran), so a reader written first blocks the export that
   would have generated what it needs. If you cannot publish, make the staging change, say the code
   compiles only after it, and leave the reader for after.
5. **Commit** the three files of each table you published, with any new `.meta`.

## A new table

1. Create it in the editor (it adds a Manifest row), or by hand: a staging file and a row in
   `Content/Manifest.json` with `SchemeName`, `FilePath`, `PublishTarget` `DEFAULT`, `CSharpExportPath`,
   `CSharpNamespace`, `CSharpGenerateIds`. Mark one text column as the identifier (read: `Ids` needs it).
2. Publish the table **and the Manifest**. Only `SchemeName` and `FilePath` of a Manifest row are published, and
   `SchemaRuntime.Initialize()` finds tables from that published copy, so a table missing from
   `Assets/Plugins/Schema/Resources/Manifest.json` does not load in a build (read).
3. Check `Resources/Manifest.json` lists the table. Then step 3 to 5 above.

## A constant or property is missing

Work down this list and stop at the first yes.

1. The row or column is in the staging file but has not been published (the published JSON and `.g.cs` lack it): Publish.
2. It was published in another checkout and that copy of the `.g.cs` is uncommitted: pull or commit it there.
3. The row is not in this checkout's staging file.
4. The identifier is not a valid C# name once sanitised: spaces and `-` become `_`, `#` becomes `POUND`, commas go,
   upper-cased, then it must start with a letter or `_` and hold letters, digits and `_`. Publish fails with
   `Cannot generate identifier for ...` (read). Rename the row, or set `CSharpGenerateIds` false for that table.
5. Two ids that upper-case alike, or two columns that PascalCase alike, are written twice and do not compile (read).
6. The table has no identifier column, or `CSharpGenerateIds` is false: there is no `Ids` class (read).
7. The identifier column is Integer or Float: a value such as `5` cannot be a C# name, so Publish fails with `Cannot generate identifier` (read). Keep identifiers Text.

Names, types and the other generator rules: [references/codegen.md](references/codegen.md).

## `UnityAssetLinker.asset`

A ScriptableObject in `Assets/Plugins/Schema/Resources` that maps the GUIDs of Unity Asset columns (sprites,
prefabs) to asset paths. Publish creates one when none exists, even if no table has such a column (read).
Commit it with its `.meta`: uncommitted, each fresh checkout that publishes mints another with a new GUID (ran).
Deleting it is harmless until the next Publish recreates it.

## Do not

- Hand-edit a `.g.cs` or a published JSON to "make it build": say what to Publish instead.
- Add members to a `.g.cs`. The generated classes are `partial`; put extra members in your own file (read).
- Read a table by column name from game code (`GetDataAsString("Col")`): the typed wrapper is what fails to
  compile when a column is renamed. Reading is in [[table-reading]]; changing a column's type is [[column-types]].
- Hand-edit a staging file while that table has unsaved edits open in the editor: its next Save can overwrite you (unverified).

## Related

[[table-reading]], [[column-types]]
