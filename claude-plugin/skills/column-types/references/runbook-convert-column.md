# Runbook: change a Text column to Reference or Color by editing its type block

Use after [SKILL.md](../SKILL.md) Phase 2 ended with exit 0 or 2. Package 0.3.x, checked at `e9089e0`.
The blocks below are the shapes the package itself writes: the Color block is in the playground project's
`Content/Entities.json`, the Reference block is the one the converter saved in a nine-column migration (ran), and
its properties are in `Core/Data/Type/ReferenceDataType.cs` (read). Editing them by hand is not something that
migration did, so step 6 is the proof.

1. **Commit** the table's three files. Close the table in the Schema editor so it holds no unsaved edit.
2. **Open the staging file** of the table that owns the column (`Content/<Table>.json`). In `Attributes`, find the column.
3. **Replace its `DataType` block.** A link, to the identifier column `ID` of table `Ports`:

   ```json
   "DataType": {
     "$type": "Schema.Core.Data.ReferenceDataType, Schema.Core",
     "ReferenceSchemeName": "Ports",
     "ReferenceAttributeName": "ID",
     "SupportsEmptyReferences": true,
     "DefaultValue": ""
   },
   ```

   A colour:

   ```json
   "DataType": {
     "$type": "Schema.Runtime.Type.ColorDataType, Schema.Runtime",
     "DefaultValue": "#FFFFFF"
   },
   ```

   Set the column's own `"DefaultValue"` to match (`""` for a link, so a new row starts unlinked; `"#FFFFFF"` for a colour).
   Leave every cell as it is. For a colour with blank cells, write the colour the old code drew for a blank (usually white) into those cells, and say so.
4. **Reopen the Schema editor** so it loads the file from disk (restart Unity if it still shows the old type). Open the table.
5. **Publish the target table first if it changed, then this table.** Publish validates every cell and refuses a table with a
   bad one (read; its message may not name the cell, which is why the preflight comes first), so a typo that slipped through stops here. Then Publish the Manifest only if you added a table.
6. **Prove nothing else moved**: `git show HEAD:Content/<Table>.json > old.json`, then
   `python3 <skill-dir>/scripts/preflight.py cells old.json Content/<Table>.json` prints `no cell changed`. `git diff` on the
   staging file shows the type block, the default and nothing else; the published JSON differs by the same block; the `.g.cs` property type changed.
7. **Fix the code the compiler names.** For a Reference, the readers of the column no longer compile; use the helpers in Phase 4.
8. **Publish again.** No file changes. Commit the three files.

If a step fails, stop: `git restore` the files from step 1, and report the step and its message.
