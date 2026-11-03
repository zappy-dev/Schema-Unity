"""Stub: the repairkit host for Schema-Unity (built in the next commit)."""
import os
from pathlib import Path

from repairkit import paths
from repairkit.git_workspace import GitWorktreeWorkspace

DOCTOR_WORKSPACE = "repairkit-doctor"


def default_state_root():
    return Path("/nowhere")


_root = Path(os.environ.get("SCHEMA_REPAIR_STATE_ROOT") or default_state_root())
paths.configure(paths.PathHost(config_get={paths.KEY_DATA_ROOT: str(_root)}.get, default_repo=_root))


class SchemaWorktreeWorkspace(GitWorktreeWorkspace):
    def __init__(self, *a, base_branch="main", unity_cli=None, **kw):
        super().__init__(*a, **kw)
