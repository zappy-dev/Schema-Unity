---
name: column-types does not load for changing a serialized field's type
tags: [column-types, near-miss]
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
The Ship prefab has a serialized int field called rig (0 is fore-and-aft, 1 is square) that I want to turn into a Rig enum without losing what's already set on the prefabs and scenes. What's the safe way to change the field's type?
