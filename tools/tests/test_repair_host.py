"""
Tests for tools/repair_host.py, the repairkit host for Schema-Unity.

What they hold the host to (docs/RepairKit-Plan.md, phase 2):

* the state root and socket are Schema-Unity's own (``~/.schema-unity/repairkit``), under the 104-character
  socket limit, and never the tank game's;
* AUTO_REPAIR is on: a submitted report starts a repair through ``maybe_auto_fix`` (phase 3);
* repairs base on the integration branch (``main``) whatever the primary checkout has out, and promote only
  into a primary checkout that is on that branch;
* the doctor's throwaway workspace leaves no branch behind.

The host installs repairkit's seams when imported, so the state root is pointed at a temp directory first.
Run with the daemon's venv:  ~/.schema-unity/repairkit/venv/bin/python -m pytest tools/tests -q
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

_STATE = Path(tempfile.mkdtemp(prefix="sk-host-"))
os.environ["SCHEMA_REPAIR_STATE_ROOT"] = str(_STATE)
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import repair_host  # noqa: E402
from repairkit import agent_queue, settings, workspace  # noqa: E402
from repairkit import host as rk_host  # noqa: E402


def git(cwd: Path, *args: str) -> str:
    return subprocess.run(["git", *args], cwd=cwd, check=True, capture_output=True, text=True).stdout.strip()


def make_repo() -> Path:
    """A repo with `main` and a second branch `other` checked out, like the primary checkout tonight."""
    repo = Path(tempfile.mkdtemp(prefix="sk-repo-"))
    git(repo, "init", "-q", "-b", "main")
    git(repo, "config", "user.email", "t@example.com")
    git(repo, "config", "user.name", "t")
    (repo / "a.txt").write_text("main\n")
    git(repo, "add", "-A")
    git(repo, "commit", "-q", "-m", "main")
    git(repo, "checkout", "-q", "-b", "other")
    (repo / "a.txt").write_text("other\n")
    git(repo, "commit", "-q", "-am", "other")
    return repo


class StateRootTests(unittest.TestCase):
    def test_default_state_root_is_schema_unitys_own_and_the_socket_fits(self) -> None:
        root = repair_host.default_state_root()
        self.assertEqual(root, Path.home() / ".schema-unity" / "repairkit")
        socket = root / "repairkit.sock"
        self.assertLess(len(str(socket)), 104)
        self.assertNotIn(".tankslop", str(socket))

    def test_the_installed_socket_is_under_the_configured_root(self) -> None:
        from repairkit import paths
        self.assertEqual(paths.socket_path(), _STATE.resolve() / "repairkit.sock")


class AutoRepairOnTests(unittest.TestCase):
    def test_auto_repair_is_on(self) -> None:
        self.assertIs(settings.get(settings.AUTO_REPAIR), True)

    def test_a_submitted_report_starts_a_repair(self) -> None:
        from repairkit import store
        started: list[str] = []
        original = agent_queue._start_fix_thread
        agent_queue._start_fix_thread = lambda report, **kw: started.append(report.id) or True
        try:
            report = store.submit("the tooltip column renders cut off", source="schema-editor", context={})
            agent_queue.maybe_auto_fix(report)
        finally:
            agent_queue._start_fix_thread = original
        self.assertEqual(started, [report.id])


class ProfileTests(unittest.TestCase):
    def test_a_repair_may_never_push_or_touch_the_tank_daemon(self) -> None:
        denied = " ".join(pattern for pattern, _ in rk_host.host().denied_commands)
        self.assertIn("push", denied)
        self.assertIn("tankslop", denied)

    def test_the_gate_and_the_host_are_held_for_review_when_a_repair_edits_them(self) -> None:
        held = rk_host.requires_review(["tools/repair_gate.py", "tools/repair_gate_known_failures.json",
                                        "SchemaPlayground/Packages/com.devzappy.schema.unity/Editor/Foo.cs"])
        self.assertIn("tools/repair_gate.py", held)
        self.assertIn("tools/repair_gate_known_failures.json", held)
        self.assertNotIn("SchemaPlayground/Packages/com.devzappy.schema.unity/Editor/Foo.cs", held)


class WorkspaceTests(unittest.TestCase):
    def setUp(self) -> None:
        self.repo = make_repo()
        self.state = Path(tempfile.mkdtemp(prefix="sk-ws-"))
        self.ws = repair_host.SchemaWorktreeWorkspace(
            repo=self.repo, state_root=self.state, repo_name="schema-unity",
            validate_cmd=("/bin/echo",), base_branch="main",
        )

    def test_a_repair_bases_on_main_not_on_what_the_primary_has_out(self) -> None:
        space = self.ws.create("rep-1")
        self.assertEqual(git(Path(space.root), "rev-parse", "HEAD"), git(self.repo, "rev-parse", "main"))
        self.assertEqual((Path(space.root) / "a.txt").read_text(), "main\n")
        self.ws.destroy("rep-1", force=True)

    def test_promote_refuses_a_primary_checkout_that_is_not_on_main(self) -> None:
        self.ws.create("rep-2")
        with self.assertRaises(workspace.WorkspaceError) as caught:
            self.ws.promote("rep-2")
        self.assertIn("main", str(caught.exception))
        self.assertEqual(git(self.repo, "rev-parse", "--abbrev-ref", "HEAD"), "other")
        self.ws.destroy("rep-2", force=True)

    def test_the_doctor_workspace_leaves_no_branch_behind(self) -> None:
        self.ws.create(repair_host.DOCTOR_WORKSPACE)
        self.ws.destroy(repair_host.DOCTOR_WORKSPACE, force=True)
        branches = git(self.repo, "branch", "--format=%(refname:short)").split()
        self.assertNotIn(f"repair/{repair_host.DOCTOR_WORKSPACE}", branches)

    def test_readiness_names_a_missing_unity_cli(self) -> None:
        ws = repair_host.SchemaWorktreeWorkspace(
            repo=self.repo, state_root=self.state, repo_name="schema-unity",
            validate_cmd=("/bin/echo",), base_branch="main", unity_cli=self.state / "no-unity",
        )
        gate = ws.readiness()["schema-unity"]
        self.assertFalse(gate["ok"])
        self.assertIn("no-unity", gate["reason"])

    def test_readiness_says_when_the_base_branch_lacks_the_gate(self) -> None:
        gate = self.ws.readiness()["schema-unity"]
        self.assertFalse(gate["ok"])
        self.assertIn("tools/repair_gate.py", gate["reason"])


if __name__ == "__main__":
    unittest.main()
