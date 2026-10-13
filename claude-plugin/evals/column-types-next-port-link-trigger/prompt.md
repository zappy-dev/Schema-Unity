---
name: column-types loads before the first edit when a text column should be a link
tags: [column-types, trigger]
max_turns: 12
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
NextPort in the Ports table is plain text holding another port's ID, and a typo in it only shows up at runtime when a voyage stalls. I want the data editor itself to reject a bad port ID. The last port still has no next port. Set that up and make VoyagePlanner work with it.
