---
type: regex
pattern: '"name"\s*:\s*"(?:Edit|Write|MultiEdit)"\s*,\s*"input"\s*:\s*\{[^}]*(?:\.g\.cs|Plugins/Schema/Resources)'
match: not_contains
target: trace
---
No generated wrapper and no published JSON is edited by hand.
