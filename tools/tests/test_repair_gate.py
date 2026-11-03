"""
Tests for tools/repair_gate.py, the gate the Schema-Unity repairkit daemon runs in a repair workspace.

Criteria (docs/RepairKit-Plan.md 4b): A2.2, the gate fails when an EditMode test fails, read from the results
file and never from an exit code; plus the gate's own edges: the known pre-existing failure does not make it
red, an empty run (total 0) and a missing or unreadable results file do, and a missing dotnet SDK skips the
Core half cleanly.

Run with the daemon's venv (stdlib only, so any python3 >= 3.9 works too):

    ~/.schema-unity/repairkit/venv/bin/python -m pytest tools/tests -q
"""
from __future__ import annotations

import json
import sys
import tempfile
import textwrap
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import repair_gate as gate  # noqa: E402

KNOWN = "Schema.Unity.Editor.Tests.SchemaEditorWindowTests.Test_Click_Create_New_Scheme_Button"


def nunit(cases: list[tuple[str, str]], *, root_failed: int | None = None, root_total: int | None = None) -> str:
    """A results file shaped like Unity 6's (test-run > test-suite > test-case), with the given cases."""
    failed = sum(1 for _, r in cases if r == "Failed")
    passed = sum(1 for _, r in cases if r == "Passed")
    body = "\n".join(
        f'      <test-case id="{i}" name="{n.rsplit(".", 1)[-1]}" fullname="{n}" result="{r}">'
        + ("<failure><message><![CDATA[Expected: True\n  But was:  False]]></message></failure>" if r == "Failed" else "")
        + "</test-case>"
        for i, (n, r) in enumerate(cases, start=1000)
    )
    total = len(cases) if root_total is None else root_total
    rf = failed if root_failed is None else root_failed
    overall = "Failed(Child)" if rf else "Passed"
    return textwrap.dedent(f"""\
        <?xml version="1.0" encoding="utf-8"?>
        <test-run id="2" testcasecount="{total}" result="{overall}" total="{total}" passed="{passed}" failed="{rf}" inconclusive="0" skipped="0">
          <test-suite type="TestSuite" name="Unity6" fullname="Unity6" result="{'Failed' if rf else 'Passed'}" total="{total}" passed="{passed}" failed="{rf}">
            <test-suite type="Assembly" name="Schema.Unity.Editor.Tests.dll" result="{'Failed' if rf else 'Passed'}">
        """) + body + "\n    </test-suite>\n  </test-suite>\n</test-run>\n"


def trx(total: int, failed: int) -> str:
    return textwrap.dedent(f"""\
        <?xml version="1.0" encoding="utf-8"?>
        <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <ResultSummary outcome="{'Failed' if failed else 'Completed'}">
            <Counters total="{total}" executed="{total}" passed="{total - failed}" failed="{failed}" error="0" />
          </ResultSummary>
        </TestRun>
        """)


class NUnitReadingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = Path(tempfile.mkdtemp())
        self.known = {KNOWN: "pre-existing on main before the collector"}

    def write(self, text: str, name: str = "results.xml") -> Path:
        path = self.tmp / name
        path.write_text(text, encoding="utf-8")
        return path

    def test_all_passed_is_green(self) -> None:
        v = gate.read_nunit(self.write(nunit([("A.One", "Passed"), ("A.Two", "Passed"), ("A.Three", "Passed")])), self.known)
        self.assertTrue(v.passed, v.reason)
        self.assertEqual(v.total, 3)
        self.assertEqual(v.failed, 0)

    def test_A2_2_a_failed_test_makes_it_red_and_is_named(self) -> None:
        v = gate.read_nunit(self.write(nunit([("A.One", "Passed"), ("Planted.Fails", "Failed")])), self.known)
        self.assertFalse(v.passed)
        self.assertEqual(v.unexpected, ["Planted.Fails"])
        self.assertIn("Planted.Fails", v.reason)

    def test_the_known_failure_alone_does_not_make_it_red(self) -> None:
        v = gate.read_nunit(self.write(nunit([("A.One", "Passed"), (KNOWN, "Failed")])), self.known)
        self.assertTrue(v.passed, v.reason)
        self.assertEqual(v.known_hit, [KNOWN])
        self.assertEqual(v.unexpected, [])

    def test_the_known_failure_does_not_excuse_another(self) -> None:
        v = gate.read_nunit(self.write(nunit([(KNOWN, "Failed"), ("Planted.Fails", "Failed")])), self.known)
        self.assertFalse(v.passed)
        self.assertEqual(v.unexpected, ["Planted.Fails"])

    def test_a_known_failure_that_now_passes_is_noted_not_red(self) -> None:
        v = gate.read_nunit(self.write(nunit([(KNOWN, "Passed")])), self.known)
        self.assertTrue(v.passed, v.reason)
        self.assertEqual(v.known_passing, [KNOWN])

    def test_total_zero_is_red(self) -> None:
        v = gate.read_nunit(self.write(nunit([])), self.known)
        self.assertFalse(v.passed)
        self.assertEqual(v.total, 0)
        self.assertIn("no tests", v.reason)

    def test_missing_results_file_is_red(self) -> None:
        v = gate.read_nunit(self.tmp / "never-written.xml", self.known)
        self.assertFalse(v.passed)
        self.assertIn("no results file", v.reason)

    def test_unreadable_results_file_is_red(self) -> None:
        v = gate.read_nunit(self.write("<test-run total='3'"), self.known)
        self.assertFalse(v.passed)
        self.assertIn("could not be read", v.reason)

    def test_root_failure_count_the_cases_do_not_explain_is_red(self) -> None:
        # e.g. a suite-level SetUp failure counted at the root but not on any one test-case
        v = gate.read_nunit(self.write(nunit([("A.One", "Passed")], root_failed=2)), self.known)
        self.assertFalse(v.passed)
        self.assertIn("root", v.reason)

    def test_inconclusive_and_skipped_are_not_failures(self) -> None:
        v = gate.read_nunit(self.write(nunit([("A.One", "Passed"), ("A.Two", "Inconclusive"), ("A.Three", "Skipped")])), self.known)
        self.assertTrue(v.passed, v.reason)


class KnownFailuresFileTests(unittest.TestCase):
    def test_every_entry_needs_a_reason(self) -> None:
        path = Path(tempfile.mkdtemp()) / "known.json"
        path.write_text(json.dumps({"known_failures": [{"test": KNOWN, "reason": ""}]}), encoding="utf-8")
        with self.assertRaises(ValueError):
            gate.load_known_failures(path)

    def test_the_committed_file_names_the_one_pre_existing_failure_with_a_reason(self) -> None:
        known = gate.load_known_failures(gate.KNOWN_FAILURES_FILE)
        self.assertEqual(list(known), [KNOWN])
        self.assertTrue(known[KNOWN].strip())

    def test_an_absent_file_means_no_exclusions(self) -> None:
        self.assertEqual(gate.load_known_failures(Path(tempfile.mkdtemp()) / "absent.json"), {})


class CoreHalfTests(unittest.TestCase):
    def test_no_dotnet_skips_cleanly_with_a_note(self) -> None:
        v = gate.core_half(Path(tempfile.mkdtemp()), dotnet=None, out_dir=Path(tempfile.mkdtemp()))
        self.assertTrue(v.passed)
        self.assertTrue(v.skipped)
        self.assertIn("dotnet", v.reason)

    def test_trx_pass_fail_and_empty(self) -> None:
        tmp = Path(tempfile.mkdtemp())
        (tmp / "ok.trx").write_text(trx(12, 0), encoding="utf-8")
        (tmp / "bad.trx").write_text(trx(12, 1), encoding="utf-8")
        (tmp / "empty.trx").write_text(trx(0, 0), encoding="utf-8")
        self.assertTrue(gate.read_trx(tmp / "ok.trx").passed)
        self.assertFalse(gate.read_trx(tmp / "bad.trx").passed)
        self.assertFalse(gate.read_trx(tmp / "empty.trx").passed)
        self.assertFalse(gate.read_trx(tmp / "absent.trx").passed)


class WholeGateTests(unittest.TestCase):
    def test_a_missing_unity_cli_is_a_gate_that_never_started(self) -> None:
        ws = Path(tempfile.mkdtemp())
        (ws / "TestProjects" / "Unity6").mkdir(parents=True)
        v = gate.unity_half(ws, unity_cli=ws / "no-such-unity", out_dir=ws, timeout=60, known={})
        self.assertFalse(v.passed)
        self.assertFalse(v.started)

    def test_a_workspace_without_the_test_project_never_started(self) -> None:
        ws = Path(tempfile.mkdtemp())
        v = gate.unity_half(ws, unity_cli=Path("/bin/echo"), out_dir=ws, timeout=60, known={})
        self.assertFalse(v.started)
        self.assertIn("TestProjects/Unity6", v.reason)

    def test_exit_codes_say_pass_red_and_could_not_start(self) -> None:
        ok = gate.Verdict(passed=True, reason="ok")
        red = gate.Verdict(passed=False, reason="red")
        unstarted = gate.Verdict(passed=False, started=False, reason="no unity")
        self.assertEqual(gate.exit_code([ok, ok]), gate.EXIT_PASS)
        self.assertEqual(gate.exit_code([ok, red]), gate.EXIT_RED)
        self.assertEqual(gate.exit_code([ok, unstarted]), gate.EXIT_NOT_STARTED)
        # A red half outranks one that never started: the change is judged by what did run.
        self.assertEqual(gate.exit_code([red, unstarted]), gate.EXIT_RED)


if __name__ == "__main__":
    unittest.main()
