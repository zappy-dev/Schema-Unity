# Known issues in the Schema package, open at 0.3.1 + e9089e0

Found while converting nine Text columns to Reference and Color columns in a Unity game project, and
reproduced on scratch copies of its tables (2026-10-12). **ran** = executed against commit `e9089e0`;
**read** = read in the package source. These describe that pin. Some may be fixed in a later release: check
`package.json` and the changelog, then re-run a repro before dropping a workaround. `T` below is a table with
identifier column `ID`; a "Text column" is one with the Text type.

Severity: **data loss**, **crash**, **wrong silently**, **annoyance**.

| # | What happens | Repro | Severity | Workaround |
|---|---|---|---|---|
| 1 | A blank cell converted to a Reference becomes the target's first row, reported as a pass. A new row from the editor's "+" starts linked to the first row the same way (ran) | Text cells `a`, ``, `b`; target ids `a`, `b`; Convert Type to Reference | wrong silently | Route B (SKILL.md, Phase 3); default `""` |
| 2 | A blank cell converted to Color becomes the string `#000000`, not a Color; Publish then refuses the table, and a script that publishes without validation drops the row (ran) | Text column with a blank; Convert Type to Color | wrong silently | write the intended colour into blanks first |
| 3 | The conversion is not atomic. A bad cell leaves earlier cells converted, the column still Text, the table not dirty (ran) | cells `#ff9a9a`, `notacolor`; Convert Type to Color | wrong silently | preflight; commit first; `git restore` |
| 4 | A failed conversion names no row, only `Value does not exist as an identifier`; nothing is logged unless `SCHEMA_DEBUG` is defined (ran) | any bad reference above | annoyance | the preflight names it |
| 5 | No dry run, no report, no undo: the conversion is not a command the editor can undo (read) | | annoyance | commit before |
| 6 | Alpha is accepted, then dropped on save; `#FFF0` is saved as opaque `#FFFFFF` (ran; `ColorJsonConverter` writes `#RRGGBB`, read) | convert `#FF000080` | data loss | keep `#RRGGBB` |
| 7 | A colour name Unity parses (`red`) is refused, because the converter puts a `#` before everything without one (ran) | cell `red` | annoyance | write `#RRGGBB` |
| 8 | The Color default is a string, so a new row is invalid until saved and reloaded; adding a row with hex text fails "Value is not a valid color" (ran) | convert, then add a row | annoyance | Route B |
| 9 | A Resources publish that ignores a row it cannot add drops it with a pass; the editor's Publish validates first, a script that copies its code may not (read) | issue 2, then publish unvalidated: 12 of 13 rows | data loss | always validate |
| 10 | Duplicate ids in the target convert and load; a link resolves to the first row (ran) | two rows with id `x` | wrong silently | the preflight reports duplicates |
| 11 | A reference cycle between tables drops those tables and everything that depends on them, and the load still says "Loaded N schemes" (ran) | A links to B and B links to A | wrong silently | never link tables in a cycle; self-links are fine |
| 12 | The generated property of a Reference throws on a blank or dangling link; the Color property throws `Color not found` for a non-Color (ran) | read `NextPort` of the last row | crash | helpers in the [skill](../SKILL.md), Phase 4 |
| 13 | A bad value at load is one log line (the table, the row id and the row), and the load still reports success (ran) | dangling id in a published table | wrong silently | check the log at start |
| 14 | Reference validation is quadratic: 51 rows 1-2 ms, 500 rows 65-120 ms, 2,000 rows 1-2 s, 5,000 rows 6-11 s, for each of convert, validate and load (ran) | self-chained table | annoyance | keep link tables small |
| 15 | The generated Reference getter costs about 120 times a plain read and allocates (ran) | 100,000 reads on 51 rows: 0.6-0.7 s, 16-17 gen0 collections | annoyance | read at load |
| 16 | Converting an unknown column throws `NullReferenceException` (ran; `attributes.Find` unchecked, read) | | crash | |
| 17 | Running Convert Type again on a converted column resets the column default to the first row (ran) | menu twice | annoyance | skip converted columns |
| 18 | Publish creates `UnityAssetLinker.asset` even when no column links a Unity asset (ran) | publish any table | annoyance | commit it |
| 19 | `GetDataAsString` on a Color cell returns `Color.ToString()` (`RGBA(...)`), so a text reader compiles and silently fails to parse (ran) | | wrong silently | typed property only |
| 20 | The wrapper of a table with a Reference cannot be generated until the target table is loaded; "Reference scheme does not exist" (ran) | publish with the target unloaded | crash | publish in reference order |
| 21 | Adding a row that links into a table not yet loaded fails validation, even to a row already added: the context scope clears the scheme on dispose instead of restoring it (ran; cause read) | new table, self-link, add `B` then `A` linking to `B` | annoyance | add rows unvalidated, validate at publish |
