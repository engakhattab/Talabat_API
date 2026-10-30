# Walkthrough: Arabic skills guide and per-ticket decision tree

> Branch: `docs/skills-guide-ar` → `main`. Documentation-only change, so this is a short walkthrough (sections 1 and 3).

## 1. Intent

The owner did not remember which Matt Pocock skill to run for which kind of ticket, or how to tell whether a ticket
needs a spec first. The existing English guide (`docs/agents/skills-workflow.md`) described the flows, but it had no
decision rule for "spec or not". It was also missing two steps that became standard in T2: approving test seams
before `/implement`, and writing the learning walkthrough before the PR.

## 3. File-by-file breakdown

| File | Change | Why there |
|---|---|---|
| `docs/agents/skills-guide-ar.md` (new) | Arabic, practical guide: a decision tree with four routes (A: needs a spec, B: ready ticket, C: bug, D: small change), copy-paste commands, mid-work skills, session management, and a closing checklist | Next to the other agent and workflow docs. Written in Arabic because it is the owner's working reference; the English file stays the agents' source of truth. |
| `docs/agents/skills-workflow.md` | Adds the seam-approval step (`/tdd` before `/implement`) and the walkthrough step, renumbers the steps, and points to the Arabic guide | Keeps the English flow in step with how T2 actually ran, so agents following it do the same. |

The Arabic guide links back to `skills-workflow.md` for details instead of copying them. It repeats only what a person
needs at a glance (the steps and commands), so the two files have a clear split: the *decision* lives in the Arabic
guide, the *full mechanics* in the English one.
