# Skills for AI agents: a Claude Code plugin in this repository

**Recommendation.** Keep the skills that teach an AI agent to use Schema in this repository, as a Claude Code
plugin ([`claude-plugin/`](../claude-plugin/README.md)), and install that plugin in every project that uses
Schema. Each project keeps one thin skill of its own for what only it knows (its paths, scripts and tests) and
links the plugin's skills for the mechanics. Do not copy the mechanics into each project.

## Why here and not in each project

1. **The facts are about the package, so they change with the package.** What Publish writes, how a result
   throws, which conversions corrupt data: these are true of Schema 0.3.x for every project. The pull request that
   fixes one of them can delete its workaround from the skill and its row from the issue list in the same change.
   A copy in a project cannot see that pull request. Before this plugin the only Schema skills lived in one game
   project's private repository, about 22 KB of codegen rules, result semantics and 21 numbered package issues,
   unreachable by every other user of the package.
2. **It measurably changes what an agent does.** In a small Unity project that uses Schema, with no Schema skill
   installed, an agent given six realistic requests loaded no skill, and five of the six edited files first (one run
   each). Asked to fix a missing generated constant, it edited the generated wrapper or the published JSON by hand;
   asked to turn a text column into a link, it guessed the column's type block and edited the published copy too;
   asked whether Convert Type was safe on a column with a blank cell, it did not know that the blank is filled with
   the first row. With the plugin, all six requests loaded the matching skill before the first edit (12 of 12 runs),
   six neighbouring requests loaded none (12 of 12), and both behaviour checks passed (4 of 4). The numbers and the
   method are in [claude-plugin/evals/README.md](../claude-plugin/evals/README.md).
3. **A public repository forces the line between what is generic and what is private.** Everything in the plugin
   has to be true in a project it has never seen, with neutral example tables. A project's facts cannot leak into it.
4. **One copy drifts less than many.** The package already has docs ([Key Concepts](KeyConcepts.md),
   [Editor Guide](EditorGuide.md), [Runtime Guide](RuntimeGuide.md)). The skills link them, at the tag that
   matches the package version, and add only what they do not say, so the docs stay the source of truth.

## What stays in the project

A thin project skill (`.claude/skills/<name>/SKILL.md`, or a skills-directory plugin if the project keeps
evals) that holds only local facts and says which plugin skill to load for the rest. Local facts are:

- where the project keeps its staging, published and generated files, if they are not the defaults;
- the scripts, seeds and tests the project wrapped around Schema (a headless export, a type-migration guard);
- its naming rules, which table is read by which class, and which helper classes extend the generated entries;
- its hot-path and performance rules, and how it lands changes.

A thin skill for the ship-trading example project used by the plugin's evals:

```markdown
---
name: data-tables
description: Work with Tidewater's tables (Ships, Ports, Cargo) -- add a column or row, run tools/export-tables.sh, or read a table from a script. Use when asked to add a stat to a ship, port or cargo, or when export-tables.sh fails. The Schema mechanics are in the schema-unity plugin.
metadata:
  kind: runbook
---

# Tidewater data tables

Mechanics (the three copies of a table, Publish, reading a result safely, Reference and Color columns): load
`schema-unity:table-authoring`, `schema-unity:table-reading`, `schema-unity:column-types`. They describe
package 0.3.x; this project pins 0.3.1 at e9089e0.

- Staging files are in `Content/`, generated wrappers in `Assets/Scripts/Schemes` (namespace `Tidewater.Schemes`).
- Headless, run `tools/export-tables.sh` instead of Publish; it needs the editor closed.
- Port links are read with `PortsEntry.NextPortOrNull()` (`Assets/Scripts/Schemes/PortLinks.cs`).
```

When the plugin and the project disagree, the project's skill names the plugin's rule it overrides and why.

## Versioning against the package

- The plugin's `version` is `<package major>.<package minor>.<plugin revision>`. Plugin 0.3.x describes package 0.3.x.
- Every skill opens with a line saying which package line it describes and what it was checked against
  ("Describes package `com.devzappy.schema.unity` 0.3.x, checked at 0.3.1 plus commit `e9089e0`"), tags what it
  asserts `ran`, `read` or `unverified`, and states known problems as open at that pin. The package version alone is
  not enough: the 0.3.1 release does not compile in a player build, and the commit that fixes it carries the same
  `package.json` version.
- `claude-plugin/scripts/check_pin.py` fails when `package.json` moves to a new minor version while the plugin, a pin
  line or a docs link still names the old one. Run it in the change that bumps the package. The failure is the
  prompt to re-read every skill, re-run the repros in `known-issues.md`, drop the workarounds a release fixed
  (in the same change as the fix), and bump the plugin.
- A project on an older package installs the plugin from a checkout at the matching tag, so the skills it loads
  describe the package it has.

## Who maintains it

The Schema maintainers own the plugin, because only they change the behaviour it describes. The rule is the one the
docs already follow: a pull request that changes what a skill states changes the skill. A project that
finds something new (an issue, a trap, a better rule) sends it here as a pull request and keeps no private copy.
Each project owns its thin skill. Before a release, run the plugin's evals once (about $2 for a pass of 14
cases at one run each; they start model sessions) and commit `evals/coverage.json`.

## What it costs, and what is not settled

- **Context.** The three descriptions are about 600 tokens in every session of every project that installs the plugin
  (`claude plugin details` estimates about 800), and 2 to 2.3k tokens when a skill loads. Install it at project
  scope, in projects that use Schema, not at user scope.
- **Drift between plugin and package.** It is a real risk, and it is mitigated, not removed: pin lines, the check
  script and the issue list's "open at" wording make a stale skill visible. A skill that is wrong about a fixed bug
  makes an agent apply a workaround that is no longer needed, which is cheap; one that is wrong about a new bug is
  the dangerous case, which is why a behaviour change must update the skill in the same pull request.
- **Install path.** The marketplace manifest and the install steps were run against a local clone. The
  `zappy-dev/Schema-Unity` form is unverified until the plugin is on the default branch, and a project's teammates
  need a Claude Code release that has plugin marketplaces.
- **Triggering is statistical.** The evals show each skill loads before the first edit for the requests written, and
  not for neighbouring ones. They are two requests per skill per direction; they do not say how often a skill loads
  in a real session, and a session's skill list is fixed when it starts.
- **The evaluator sees only a plugin.** A project's own evals can test its thin skill, not whether the agent then
  loads the plugin's skills; that is tested here.
