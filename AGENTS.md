# Agent Context

Full project context (architecture, conventions, commands, gotchas, current status) lives in `CLAUDE.md`.
Read it first. The detailed audit and roadmap are in `PROJECT_STATUS_REPORT.md`.

## Git attribution (strict, permanent)

Never add Claude, Anthropic, or AI attribution as git commit author or via `Co-authored-by` trailer. All commits must
strictly use the local git config user and email with zero AI co-authorship metadata.

## Learning walkthrough (strict, permanent)

Every change (ticket, feature, fix, refactor) ships `docs/learning/<ticket>-technical-walkthrough.md` before its PR
opens. It is a teaching document explaining what changed in each file, how, and why. Follow the format and required
sections in `docs/learning/README.md`.

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: **none active**. The Phase 10 remediation (except Identity hardening FR-R006, config-driven CORS T062
  and `ISystemClock` removal T065) and the 2026-10-13 baseline remediation are merged into `main` (`5016865`).
- Branch: create a short-lived task branch from `main` and open a PR back into `main`.
- Next candidates: order lifecycle design (`PROJECT_STATUS_REPORT.md` §17 Phase B) or Delivery readiness Phase 5
  (`docs/delivery-backend-readiness-plan.md`).
- Governing principles: `.specify/memory/constitution.md` v3.0.1.

Keep business names `CustomerId` and `Delivery.AssignedAgentId` (FKs reference `AspNetUsers.Id`). Domain and
Application must stay free of Duende, IdentityServer, JWT, EF Core, and ASP.NET Core types. The single sanctioned
exception is Domain's `Microsoft.Extensions.Identity.Stores` reference, so that `User` can inherit `IdentityUser<int>`.
<!-- SPECKIT END -->
