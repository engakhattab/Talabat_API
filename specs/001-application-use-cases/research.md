# Phase 0 Research: Application Use Cases

## Decision: Use CQRS-Lite Handlers Without MediatR

**Decision**: Implement each use case as an explicit Application handler with request and response models, without installing MediatR or any command bus package.

**Rationale**: The repository currently has no mediator dependency, and the roadmap says not to install MediatR unless approved. Explicit handlers keep use cases easy to review, test, and later adapt to MediatR if desired.

**Alternatives considered**:

- Application services grouped by area: simpler at first, but can become broad classes with mixed responsibilities.
- MediatR handlers: familiar pattern, but adds a package and framework decision outside the current scope.

## Decision: Keep Customer Context Caller-Supplied

**Decision**: Phase 3 request models include explicit `customerId` values. Do not add `ICurrentUser`, claims abstractions, token models, roles, or Identity/Auth framework concepts.

**Rationale**: Identity/Auth is reserved for a future phase. Phase 3 still needs customer-scoped behavior, but that scope can be represented as request data supplied by tests, future APIs, or future authenticated adapters.

**Alternatives considered**:

- Add a current-user abstraction now: useful later, but it risks shaping the model around an identity approach that has not been selected.
- Store identity user IDs in Domain entities: rejected because Domain must remain independent from Identity/Auth implementation details.

## Decision: Use Transport-Neutral Application Results

**Decision**: Add Application-level result/error contracts for expected business outcomes, and keep HTTP status, API response, and controller mapping outside Phase 3.

**Rationale**: The spec requires outcomes that future presentation channels can map without leaking transport details. Use cases need a clear way to report expected outcomes such as not found, invalid cart state, expired cart, cross-restaurant conflict, unavailable products, and ownership mismatch.

**Alternatives considered**:

- Throw Domain exceptions all the way to API: keeps code small but makes expected business outcomes harder to test and map consistently.
- Return API-shaped responses: rejected because API is a later phase and Application must stay transport-neutral.

## Decision: Add Application ID Generation Abstraction (Superseded By Phase 3.5)

**Decision**: Add a small Application abstraction for IDs needed by aggregate factories, covering at least cart IDs, customer address IDs, and order IDs.

**Rationale**: Existing Domain factories require integer IDs, but persistence and database-generated IDs are deferred to Phase 4. Phase 3 needs a testable way to create aggregates without selecting a database or infrastructure strategy.

**Phase 3.5 supersession (2026-07-11)**: This decision is no longer active. Phase 3.5 moved construction to SQL Server IDENTITY-compatible integer keys: aggregate and child entity self IDs start as `0`, persistence assigns positive IDs on save, and Application create handlers read generated IDs only after `SaveChangesAsync`. The temporary Application-side ID generator abstraction and fake were removed before EF Core mappings or migrations were introduced.

**Alternatives considered**:

- Generate IDs in Domain: rejected because ID generation is an infrastructure/application concern.
- Use temporary hardcoded IDs in handlers: rejected because it makes tests misleading and persistence integration harder.
- Change aggregates to accept unset IDs: rejected for Phase 3 because it is a broader Domain design change.

## Decision: Add Restaurant Local-Time Abstraction

**Decision**: Add an Application abstraction that provides `TimeOnly` local time for a restaurant from the current UTC time.

**Rationale**: `CheckoutDomainService.ValidateCheckout` requires `restaurantLocalTime`, while the current `Restaurant` aggregate has opening hours but no time-zone policy. The Application layer should not assume UTC equals restaurant local time.

**Alternatives considered**:

- Use `TimeOnly.FromDateTime(IClock.UtcNow)`: simple, but incorrect once restaurants can operate in different local time zones.
- Add time-zone fields to `Restaurant` in Phase 3: possible later, but not required to implement use-case orchestration.

## Decision: Return Application Read Models, Not Domain Aggregates

**Decision**: Query handlers should return Application read models that expose only data needed by the use case.

**Rationale**: Returning aggregates to callers leaks mutation-capable domain objects and makes future API mapping less controlled. Read models keep Application contracts stable and transport-neutral.

**Alternatives considered**:

- Return Domain aggregates directly: fast to implement, but weakens boundaries.
- Add API DTOs in Application: rejected because API response models belong to the API phase.

## Decision: Reject Cross-Restaurant Cart Additions

**Decision**: If a customer has an active cart for one restaurant and adds a product from another restaurant, the add-item use case returns a conflict-style Application outcome and preserves the existing cart unchanged.

**Rationale**: `Cart.AddItem` already enforces one restaurant per cart by throwing `CrossRestaurantCartException`. Preserving the existing cart avoids silently losing customer selections.

**Alternatives considered**:

- Replace the cart automatically: rejected because it discards existing choices without an explicit user decision.
- Clear then add: rejected for the same reason and because `Cart.Clear` transitions the cart out of active state.

## Decision: Show Unavailable Menu Products With Availability Flags

**Decision**: The get-menu use case should include products and clearly mark availability rather than silently hiding unavailable products.

**Rationale**: The spec allows unavailable products to be excluded or distinguished. Distinguishing keeps the menu stable and allows future customer UI to explain why an item cannot be added.

**Alternatives considered**:

- Exclude unavailable products: simpler but can confuse users when known products disappear.

## Decision: Current Product Price Is Authoritative At Checkout

**Decision**: Checkout uses the current product price from the `Restaurant` aggregate, and the created `Order` snapshots that price.

**Rationale**: Cart items currently store product ID, name, and quantity, not price. `CheckoutDomainService` creates `CheckoutItemSnapshot` from current `Product.CurrentPrice`, and `Order.CreateFromCheckout` snapshots that price into order items.

**Alternatives considered**:

- Reject checkout when price changed since cart add: not currently supportable without storing cart-time prices.
- Store prices in cart in Phase 3: broader Domain change and not required by the current spec.

## Decision: Do Not Add Idempotency Keys In Phase 3

**Decision**: Duplicate checkout submissions are handled by cart state. A successful checkout marks the active cart checked out; later attempts against that cart become invalid. Do not add idempotency-key contracts until API/payment phases.

**Rationale**: Phase 3 has no transport layer, no payment, and no distributed retry boundary. Adding idempotency keys now would overfit future API behavior.

**Alternatives considered**:

- Add idempotency keys to Application checkout now: potentially useful later, but introduces storage and API semantics before persistence and payment exist.

## Decision: Use xUnit For Phase 3 Application Tests

**Decision**: Use xUnit for .NET unit tests in Phase 3.

**Rationale**: The Phase 3 task plan creates `tests/Talabat.Application.Tests` as an xUnit project. xUnit is a common .NET unit testing framework and fits focused Application handler tests with fake repositories, fake clocks, fake ID generation, and fake local-time resolution.

**Alternatives considered**:

- MSTest: viable, but not selected by the current task plan.
- NUnit: viable, but not selected by the current task plan.
- Framework-neutral task wording: rejected because the generated tasks already need a concrete test project setup.

## Decision: Defer Delivery Workflows Completely

**Decision**: Do not implement Delivery task creation, assignment, lifecycle transitions, agent workflows, or delivery status use cases in Phase 3.

**Rationale**: The Phase 3 clarification explicitly defers Delivery. The Customer Website backend path must stabilize first.

**Alternatives considered**:

- Include basic delivery creation after checkout: rejected because it couples customer checkout to Delivery Website/backend concerns too early.
