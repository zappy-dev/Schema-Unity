# Conversion, publish and load safety fixes

These fixes came out of moving existing Text columns to Reference and Color columns in a project that already had data in them. Each one removes a case where Schema reported success, or only logged a line, while data was lost, changed meaning, or went missing. The numbers are the numbers of the findings in that project's report, kept here so the two lists can be matched; the last section maps each one to its commit.

Every entry has the same three parts: what was wrong, what changed, and how you notice the change.

## 3, 4, 16: `ConvertAttributeType` is all-or-nothing, and says where it failed

**What was wrong.** Converting a column wrote each converted cell as it went. When a later cell could not convert, the call returned a failure but the cells before it stayed converted, the column kept its old type, and the scheme was not marked dirty, so a save later wrote the converted values into a column of the old type. The failure named the column and the data type but not the row or the value. Converting a column that does not exist threw a `NullReferenceException`.

**What changed.** `DataScheme.ConvertAttributeType` converts every cell into a buffer first and writes only if all of them converted. On failure the cells, the column type, the column default and the dirty state are exactly as they were. The failure message names the scheme, the column, and each row that failed, with its identifier (or its position, counting from 1, when the scheme has no identifier column) and the value, up to ten rows and then a count. Converting an unknown column, or converting to a `null` type, returns a failed result.

**How you notice.** A conversion either happens completely or not at all. The message reads like `Cannot convert attribute Score of scheme 'Items' to type DataType: Integer, 2 of 5 rows failed: row 2 ('Item_2') value 'oops': ...`.

## 1, 2: a blank cell keeps its meaning when its column changes type

**What was wrong.** When a Text column was converted, a blank cell (empty, whitespace only, null, or no value at all) became the new type's default value without asking the type. For a Reference column the default is the first row of the referenced scheme, so a blank cell, such as the end of a chain of "next" links, silently became a link to the first row. For a Color column the default is the hex text `#000000`, so a blank cell became that string, which is not a `Color`: the scheme then failed validation, and publishing it dropped the row (issue 9).

**What changed.** `DataType` has a new virtual method, `ConvertEmptyValue`, which decides what a blank cell becomes. Its default implementation uses the type's default value if that is a valid value of the type, converts it first if it is held in another form, and otherwise fails, so that the conversion can name the row. `ReferenceDataType` overrides it: a blank cell stays blank (an empty string) when `SupportsEmptyReferences` is true, and fails with "Empty references are not allowed." when it is false. `ColorDataType` overrides it to return the type's default as a `Color`, and its `ConvertValue` returns the same for null, empty and whitespace input instead of the string `#000000`. A cell with no value at all is treated as blank.

**How you notice.** Converting a Text column that has blank cells to a Reference column leaves them blank. Converting it to a Color column gives black (or the color the type was created with), as a real `Color`, and the scheme validates. Converting blank cells to a type that does not allow them now fails and names the rows. That includes a file path or folder type created with `allowEmptyPath: false`, which used to keep an empty path that failed its own validation.

`ColorDataType.ConvertValue` of a blank value used to return the string `"#000000"`; three tests that asserted that string now assert `Color.black`.

This does not change the default that a Reference column gives to a *new* row: `new ReferenceDataType(scheme, attribute)` still defaults to the first row of a loaded scheme, and the column default is still set to it by a conversion.

## 6: the alpha of a color survives a save

**What was wrong.** A color with an alpha channel was accepted when it was converted (`#FF000080`) and kept in memory, but the JSON converter wrote only the red, green and blue digits. Saving and reloading made it opaque, and a fully transparent white (`#FFFFFF00`) came back as opaque white, with no message.

**What changed.** `ColorJsonConverter` writes `#RRGGBBAA` when the alpha, rounded to the 8 bits the text can hold, is below 255. A fully opaque color is still written as `#RRGGBB`, so files that only hold opaque colors do not change.

**How you notice.** A color column that holds alpha saves it. Reading a file back gives the same bytes, and saving it again writes the same text.

## 11: tables that reference each other no longer vanish at load

**What was wrong.** `DataScheme.TopologicalSortByReferences` detected a cycle between tables (`A` has a column that references `B` and `B` has one that references `A`), created a failure, threw it away, and returned a pass with the cyclic tables, and every table that depended on one, left out of the list. Loading a manifest then loaded the rest and reported `Loaded 8 schemes`. The tables simply were not there, in the editor or in the game.

**What changed.** `TopologicalSortByReferences` now returns a failed result whose message names the tables it could not place, and the tables each of them is waiting for: `Unable to topological sort the following schemes, check for cyclical dependencies or for a reference to a scheme that is not loaded: Items (references Zones), Zones (references Items)`. A table that references itself is not a cycle and still sorts. `Schema.LoadManifest` loads the tables that can be placed, and reports the others in its message with the status `FAILED_TO_LOAD_ENTRIES`, not `FULLY_LOADED`; it no longer prints `Loaded N schemes` for a manifest that lost some. The editor's Publish All, which sorts the same way, now stops with that message instead of publishing a subset.

**How you notice.** A project with a cycle between tables loads with a partial status that names the tables, instead of with some of them missing.

## 13: a bad value met at load makes the load partial

**What was wrong.** When a table was loaded and a cell was not valid for its column (a reference to a row that does not exist, text in an integer column) and could not be converted, `Schema.LoadDataScheme` logged one error and kept going. `Schema.LoadManifest` still returned `FULLY_LOADED`, although the status enum has `FAILED_TO_LOAD_ENTRIES` for exactly this. A game then failed later, when the cell was read.

**What changed.** `LoadManifest` collects every such cell while it loads the tables, and when there is any it returns the status `FAILED_TO_LOAD_ENTRIES` with a message that has one line per cell: `Scheme 'Items', row 2 ('Item_2'), attribute 'Home': value 'Zone_Typo' is not valid: ...`. The table is still loaded and the bad value is kept as it is, so that the table can be opened and fixed in the editor; the public `LoadDataScheme` keeps its signature and its result. `SchemaRuntime.Initialize` still returns a pass for a partial load, because the rest of the data can be used, but its message now begins `Schema Runtime loaded with errors:` and the same text is written with `Debug.LogError`. The CLI already printed the message for a status other than `FULLY_LOADED`.

**How you notice.** Check the status of `LoadManifest`: `FAILED_TO_LOAD_ENTRIES` means something in the data needs attention, and the message says what.

## 9: publishing fails when a row cannot be added

**What was wrong.** The editor built the table to publish by adding each row to a copy of the table, and ignored the result of adding it. A row that failed validation was simply missing from the published file, and the publish reported success. The editor's Publish validates the table first, which hides this, but any publish that skips that step (a script, or a copy of the publish code that does not call `IsValidScheme`) loses the row without a message.

**What changed.** Building the copy moved into Core as `DataScheme.CreatePublishedCopy(context)`, which the editor's `PublishToResources` now calls. It validates each value of the published columns and fails with the scheme, the row, the column and the value, instead of leaving the row out: `Cannot publish scheme 'Items': row 2 ('Item_2'), column 'Score': value 'not a number' is not valid: ...`. It does not change the table it copies. Code outside the package that has its own copy of the old publish loop keeps the old behavior until it calls `CreatePublishedCopy`.

**How you notice.** A publish either writes every row of the table or fails and says which row and column are the problem.

## 12: reading a blank or dangling reference without an exception

**What was wrong.** The generated property for a Reference column is `XScheme.GetEntry(...).Result`, and `Result` throws when the lookup fails. Reading the link of the last row of a chain threw, although the column allows blank references. There was no way to read a reference that did not throw.

**What changed.** Nothing that exists. Next to it, each generated entry wrapper gets a property `<Column>OrNull` for every Reference column, and each generated scheme class gets `GetEntryOrNull(id)`; both return `null` for a blank id and for an id that no entry has. The new property is not generated when the table already has a column with that name. `docs/RuntimeGuide.md` says when to use which. The existing property still throws, so code that relies on that does not change. A test compiles generated wrappers (a table that references another and one that references itself) and reads blank, dangling and good references through both.

**How you notice.** After you publish again, the generated `.g.cs` files have the new members. A project that already has its own member with one of those names (for instance a hand-written `HomeOrNull` in a partial class) gets a compile error until it removes its own.
