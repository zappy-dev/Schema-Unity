---
name: repair-loop
description: File Schema Editor or Schema package feedback to the local Schema-Unity repairkit daemon, follow each repair until it lands on the package's main, and integrate the fixes into a project that uses the package (release, tag, re-pin, gate, land). Use when the user reports a problem with the Schema Editor or the package and wants it fixed ("file this to the schema repairkit", "send Schema feedback"), asks what happened to a Schema report or repair, when a report sits pending and no repair started, when a repair is held for review, failed or blocked, or when asked to "integrate the schema repairs", "release the package", "pin the new Schema" or "pull the fixes into the game". Not for changing a table's data (table-authoring) or reading tables from code (table-reading).
metadata:
  kind: runbook
---

# The Schema feedback and repair loop

**A report becomes a repair branch, a passing repair becomes a commit on the package's `main`, and a project gets it
only when the package is released and the project is re-pinned.** Each step has its own command. Follow them in this
order and confirm each one before you take the next.

Describes package `com.devzappy.schema.unity` 0.3.x with repairkit v1.2.1, checked 2026-11-03 (**ran**).
Background: `docs/RepairKit-Daemon.md` and `docs/RepairKit-Plan.md` in the Schema-Unity checkout.

## 0. Where things are

| What | Where |
|---|---|
| Daemon (launchd `com.devzappy.schema-unity.repairkit`) | socket `~/.schema-unity/repairkit/repairkit.sock`, state and log in `~/.schema-unity/repairkit/` |
| CLI | `tools/repairkit.sh <op>` in the Schema-Unity primary checkout (`~/src/Schema-Unity`), which must stay on `main` |
| Gate a repair must pass | `tools/repair_gate.py`: Core tests (dotnet) + EditMode in `TestProjects/Unity6`, known failures listed with reasons |

This daemon belongs to Schema-Unity. Never submit a Schema report to another project's daemon, and never submit a
game's report to this one.

## 1. File a report

- **From the Schema Editor**: press **Feedback**. From package 0.3.3 the socket field fills itself when the daemon
  sits at the standard path, and a leading `~` is expanded. On 0.3.2, paste the full path
  `/Users/<you>/.schema-unity/repairkit/repairkit.sock`.
- **From a session**: `python3 <this skill>/scripts/file_report.py "<what is wrong, what you expected>" --photo
  <png|jpg> --context '<json object>'`. It sends `submit`, then `attach_photos`, then `maybe_auto_fix`, prints the
  report id, and uses only the standard library. Do not use `tools/repairkit.sh submit --context`: repairkit 1.2.1's
  CLI fails with "dictionary update sequence element #0" on any JSON context.

Write the report the way a person would: the symptom, where it shows up (project, Unity version, package version),
and the expected behaviour as a checkable sentence. Name the scope when a fix needs work in another repository
too: a repair only changes Schema-Unity.

## 2. Make sure a repair started

`tools/repairkit.sh running-agents-summary` lists the running repair and the queue; one repair runs at a time.
A report that stays `pending` was never asked for. repairkit starts a repair only when the client sends
`maybe_auto_fix` after `submit`. The collector sends it from 0.3.3; 0.3.2 does not. Ask for it by hand:
`tools/repairkit.sh maybe-auto-fix <id>`, or `tools/repairkit.sh enqueue-fix <id> --note "<extra context>"`, which
puts the note into the agent's prompt.

## 3. Follow it to an ending

To wait, start **one** background until-loop on `tools/repairkit.sh health` and wait until `queue.running`,
`queue.scheduled` and `queue.queued` all read 0 twice in a row, a minute apart. Do not poll in the foreground. Then
`tools/repairkit.sh get-report <id>` for each report:

| `status` | Meaning | You do |
|---|---|---|
| `fixed` | Passed the gate and merged into the primary checkout's `main`, locally | Section 4 |
| `awaiting_review` | Passed, but held because of the files it edited (gate, host, test-project settings, `package.json`) | `review-diff <id>`, show the user, and after their yes, `promote-reviewed <id> --commit <sha> --message "<their words>"` |
| `failed` | The agent or the gate failed; the summary says which | Read the summary and the log (`~/.schema-unity/repairkit/repairkit.log`). Retry with `enqueue-fix <id> --note` or report it to the user |
| `blocked` | The environment refused, e.g. the primary checkout is off `main` or has edits | Clear the block. The report becomes `pending` again by itself |

`git -C ~/src/Schema-Unity log --oneline` shows what landed.

## 4. Integrate into a project that uses the package

1. **Gate `main`**: `~/.schema-unity/repairkit/venv/bin/python tools/repair_gate.py --workspace .` in a worktree of
   `main` reads `GATE PASS`.
2. **Release**: bump `version` and `changelogUrl` in `SchemaPlayground/Packages/com.devzappy.schema.unity/package.json`
   and the two version lines in `README.md`, then commit `Release vX.Y.Z: ...` and fast-forward `main`.
   **Pushing `main` and the tag `vX.Y.Z` needs the user's yes for this release.** A yes for an earlier release does
   not carry over.
3. **Re-pin the project**: in a worktree of the project, set the package line in `Packages/manifest.json` to
   `...com.devzappy.schema.unity#<full 40-character sha of the tag commit>`. UPM needs the full sha. The first
   batch run updates `packages-lock.json`.
4. **Gate and land the project** the project's own way. In the tank game, run `tools/validate.sh --fast`, then
   `tools/land.py <branch> --approved-by "<the user's words>"`.
5. **Restart the daemon only if `tools/repair_host.py` changed**:
   `launchctl kickstart -k gui/$(id -u)/com.devzappy.schema-unity.repairkit`. The daemon loads the host once, at
   start.

An open Editor on the project picks up the new pin when it regains focus.

## Never

- Push, tag on the remote, or open a PR without the user's words for that change.
- Edit, stash or switch branches in the primary checkout `~/src/Schema-Unity`. Repairs merge into it.
- Promote a held repair the user has not seen, or write an approval the user did not type.
