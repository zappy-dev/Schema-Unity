---
name: table-authoring loads before the first edit when a new row is missing from the code
tags: [table-authoring, trigger]
max_turns: 12
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
I added a Spice row to Cargo in the data editor and saved it, then put it in TradeBoard's OpeningDemand, but the compiler says there's no such constant. Make it build.
