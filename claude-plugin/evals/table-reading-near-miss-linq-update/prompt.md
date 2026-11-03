---
name: table-reading does not load for a per-frame allocation in unrelated code
tags: [table-reading, near-miss]
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
The Profiler shows WindModel.Update allocating every frame and causing GC spikes once a few hundred ships are out. Fix it without changing what it does.
