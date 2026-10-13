# Contract: Phase 3 Behavior Proof

**Feature**: Unified User Behavior and Governance  
**Purpose**: Freeze the evidence expected from the six named business-behavior groups.

## CapabilityRoleDriftTests — Infrastructure

| Scenario | Required evidence |
|---|---|
| Register Customer | Customer flag and exactly Customer role. |
| Register applicant | PendingApproval with no DeliveryAgent flag, role, or status. |
| Grant Customer to approved agent | Same ID; Customer + DeliveryAgent flags and roles; approved agent state retained. |
| Approve applicant | DeliveryAgent flag/role and Offline status. |
| Reject applicant | Rejected with no DeliveryAgent flag/role/status. |
| Deactivate user | Existing flags/roles unchanged; inactive state and refreshed stamp. |
| Missing DeliveryAgent role during approval | Failure result; fresh scope shows PendingApproval, no flag/role/status, unchanged stamp. |

The comparison is made after reloading from a fresh service scope. Expected roles are calculated from
the four fixed `UserType` mappings, never supplied by the test caller to production code.

## MultiRoleJourneyTests — Identity

Required sequence:

1. register delivery-agent applicant through `/account/register/delivery-agent`;
2. capture one positive integer ID;
3. approve that ID through `IUserCapabilityService`;
4. log in with the same account;
5. grant Customer capability to that same ID at service level;
6. build/reload the current Identity principal;
7. prove the database contains one user with both flags and both roles, the same ID, Approved
   application state, and retained agent operational state.

This is endpoint/service end-to-end coverage. It does not introduce an interactive client or a
cross-host token acquisition flow.

## AgentAssignmentAuthorizationTests — Application

| User arrangement | Assignment outcome |
|---|---|
| Customer only | `DeliveryAgentNotInitializedException` |
| Approved Offline | `AgentNotAvailableException` |
| Approved Suspended | `AgentNotAvailableException` |
| Approved Available | assigned to same `User.Id`; agent becomes Busy |
| Matching Busy + complete | Delivered; agent becomes Available |
| Matching Busy + cancel | Cancelled; agent becomes Available |

A future Application delivery-assignment entry point must require the DeliveryAgent role before
calling the Domain service. The current phase proves the Domain half and does not create the full
Delivery API.

## ConcurrencyConflictTests — Infrastructure and Customer API

Infrastructure proof:

- two independent contexts load the same user and rowversion;
- writer A saves a profile change through `IUnitOfWork`;
- writer B's stale save through `IUnitOfWork` throws `ConcurrencyConflictException`;
- a fresh read retains writer A's data and a new rowversion.

Customer API proof:

- authenticated Customer profile update reaches the handler/save boundary;
- a deterministic test-only stale-save failure returns 409 ProblemDetails;
- response contains `ConcurrencyConflict` and the Domain conflict message;
- no persistence implementation detail is serialized.

## OwnershipTests — Customer API

Required evidence:

- production bearer options use role claim type `role`;
- test principal can materialize Customer and DeliveryAgent roles;
- stored Customer capability, not role claim, controls business access;
- missing/malformed/non-positive subjects return empty 401;
- customer A using customer B's order or address ID receives 404;
- customer A's `/api/me/cart` response contains only A's cart or the empty-cart result, never B's;
- static controller/contract scan finds no route/body CustomerId ownership input.

## SessionInvalidationTests — Identity

Required sequence for deactivation:

1. register/login active user and retain cookie;
2. verify cookie can access `/account/me`;
3. deactivate through `IUserCapabilityService`;
4. with test-only zero validation interval, reuse the same cookie;
5. receive 401.

Repeat the existing-cookie proof after soft deletion. Keep the production configuration assertion at
exactly five minutes and preserve indistinguishable 401 responses for invalid credentials, inactive,
and deleted accounts.

## Acceptance Rule

Each group must pass in its owning existing test project. A skipped SQL test because neither Docker
nor LocalDB is available is not final acceptance evidence; it is an environmental blocker to report.
