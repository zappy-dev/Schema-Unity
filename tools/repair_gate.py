"""Stub: the gate for Schema-Unity repairs (built in the next commit)."""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

KNOWN_FAILURES_FILE = Path(__file__).resolve().parent / "repair_gate_known_failures.json"
EXIT_PASS, EXIT_RED, EXIT_NOT_STARTED = 0, 1, 3


@dataclass
class Verdict:
    passed: bool = True
    started: bool = True
    skipped: bool = False
    total: int = 0
    failed: int = 0
    unexpected: list = field(default_factory=list)
    known_hit: list = field(default_factory=list)
    known_passing: list = field(default_factory=list)
    reason: str = ""


def read_nunit(path, known):
    return Verdict()


def read_trx(path):
    return Verdict()


def load_known_failures(path):
    return {}


def core_half(workspace, *, dotnet, out_dir):
    return Verdict()


def unity_half(workspace, *, unity_cli, out_dir, timeout, known):
    return Verdict()


def exit_code(verdicts):
    return EXIT_PASS
