# Walkthrough: making learning walkthroughs a permanent rule

> Branch: `docs/learning-walkthrough-rule` → `main`. Documentation-only change, so this is a short walkthrough
> (sections 1 and 3).

## 1. Intent

The owner's goal is to learn Clean Architecture, DDD and enterprise backend patterns from every change. T2 produced
the first walkthrough on request. This change turns that into a **permanent rule for every agent** (Claude, Codex or
any other) and every human contributor, so the walkthrough no longer depends on someone remembering to ask.

## 3. File-by-file breakdown

| File | Change | Why there |
|---|---|---|
| `docs/learning/README.md` (new) | The rule, the file naming, and the six required sections | **Single source of truth.** The format lives in one place, so changing it is a one-file edit. It sits next to the walkthroughs it governs. |
| `CLAUDE.md` (Conventions) | One bullet: walkthrough mandatory before the PR, with a pointer to the README | Claude Code loads `CLAUDE.md` into every session. A short *pointer* keeps that always-loaded cost low while making the rule impossible to miss. |
| `AGENTS.md` | A "Learning walkthrough (strict, permanent)" section with the same pointer | `AGENTS.md` is the file other agents read (for example Codex and Copilot agents). Placing it beside the other "strict, permanent" rule (git attribution) signals equal weight. |

The pointers restate only *when* the rule applies, never the format. Duplicating the section list in three files would
let them drift apart; that is the *duplication* smell applied to documentation.
