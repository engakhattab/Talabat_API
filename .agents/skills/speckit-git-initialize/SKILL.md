---
name: speckit-git-initialize
description: "[DISABLED] Suggest repository init commands — never execute. Read-only mode."
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: git:commands/speckit.git.initialize.md
  policy: safe-repository-mode-v1
---

# Initialize Git Repository — DISABLED (Read-Only Mode)

> [!CAUTION]
> **SAFE REPOSITORY MODE ACTIVE.** This skill is permanently set to read-only.
> It must NEVER initialize repositories, create commits, or execute any repository-modifying command.

## Behavior

This command is **disabled by policy**. When invoked (directly or via hook), it must:

1. **Refuse to execute** any Git write operation.
2. **Check** if a Git repository already exists (read-only: `git rev-parse --is-inside-work-tree`).
3. **Output a suggestion** showing the user the initialization commands.
4. **Label the output** with `⚠️ MANUAL EXECUTION REQUIRED`.
5. **Never** run `git init`, `git add .`, `git commit`, or any initialization script.
6. **Never** invoke `.specify/extensions/git/scripts/*/initialize-repo.*`.

## Required Output Format

When this skill is triggered, output ONLY the following:

```
⚠️ MANUAL EXECUTION REQUIRED — Repository initialization is disabled (Safe Repository Mode)

To initialize a Git repository manually, run:

  git init
  git add .
  git commit -m "Initial commit"

This skill will not execute these commands automatically.
```

If a repository already exists, output:

```
ℹ️ Git repository already detected. No action needed.
```

## Forbidden Operations

- `git init` (any form)
- `git add .` (any form)
- `git commit` (any form)
- `git config init.defaultBranch`
- Execution of `initialize-repo.sh` or `initialize-repo.ps1`
- Any shell command that initializes a repository or creates an initial commit