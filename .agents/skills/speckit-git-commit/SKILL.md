---
name: speckit-git-commit
description: "[DISABLED] Suggest commit commands — never execute. Read-only mode."
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: git:commands/speckit.git.commit.md
  policy: safe-repository-mode-v1
---

# Auto-Commit Changes — DISABLED (Read-Only Mode)

> [!CAUTION]
> **SAFE REPOSITORY MODE ACTIVE.** This skill is permanently set to read-only.
> It must NEVER execute `git add`, `git commit`, or any repository-modifying command.

## Behavior

This command is **disabled by policy**. When invoked (directly or via hook), it must:

1. **Refuse to execute** any Git write operation.
2. **Output a suggestion** showing the user what commands they could run manually.
3. **Label the output** with `⚠️ MANUAL EXECUTION REQUIRED`.
4. **Never** run `git add .`, `git commit`, or any staging/commit script.
5. **Never** invoke `.specify/extensions/git/scripts/*/auto-commit.*`.

## Required Output Format

When this skill is triggered, output ONLY the following:

```
⚠️ MANUAL EXECUTION REQUIRED — Auto-commit is disabled (Safe Repository Mode)

If you want to commit your current changes, run these commands manually:

  git add .
  git commit -m "<descriptive message>"

This skill will not execute these commands automatically.
```

## Forbidden Operations

- `git add` (any form)
- `git commit` (any form)
- `git stage` (any form)
- Execution of `auto-commit.sh` or `auto-commit.ps1`
- Any shell command that modifies the Git staging area or commit history

## Configuration

This skill ignores all configuration in `.specify/extensions/git/git-config.yml`.
The `auto_commit` section has no effect. All events are treated as `enabled: false`.