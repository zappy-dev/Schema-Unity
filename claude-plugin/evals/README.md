# Evals for the schema-unity plugin

Fourteen cases for three skills: per skill two triggers, two near-misses and, for two skills, a behaviour case.
They start model sessions and cost money (**$3.90 for the recorded run, 14 cases at 2 runs each**; one pass at one
run each is about $2). Run them from this repository:

```sh
claude plugin eval claude-plugin --runs 1 --ablation none --no-publish -j 4 --judge-model sonnet \
  --scaffold --allow-tools Edit Write --trust-plugin --max-cost-usd 5
```

`--scaffold` stages the fixture; `--allow-tools Edit Write` is what lets the model edit in the throwaway directory
(without it the before-edit graders pass vacuously); `--ablation none` because the default adds a no-plugin arm that
turns the `tool_used: Skill` graders into unscored indicators; `--judge-model sonnet` because the default judge
failed correct long answers elsewhere. **Check that every case's cost is above $0.** When the account is rate
limited the run does not fail: sessions answer "You've hit your session limit" at $0, and every near-miss passes.

## What a case is

- **Fixture** (`_fixture/`): a small Unity project, "Tidewater", whose design data is three Schema tables (Ships,
  Ports, Cargo), with the staging files, the published copies, the generated wrappers and a few scripts that read
  them. Each case's `scaffold.sh` copies it into the run's working directory, adds the case's `overlay/` if any (a
  row saved but not published; a screen that reads before `Initialize`; a Unity Localization string table), and
  commits it.
- **Triggers** are written as a person in the middle of the work would write them: no skill name, no
  generated-code mechanics, no "plugin" and no "Schema". Two graders: the skill loads (`tool_used: Skill`), and no
  Edit or Write happens before it does (a regex on the trace).
- **Near-misses** are neighbouring requests asked in the same repository: a PostgreSQL column and a foreign key, a
  `JsonUtility` save file, a per-frame allocation in unrelated code, a Unity Localization string table, a serialized
  field's type. None of the plugin's skills may load.
- **Behaviour** cases measure what the skills teach: a missing constant must not be "fixed" by editing the
  generated file or the published JSON, and must be met with "publish the table"; Convert Type on a column with
  a blank cell must be warned about, because at this package pin it fills the blank with the first row.

## Baseline and result

Baseline: the committed evals (commit `625583d`) against this plugin with a manifest and **no skills**, one run
each, 2026-10-13, $2.23. Result: 6 of 14 cases pass, and the six are the near-misses, which pass because
nothing can load. Then the same evals with the skills, final run at 2 runs each, 14 of 14 cases, 28 of 28 runs,
$3.90 (`coverage.json` records it).

| | no skills (1 run each) | with skills (2 runs each) |
|---|---|---|
| trigger: skill loads | 0 of 6 | 12 of 12 |
| trigger: no edit before the load | 1 of 6 (that run edited nothing at all) | 12 of 12 |
| near-miss: nothing loads | 6 of 6 | 12 of 12 |
| behaviour: all graders | 0 of 2 | 4 of 4 |

What the model did without skills: in the missing-constant case it edited the generated wrapper or the published
JSON by hand; asked to turn a text column into a link it guessed the type block and edited the published copy as
well as the staging file; asked about Convert Type it did not know that a blank cell is filled with the first row.

### Triggers (the skill loads before the first Edit or Write)

| case | no skills | with skills |
|---|---|---|
| `column-types-next-port-link-trigger` | loads 0/1, before-edit grader 0/1 | loads 2/2, before-edit grader 2/2 |
| `column-types-port-tint-trigger` | loads 0/1, before-edit grader 1/1 | loads 2/2, before-edit grader 2/2 |
| `table-authoring-missing-constant-trigger` | loads 0/1, before-edit grader 0/1 | loads 2/2, before-edit grader 2/2 |
| `table-authoring-ship-fuel-trigger` | loads 0/1, before-edit grader 0/1 | loads 2/2, before-edit grader 2/2 |
| `table-reading-empty-port-list-trigger` | loads 0/1, before-edit grader 0/1 | loads 2/2, before-edit grader 2/2 |
| `table-reading-unknown-ship-id-trigger` | loads 0/1, before-edit grader 0/1 | loads 2/2, before-edit grader 2/2 |

### Near-misses (no skill of the plugin loads)

| case | no skills | with skills |
|---|---|---|
| `column-types-near-miss-postgres-foreign-key` | passes 1/1 | passes 2/2 |
| `column-types-near-miss-serialized-field-type` | passes 1/1 | passes 2/2 |
| `table-authoring-near-miss-localization-table` | passes 1/1 | passes 2/2 |
| `table-authoring-near-miss-postgres-column` | passes 1/1 | passes 2/2 |
| `table-reading-near-miss-json-save` | passes 1/1 | passes 2/2 |
| `table-reading-near-miss-linq-update` | passes 1/1 | passes 2/2 |

### Behaviour (what the skills teach)

| case | no skills | with skills |
|---|---|---|
| `column-types-convert-blank-link-behaviour` | passes 0/1 | passes 2/2 |
| `table-authoring-published-copies-behaviour` | passes 0/1 | passes 2/2 |

## How the cases changed while they were written

- First run with skills: 13 of 14. The localization near-miss loaded `table-authoring`: the fixture had no string table,
  so the model went to look for it among the Schema tables. The overlay gives it a real Unity Localization table.
- That was not enough. At 5 runs it still loaded the skill once in five (trace: "This is a Schema-managed table, so
  I'll load the skill", before it had read the project), and in a 2-run record it loaded twice. The description
  opened with "table" and put its exclusion last. It now opens with the package and what it is not for (Unity
  Localization string tables, SQL, other table libraries). Measured after that change: the near-miss 6 of 6 and both
  `table-authoring` triggers 3 of 3, then the recorded run above.
- Both changes are to the fixture and the description; no prompt was weakened.

## Limits

Two runs per case, one model, one small fixture: this shows the descriptions load the skills for the requests
written and not for the neighbouring ones. It does not give a trigger rate for real sessions. The behaviour graders
include a model judge for one criterion each. The behaviour cases pin package behaviour at commit `e9089e0`
(Convert Type fills a blank with the first row); when a release fixes that, update the case and the skill together.
A running session's skill list is fixed when it starts, so a green run does not show that an open session has the plugin.
