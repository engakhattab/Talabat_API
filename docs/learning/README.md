# Learning walkthroughs

The project owner's primary goal is to **learn** Clean Architecture, DDD and enterprise backend engineering, not only
to ship code. So every change ships a **walkthrough**: a teaching document that explains what was built, how, and why.

## The rule (permanent, for every agent and every human)

Any ticket, feature, bug fix or refactor that changes code, tests, contracts or configuration ships a walkthrough
**before its PR is opened**. The walkthrough is part of the definition of done, alongside green tests and committed
contracts. Commit it on the task branch as its own `docs(learning): ...` commit and link it in the PR body.

- File: `docs/learning/<ticket>-technical-walkthrough.md`. Use the ticket id when there is one (`T3-...`); otherwise
  the branch slug (`fix-pkcs-dos-advisories-technical-walkthrough.md`).
- Language: English, using the [`CONTEXT.md`](../../CONTEXT.md) glossary terms.
- Pure documentation or tooling-only changes need a short walkthrough (sections 1 and 3 only).

## Required sections

1. **Domain and business intent**: the ubiquitous-language terms involved, and every business rule or design decision
   fulfilled or revised (rule IDs, design sections, ADRs).
2. **Layer-by-layer flow**: how the request travels through API → Application → Domain → Infrastructure and back,
   including the read side the change affects.
3. **File-by-file breakdown**: every file created, modified or deleted (production *and* tests). For each: what
   changed, and **why it lives there**. Give the architectural or pattern rationale (aggregate invariant vs use-case
   orchestration, value-object snapshot, transaction boundary, exception vs result, and so on).
4. **Tricky concepts and decisions**: the edge cases and the reasoning behind them, such as concurrency and RowVersion,
   transactions and rollback, idempotency, error and status-code mapping, authorization, and trade-offs not taken.
5. **Commit trail**: the red/green/contract/docs commits and what each proves.
6. **Self-check questions**: 3 to 5 questions the owner can answer to confirm understanding.

Teach rather than just list. A good walkthrough lets the owner rebuild the change from scratch and defend each
decision in a code review. [`T2-technical-walkthrough.md`](T2-technical-walkthrough.md) (on the Phase 2 branch) is the
reference example.
