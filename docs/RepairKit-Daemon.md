# The Schema-Unity repairkit daemon

The Schema Editor's **Feedback** button (docs/EditorGuide.md, "Send Feedback") files reports with a local
[repairkit](https://github.com/zappy-dev/repairkit) daemon over a Unix socket. This is that daemon for this
repository: its own install, its own state, its own socket. It is not the tank game's daemon and never talks to it.
The plan it comes from is docs/RepairKit-Plan.md (phase 2).

| What | Where |
|---|---|
| Host module (every repairkit seam) | `tools/repair_host.py` |
| Gate a repair must pass | `tools/repair_gate.py`, known failures in `tools/repair_gate_known_failures.json` |
| CLI and daemon wrapper | `tools/repairkit.sh` |
| launchd agent (written, not loaded) | `tools/launchd/com.devzappy.schema-unity.repairkit.plist` |
| repairkit itself | v1.2.1, in its own venv `~/.schema-unity/repairkit/venv` |
| Store, workspaces, log | `~/.schema-unity/repairkit/` (`repairkit.log`, `feedback-reports/`, `workspaces/`) |
| Socket (the Feedback dialog fills this in itself) | `~/.schema-unity/repairkit/repairkit.sock` |

## Install, start, check

```sh
tools/repairkit.sh install     # once: makes the venv, installs repairkit v1.2.1 from ~/src/repairkit (needs uv)
tools/repairkit.sh start       # background daemon; `stop`, `restart`; `serve` runs it in the foreground
tools/repairkit.sh health      # JSON: queue, gates, self_check, identity (base branch, state root)
tools/repairkit.sh doctor      # starts the self-check: a throwaway worktree off main, then the whole gate (minutes)
tools/repairkit.sh list-reports --limit 5
```

`doctor` returns at once; `health` shows `self_check.ok` once the gate has run (`null` until then). The self-check
does not run on its own at start-up: the gate is a full Unity run, and a daemon started at login should not start
Unity unasked.

## What it does with a report

Repairs it, automatically (`repair.auto` true in `tools/repair_host.py`, phase 3, like Mino): every submitted report
starts a repair, one at a time, using the Claude Code CLI through `claude-agent-sdk` in the daemon's venv. A report
filed before that, or one that failed, is started by hand with `tools/repairkit.sh enqueue-fix ID`. Each repair runs in
one git worktree per repair under `~/.schema-unity/repairkit/workspaces/`, cut
from **`main`** (whatever the primary checkout has out), with `TestProjects/Unity6/Library` cloned in from the
primary checkout; the gate runs there; a passing repair is merged into the primary checkout only if that checkout is
on `main` with no tracked edits. Nothing is pushed. A repair that edits the gate, the host or the test project's
settings is held for a person even when it passes.

## The gate

`python3 tools/repair_gate.py [--workspace DIR]` runs, in order:

1. `dotnet test Schema/Schema.Core.Tests`, judged from its TRX file. **Skipped with a note** while no dotnet SDK is
   installed (this Mac today); installing the .NET 8 SDK turns it on.
2. The EditMode tests of `TestProjects/Unity6` (the package and its tests, via `testables`) through
   `~/.unity/bin/unity test`, queued in the machine-wide `unity` pool of project-skills' `heavy_job.py` so it waits
   for the tank game's Unity jobs rather than running beside them.

Pass or fail comes from the NUnit XML (each test-case's `result`, cross-checked with the root's `total` and
`failed`), never from an exit code. No results file, or zero tests, is red. A failing test is named in the verdict.
The tests listed in `tools/repair_gate_known_failures.json` (today one: `SchemaEditorWindowTests.Test_Click_Create_New_Scheme_Button`,
pre-existing) do not make it red; the gate says when one starts passing so it can be taken off the list. Exit 0 is
pass, 1 red, 3 "could not start" (no Unity CLI, no test project), which repairkit reports as the machine's fault,
not the change's. The gate's own tests: `~/.schema-unity/repairkit/venv/bin/python -m pytest tools/tests -q`.

## Run it under launchd

The plist runs `tools/repairkit.sh serve` from the primary checkout `/Users/zappy/src/Schema-Unity`, so that checkout
must be on `main` and contain `tools/` (this branch landed) first.

```sh
cp tools/launchd/com.devzappy.schema-unity.repairkit.plist ~/Library/LaunchAgents/
launchctl bootstrap gui/$(id -u) ~/Library/LaunchAgents/com.devzappy.schema-unity.repairkit.plist
tools/repairkit.sh health                                    # answers once launchd has started it
launchctl kickstart -k gui/$(id -u)/com.devzappy.schema-unity.repairkit   # restart (tools/repairkit.sh restart does this)
launchctl bootout gui/$(id -u)/com.devzappy.schema-unity.repairkit        # stop and unload
```

Stop any `tools/repairkit.sh start` daemon first (`tools/repairkit.sh stop`): one daemon per socket (repairkit holds
a lock). launchd's output goes to `~/.schema-unity/repairkit/launchd.out`; repairkit's own log is `repairkit.log`.

## Overrides (for testing)

`SCHEMA_REPAIR_BASE` (default `main`), `SCHEMA_REPAIR_STATE_ROOT` (default `~/.schema-unity/repairkit`; keep the
socket path under 104 characters), `SCHEMA_REPAIR_PROJECT_ROOT` (default the primary checkout of this repository),
`SCHEMA_REPAIR_LIBRARY_SEED` (a `Library/` to clone into workspaces), `SCHEMA_UNITY_CLI`. They are read by the host
when the daemon starts.

## The end-to-end check

`WindowCaptureVerify.SubmitToDaemon` (in the package's tests) pings the daemon, then files one report through the
collector's own `FeedbackSender` with the Editor's context and a small PNG, using the socket in
`SCHEMA_VERIFY_SOCKET` and an outbox under `SCHEMA_VERIFY_OUT` (never the EditorPrefs key or the shared outbox):

```sh
SCHEMA_VERIFY_SOCKET=$HOME/.schema-unity/repairkit/repairkit.sock SCHEMA_VERIFY_OUT=/tmp/sk-e2e \
  ~/.unity/bin/unity run TestProjects/Unity6 -- -executeMethod Schema.Unity.Editor.Feedback.Tests.WindowCaptureVerify.SubmitToDaemon
tools/repairkit.sh list-reports --limit 1
```

It files a real report labelled `[phase 2 e2e smoke]`; dismiss it with
`tools/repairkit.sh update-report ID --status dismissed` if it is in the way.
