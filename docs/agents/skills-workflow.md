# Skills workflow (Matt Pocock engineering skills)

Which skill to run, in what order, for the work this project actually does. It sits under
[`ROADMAP.md`](../../ROADMAP.md): the roadmap decides **what** happens next (phases, gates, strictly sequential);
this file decides **how** each piece of work moves. Skill names are typed as `/mattpocock-skills:<name>`;
the short form `/<name>` is used below.

Setup is done (`/setup-matt-pocock-skills`): tracker = GitHub Issues, default triage labels, single-context domain
docs. Re-run it only to change one of those.

## 1. The main flow: design phase → tickets → build

Use this for every phase that starts with an open question (Phase 1 was one; so are the owner/admin API design,
Phase 11 events, and each M7 feature). Steps 1–3 run in **one unbroken session**; do not `/clear` or `/compact`
until the tickets exist.

| Step | Skill | What it produces here |
|---|---|---|
| 1 | `/grill-with-docs` | The decisions, asked one round at a time. Updates `CONTEXT.md` and adds ADRs in `docs/adr/` as answers land. Replaces plain grilling whenever we are in this repo. |
| 1a *(only if a question needs running code)* | `/prototype` | A throwaway answer to one state-model or UI question, kept on a `prototype/<name>` branch and linked from the ticket. |
| 2 | `/to-spec` | The design document under `specs/<nnn>-<slug>/` (like `specs/011-order-lifecycle/design.md`), published to the issue tracker. The owner accepts it before step 3. |
| 3 | `/to-tickets` | Vertical-slice tickets (T0, T1, …) as GitHub issues with blocking links, plus the phase plan table in `ROADMAP.md`. Creates the integration branch `feature/phase-<n>-<slug>`. |

Tickets made by `/to-tickets` are already agent-ready: **do not run `/triage` on them.**

## 2. Building a ticket

One ticket per session, blockers first, strictly in order.

1. **`/clear`** (fresh context: the ticket is self-contained).
2. **`/implement T<n>`**. It does, in order:
   - reads the issue, `ROADMAP.md`, the design and `CONTEXT.md`;
   - creates the task branch off the integration branch;
   - drives **`/tdd`**: names the design §4 tests, commits them failing (red), then the production code (green),
     refactors with the suite green; a retired test is replaced in the same red commit, naming the rule ID;
   - regenerates any changed OpenAPI contract **in its own commit**;
   - runs the full `dotnet test` once at the end;
   - runs **`/code-review`** against the integration branch (Standards + Spec, in parallel) and fixes what is in
     scope, test-first;
   - pushes and opens the PR into the integration branch.
3. **Auto-fix on the PR** (app CI monitor) instead of watching CI by hand. Merge when CI is green, then close the
   issue with a comment pointing at the PR (issues close automatically only on merges into `main`).
4. After the last ticket: run the phase gate, open the single PR from the integration branch into `main`, update
   "Current position" in `ROADMAP.md`.

Use **`/tdd`** on its own for a small behaviour that has no ticket, and **`/code-review`** on its own to review any
branch or PR against a fixed point (e.g. `since main`).

## 3. When something else comes up

| Situation | Skill |
|---|---|
| A bug that resists a first look, a flaky test, a regression | `/diagnosing-bugs` (builds a command that goes red on the bug first, ends with a regression test) |
| Issues opened by someone else, or raw requests piling up | `/triage` |
| A merge or rebase stopped on conflicts | `/resolving-merge-conflicts` (in an app worktree, sync with `sync_with_base_branch` first) |
| A factual question about a library or spec (Duende, EF Core, Angular, SignalR, outbox patterns) | `/research` (background agent, cited Markdown file in the repo; take the result into `/grill-with-docs`) |
| A step only a human can do (secrets, CI variables, staging setup in Phase 12, a production cutover) | `/wizard` |
| A word in the code or docs means two things, or a new term appears | `/domain-modeling` (updates `CONTEXT.md` / ADRs) |
| Designing the shape of one module or seam | `/codebase-design` |
| Spare time between phases | `/improve-codebase-architecture` (finds deepening opportunities; a picked one goes into `/grill-with-docs`) |
| Editing `CLAUDE.md`, `AGENTS.md` or anything under `docs/agents/` | `/writing-for-agents` |
| Not sure which skill fits | `/ask-matt` |

## 4. Between phases and sessions

- **Continue** in the same session when the next step needs what is already in context.
- **`/clear`** between tickets, and whenever nothing in the session matters to what comes next.
- **`/compact`** at a phase boundary when the session is long but its thinking still matters (e.g. before
  `/to-tickets` if the grilling ran long).
- **`/handoff`** when work moves to **another directory**, which here means the backend → frontend switch
  (`D:\link-dev\talabat-frontend`, Phase 3 onwards): hand off the contract change and what the screens need.
- **`/wait-what`** when an explanation did not land; it restates it in `CONTEXT.md` vocabulary.

## 5. Mapping to the roadmap

| Roadmap phase type | Flow |
|---|---|
| Design (Phase 1; the design work that opens Phases 6, 8, 11, 14+) | §1 (grill → spec → tickets) |
| Backend build (Phases 2, 4, 6, 8, 10, 11) | §2, one `/implement` per ticket |
| Frontend build (Phases 3, 5, 7, 9) | `/handoff` from backend, then §2 in the frontend repo |
| Deployment and release (Phases 12, 13) | §2 for code, `/wizard` for the human-only steps |
