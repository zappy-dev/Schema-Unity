#!/usr/bin/env python3
"""
The gate a Schema-Unity repair must pass: run in a repair workspace by the repairkit host (tools/repair_host.py),
and by hand from any checkout:

    python3 tools/repair_gate.py [--workspace DIR] [--out DIR] [--timeout SECONDS]

Two halves, cheapest first:

1. **Core** -- ``dotnet test Schema/Schema.Core.Tests`` with a TRX results file. Skipped, with a note, when no
   ``dotnet`` is on PATH (this Mac has no SDK yet; docs/RepairKit-Plan.md phase 2 Result).
2. **EditMode** -- the package's EditMode tests in ``TestProjects/Unity6`` (the package and its tests come in by
   ``file:`` path and ``testables``), through the ``unity`` CLI, queued in the machine-wide ``unity`` pool of
   project-skills' heavy_job.py when it is installed, so a repair never runs Unity beside the tank game's two.

Pass and fail are read from the results files (NUnit XML: every ``test-case``'s ``result``, cross-checked with
the root's ``total`` and ``failed``), never from an exit code. A run with no tests, or no results file, is red.
Failures listed in ``tools/repair_gate_known_failures.json`` (each with its reason) do not make it red; any other
failure does, and is named.

Exit codes: 0 pass; 1 red (a test failed, or nothing ran); 3 the gate could not start (no Unity CLI, no test
project, the heavy-job queue timed out), which the host reports as "this machine could not judge the change"
rather than "the change is bad". The last line of output is a one-line verdict; ``--json`` writes the whole
verdict to a file.

Standard library only, Python 3.9+, so it runs under whatever python3 a workspace finds.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Dict, List, Optional

KNOWN_FAILURES_FILE = Path(__file__).resolve().parent / "repair_gate_known_failures.json"
UNITY_PROJECT = Path("TestProjects") / "Unity6"
CORE_TESTS = Path("Schema") / "Schema.Core.Tests"
DEFAULT_UNITY_CLI = Path.home() / ".unity" / "bin" / "unity"
DEFAULT_TIMEOUT = 1500  # seconds for the Unity run; a cold import of TestProjects/Unity6 is a few minutes
QUEUE_WAIT_MINUTES = 45

EXIT_PASS, EXIT_RED, EXIT_NOT_STARTED = 0, 1, 3
HEAVY_JOB_WAITED_OUT = 75


@dataclass
class Verdict:
    """One half of the gate."""

    passed: bool = False
    #: False: the half never judged anything (no tool, no project). Reads oppositely from "a test failed".
    started: bool = True
    #: True: deliberately not run (no dotnet). Counts as passing, with the reason saying why.
    skipped: bool = False
    total: int = 0
    failed: int = 0
    unexpected: List[str] = field(default_factory=list)
    known_hit: List[str] = field(default_factory=list)
    known_passing: List[str] = field(default_factory=list)
    reason: str = ""
    name: str = ""
    results_file: str = ""


# --- known failures -------------------------------------------------------------------------------------------


def load_known_failures(path: Path) -> Dict[str, str]:
    """``{test fullname: reason}``. An absent file excludes nothing; an entry with no reason is refused."""
    if not path.is_file():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    known: Dict[str, str] = {}
    for entry in data.get("known_failures", []):
        test = str(entry.get("test") or "").strip()
        reason = str(entry.get("reason") or "").strip()
        if not test or not reason:
            raise ValueError(f"{path}: every known failure needs a test fullname and a reason: {entry!r}")
        known[test] = reason
    return known


# --- reading results ------------------------------------------------------------------------------------------


def _int(value: Optional[str]) -> Optional[int]:
    try:
        return int(value) if value is not None else None
    except ValueError:
        return None


def read_nunit(path: Path, known: Dict[str, str]) -> Verdict:
    """Judge one NUnit 3 results file (what Unity's test runner writes)."""
    v = Verdict(name="editmode", results_file=str(path))
    if not path.is_file():
        v.reason = f"no results file at {path}: the run did not finish (compile error, crash or timeout?)"
        return v
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        v.reason = f"the results file {path} could not be read: {exc}"
        return v

    cases = list(root.iter("test-case"))
    failed_cases = [c.get("fullname") or c.get("name") or "?" for c in cases if c.get("result") == "Failed"]
    v.total = len(cases)
    v.failed = len(failed_cases)
    v.unexpected = [name for name in failed_cases if name not in known]
    v.known_hit = [name for name in failed_cases if name in known]
    v.known_passing = sorted(
        (c.get("fullname") or "") for c in cases if c.get("fullname") in known and c.get("result") == "Passed"
    )

    root_total, root_failed = _int(root.get("total")), _int(root.get("failed"))
    if v.total == 0 or root_total == 0:
        v.reason = "no tests ran (total 0): an empty run judges nothing"
        return v
    if root_failed is not None and root_failed > v.failed:
        v.reason = (
            f"the root counts {root_failed} failure(s) but only {v.failed} test-case(s) failed: "
            f"a suite-level failure (SetUp or OneTimeSetUp?) the cases do not explain"
        )
        return v
    if v.unexpected:
        shown = ", ".join(v.unexpected[:10]) + (f" (+{len(v.unexpected) - 10} more)" if len(v.unexpected) > 10 else "")
        v.reason = f"{len(v.unexpected)} of {v.total} test(s) failed: {shown}"
        return v
    v.passed = True
    v.reason = f"{v.total - v.failed} of {v.total} passed"
    if v.known_hit:
        v.reason += f"; known failure(s) excluded: {', '.join(v.known_hit)}"
    if v.known_passing:
        v.reason += f"; listed as known but now passing (remove from the list): {', '.join(v.known_passing)}"
    return v


def read_trx(path: Path) -> Verdict:
    """Judge one ``dotnet test --logger trx`` results file from its ResultSummary counters."""
    v = Verdict(name="core", results_file=str(path))
    if not path.is_file():
        v.reason = f"no TRX results at {path}"
        return v
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError as exc:
        v.reason = f"the TRX file {path} could not be read: {exc}"
        return v
    counters = next((el for el in root.iter() if el.tag.endswith("Counters")), None)
    if counters is None:
        v.reason = f"{path} has no ResultSummary counters"
        return v
    v.total = _int(counters.get("total")) or 0
    v.failed = sum(_int(counters.get(k)) or 0 for k in ("failed", "error", "timeout"))
    if v.total == 0:
        v.reason = "no Core tests ran (total 0)"
    elif v.failed:
        v.unexpected = [
            el.get("testName", "?")
            for el in root.iter()
            if el.tag.endswith("UnitTestResult") and el.get("outcome") not in ("Passed", "NotExecuted")
        ]
        v.reason = f"{v.failed} of {v.total} Core test(s) failed: {', '.join(v.unexpected[:10])}"
    else:
        v.passed = True
        v.reason = f"{v.total} of {v.total} Core tests passed"
    return v


# --- running the halves ---------------------------------------------------------------------------------------


def core_half(workspace: Path, *, dotnet: Optional[str], out_dir: Path, timeout: int = 600) -> Verdict:
    """``dotnet test`` on Schema.Core.Tests, or a clean skip when there is no dotnet SDK.

    Not yet run against a real SDK on this machine (there is none): the TRX reading is unit-tested only.
    """
    if not dotnet:
        return Verdict(
            name="core", passed=True, skipped=True,
            reason="skipped: no dotnet SDK on PATH, so Schema.Core.Tests did not run (install the .NET 8 SDK to enable it)",
        )
    project = workspace / CORE_TESTS
    if not project.is_dir():
        return Verdict(name="core", started=False, reason=f"{project} is missing")
    trx = out_dir / "core.trx"
    if trx.exists():
        trx.unlink()
    try:
        run = subprocess.run(
            [dotnet, "test", str(project), "--logger", f"trx;LogFileName={trx}"],
            cwd=workspace, capture_output=True, text=True, timeout=timeout,
        )
    except subprocess.TimeoutExpired:
        return Verdict(name="core", reason=f"dotnet test did not finish in {timeout} s")
    (out_dir / "core.log").write_text(run.stdout + run.stderr, encoding="utf-8")
    return read_trx(trx)


def heavy_job_script() -> Optional[Path]:
    """project-skills' heavy_job.py (the machine-wide Unity queue), newest installed copy first, or None."""
    override = os.environ.get("SCHEMA_HEAVY_JOB")
    if override:
        return Path(override) if Path(override).is_file() else None
    cached = sorted(
        Path.home().glob(".claude/plugins/cache/*/project-skills/*/scripts/heavy_job.py"),
        key=lambda p: tuple(int(part) if part.isdigit() else 0 for part in p.parent.parent.name.split(".")),
    )
    for candidate in (*reversed(cached), Path.home() / "src/zappy-project-skills/scripts/heavy_job.py"):
        if candidate.is_file():
            return candidate
    return None


def compile_errors(text: str, workspace: Path) -> List[str]:
    """The ``error CS`` lines in a Unity log, each once, with the workspace prefix taken off, at most ten."""
    prefix = str(workspace).rstrip("/") + "/"
    found: List[str] = []
    for line in text.splitlines():
        if "error CS" not in line:
            continue
        line = line.strip().replace(prefix, "")
        if line not in found:
            found.append(line)
    return found[:10]


def unity_half(
    workspace: Path, *, unity_cli: Path, out_dir: Path, timeout: int, known: Dict[str, str]
) -> Verdict:
    """The EditMode suite of TestProjects/Unity6 in this workspace, judged from its NUnit XML."""
    project = workspace / UNITY_PROJECT
    if not project.is_dir():
        return Verdict(
            name="editmode", started=False,
            reason=f"{UNITY_PROJECT} is missing in {workspace}: this checkout predates the Unity 6 test project",
        )
    if not (unity_cli.is_file() and os.access(unity_cli, os.X_OK)):
        return Verdict(name="editmode", started=False, reason=f"the unity CLI {unity_cli} is not an executable file")

    xml = (out_dir / "editmode.xml").resolve()
    if xml.exists():
        xml.unlink()
    cmd = [
        str(unity_cli), "test", str(project), "--mode", "EditMode", "--output", str(xml),
        "--non-interactive", "--no-banner", "--format", "json", "--timeout", str(timeout),
    ]
    queue = None if os.environ.get("HEAVY_JOB_DISABLE") == "1" else heavy_job_script()
    if queue is not None:
        cmd = [sys.executable, str(queue), "run", "--name", "unity", "--by", "schema-unity-gate",
               "--wait-minutes", str(QUEUE_WAIT_MINUTES), "--", *cmd]
    began = time.monotonic()
    bound = timeout + QUEUE_WAIT_MINUTES * 60 + 120
    try:
        run = subprocess.run(cmd, cwd=workspace, capture_output=True, text=True, timeout=bound)
    except subprocess.TimeoutExpired:
        return Verdict(name="editmode", reason=f"the Unity run did not return within {bound} s")
    except OSError as exc:
        return Verdict(name="editmode", started=False, reason=f"could not run {cmd[0]}: {exc}")
    output = run.stdout + run.stderr
    (out_dir / "editmode.log").write_text(" ".join(cmd) + f"\n\nexit {run.returncode}\n\n" + output, encoding="utf-8")
    if queue is not None and run.returncode == HEAVY_JOB_WAITED_OUT and not xml.exists():
        return Verdict(name="editmode", started=False,
                       reason=f"waited {QUEUE_WAIT_MINUTES} minutes for a slot in the unity heavy-job pool")

    v = read_nunit(xml, known)
    if not xml.exists():
        # The CLI says only "Scripts have compiler errors."; the error CS lines are in the project's Editor.log.
        editor_log = project / "Logs" / "Editor.log"
        log_text = editor_log.read_text(encoding="utf-8", errors="replace") if editor_log.is_file() else ""
        errors = compile_errors(output + "\n" + log_text, workspace)
        if errors:
            v.reason = "compile errors: " + " | ".join(errors)
    v.reason += f" ({time.monotonic() - began:.0f} s)"
    return v


def exit_code(verdicts: List[Verdict]) -> int:
    """Red if any half that ran is red; could-not-start if a half never started and none is red; else pass."""
    if any(v.started and not v.passed for v in verdicts):
        return EXIT_RED
    if any(not v.started for v in verdicts):
        return EXIT_NOT_STARTED
    return EXIT_PASS


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="The gate a Schema-Unity repair must pass.")
    parser.add_argument("--workspace", type=Path, default=Path.cwd(), help="the checkout to judge (default: cwd)")
    parser.add_argument("--out", type=Path,
                        help="where results and logs go (default: $REPAIRKIT_WORKSPACE_STATE, else <workspace>-gate beside it)")
    parser.add_argument("--timeout", type=int, default=DEFAULT_TIMEOUT, help="seconds before the Unity run is killed")
    parser.add_argument("--unity-cli", type=Path, default=Path(os.environ.get("SCHEMA_UNITY_CLI") or DEFAULT_UNITY_CLI))
    parser.add_argument("--known-failures", type=Path,
                        help="default: tools/repair_gate_known_failures.json in the workspace")
    parser.add_argument("--skip-core", action="store_true", help="do not run the dotnet half")
    parser.add_argument("--json", type=Path, help="also write the verdict here as JSON")
    args = parser.parse_args(argv)

    workspace = args.workspace.expanduser().resolve()
    default_out = os.environ.get("REPAIRKIT_WORKSPACE_STATE") or str(workspace.parent / f"{workspace.name}-gate")
    out_dir = (args.out or Path(default_out)).expanduser().resolve()
    out_dir.mkdir(parents=True, exist_ok=True)
    known_file = args.known_failures or (workspace / "tools" / KNOWN_FAILURES_FILE.name)
    try:
        known = load_known_failures(known_file)
    except (ValueError, json.JSONDecodeError) as exc:
        print(f"gate: {exc}")
        print("GATE RED: the known-failures list is invalid")
        return EXIT_RED

    verdicts: List[Verdict] = []
    if args.skip_core:
        verdicts.append(Verdict(name="core", passed=True, skipped=True, reason="skipped: --skip-core"))
    else:
        verdicts.append(core_half(workspace, dotnet=shutil.which("dotnet"), out_dir=out_dir))
    print(f"gate core: {verdicts[-1].reason}", flush=True)
    verdicts.append(unity_half(workspace, unity_cli=args.unity_cli, out_dir=out_dir, timeout=args.timeout, known=known))
    print(f"gate editmode: {verdicts[-1].reason}", flush=True)
    print(f"gate results: {out_dir}")

    code = exit_code(verdicts)
    if args.json:
        args.json.write_text(json.dumps({"exit": code, "halves": [asdict(v) for v in verdicts]}, indent=2),
                             encoding="utf-8")
    word = {EXIT_PASS: "GATE PASS", EXIT_RED: "GATE RED", EXIT_NOT_STARTED: "GATE NOT STARTED"}[code]
    print(f"{word}: " + "; ".join(f"{v.name}: {v.reason}" for v in verdicts))
    return code


if __name__ == "__main__":
    sys.exit(main())
