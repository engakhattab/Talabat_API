# Feature Specification: Phase 2 Tactical Design

## Overview
Phase 2 (Tactical Design) translates the Strategic Design of the Talabat MVP into concrete object-oriented design building blocks. It defines the entities, value objects, domain methods, exceptions, and repository contracts before any code is written.

## Problem Statement
Without a formal tactical design, the implementation phase risks becoming ad-hoc, leading to an anemic domain model, leaked business logic into application services, and violations of aggregate boundaries.

## Goals
- Design rich domain models that encapsulate state and behavior.
- Define self-validating, immutable value types.
- Ensure business invariants are correctly assigned to aggregate roots.
- Express business failures using ubiquitous language via domain exceptions.
- Establish strict data access contracts that prevent infrastructure leakage into the domain.

## User Scenarios & Testing

### Primary Scenarios
- **Given** a valid cart and checkout request, **When** products are available, **Then** use current Catalog prices to create an immutable order and clear the cart.
- **Given** an attempt to add a product to a cart, **When** the product belongs to a different restaurant, **Then** reject with `CrossRestaurantCartException`.
- **Given** an attempt to modify an expired cart, **When** adding or removing items, **Then** reject with `CartExpiredException`.

### Edge Cases
- **Given** a restaurant closes during an active checkout, **When** checkout is processed, **Then** reject with `RestaurantClosedException`.
- **Given** a product price changes while sitting in a cart, **When** cart details or checkout are requested, **Then** use the current Catalog price without comparing an old cart price.

## Functional Requirements
1. The domain model must encapsulate all state mutations within private setters and exposed domain methods.
2. The `Cart` aggregate must enforce cross-item rules (single restaurant, expiration, valid quantity).
3. Value objects (`Money`, `TimeRange`) must be immutable and self-validating upon instantiation.
4. Each domain exception must map to a specific business rule violation with a clear, business-focused message.
5. Repository interfaces must return materialized collections (`IReadOnlyList<T>`) rather than `IQueryable` to prevent querying leaks.

## Key Entities
- **Catalog**: Restaurant (Aggregate Root), Product (Entity)
- **Basket**: Cart (Aggregate Root), CartItem (Entity)
- **Ordering**: Order (Aggregate Root), OrderItem (Entity)
- **Identity**: Customer (Aggregate Root), CustomerAddress (Entity)

## Success Criteria
- All 8 core business invariants from Phase 1 are mapped to specific methods on aggregate roots.
- All entities have private setters and expose domain methods instead of public property mutations.
- The `Talabat.Domain` design dictates zero dependencies on external frameworks or databases.
- The domain design enables the complete separation of reads (queries) and writes (commands) orchestrations in Phase 3.

## Scope

### In Scope
- Entity and aggregate root design with properties and methods.
- Value object design.
- Domain method pseudocode and guard clauses.
- Domain exception hierarchy.
- Repository interface definitions.

### Out of Scope
- Actual implementation and language specifics (reserved for Phase 4).
- Application layer handlers and DTOs (reserved for Phase 3).
- Infrastructure mappings and database configurations.

## Dependencies
- Phase 1 Strategic Design must be complete and ratified.

## Assumptions
- The system operates within a single timezone.
- A single currency is used for all monetary values in the MVP.
- All domain interactions are synchronous.
