---
name: column-types
description: Change what a Schema table column holds -- above all turn a Text column that holds another row's ID into a Reference column (a checked link), or a hex-colour string into a Color column -- and read the result from code. Use when a text column should be a link or lookup into another table, when a mistyped ID should be rejected by the table editor instead of failing at runtime, when a hex string is parsed in code on every use, when asked how to change a column's type or whether the editor's Convert Type is safe, or when a link reads blank or throws "This result failed", a chain of links never ends, a colour comes out black or white, a new row starts linked to the first row, or tables vanish after a link was added.
metadata:
  kind: playbook
---

# Reference and Color columns

**A column that holds another table's row ID is a Reference, a colour is a Color, and a typed column is
decided and checked cell by cell, never converted on trust.** The editor's Convert Type reports success while
changing cells.

Describes package `com.devzappy.schema.unity` 0.3.x, checked at 0.3.1 plus commit `e9089e0`.
**ran** = executed against that commit by a project that converted nine columns (2026-10-12); **read** = read in the
package source. Every problem below is open at that pin: check the changelog before trusting a workaround.
All 21 numbered issues, with repros: [references/known-issues.md](references/known-issues.md).

## Phase 1: should it be typed?

| The column holds | Make it | What you get | What it costs |
|---|---|---|---|
| another table's row ID | Reference | a mistyped or deleted ID fails Publish, not the game (read: `IsValidValue`); a typed property | the property's C# type becomes the other table's entry class and throws on a blank link (ran, Phase 4) |
| a `#RRGGBB` colour | Color (plugin type) | a real `UnityEngine.Color` property; no parsing in game code | alpha is dropped on save (ran, #6) |
| an enum name, a Resources key, a number | stay Text, Integer, Float | there is no enum type; FilePath wants a real file at that path; Unity Asset links by GUID | none |

Do not start Phase 2 for a column that stays Text. Two tables that reference each other (Ships links to Ports,
Ports links to Ships) are the one structure to refuse: the load drops both silently (ran, #11). A table linking to
itself, like a "next level" or "next port" column, is fine. Validation is quadratic in rows (ran, #14):
51 rows cost 1 to 2 ms, 2,000 rows about 1 s per validate, convert and load. Fine for design data, not for a big log.

## Phase 2: preflight the cells

Commit first so the change is one reviewable diff. Then, with the staging files named by the Manifest:

```sh
python3 <skill-dir>/scripts/preflight.py link  Content/Ports.json NextPort Content/Ports.json   # a link: SOURCE COLUMN TARGET
python3 <skill-dir>/scripts/preflight.py color Content/Ports.json Tint                          # a colour
```

`<skill-dir>` is this skill's base directory, which Claude Code shows when the skill loads. The script reads staging
files only and changes nothing; it needs Python 3 and nothing else. It names the table, row and value of every cell that
cannot convert: a typo, a different case, a trailing space, a colour name, a hex without `#`, alpha, a duplicate
id in the target. The editor's own conversion names no row (ran, #4).

| Exit | Meaning | Next |
|---|---|---|
| 1 | a cell cannot convert | fix the data, run again. Do not convert: the editor leaves a failed conversion half done (ran, #3) |
| 2 | only blank cells | Route B. A blank link is often meant (the last stage, "no reward") and Convert Type does not keep it |
| 0 | clean, no blanks | Route A or B |

## Phase 3: choose the route

**Route B, the default: change the type block in the staging file.** Same file the editor would write, no
conversion code involved. [references/runbook-convert-column.md](references/runbook-convert-column.md) has the exact steps and blocks.

**Route A, only at exit 0: Convert Type in the editor** (column header menu). It is safe on these cells and then still
wrong in two ways you must undo: it sets the column default to the first row, so new rows start linked to it (ran, #1; read: the method ends by setting the default) and a new Color row holds the text `#000000` that Publish refuses until saved and reloaded (ran, #8).

Either way, afterwards: `python3 <skill-dir>/scripts/preflight.py cells old.json new.json` (with `old.json` from
`git show HEAD:<file>`) must print `no cell changed`. A changed cell means the conversion altered data:
restore the file from git and use Route B.

## Phase 4: reading the column

The generated property of a Reference is `XScheme.GetEntry(<id>).Result`: it throws `InvalidOperationException:
This result failed ... Entry not found` for a blank or dangling link, although blanks are allowed (ran, #12). Add
non-throwing helpers in your own file, beside the generated class (it is `partial`):

```csharp
namespace Tidewater.Schemes {                          // the generated file's namespace
    public partial class PortsEntry {
        public string NextPortId() => DataEntry.GetDataAsString("NextPort");     // "" for the last port
        public PortsEntry NextPortOrNull() =>
            PortsScheme.GetEntry(NextPortId()).Try(out var p) ? p : null;
    }
}
```

Game code calls `NextPortId()` or `NextPortOrNull()`, never the property. A Reference cell still reads as the id
string through `GetDataAsString` (ran). Never read a Color through `ColorUtility` or `GetDataAsString`: on a Color
cell the string is `RGBA(...)` text and every parse falls back (ran, #19). A link property costs about 120 times a
plain read and allocates (ran, #15): read links at load ([[table-reading]]). Old code that read the column as a string
stops compiling for a Reference, which is the point; for a Color it keeps compiling and reads white.

## Phase 5: verify, then report

1. Publish the table twice; the second Publish changes no file (`git status` clean). A target table must be loaded first,
   which the editor's Publish All arranges (ran, #20).
2. The `cells` check printed `no cell changed`; the diff shows only the column's type block, the default and the `.g.cs` property type.
3. The code compiles, and a blank link, a dangling link and a normal link each read without an exception.
4. Report what changed and what you could not check (the editor's interactive menus; a stripped player).

## Do not

- Convert Type on a column with blank cells, or leave a link column's default at the first row: every new row from the editor's "+" starts linked to it.
- Read a link with `.Result`, a colour with `ColorUtility`, or a column by name in game code.
- Hand-edit a `.g.cs` or a published JSON; Publish rewrites them ([[table-authoring]]).

## Related

[[table-authoring]], [[table-reading]]
