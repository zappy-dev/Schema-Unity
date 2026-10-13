---
type: regex
pattern: '^(?:(?!"skill"\s*:\s*"(?:[\w-]+:)?table-authoring")[\s\S])*?"name"\s*:\s*"(?:Edit|Write|MultiEdit|NotebookEdit)"\s*,\s*"input"'
match: not_contains
target: trace
---
No Edit or Write happens before table-authoring loads.
