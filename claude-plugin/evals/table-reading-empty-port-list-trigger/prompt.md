---
name: table-reading loads before the first edit when data is missing only in a build
tags: [table-reading, trigger]
max_turns: 12
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
On a development build the harbor screen comes up with no ports at all, though it's fine in the editor, and nothing useful shows in the log. Find out why and fix it.
