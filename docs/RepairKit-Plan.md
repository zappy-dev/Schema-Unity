# Plan: RepairKit feedback for the Schema Editor

Status: approved 2026-11-03 (defaults taken on every question, section 7); phase 0 in progress on branch
`repairkit-feedback`.

## 1. Goal

While editing tables in **Tools > Schema Editor**, in any Unity project that uses the package (the tank game
first), a person presses **Send Feedback**, types what is wrong, and the report lands in a local
[repairkit](https://github.com/zappy-dev/repairkit) daemon **for this repository**, with a screenshot of the Schema
Editor window taken at the moment of submission and enough context to reproduce it. Later phases let that daemon
repair Schema-Unity in a worktree, gate the fix, and promote it locally. Nothing is pushed.

Success, end to end: the user is in the tank game's Editor, the Coax row's tooltip column renders cut off, they press
Send Feedback, and within seconds `repairkit list_reports` on the Schema-Unity daemon shows the report with
`photos: [schema-editor.png]` and a `context` naming the host project, the package commit and the open scheme.

## 2. Why now, and what it is not

The tank game adopted Schema for tuning tables (tank game D-083). Using it day to day produces Schema-Editor
feedback (the first: Publish is one click away and writes files a baking project must never ship). Today that
feedback has nowhere to go but a chat.

Not in scope: Schema's runtime, player builds, or a web collector. The collector is Editor-only; an Editor on a desktop
can open a Unix socket, so no HTTP service is needed.

Prior art: `~/src/feedback-loop-unity` is a scaffold of the same idea with its own server (dispatch stubbed). This plan
does not build on it: repairkit already is the server, the gate and the promote.

## 3. Facts the plan rests on (checked 2026-11-03)

| Fact | Where |
|---|---|
| repairkit takes a report with `submit(message, source, context)` and images with `attach_photos(id, [{name, data}])`, `data` a data-URL, at most 2 MB each | `repairkit/contract/ops.json`; tank `tools/dashboard/repairkit_bridge.py:150` |
| `context.hold_for_review` holds a validated repair for review; `acceptance` and `resume_from` steer it | `ops.json`, `submit` |
| The wire is one JSON request line over a Unix socket, one JSON reply line | `repairkit/clients/csharp/RepairkitClient.cs` `CallAsync` |
| The generated C# client uses **System.Text.Json**, which Unity 6's Mono profile does not ship | same file, `using System.Text.Json` |
| `SchemaEditorWindow` is IMGUI (`OnGUI`, `SchemaEditorWindow.cs:591`) | package `Editor/UI/` |
| Publish writes JSON to the hard-coded `Assets/Plugins/Schema/Resources` and C# to `CSharpExportPath` | `Runtime/UnityAssetLinker.cs:21`, `Editor/EditorStorageUtility.cs:34` |
| A host for a Unity project that is not Mino exists to copy | `repairkit/examples/unity_host.py` |
| Schema-Unity is already a Nexus project | `nexus projects` |
| Tests: `dotnet test Schema/Schema.Core.Tests` (no Unity) and SchemaPlayground EditMode (`Schema.Unity.Editor.Tests.asmdef`) | repo |

## 4. Phases

### Phase 0: two spikes, no report filed (zero change to the package)

In SchemaPlayground only, an Editor script on a throwaway branch:

- **0a Transport.** Port `CallAsync` to Newtonsoft (already a Schema dependency): one line out, one line in. Call
  `ping` on the tank game's running daemon socket (`~/.tankslop/repairkit/graham_4player_tank_game/`), so no new
  daemon is needed to prove `UnixDomainSocketEndPoint` connects from Unity 6 on macOS.
- **0b Screenshot.** Capture the Schema Editor window with `InternalEditorUtility.ReadScreenPixel(position.min, w, h)`
  scaled by `EditorGUIUtility.pixelsPerPoint` (Retina), encode PNG, report its size. Also try `GrabPixels` on the
  window's GUIView as the fallback if macOS Screen Recording permission blocks `ReadScreenPixel`.

**Done:** a ping reply printed in the Console and a correct, full-resolution PNG of the window on disk. If either
fails, the plan changes before any package code is written.

### Phase 1: the collector, report-only

In the package, `Editor/Feedback/` under its own asmdef `Schema.Unity.Editor.Feedback` (Editor platform only):

- **Entry points:** a **Feedback** button in the Schema Editor toolbar and **Tools/Schema/Send Feedback…**. Both
  capture the window **first**, then open the dialog, so the dialog is never in the shot.
- **Dialog:** message (required), the screenshot thumbnail with a remove toggle, an "include the open scheme's data"
  toggle, Send. Ctrl/Cmd+Enter sends.
- **Settings (per machine, EditorPrefs):** daemon socket path; empty means the button explains how to set it and
  nothing else happens. No project file changes, so a host project gains nothing to commit.
- **Report:** `source: "schema-editor"`; `context` = host project path and name, Unity version, package version and
  git commit (from `PackageInfo`), Manifest and Content paths, selected scheme and selected cell, the last 50 Console
  lines from Schema, and, if toggled, the open scheme's staging JSON. `context.hold_for_review: "phase 1: report only"`
  so nothing repairs until the gate is trusted.
- **Outbox:** if the daemon is unreachable the report and PNG are written to `~/.schema-unity/feedback-outbox/` and
  sent on the next successful connect, so no report is lost to a stopped daemon.
- **Tests (EditMode, SchemaPlayground):** context assembly; the outbox round-trip against a fake socket server; the
  2 MB cap (downscale, then JPEG); empty-socket-path refusal.

**Done:** the end-to-end success in section 1, from the tank game's Editor, against the Schema-Unity daemon of phase 2
(or, before it exists, the outbox).

### Phase 2: a Schema-Unity daemon

- `tools/repair_host.py`, copied from `examples/unity_host.py` and checked against the tank game's
  `tools/repairkit_host.py` for the two things the example lacks: a worktree workspace that warms the Unity `Library`,
  and the wrapper runtime.
- State root `~/.schema-unity/repairkit/` (the socket path must stay under 104 characters).
- **Gate:** `dotnet test Schema/Schema.Core.Tests` first (seconds, no Unity), then SchemaPlayground EditMode through the
  `unity` CLI with an XML pass/fail read, never the exit code. The Unity half takes the same `unity-editor` lease the
  tank game uses.
- repairkit installed from a tag into the daemon's own venv, run under launchd, `doctor` green on unchanged code.

**Done:** `health.self_check` passes; the phase 1 report shows in `list_reports`.

### Phase 3: repairs on

- Drop `hold_for_review` for reports the user sends to repair; the agent prompt says to reproduce first in
  SchemaPlayground, using the attached scheme JSON as a fixture.
- Evidence: an "after" screenshot of the window from an EditMode capture where the fix is visual; `demo_required`
  false otherwise.
- A promoted fix lands on Schema-Unity's integration branch locally. Consumers pin the package by full commit, so the
  tank game takes the fix as its own change (a pin bump through its `land` tool); the report's `commit_sha` is the pin.

**Done:** one real report from the tank game's Editor repaired, gated and promoted, and the tank game pinned to it.

## 4b. Acceptance criteria

| # | Criterion (from the user's side) | Check |
|---|---|---|
| A0.1 | A Unity 6 Editor on macOS gets a `ping` reply from a repairkit daemon socket | spike, Console output |
| A0.2 | A PNG of the Schema Editor window comes out at full Retina resolution, not black, without the dialog in it | spike, the file, by eye |
| A1.1 | Send Feedback from the tank game's Editor files a report whose `source` is `schema-editor` with `schema-editor.png` attached | e2e against a daemon, `list_reports` |
| A1.2 | The report's context names the host project, Unity version, package version and commit, and the open scheme | EditMode test on context assembly |
| A1.3 | With the daemon stopped, a report survives in the outbox and is sent on the next connect | EditMode test, fake socket server |
| A1.4 | A screenshot over 2 MB is downscaled, then sent as JPEG, never refused | EditMode test |
| A1.5 | With no socket path set, the button explains setup and sends nothing; the host project's files do not change | EditMode test + `git status` in the host |
| A1.6 | No report repairs in phase 1 (`hold_for_review` set) | EditMode test on the request |
| A2.1 | The Schema-Unity daemon's `health.self_check` passes on unchanged code | `doctor` |
| A2.2 | The gate fails when a Core test fails and when an EditMode test fails, read from results, not exit codes | inject a failing test into a throwaway workspace |
| A3.1 | One real report from the tank game is repaired, gated and promoted onto Schema-Unity's integration branch | report `commit_sha` |
| A3.2 | The tank game pins that commit and its gate (incl. `SchemaTuningBakeTests`) passes | tank `tools/validate.sh --fast` XML |
| A3.3 | The person who filed it says the fix is right | human |

## 4c. The decision to record (when approved)

Schema Editor feedback goes to a local repairkit daemon for Schema-Unity over its Unix socket; the collector lives in
the package (Editor only, inert until a socket path is set per machine); it is report-only until the gate is trusted;
fixes reach consumers only as a pin bump through the consumer's own gate. **Rejected:** building on
`feedback-loop-unity`'s own server (repairkit already is one); an HTTP collector (the Editor can open a socket);
routing Schema reports through the tank game's daemon (a repair would run against the wrong repository).

## 5. Lanes

| Lane | Phase | Agent | Parallel with |
|---|---|---|---|
| Spikes | 0a, 0b | one Sonnet agent in a Schema-Unity worktree | nothing; gates the rest |
| Collector | 1 | Sonnet | Daemon |
| Daemon | 2 | Opus (host and gate are where a wrong call is costly) | Collector |
| Repairs on | 3 | the lead, with the user | after both |

## 6. Risks

| Risk | Mitigation, cheapest first |
|---|---|
| `ReadScreenPixel` needs macOS Screen Recording permission, or returns black | 0b tries it first; fallback `GrabPixels` on the GUIView; last resort, a render of the table to a texture |
| System.Text.Json missing in Unity | 0a ports the 30-line `CallAsync` to Newtonsoft; later, repairkit's client generator gains a Unity flavour so the op list cannot drift |
| The Unity half of the gate contends with the tank game for the Editor lease | Core tests run first and fail fast; EditMode runs queued like the tank game's batch jobs |
| Host project data (the open scheme) leaves the host repo | Off by a toggle; only a local daemon ever sees it; nothing is uploaded |
| The daemon serves a branch that is not the package's release line | Open question 1 |
| A Schema-Unity repair breaks the tank game | The tank game pins by commit and its own gate (SchemaTuningBakeTests) runs on the pin bump |

## 7. Decisions (the user approved the plan with the defaults, 2026-11-03)

1. **Integration branch: `main`.** `runtime-player-build` (e9089e0, the tank game's pin) was already merged into
   `main` and `origin/main`, so the daemon serves `main`. Tagging 0.3.2 is a release, the user's call.
2. **Screenshot scope:** the Schema Editor window only.
3. **Where reports are reviewed:** repairkit's dashboard for phases 1-2, the Nexus board from phase 3.
4. **Who gets the button:** every Schema user, inert until a socket path is set.
5. **Unity 6 test project** (asked while building: SchemaPlayground and SampleProjects/EmptyProject are Unity
   2020.3.48f1, which is not installed here). A new `TestProjects/Unity6/` references the package and its tests by
   `file:` path; phase 0, the collector's tests and the gate's EditMode half run there. SchemaPlayground stays on 2020.3,
   untouched. The collector compiles only on Unity 2021.2 or newer (an asmdef version define), so a 2020.3 user sees
   nothing; read every "SchemaPlayground EditMode" above as `TestProjects/Unity6` EditMode.

## 8. Feedback to file first

Once phase 1 works: "Publish is one click from Save in a project that bakes tables (tank game D-083); give the
Manifest a per-project *Publish disabled* setting, and make the Resources output path configurable." The tank game
also gets a guard test that fails while `Assets/Plugins/Schema/Resources` or `Assets/Scripts/Schemes` exists.
