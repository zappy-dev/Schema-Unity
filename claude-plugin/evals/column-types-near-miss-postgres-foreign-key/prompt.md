---
name: column-types does not load for a database foreign-key question
tags: [column-types, near-miss]
max_turns: 8
allowed_tools: [Read, Glob, Grep, Skill, Edit, Write]
---
Unrelated to the game: in PostgreSQL I have an orders table with a customer_id column that is a plain integer. How do I turn it into a foreign key to customers(id) on a table with millions of rows without a long lock, and what happens to rows whose customer_id points at nothing?
