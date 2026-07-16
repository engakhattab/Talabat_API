# Agent Context

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: Minimal Identity/Auth Setup Before Business APIs (Phase 6)
- Roadmap: `PROJECT_IMPLEMENTATION_ROADMAP.md`
- Manual guide: `docs/identity/duende-aspnet-identity-setup-guide.md`
- Completed persistence plan: `specs/002-persistence-infrastructure/plan.md`

Scope guard for the current increment: review and complete only the compile-ready Identity persistence
foundation. This increment may make `ApplicationUser` a valid ASP.NET Core Identity persistence model,
extend the existing Infrastructure `TalabatDbContext` with the Identity model, add the approved one-way
`Talabat.Identity -> Talabat.Infrastructure` project reference, add the host to the solution, and apply
the existing approved OpenAPI vulnerability fix.

Do not add register/login/logout endpoints, Duende or Identity service registration, migrations,
business APIs, Angular/frontend code, profile linkage, custom tokens, or advanced authentication in
this increment. Domain and Application must remain free of Identity/Auth dependencies and types.
<!-- SPECKIT END -->
