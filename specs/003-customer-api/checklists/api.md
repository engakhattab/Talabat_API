# API Requirements Quality Checklist: Talabat Customer API

**Purpose**: Validate completeness, clarity, consistency, and measurability of the Customer API specification before implementation planning  
**Created**: 2026-07-16  
**Updated**: 2026-07-16 (post-plan alignment)  
**Depth**: Standard  
**Audience**: Reviewer (pre-plan quality gate)  
**Feature**: [spec.md](../spec.md)

## Requirement Completeness

- [x] CHK001 - Are HTTP methods (GET, POST, PUT, DELETE, PATCH) specified for each endpoint listed in FR-006 through FR-011? [Completeness, Spec §FR-006–FR-011]
- [x] CHK002 - Are request body shapes specified for mutation endpoints (add to cart, update quantity, update profile, add address, checkout)? [Completeness, Spec §FR-007–FR-010]
- [x] CHK003 - Are response body shapes specified for each endpoint, including success and error cases? [Completeness, Gap]
- [x] CHK004 - Are HTTP status codes defined for all success responses (200 vs 201 vs 204), not just error responses? [Completeness, Spec §FR-004]
- [x] CHK005 - Is the complete list of Domain exception types and their HTTP status code mappings documented in FR-004? [Completeness, Spec §FR-004]
- [x] CHK006 - Are pagination parameters (page size limits, default values, maximum page size) defined for list endpoints in FR-006 and FR-011? [Completeness, Spec §FR-006, §FR-011]
- [x] CHK007 - Are filtering and sorting parameters specified for the restaurant browse endpoint in FR-006? [Completeness, Spec §FR-006]
- [x] CHK008 - Is the specific route structure documented beyond the `/api/me/...` pattern (e.g., full URL templates for each endpoint)? [Completeness, Spec §FR-012]
- [x] CHK009 - Are the fields required to create a `Customer` profile (full name, positive age, optional phone) defined? [Completeness, Spec §FR-008, §FR-018]
- [x] CHK010 - Is the `AddApplication()` DI extension's scope documented — which handlers it registers and how they are discovered? [Completeness, Spec §FR-003]
- [x] CHK011 - Are the specific health check components listed (database only, or also other dependencies)? [Completeness, Spec §FR-021]

## Requirement Clarity

- [x] CHK012 - Is "smallest viable audience and scope contract" in FR-005 defined with a concrete minimum (e.g., a single audience string, a single scope)? [Clarity, Spec §FR-005]
- [x] CHK013 - Is "optional filtering/pagination" in FR-006 quantified — which filter fields are supported, or is it left to planning? [Clarity, Spec §FR-006]
- [x] CHK014 - Is "structured unavailable-item details" in FR-010 defined with a specific response shape or left to planning? [Clarity, Spec §FR-010]
- [x] CHK015 - Does "thin transport adapters" in FR-014 have measurable criteria (e.g., max lines of code, no conditional branching on business rules)? [Clarity, Spec §FR-014]
- [x] CHK016 - Is the create-on-first-use behavior in FR-018 unambiguous — which fields are required and what sets the `IdentityUserId` linkage (token `sub`)? [Clarity, Spec §FR-018]
- [x] CHK017 - Is "permissive localhost-only CORS policy" in FR-020 specific about which HTTP methods and headers are allowed? [Clarity, Spec §FR-020]
- [x] CHK018 - Is "standard healthy/unhealthy response" in FR-021 defined — custom JSON body vs. standard health check response format? [Clarity, Spec §FR-021]

## Requirement Consistency

- [x] CHK019 - Is the `DomainValidationException → 422 or 400` ambiguity in FR-004 resolved — one status code must be chosen for consistency? [Consistency, Spec §FR-004]
- [x] CHK020 - Are authentication requirements consistent — FR-006 says anonymous for catalog, but does the OpenAPI doc in FR-013 reflect this split? [Consistency, Spec §FR-006, §FR-013]
- [x] CHK021 - Is the create-profile-then-use flow consistent — do owner-scoped endpoints return `409 ProfileNotCreated` until a profile exists, per scenario 10 and FR-022? [Consistency, Spec §FR-018, §FR-022, §Scenario 10]
- [x] CHK022 - Does the "host-specific DTOs" requirement in FR-017 conflict or overlap with Application read models — is the mapping boundary clear? [Consistency, Spec §FR-017]

## Acceptance Criteria Quality

- [x] CHK023 - Is SC-001's "within 4 weeks" measurable independently of external factors (e.g., team size, parallel work)? [Measurability, Spec §SC-001]
- [x] CHK024 - Is SC-002's "under 2 seconds" measured at what boundary — server response time, or including network latency? [Measurability, Spec §SC-002]
- [x] CHK025 - Is SC-005's "structured, human-readable error responses" defined with specific structure requirements (e.g., RFC 9457 fields: type, title, status, detail)? [Measurability, Spec §SC-005]
- [x] CHK026 - Is SC-006's "complete and accurately describes all endpoints" verifiable — what tool or method validates completeness? [Measurability, Spec §SC-006]
- [x] CHK027 - Is SC-007's "zero business logic" in controllers defined with a testable boundary (e.g., no if/switch on domain state, no domain method calls outside handler delegation)? [Measurability, Spec §SC-007]
- [x] CHK028 - Is SC-009's "structured authorization matrix" format defined — table columns, required fields per endpoint? [Measurability, Spec §SC-009]

## Scenario Coverage

- [x] CHK029 - Are requirements defined for what happens when a customer's cart belongs to a restaurant that has since become inactive/closed? [Coverage, Gap]
- [x] CHK030 - Are requirements defined for the concurrent first-time profile-create race — two simultaneous `POST /api/me/profile` from one account (unique index → `409 ProfileAlreadyExists`)? [Coverage, Gap]
- [x] CHK031 - Is the profile GET behavior before creation defined (`404 ProfileNotCreated`), and after creation (the customer-supplied profile)? [Coverage, Spec §FR-008, §FR-018]
- [x] CHK032 - Are requirements defined for checkout when the customer has no default address set? [Coverage, Spec §FR-010]
- [x] CHK033 - Are requirements defined for cart operations when the customer already has an active cart from a different restaurant? [Coverage, Spec §FR-007]

## Edge Case Coverage

- [x] CHK034 - Is the behavior specified when a token's subject claim does not match any known Identity account (e.g., deleted account, forged claim)? [Edge Case, Spec §FR-018]
- [x] CHK035 - Is the behavior specified when an order detail request uses a valid order ID that belongs to another customer — `403 Forbidden` vs `404 Not Found`? [Edge Case, Spec §FR-011]
- [x] CHK036 - Is the behavior specified when the OpenAPI document is requested with invalid Accept headers? [Edge Case, Spec §FR-013]
- [x] CHK037 - Are request size limits or payload validation requirements defined to prevent abuse? [Edge Case, Gap]

## Security & Auth Boundary

- [x] CHK038 - Are requirements defined for which specific token claims are read and how an invalid/missing `sub` claim is handled? [Coverage, Spec §FR-005, §FR-018]
- [x] CHK039 - Is the trust boundary documented — does the API validate the token signature, issuer, audience, and expiration, or rely on middleware defaults? [Completeness, Spec §FR-005]
- [x] CHK040 - Are requirements defined for preventing enumeration attacks through owner-scoped endpoints (e.g., returning 404 instead of 403 for non-owned resources)? [Security, Gap]
- [x] CHK041 - Is the scope of the provisional `sub`-to-Customer linkage key documented — is it stored as a column on Customer, a separate mapping table, or resolved at runtime only? [Clarity, Spec §FR-018]
- [x] CHK042 - Are requirements defined for handling revoked or rotated tokens between the Identity authority and the Customer API? [Edge Case, Gap]

## Dependencies & Assumptions

- [x] CHK043 - Is the assumption that Application handlers accept a `customerId` parameter validated — the API resolves it from `ICurrentUser.CustomerId` (after profile creation) with no handler-interface change? [Assumption, Spec §Assumptions]
- [x] CHK044 - Is the assumption that `AddInfrastructure()` already exists validated against the actual codebase? [Assumption, Spec §FR-003]
- [x] CHK045 - Is the dependency on `Talabat.Identity` discovery endpoint availability documented with a fallback or cached-key strategy? [Dependency, Spec §FR-005]
- [x] CHK046 - Are the rename implications for existing CI, documentation cross-references, and `appsettings` files captured in FR-001? [Completeness, Spec §FR-001]

## Architectural Constraints

- [x] CHK047 - Is the boundary between "host-specific DTOs" (FR-017) and Application read models defined — who owns the mapping and where does it live? [Clarity, Spec §FR-017]
- [x] CHK048 - Are requirements defined for preventing Domain/Infrastructure type leakage — is there a compile-time or test-time enforcement mechanism specified? [Coverage, Spec §FR-014]
- [x] CHK049 - Is the relationship between FR-019 test project and the existing `tests/Talabat.Application.Tests` clear — shared test infrastructure, separate test database, or independent? [Completeness, Spec §FR-019]
