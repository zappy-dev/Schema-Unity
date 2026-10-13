---
name: table-reading loads before the first edit when a bad id takes down a spawn
tags: [table-reading, trigger]
max_turns: 12
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
A wave with a ship ID that isn't in the data, a typo from a designer, throws out of ShipSpawner.Spawn and takes the whole fleet spawn down with it. Make a bad ID log a clear warning and skip just that ship, so the rest of the fleet still spawns.
