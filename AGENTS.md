# Agent Context

<!-- SPECKIT START -->
Current roadmap increment:

- Phase: `Talabat.Customer.API` — customer-facing business API (Phase 7)
- Roadmap: `PROJECT_IMPLEMENTATION_ROADMAP.md`
- Spec: `specs/003-customer-api/spec.md`
- Governing scope: `.specify/memory/constitution.md` -> "Current Phase Scope: Phase 7"

Scope guard for the current increment: rename `Talabat.API` -> `Talabat.Customer.API` (before adding
endpoints) and remove the template `WeatherForecast`; add an `AddApplication()` DI extension; expose
customer use cases through thin attribute-routed controllers (Catalog anonymous; Cart/Customer/Address/
Checkout/Orders authenticated on `/api/me/...`); add `DomainException` -> ProblemDetails mapping,
JwtBearer validation against the Identity authority, explicit `Customer` profile creation on first use
(`POST /api/me/profile`; the token `sub` sets the linkage) via an Application use case, a read-only
framework-neutral `ICurrentUser`, dev `localhost` CORS, `/health`, OpenAPI, and
`tests/Talabat.Customer.API.Tests`. Owner-scoped endpoints return `409 ProfileNotCreated` until a
profile exists. Also add the missing production `IClock`/`IRestaurantLocalTimeProvider` implementations
in Infrastructure (only test fakes exist today) and register them.

Do not: add DeliveryAgent or account-management endpoints; add a token-issuing client to
`Talabat.Identity` or mint real user tokens (tests use test-minted JWTs; real acquisition is Phase 9);
finalize token/claims/scopes or linkage rules (Phase 9); put business logic or EF types in controllers;
expose Domain entities/read models as responses; add frontend. `Talabat.Customer.API` must not reference
`Talabat.Identity`. Domain and Application stay free of web/Identity packages; the only Domain changes
permitted are the nullable framework-neutral `IdentityUserId` scalar on `Customer` and a
`CreateForAccount` factory that sets it — the `Customer` name/age invariants stay intact (no empty
profiles).
<!-- SPECKIT END -->
