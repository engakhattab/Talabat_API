# Delivery Backend Readiness Plan

## Scope and current-state decisions

This plan prepares the existing backend for the Delivery Angular application. It preserves the current dependency direction (`API -> Application -> Domain`, with Infrastructure implementing abstractions), uses the unified `User` aggregate and Identity roles, keeps Delivery lifecycle transitions server-authoritative, and adds no frontend code.

Already implemented and to be reused:

- `User` already owns `AgentApprovalStatus`, `DeliveryAgentStatus`, `VehicleType`, `CurrentLocation`, application submission, approval/rejection, online/offline, busy/available, suspension, and location-update behavior.
- `UserCapabilityService` already registers applicants, approves/rejects them transactionally, synchronizes capability roles, and refreshes the security stamp on approval.
- `TalabatProfileService` already emits `role` and `delivery_agent_id` only after `UserType.DeliveryAgent` is granted.
- `Talabat.Delivery.API` already validates `delivery.api`, requires the `DeliveryAgent` role for operational endpoints, resolves the current user from `sub`, rechecks persisted capabilities, and exposes assignment, lifecycle, history, online/offline, and location commands.
- Checkout already creates a `Delivery` for a successful order; `Delivery`, its row-version concurrency, repositories, handlers, SQL Server configuration, ProblemDetails mapping, OpenAPI security, and focused tests already exist.
- Development-only approval endpoints exist in `Talabat.Identity`, but they are not production Admin capability and must be replaced, not promoted as-is.
- A guarded Development/E2E provisioning CLI already exists in `Talabat.Identity`; Delivery fixtures should extend that boundary.

Confirmed gaps:

- No authenticated applicant-facing application/profile contract exists.
- No Admin API host or production-authorized approval endpoints exist.
- No read endpoint exposes authoritative `DeliveryAgentStatus`.
- Pending/active contracts expose IDs and customer data but no restaurant name or pickup address.
- Restaurants have no persisted pickup address or coordinates. Agent coordinates exist, but restaurant coordinates do not.
- Delivery-specific deterministic fixtures and an Admin OpenAPI document do not exist.

Implementation must start from a committed baseline; the current Phase 10 remediation and E2E-provisioner worktree changes should be finalized first.

## Phase 1 — Delivery Applicant Contract

**Objective**

Allow any authenticated Delivery SPA user with `delivery.api` scope to read their own application/profile and safely update editable fields before or after approval, without granting access to operational agent endpoints.

**Existing code to reuse**

- `User`, `AgentApprovalStatus`, `VehicleType`, `IUserRepository`, `ICurrentUser`, `CurrentUserCapabilityResolver`, `UseCaseResult`, `ApplicationErrorCodes`, and Delivery API ProblemDetails mapping.
- Existing public `POST /account/register/delivery-agent` remains the application-creation entry point unless registration UX is separately redesigned.

**Projects/layers affected**

- Domain: `Talabat.Domain/Aggregates/Users/User.cs` only if a focused applicant-profile update method is required.
- Application: new current-applicant profile query/update use cases under `DeliveryAgents` or `DeliveryApplicants`.
- Infrastructure: reuse `UserRepository`; add a narrow read implementation only if the existing aggregate query is insufficient.
- API: `Talabat.Delivery.API` controller, contracts, policy, OpenAPI, and tests.

**API/contracts**

- `GET /api/agent/me/application` -> status (`Pending`, `Approved`, or `Rejected`). Return 404 with a stable `errorCode` when the current user has never applied.
- `GET /api/agent/me/profile` -> `fullName`, `phoneNumber`, `vehicleType`, `applicationStatus`, and nullable `deliveryAgentStatus`.
- `PUT /api/agent/me/profile` -> update only `fullName`, `phoneNumber`, and `vehicleType`; never accept user ID, approval status, role, availability, or location.

**Authorization rules**

- Add `DeliveryApplicantAccess`: authenticated user plus `delivery.api` scope, with no `DeliveryAgent` role requirement.
- Resolve the user exclusively from `sub`/`ICurrentUser.UserId`.
- Keep all `/api/agent/deliveries`, `/api/agent/status`, and `/api/agent/location` endpoints under the existing `DeliveryAgentAccess` policy. Pending/rejected users must receive 403 there.

**Domain/application changes**

- Add a domain operation that validates and updates applicant/agent-owned profile fields while preserving approval and runtime status.
- Do not make profile edits resubmit a rejected application or convert status implicitly; any future resubmission is a separate business decision.
- Add stable error codes for missing application and invalid applicant profile.

**Identity implications**

- No new role or claim is required. Pending/rejected applicants authenticate and receive scope, but no `DeliveryAgent` role or `delivery_agent_id` claim.
- Profile reads must use persisted state, not stale token claims.

**Database migration needs**

- None; the required fields already exist in `AspNetUsers`.

**Focused tests**

- Domain tests for editable fields and immutable approval/runtime state.
- Application tests for current-user lookup, missing application, validation, and persistence.
- Delivery API tests proving scope-only applicant access, claim-derived identity, cross-user input absence, and 403 on operational endpoints for Pending/Rejected users.
- OpenAPI tests for schemas, ProblemDetails, and security requirements.

**Exit criteria**

- Pending, Approved, and Rejected users can read their own status/profile.
- Safe updates persist without changing approval, role, availability, or another user.
- Pending/Rejected tokens cannot access operational endpoints.

**Dependencies/blockers**

- Phase 10 remediation must be committed and green.
- Confirm whether rejected applicants may submit a new application; this phase deliberately does not infer that rule.

**Recommended commit message**

`feat(delivery): add applicant self-service contract`

## Phase 2 — Admin Approval Slice

**Objective**

Provide the smallest production-secured Admin API required to list, inspect, approve, and reject Delivery Agent applications.

**Existing code to reuse**

- `UserCapabilityService.ApproveDeliveryAgentAsync` / `RejectDeliveryAgentAsync`, `User.ApproveDeliveryAgentApplication`, `User.RejectDeliveryAgentApplication`, role synchronization, security-stamp refresh, `Admin` role/user type, role seeding, SQL Server infrastructure, and API security/OpenAPI patterns.
- Port relevant assertions from `AgentApprovalEndpointTests`; do not build a second approval engine.

**Projects/layers affected**

- New minimal host and tests: `Talabat.Admin.API` and `Talabat.Admin.API.Tests`.
- Application: pending-list and application-detail queries; approval/rejection may continue through `IUserCapabilityService`.
- Infrastructure: focused applicant read/query implementation.
- Identity: Admin API resource/scope/client registration and role claims.
- Remove or retain as explicitly Development-only the existing unprotected Identity approval routes after the Admin API is working; they must not remain the production path.

**API/contracts**

- `GET /api/delivery-agent-applications?status=Pending` -> minimal list with user ID, name, vehicle type, application status, and submitted timestamp.
- `GET /api/delivery-agent-applications/{userId:int}` -> application details with only review-relevant fields.
- `POST /api/delivery-agent-applications/{userId:int}/approve`.
- `POST /api/delivery-agent-applications/{userId:int}/reject`.
- Add committed `openapi/admin-api.json`; keep the surface restricted to this slice.

**Authorization rules**

- Require authenticated token, exact `admin.api` scope, `Admin` role, active/non-deleted persisted user, and the existing `sub` claim model.
- Route user IDs are valid here because an administrator manages another user; applicant `me` routes never accept them.
- Unauthorized/forbidden/not-found/conflict responses use existing ProblemDetails plus root `errorCode` conventions.

**Domain/application changes**

- Reuse current approval/rejection transitions. Add only read models/queries and any missing error codes.
- Approval remains atomic: set `Approved`, grant `UserType.DeliveryAgent`, initialize status to `Offline`, synchronize role, and refresh security stamp.
- Do not add restaurant, customer, or generic user administration.

**Identity implications**

- Add `admin.api` scope/resource exposing `role`; add a dedicated Admin client configuration rather than granting Admin scope to customer/delivery clients.
- Production admin identities must be created by an approved operational bootstrap process, never by a public registration endpoint.
- Real-token tests must prove role/scope combinations and post-approval Delivery authorization propagation.

**Database migration needs**

- None for approval state. If reliable `submittedAt` cannot be represented by existing audit fields, add a dedicated application-submitted timestamp only after confirming `CreatedAt` semantics are insufficient.

**Focused tests**

- Application query tests and existing approval lifecycle tests.
- Admin API integration tests for Admin success, non-Admin 403, missing scope 403, anonymous 401, list filtering, details, repeat approve/reject conflicts, and no unrelated PII.
- Identity real-token and security-stamp tests showing a newly approved user obtains DeliveryAgent authorization only through refreshed authentication.
- Admin OpenAPI quality/security/committed-document parity tests matching existing API suites.

**Exit criteria**

- A production-configured Admin token can list, inspect, approve, and reject applications.
- Non-Admins cannot access the slice.
- Approval results in authoritative `DeliveryAgent` role/capability and `Offline` status; a refreshed token passes Delivery API authorization.
- No production approval route depends on `IHostEnvironment.IsDevelopment()`.

**Dependencies/blockers**

- Phase 1 contract/status semantics.
- Decide the controlled production bootstrap mechanism and configured redirect/origin for the first Admin identity/client.

**Recommended commit message**

`feat(admin): add delivery applicant approval slice`

## Phase 3 — Agent Runtime State

**Objective**

Expose authoritative persisted Delivery Agent status while preserving the existing online/offline domain transitions as the sole writers.

**Existing code to reuse**

- `User.DeliveryAgentStatus`, `GoOnline`, `GoOffline`, `MarkBusy`, `MarkAvailable`, existing handlers, repository/unit of work, `StatusController`, and `DeliveryAgentAccess`.

**Projects/layers affected**

- Application: current-agent status query/handler.
- Delivery API: response contract plus `GET` action on the existing status route.
- Tests/OpenAPI only; no new state store.

**API/contracts**

- `GET /api/agent/status` -> `{ status }` using the existing `DeliveryAgentStatus` enum.
- Keep `PUT /api/agent/status/online` and `/offline` with their current 204 and ProblemDetails contracts.

**Authorization rules**

- Existing `DeliveryAgentAccess` only. Pending/Rejected applicants receive 403.
- Agent ID derives from authenticated current-user claims and persisted capability resolution.

**Domain/application changes**

- No new availability model or duplicate flag.
- Query persisted `User.DeliveryAgentStatus`; return 404/409 with stable codes for inconsistent agent state rather than guessing a default.
- Preserve current rules: Busy cannot go offline; Suspended cannot transition online/offline; assignment/lifecycle handlers remain responsible for Busy/Available transitions.

**Identity implications**

- None beyond the approved role/capability established in Phase 2.

**Database migration needs**

- None; status is already persisted and constrained.

**Focused tests**

- Application tests for Offline/Available/Busy/Suspended reads.
- API tests for GET status, existing PUT persistence, forbidden applicant access, and ProblemDetails conflicts.
- Regression tests proving assignment sets Busy and terminal/cancellation behavior releases the agent according to existing rules.

**Exit criteria**

- Status reads match SQL Server state after every online/offline/assignment/lifecycle transition.
- No client-owned availability state or parallel backend flag exists.

**Dependencies/blockers**

- Phase 2 approval propagation must produce an approved agent initialized to `Offline`.

**Recommended commit message**

`feat(delivery): expose authoritative agent status`

## Phase 4 — Delivery Operational Contract

**Objective**

Give couriers stable restaurant identity and pickup instructions while minimizing customer data and retaining server-owned lifecycle/location rules.

**Existing code to reuse**

- `Restaurant`, `Address`, `Delivery`, delivery creation after checkout, pending/active/history handlers, Delivery repository, `GeoLocation`, lifecycle handlers, and existing response mapping.

**Projects/layers affected**

- Domain: Restaurant pickup address and an immutable pickup snapshot on `Delivery`.
- Application: delivery creation and pending/active read DTOs.
- Infrastructure: EF mappings, seed data, repository projections, and migration.
- Delivery API: pending/active/history contracts and OpenAPI.

**API/contracts**

- Pending queue adds restaurant name and pickup locality/address; remove customer delivery city from the unassigned queue.
- Active delivery adds restaurant name plus pickup street/city/building/floor and keeps the delivery destination needed to complete the job.
- Remove `CustomerId` from active/history responses unless a separately documented operational need proves it necessary.
- Do not expose restaurant coordinates: none are reliably persisted today.

**Authorization rules**

- Existing DeliveryAgent policy and ownership checks remain.
- Pending queue remains visible only to approved agents; active/history data remains limited to the assigned current agent.

**Domain/application changes**

- Add a required pickup `Address` to `Restaurant`.
- Capture immutable restaurant name and pickup address into `Delivery` when checkout creates it, so an in-progress job does not change when restaurant data changes.
- Extend the create-for-order command/handler and DTO mapping; do not weaken existing delivery transition rules or row-version concurrency.
- Tighten `User.UpdateLocation`: accept coordinates only for approved agents in `Available` or `Busy` state; return a conflict for Offline/Suspended. Do not require an active delivery because Available agents may report position before assignment.

**Identity implications**

- None.

**Database migration needs**

- Required migration for Restaurant pickup-address columns and Delivery pickup snapshot columns.
- Backfill seeded/existing restaurants with reviewed addresses, backfill existing deliveries from their restaurant, then enforce non-null columns/check constraints. Do not fabricate coordinates.

**Focused tests**

- Restaurant/Delivery domain tests for required pickup data and immutable snapshots.
- Checkout delivery-creation tests proving correct snapshot capture.
- SQL Server persistence/migration tests and seed-data validation.
- Pending/active API tests for restaurant fields, assigned-agent ownership, and absence of unnecessary customer identifiers/data.
- UpdateLocation tests for valid coordinates, range failures, Offline/Suspended conflicts, and Available/Busy success.
- OpenAPI contract parity tests.

**Exit criteria**

- Pending deliveries identify the restaurant without disclosing customer destination data.
- Active delivery contains stable restaurant name/pickup address and necessary drop-off address.
- No unreliable coordinates are exposed.
- Location and lifecycle changes remain validated and persisted by the domain/application layer.

**Dependencies/blockers**

- Product owner/operations must supply trustworthy pickup addresses for existing seeded restaurants before migration is finalized.

**Recommended commit message**

`feat(delivery): add pickup-ready operational contracts`

## Phase 5 — Test and Contract Readiness

**Objective**

Provide deterministic backend-owned Delivery test state, publish reproducible Delivery/Admin contracts, and establish focused release gates before frontend implementation begins.

**Existing code to reuse**

- `Talabat.Identity --e2e-provision` command parsing, safety gates, marker ownership, JSON metadata, SQL Server transaction/idempotency tests, aggregate constructors, role synchronization, and existing runtime-vs-committed OpenAPI tests.

**Projects/layers affected**

- Identity command and Infrastructure Development/E2E provisioner/options/contracts.
- Delivery/Admin API committed OpenAPI documents and tests.
- Focused Domain/Application/Infrastructure/API/Identity test projects.

**API/contracts**

- Extend the existing non-HTTP CLI with delivery-specific operations such as `prepare-delivery` and `restore-delivery`; do not introduce another public provisioner.
- Versioned JSON metadata must identify the marker-owned pending applicant, approved agent, pending delivery, restaurant/pickup fixture, and supported reset operation without emitting credentials, tokens, connection strings, or customer PII.
- Regenerate `delivery-api.json` and new `admin-api.json` through the repository's OpenAPI build/runtime process; update downstream generated clients only in their owning contract workflow.

**Authorization rules**

- Provisioning remains unavailable outside Development/E2E, requires explicit enablement and environment-provided synthetic credentials, and positively verifies marker ownership before mutation.
- Provisioner setup is CLI-only and never participates in production request routing.

**Domain/application changes**

- Provision through existing aggregates/services where practical: pending applicant through application submission, approved agent through the approval service, pending delivery through order/delivery creation semantics.
- Reset mutable status/location/assignment state idempotently and delete or repair only marker-owned fixtures.

**Identity implications**

- Use separate environment-provided credentials for pending and approved synthetic users.
- Verify Pending has no DeliveryAgent role/claim; Approved has synchronized role/capability and a refreshed security stamp.

**Database migration needs**

- None beyond Phase 4. Provisioner data is runtime fixture data, not migration seed data.

**Focused tests**

- Real SQL Server idempotency sequence: prepare twice, mutate state, restore twice, prepare again.
- Assert pending/approved role and status boundaries, exactly one marker-owned pending delivery, stable IDs, safe JSON, and refusal in unsafe environments/configurations.
- Delivery/Admin API authorization, contract quality, and committed OpenAPI parity tests.
- Focused builds/tests for Domain, Application, Infrastructure E2E, Identity, Delivery API, and Admin API; no broad suite is required unless a focused failure indicates cross-cutting impact.

**Exit criteria**

- One command creates a repeatable pending applicant, approved agent, and pending delivery; cleanup/reset is safe after success or interruption.
- Delivery and Admin OpenAPI documents are committed, reproducible, secured, and consumed without manual edits.
- All focused builds/tests pass against SQL Server, and a real-token smoke test proves applicant, approved-agent, and Admin authorization boundaries.

**Dependencies/blockers**

- Phases 1–4 complete.
- Dedicated synthetic credentials and local/CI SQL Server must be available.
- Admin client/bootstrap decision from Phase 2 and pickup-address data from Phase 4 must be resolved.

**Recommended commit message**

`test(delivery): add deterministic fixtures and contract gates`

## Delivery frontend start gate

Do not start the Delivery Angular frontend until all five phase exit criteria pass, the Delivery/Admin OpenAPI documents are committed, and the generated frontend clients can be reproduced without hand edits. Phase 1 is the first implementation increment; this document does not authorize starting it automatically.
