#!/usr/bin/env python3
"""Judge a Schema staging file before a column changes type, and compare it after. Stdlib only, read-only.

Usage:
  preflight.py link  SOURCE.json COLUMN TARGET.json   # can SOURCE's Text COLUMN become a Reference to TARGET's identifier?
  preflight.py color SOURCE.json COLUMN               # can SOURCE's Text COLUMN become a Color?
  preflight.py cells OLD.json NEW.json                # did any cell's value change between two versions of a staging file?

SOURCE and TARGET are staging files (the Manifest row's FilePath, usually Content/<Table>.json). For a check
before a change, take OLD from `git show HEAD:Content/<Table>.json > old.json`.

Exit codes: 0 clean; 1 a cell cannot convert, or a cell changed; 2 only blank cells found, which the editor's
Convert Type fills in (a Reference blank becomes the target's first row, a Color blank becomes the text "#000000").
Every finding names the table, the row's identifier and the value, so the fix is a cell, not a hunt.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

HEX6 = re.compile(r"^#[0-9A-Fa-f]{6}$")
HEX8 = re.compile(r"^#[0-9A-Fa-f]{8}$")


def load(path: str) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def id_column(table: dict) -> str | None:
    for a in table.get("Attributes", []):
        if a.get("IsIdentifier"):
            return a["AttributeName"]
    return None


def row_label(table: dict, row: dict, index: int) -> str:
    col = id_column(table)
    return f"{table.get('SchemeName', '?')} row {index + 1} ({row.get(col, '?')})" if col else f"{table.get('SchemeName', '?')} row {index + 1}"


def check_link(src: dict, column: str, target: dict) -> tuple[list[str], list[str]]:
    problems, blanks = [], []
    tid = id_column(target)
    if tid is None:
        return [f"{target.get('SchemeName', '?')} has no identifier column, so nothing can reference it"], blanks
    ids = [r.get(tid) for r in target.get("Entries", [])]
    seen, dup = set(), set()
    for v in ids:
        (dup if v in seen else seen).add(v)
    for v in sorted(dup, key=str):
        problems.append(f"{target['SchemeName']}.{tid} has the id {v!r} more than once; a link resolves to the first row without a warning")
    for i, row in enumerate(src.get("Entries", [])):
        v = row.get(column, "")
        where = row_label(src, row, i)
        if v is None or str(v).strip() == "":
            blanks.append(f"{where}: {column} is blank")
        elif v not in ids:
            near = [x for x in ids if str(x).strip().lower() == str(v).strip().lower()]
            hint = f" (differs only in case or spaces from {near[0]!r})" if near else ""
            problems.append(f"{where}: {column} = {v!r} is not a {target['SchemeName']}.{tid} value{hint}")
    return problems, blanks


def check_color(src: dict, column: str) -> tuple[list[str], list[str]]:
    problems, blanks = [], []
    for i, row in enumerate(src.get("Entries", [])):
        v = row.get(column, "")
        where = row_label(src, row, i)
        s = "" if v is None else str(v)
        if s.strip() == "":
            blanks.append(f"{where}: {column} is blank")
        elif HEX6.match(s):
            continue
        elif HEX8.match(s):
            problems.append(f"{where}: {column} = {s!r} has an alpha part; Schema saves #RRGGBB and drops it")
        else:
            problems.append(f"{where}: {column} = {s!r} is not #RRGGBB (a name, a missing #, a short hex or stray spaces convert differently from how code reading the text treated it)")
    return problems, blanks


def changed_cells(old: dict, new: dict) -> list[str]:
    out = []
    col = id_column(new) or id_column(old)
    old_rows = {r.get(col): r for r in old.get("Entries", [])} if col else {}
    for i, row in enumerate(new.get("Entries", [])):
        before = old_rows.get(row.get(col)) if col else (old.get("Entries", [])[i] if i < len(old.get("Entries", [])) else None)
        where = row_label(new, row, i)
        if before is None:
            out.append(f"{where}: row is new")
            continue
        for k in sorted(set(before) | set(row)):
            if before.get(k) != row.get(k):
                out.append(f"{where}: {k} changed {before.get(k)!r} -> {row.get(k)!r}")
    new_keys = {r.get(col) for r in new.get("Entries", [])} if col else set()
    for k in old_rows:
        if k not in new_keys:
            out.append(f"{old.get('SchemeName', '?')} row {k!r} was removed")
    return out


def report(problems: list[str], blanks: list[str]) -> int:
    for p in problems:
        print("PROBLEM", p)
    for b in blanks:
        print("BLANK  ", b)
    if problems:
        print(f"{len(problems)} cell(s) cannot convert. Fix the data, then run again.")
        return 1
    if blanks:
        print(f"{len(blanks)} blank cell(s): Convert Type would fill them in. Convert by editing the column's type block instead (see the runbook).")
        return 2
    print("clean: every cell converts to the same meaning")
    return 0


def main(argv: list[str]) -> int:
    if len(argv) == 5 and argv[1] == "link":
        return report(*check_link(load(argv[2]), argv[3], load(argv[4])))
    if len(argv) == 4 and argv[1] == "color":
        return report(*check_color(load(argv[2]), argv[3]))
    if len(argv) == 4 and argv[1] == "cells":
        diffs = changed_cells(load(argv[2]), load(argv[3]))
        for d in diffs:
            print("CHANGED", d)
        print(f"{len(diffs)} cell(s) changed" if diffs else "no cell changed")
        return 1 if diffs else 0
    print(__doc__)
    return 64


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
