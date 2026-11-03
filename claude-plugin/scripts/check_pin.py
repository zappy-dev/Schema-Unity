#!/usr/bin/env python3
"""Check that the plugin still says which Schema package it describes. Stdlib only, read-only.

Usage: python3 claude-plugin/scripts/check_pin.py [REPO_ROOT]     (REPO_ROOT defaults to the repository containing this file)

Fails (exit 1) when
  - the plugin's `version` major.minor differs from the package's `version` major.minor in
    SchemaPlayground/Packages/com.devzappy.schema.unity/package.json;
  - a skill lacks the line "Describes package `com.devzappy.schema.unity` <major>.<minor>.x";
  - a skill links this repository's docs at a tag other than v<package version>.
Run it in the change that bumps the package version: a failure is the prompt to re-read every skill against
the new version, re-run the repros in column-types/references/known-issues.md, and update the pin lines.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

PACKAGE = "SchemaPlayground/Packages/com.devzappy.schema.unity/package.json"


def main(argv: list[str]) -> int:
    root = Path(argv[1]) if len(argv) > 1 else Path(__file__).resolve().parents[2]
    pkg = json.loads((root / PACKAGE).read_text(encoding="utf-8"))["version"]
    plug = json.loads((root / "claude-plugin/.claude-plugin/plugin.json").read_text(encoding="utf-8"))["version"]
    major_minor = ".".join(pkg.split(".")[:2])
    problems = []
    if ".".join(plug.split(".")[:2]) != major_minor:
        problems.append(f"plugin version {plug} does not track package {pkg} (major.minor must match)")
    pin = re.compile(r"Describes package `com\.devzappy\.schema\.unity` " + re.escape(major_minor) + r"\.x")
    for skill in sorted((root / "claude-plugin/skills").glob("*/SKILL.md")):
        text = skill.read_text(encoding="utf-8")
        rel = skill.relative_to(root).as_posix()
        if not pin.search(text):
            problems.append(f"{rel}: no 'Describes package `com.devzappy.schema.unity` {major_minor}.x' line")
        for tag in sorted(set(re.findall(r"zappy-dev/Schema-Unity/blob/(v[\w.]+)/", text))):
            if tag != f"v{pkg}":
                problems.append(f"{rel}: links docs at {tag}, package is {pkg}")
    for p in problems:
        print("PIN", p)
    print(f"{len(problems)} problem(s)" if problems else f"ok: plugin {plug} describes package {major_minor}.x (package is {pkg})")
    return 1 if problems else 0


if __name__ == "__main__":
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv))
