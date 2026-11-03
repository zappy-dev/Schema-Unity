"""
RepairKit host for Schema-Unity. Importing this module installs every repairkit seam; nothing in repairkit imports
it. Run the daemon and its CLI through tools/repairkit.sh (it sets PYTHONPATH and --host), or by hand:

    PYTHONPATH=tools ~/.schema-unity/repairkit/venv/bin/repairkit --host repair_host health

Written for repairkit **v1.2.1** (installed from that tag into ~/.schema-unity/repairkit/venv by
``tools/repairkit.sh install``). Started from repairkit's examples/unity_host.py and checked against the tank
game's tools/repairkit_host.py; where this differs from the template:

  * **Its own state.** ``~/.schema-unity/repairkit/`` holds the store, the workspaces and the socket
    (``repairkit.sock``, 50 characters, under the 104 limit). It never touches the tank game's
    ``~/.tankslop/repairkit/graham_4player_tank_game/``, and a repair may not either (denied_commands).
  * **Repairs base on ``main``** (plan decision 1), not on whatever the primary checkout has out, and promote only
    into a primary checkout that is on ``main`` with no tracked edits. ``SCHEMA_REPAIR_BASE`` overrides the branch
    for a test daemon (the phase 2 checks ran with ``rk-daemon``, since ``main`` predates the test project).
  * **The gate is tools/repair_gate.py** in the workspace: Unity EditMode in TestProjects/Unity6 read from the NUnit
    XML, with the known pre-existing failure excluded, and dotnet's Core tests when an SDK exists. Exit 3 ("could not
    start") becomes ``TestResult.started=False`` so repairkit says the machine, not the change, is at fault. The gate
    runs under this venv's python (outside the live checkout, which the doctor's ``programs`` check requires).
  * **The Unity Library is cloned** (APFS clonefile, ``cp -Rc``) into each workspace's TestProjects/Unity6 from
    ``SCHEMA_REPAIR_LIBRARY_SEED`` or the primary checkout's, so a gate does not reimport from nothing.
  * **AUTO_REPAIR is off** and the startup self-check is off (the gate is a multi-minute Unity run; ask for it with
    ``tools/repairkit.sh doctor``). Phase 1's collector also sends ``context.hold_for_review``.
  * **The gate and this host are held for review** when a repair edits them, so a repair cannot pass by changing
    what judges it.
  * The daemon's own PYTHONPATH is dropped once this module is imported (the tank game's fix for the doctor's
    ``environment`` check), and the doctor's throwaway workspace leaves no branch behind.
"""
from __future__ import annotations

import logging
import os
import shutil
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Optional

from repairkit import evidence, host, paths, provider, runtime, settings, workspace
from repairkit.git_workspace import GitWorktreeWorkspace, main_worktree

_LOG = logging.getLogger("schema_unity.repair_host")

TOOLS = Path(__file__).resolve().parent
_IMPORTED_FROM = TOOLS.parent


def default_state_root() -> Path:
    """Where this daemon keeps everything: outside every checkout, and not the tank game's root."""
    return Path.home() / ".schema-unity" / "repairkit"


def _unexport_live_pythonpath(environ=os.environ) -> Optional[str]:
    """Drop the PYTHONPATH the daemon was started with (tools/repairkit.sh sets it so this module imports).

    Nothing needs it after the import, and a daemon that keeps it hands it to every gate and agent it spawns,
    which is the fault repairkit's doctor ``environment`` check looks for."""
    return environ.pop("PYTHONPATH", None)


_unexport_live_pythonpath()

PROJECT_ROOT = Path(os.environ.get("SCHEMA_REPAIR_PROJECT_ROOT") or main_worktree(_IMPORTED_FROM)).expanduser()
STATE_ROOT = Path(os.environ.get("SCHEMA_REPAIR_STATE_ROOT") or default_state_root()).expanduser()
BASE_BRANCH = os.environ.get("SCHEMA_REPAIR_BASE") or "main"
UNITY_CLI = Path(os.environ.get("SCHEMA_UNITY_CLI") or Path.home() / ".unity" / "bin" / "unity")
#: The interpreter that runs the gate: this daemon's venv python, never one inside the live checkout.
GATE_PYTHON = sys.executable
GATE_SCRIPT = "tools/repair_gate.py"
UNITY_PROJECT = "TestProjects/Unity6"
DOCTOR_WORKSPACE = "repairkit-doctor"  # repairkit.doctor.WORKSPACE_NAME
REPO_NAME = "schema-unity"


def _git(cwd: Path, *args: str) -> subprocess.CompletedProcess:
    return subprocess.run(["git", *args], cwd=cwd, capture_output=True, text=True)


# --------------------------------------------------------------------------
# Paths. Installed before any other repairkit module resolves one.

_PATH_CONFIG = {
    paths.KEY_REPO_PATH: str(PROJECT_ROOT),
    paths.KEY_DATA_ROOT: str(STATE_ROOT),
}

paths.configure(
    paths.PathHost(
        config_get=_PATH_CONFIG.get,
        default_repo=PROJECT_ROOT,
        legacy_root=None,  # adopting repairkit fresh: socket, lock and log live in the data root
    )
)


# --------------------------------------------------------------------------
# Project shape.

host.configure(
    host.RepairHost(
        name="schema-unity",
        # Phase 3 adds an "after" screenshot for visual fixes; until then no change owes a demo.
        demo_required_globs=(),
        # Paths reach requires_review from `git diff --name-only`, already repo-relative; there is no one
        # leading segment that marks every repo-relative path here (Schema/, SchemaPlayground/, tools/...).
        repo_path_marker="",
        source_index_rels=("tools",),
        token_nav_rels=(
            "SchemaPlayground/Packages/com.devzappy.schema.unity/Editor",
            "SchemaPlayground/Packages/com.devzappy.schema.unity/Runtime",
            "Schema/Schema.Core",
        ),
        # A green gate is not enough when the change is to what judges it, or to how the package is consumed.
        promotion_requires_review=(
            "tools/repair_gate.py",
            "tools/repair_gate_known_failures.json",
            "tools/repair_host.py",
            "tools/repairkit.sh",
            "tools/launchd/",
            "TestProjects/Unity6/Packages/",
            "TestProjects/Unity6/ProjectSettings/",
            "SchemaPlayground/Packages/com.devzappy.schema.unity/package.json",
            ".github/",
        ),
        prompt_supplement="",  # phase 3 writes one (reproduce first in TestProjects/Unity6, scheme JSON as fixture)
        index_sources=(),
        commands={
            "test_workspace": f"python3 {GATE_SCRIPT}",
            "inspect_workspace": "git status --short && git log --oneline -5",
            "restart_daemon": "tools/repairkit.sh restart",
            "install_daemon": "tools/repairkit.sh start",
        },
        denied_commands=(
            (r"\bgit\s+push\b", "nothing leaves this machine from a repair; a human pushes"),
            (r"\bgh\s+(pr|release)\b", "no pull requests or releases from a repair"),
            (r"tankslop|graham_4player_tank_game", "the tank game and its repair daemon are not this repair's to touch"),
            (r"\blaunchctl\b", "the daemon's service is the user's to load and unload"),
            (r"\bgit\s+(reset\s+--hard|clean\s+-[a-z]*f|checkout\s+--\s+\.)", "do not throw work away wholesale"),
            (r"-projectPath\s+\S*/src/Schema-Unity/", "Unity must run against the repair worktree, never the live checkout"),
            (r"\bnpm\s+publish\b|openupm\s+publish", "releasing the package is the user's call (plan 7.1)"),
            (r"\brm\s+-rf\s+(/|~|\$HOME)", "no"),
        ),
    )
)


# --------------------------------------------------------------------------
# Provider: one model, the Claude Code CLI on PATH. Unused until phase 3 dispatches a repair.

class SingleModelProvider:
    def batch_choice(self) -> provider.ModelChoice:
        return provider.ModelChoice(provider="claude", model=os.environ.get("SCHEMA_REPAIR_MODEL", "claude-opus-5"))

    def agent_cli_path(self) -> Optional[str]:
        found = shutil.which("claude")
        local = Path.home() / ".local" / "bin" / "claude"
        return found or (str(local) if local.exists() else None)

    def agent_default_model(self) -> str:
        return self.batch_choice().model

    def structured_answer(self, schema, system, user, *, temperature=0, max_tokens=None, timeout=None):
        """None disables LLM triage: one repository, nothing to route."""
        del schema, system, user, temperature, max_tokens, timeout
        return None


provider.configure(SingleModelProvider())


# --------------------------------------------------------------------------
# Settings.

_SETTINGS = {
    settings.AGENT_PROVIDER: "claude",
    settings.USE_SANDBOX: True,
    settings.REQUIRE_VALIDATION: True,
    settings.BREAKER_THRESHOLD: 2,
    settings.MAX_CONCURRENT: 1,
    settings.AGENT_TIMEOUT_SECONDS: 3600,
    # On (phase 3, the user 2026-11-03: "for feedback that I am giving locally here, I want it to automatically
    # start working, similar to mino"): every submitted report starts a repair, one at a time. A repair that passes
    # the gate is merged into the primary checkout's main, locally; one that edits a path in requires_review waits.
    settings.AUTO_REPAIR: True,
    # Off: the doctor's gate is a full Unity EditMode run (minutes, one more Unity process); a daemon started at
    # login must not start one unasked. `tools/repairkit.sh doctor` asks, and health.self_check shows the verdict.
    settings.SELF_CHECK_ON_START: False,
}

settings.configure(
    settings.SettingsHost(
        get=_SETTINGS.get,
        secret=lambda name: os.environ.get(name.upper().replace(".", "_")),
        hint=lambda: "tools/repair_host.py _SETTINGS (Schema-Unity)",
    )
)


# --------------------------------------------------------------------------
# Workspace: one git worktree per repair, off the integration branch, Library cloned in.

@dataclass
class SchemaWorktreeWorkspace(GitWorktreeWorkspace):
    #: The branch repairs base on and promote into.
    base_branch: str = "main"
    unity_cli: Path = UNITY_CLI
    #: A TestProjects/Unity6/Library to clone into each workspace; default: the primary checkout's.
    library_seed: Optional[Path] = None

    def _live_head(self) -> str:
        """The base branch's tip, whatever the primary checkout has out (repairkit uses HEAD)."""
        run = _git(self.live, "rev-parse", "--verify", "--quiet", f"{self.base_branch}^{{commit}}")
        if run.returncode != 0 or not run.stdout.strip():
            raise workspace.WorkspaceError(f"the base branch {self.base_branch!r} does not exist in {self.live}")
        return run.stdout.strip()

    def create(self, name: str, *, primary_only: bool = False) -> workspace.Workspace:
        space = super().create(name, primary_only=primary_only)
        self._clone_library(Path(space.root))
        return space

    def _clone_library(self, root: Path) -> None:
        """Best effort: a missing or failed clone only makes the first gate run slower."""
        seed = self.library_seed or (self.live / UNITY_PROJECT / "Library")
        dst = root / UNITY_PROJECT / "Library"
        if not Path(seed).is_dir() or dst.exists() or not dst.parent.is_dir():
            return
        if subprocess.run(["cp", "-Rc", str(seed), str(dst)], capture_output=True).returncode != 0:
            shutil.rmtree(dst, ignore_errors=True)

    def destroy(self, name: str, *, force: bool = False) -> None:
        branch = self.branch(name)
        super().destroy(name, force=force)
        if name != DOCTOR_WORKSPACE:
            return  # a repair's branch is its evidence; repairkit decides what to keep
        exists = _git(self.live, "rev-parse", "--verify", "--quiet", branch).returncode == 0
        if exists and not _git(self.live, "rev-list", f"{self._live_head()}..{branch}").stdout.strip():
            _git(self.live, "branch", "-D", branch)

    def env(self, name: str) -> dict:
        env = super().env(name)
        env["UNITY_PROJECT_PATH"] = str(self.path(name) / UNITY_PROJECT)
        env["PATH"] = os.pathsep.join([str(self.unity_cli.parent), "/opt/homebrew/bin", os.environ.get("PATH", "")])
        return env

    def validate(self, name: str, *, repo: str = "") -> workspace.TestResult:
        result = super().validate(name, repo=repo)
        if result.started and result.returncode == 3:  # repair_gate.EXIT_NOT_STARTED
            last = next((ln for ln in reversed(result.output_tail.splitlines()) if ln.startswith("GATE")), "")
            return workspace.TestResult(
                passed=False, started=False, returncode=3, output_tail=result.output_tail,
                start_failure=last or "tools/repair_gate.py could not start",
            )
        return result

    def promote(self, name: str, *, repo: str = "") -> str:
        current = _git(self.live, "rev-parse", "--abbrev-ref", "HEAD").stdout.strip()
        if current != self.base_branch:
            raise workspace.WorkspaceError(
                f"the primary checkout {self.live} has {current!r} out, not {self.base_branch!r}; "
                f"nothing was merged. Put it on {self.base_branch} (no tracked edits) and promote again."
            )
        return super().promote(name, repo=repo)

    def readiness(self) -> dict:
        problems = []
        program = Path(self.validate_cmd[0]) if self.validate_cmd else None
        if program is None or not (program.is_file() and os.access(program, os.X_OK)):
            problems.append(f"the gate's interpreter {program} is not executable")
        if not (self.unity_cli.is_file() and os.access(self.unity_cli, os.X_OK)):
            problems.append(f"the unity CLI {self.unity_cli} is not an executable file")
        try:
            base = self._live_head()
        except workspace.WorkspaceError as exc:
            problems.append(str(exc))
        else:
            for needed in (GATE_SCRIPT, f"{UNITY_PROJECT}/Packages/manifest.json"):
                if _git(self.live, "cat-file", "-e", f"{base}:{needed}").returncode != 0:
                    problems.append(f"{self.base_branch} ({base[:8]}) has no {needed}, so a workspace off it has no gate")
        return {
            self.repo_name: {
                "ok": not problems,
                "executable": str(program) if program else None,
                "reason": "; ".join(problems),
            }
        }


workspace.configure(
    SchemaWorktreeWorkspace(
        repo=PROJECT_ROOT,
        state_root=STATE_ROOT,
        repo_name=REPO_NAME,
        purpose="Schema-Unity: the Schema core library, the Unity package (Editor, Runtime, the feedback collector) and their tests.",
        validate_cmd=(GATE_PYTHON, GATE_SCRIPT, "--workspace", "{workspace}"),
        conventions=("README.md", "docs/Contributing.md"),
        base_branch=BASE_BRANCH,
        unity_cli=UNITY_CLI,
        library_seed=Path(os.environ["SCHEMA_REPAIR_LIBRARY_SEED"]) if os.environ.get("SCHEMA_REPAIR_LIBRARY_SEED") else None,
    )
)


# --------------------------------------------------------------------------
# Runtime: started and restarted through tools/repairkit.sh.

class WrapperRuntime:
    wrapper = TOOLS / "repairkit.sh"

    def prepare(self) -> None:
        (STATE_ROOT / "workspaces").mkdir(parents=True, exist_ok=True)

    def identity(self, *, protocol: int, started_at: Optional[datetime]) -> dict:
        try:
            from importlib.metadata import version
            rk_version = version("repairkit")
        except Exception:  # noqa: BLE001
            rk_version = None
        return {
            "name": "schema-unity-repairkit",
            "service": "repairkit",
            "version": rk_version,
            "protocol": protocol,
            "started_at": started_at.isoformat() if started_at else None,
            "repo": str(PROJECT_ROOT),
            "base_branch": BASE_BRANCH,
            "state_root": str(STATE_ROOT),
            "host_module": str(Path(__file__).resolve()),
        }

    def _run(self, verb: str) -> bool:
        if not self.wrapper.exists():
            return False
        return subprocess.run([str(self.wrapper), verb], capture_output=True, text=True).returncode == 0

    def restart(self) -> bool:
        return self._run("restart")

    def start(self) -> bool:
        return self._run("start")

    def mark_release(self, reload_hint: str) -> dict:
        del reload_hint
        return {}

    def on_promoted(self, report_id: str, reload_hint: Optional[str]) -> None:
        """Nothing to release: consumers take a fix by pinning the commit through their own gate (plan phase 3)."""
        del reload_hint
        _LOG.info("repair %s promoted into %s; consumers pin the commit themselves", report_id[:8], BASE_BRANCH)


runtime.configure(WrapperRuntime())


# --------------------------------------------------------------------------
# Evidence: none yet. Phase 3 adds an "after" capture of the Schema Editor for visual fixes.

class NoEvidence:
    def toolchain_note(self) -> str:
        return ""

    def recording_instructions(self) -> str:
        return ""

    def record(self, report_id: str, *, cwd, edited_paths):
        del report_id, cwd, edited_paths
        return None

    def attach(self, report_id: str, artifact) -> bool:
        del report_id, artifact
        return False


evidence.configure(NoEvidence())
