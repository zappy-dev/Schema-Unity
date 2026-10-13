---
name: table-authoring loads before the first edit when a feature needs a new tunable value
tags: [table-authoring, trigger]
max_turns: 12
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
Right now every ship burns fuel at the same rate, which is a constant in Ship.cs. The designers want to tune it per ship, so make Ship read its own burn rate from the ship data. Sloop 0.8 a second, Brig 1.0, Galleon 1.6.
