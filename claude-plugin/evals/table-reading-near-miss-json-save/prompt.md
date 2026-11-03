---
name: table-reading does not load for a Unity JSON serialization question
tags: [table-reading, near-miss]
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
Our save files lose the HoldByCargoId dictionary: SaveGame writes it with JsonUtility and it comes back empty. What's the least invasive fix?
