---
name: speckit-git-feature
description: "[DISABLED] Suggest feature branch commands — never execute. Read-only mode."
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: github-spec-kit
  source: git:commands/speckit.git.feature.md
  policy: safe-repository-mode-v1
---

# Create Feature Branch — DISABLED (Read-Only Mode)

> [!CAUTION]
> **SAFE REPOSITORY MODE ACTIVE.** This skill is permanently set to read-only.
> It must NEVER create branches, switch branches, or execute any repository-modifying command.

## Behavior

This command is **disabled by policy**. When invoked (directly or via hook), it must:

1. **Refuse to execute** any Git write operation.
2. **Analyze** the feature description and generate a suggested branch name.
3. **Output a suggestion** showing the user the branch creation command.
4. **Label the output** with `⚠️ MANUAL EXECUTION REQUIRED`.
5. **Never** run `git checkout -b`, `git switch -c`, `git branch`, or any branch creation script.
6. **Never** invoke `.specify/extensions/git/scripts/*/create-new-feature.*`.

## Required Output Format

When this skill is triggered, output ONLY the following:

```
⚠️ MANUAL EXECUTION REQUIRED — Branch creation is disabled (Safe Repository Mode)

Suggested branch name: <generated-branch-name>

To create this branch manually, run:

  git checkout -b <generated-branch-name>

This skill will not execute this command automatically.
```

## Branch Name Generation (Read-Only)

The skill may still perform read-only analysis to suggest a branch name:
- Read the feature description
- Generate a concise 2-4 word short name
- Determine the numbering mode (sequential/timestamp) from config
- Output the suggested name as text only

## Forbidden Operations

- `git checkout -b` (any form)
- `git switch -c` (any form)
- `git branch <name>` (any form)
- Execution of `create-new-feature.sh` or `create-new-feature.ps1`
- Any shell command that creates or switches branches