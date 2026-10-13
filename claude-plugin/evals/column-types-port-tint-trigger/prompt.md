---
name: column-types loads before the first edit when a text column holds a colour
tags: [column-types, trigger]
max_turns: 12
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
PortLabel runs ColorUtility.TryParseHtmlString on the Tint column every time it draws a label and quietly goes white if someone mistypes the hex. I'd like the table itself to hold a real colour, so a bad value is caught in the editor and the parsing goes away.
