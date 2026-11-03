---
name: table-authoring does not load for a database column question
tags: [table-authoring, near-miss]
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
Side question, nothing to do with this repo: on our PostgreSQL backend I need to add a NOT NULL column with a default value to a table with millions of rows without holding a long lock. What's the safe sequence?
