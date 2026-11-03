# schema-unity: Claude Code skills for projects that use Schema

Three skills that teach an AI agent how to work with the [Schema](../README.md) data-table package in a Unity
project: change a table and its generated C#, read tables from game code, and make a column a checked link or
a real colour. They describe Schema package **0.3.x** and were checked at **0.3.1 plus commit `e9089e0`**.

| Skill | Loads when | Kind |
|---|---|---|
| `schema-unity:table-authoring` | a feature needs a new value, row or table in a Schema table; a generated constant or property is missing; someone wants to edit a `*Scheme.g.cs` or a published JSON; a table works in the editor and not in a build | runbook |
| `schema-unity:table-reading` | game code reads a table; `InvalidOperationException`/"Entry not found"; an empty screen or `No Scheme ... loaded` in a build; a mistyped ID takes down a spawn; lookups allocate; a player build fails in `Schema.Runtime` | runbook |
| `schema-unity:column-types` | a text column should be a link or a colour; asked whether Convert Type is safe; a link throws or reads blank; a new row starts linked to the first row | playbook, with a runbook and a preflight script |

The skills link Schema's own docs ([Key Concepts](../docs/KeyConcepts.md), [Editor Guide](../docs/EditorGuide.md),
[Runtime Guide](../docs/RuntimeGuide.md)) instead of copying them, and add what the docs do not say: what
Publish writes and in what order to change things, how to read a result without an exception, and the traps of
Reference and Color columns, with the issues behind them.

## Install, for a project that uses Schema

Declaring both lines at project scope writes them to the project's `.claude/settings.json`, so committing that
file gives teammates the plugin:

```sh
claude plugin marketplace add zappy-dev/Schema-Unity --scope project
claude plugin install schema-unity@schema --scope project
```

Or in one step, on Claude Code 2.1.275 or later: `claude plugin install schema-unity --marketplace zappy-dev/Schema-Unity --scope project`
(this declares the marketplace in your user settings instead). Start a new session, or run `/reload-plugins`;
`claude plugin details schema-unity@schema` lists the three skills.

How these steps are known: with the marketplace given as a local clone of this repository
(`claude plugin marketplace add <path to clone> --scope project`), the install, the settings it wrote, the
three skills and their token cost (about 800 tokens always loaded, 2 to 2.3k when one loads) were run on
2026-10-13 (**ran**). The `zappy-dev/Schema-Unity` form reads the same marketplace file from GitHub and is
**unverified** until this directory is on the default branch. `--sparse .claude-plugin claude-plugin` limits the
checkout to the plugin if you do not want the Unity sample projects.

The plugin sits in a subdirectory on purpose. This repository holds Unity projects; a `plugin.json` at the root would
make the whole checkout the plugin, and Claude Code's evaluator refuses a tree that large.

### What stays in your project

A thin skill of your own, in `.claude/skills/`, for what only your project knows: where your staging,
published and generated files live if they are not the defaults, the scripts or tests you wrap around
Schema, your naming rules, which tables exist and which code reads them. It names those, and links these
skills for the mechanics. [docs/ClaudePlugin.md](../docs/ClaudePlugin.md) has an example and the reasons.

## Which version

The plugin's `version` is `<package major>.<package minor>.<plugin revision>`. Each skill says which package
line it describes ("Describes package ... 0.3.x, checked at ...") and tags what it asserts:

- **ran**: executed against that package commit by a project using Schema (2026-10-12), and for the helper
  script by the tests here;
- **read**: read in the package source at that commit;
- **unverified**: said so, and not claimed.

Problems the skills describe (`column-types/references/known-issues.md`) are open **at that pin**. When a release
fixes one, the same change deletes the workaround from the skill and updates its pin. `scripts/check_pin.py`
fails when `package.json` moves to a new minor version without the plugin and its skills following.

| Plugin | Package | Note |
|---|---|---|
| 0.3.x | 0.3.x, checked at 0.3.1 + `e9089e0` | The released 0.3.1 does not compile in a player build (runtime code imports `UnityEditor`); `e9089e0` fixes it and is unreleased at this pin. `table-reading` says what to depend on. |

## Maintaining it

Whoever changes behaviour a skill describes changes the skill in the same pull request: the package's maintainers,
for the package. A project that finds something new sends it here (a row in `known-issues.md`, a rule in the
skill) rather than keeping a private copy. Before a release:

```sh
python3 claude-plugin/scripts/check_pin.py                        # the pin still matches the package
python3 claude-plugin/skills/column-types/scripts/test_preflight.py
claude plugin validate claude-plugin
claude plugin eval claude-plugin --runs 2 --ablation none --no-publish --judge-model sonnet --scaffold --allow-tools Edit Write
```

The evals are in [evals/](evals/README.md): per skill, requests written as a person in the middle of the work,
run in a small Unity-and-Schema project, graded on whether the skill loads before the first edit, plus
neighbouring requests that must not load it. They start model sessions and cost money (about $2 for one pass of the
14 cases); read `evals/README.md` before running them and check that each case's cost is above zero.
