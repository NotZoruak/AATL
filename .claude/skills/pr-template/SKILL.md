---
name: pr-template
description: Use this template skill as a starting point when contributing a new MaaHub skill.
when_to_use:
  - When creating a new MaaHub skill entry
  - When contributors need a complete SKILL.md example
allowed-tools:
  - Bash
  - Read
  - Grep
argument-hint: Fill in the repository, task, or target path this skill should operate on
user-invocable: true
disable-model-invocation: false
paths:
  - "src/**"
  - "docs/**"
model: sonnet
effort: medium
context:
  - references/checklist.md
shell: bash
---

# PR Template Skill

Replace this template with your actual skill instructions.

## When to use

- Describe the exact triggers for your skill.
- Mention any situations where the skill should not be used.

## Procedure

1. Inspect the target context.
2. Read any files listed under `references/` if needed.
3. Run helper scripts from `scripts/` if the skill depends on them.
4. Return structured, actionable output.

## Output format

- Describe the expected response format here.
