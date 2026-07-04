# Feature Specification: Strategic Design — Talabat Food Delivery MVP

## Overview

This feature covers Phase 1 (Strategic Design) of the Talabat food delivery backend project. This phase establishes the core Domain-Driven Design (DDD) foundation before any code is written. It formalizes all business rules from the project requirements, decomposes the domain into bounded contexts, defines aggregate boundaries, and maps all core business invariants to their responsible entities.

## Problem Statement

Building a complex system without a clear domain model leads to scattered business logic, inconsistent state, and tight coupling between unrelated features. If the MVP begins implementation without defining what a "Product" means in the Basket vs. Catalog, or without establishing the boundaries of an "Order," the architecture will quickly become an unmaintainable Big Ball of Mud, defeating the project's learning objectives.

## Goals

- Establish a formal, unambiguous set of business rules that will guide all subsequent development.
- Decompose the problem space into distinct bounded contexts (Catalog, Basket, Ordering, Identity).
- Define transactional consistency boundaries (Aggregates) to enforce cross-entity business rules.
- Produce a shared ubiquitous language to ensure consistent communication.

## User Scenarios & Testing

### Primary Scenarios

**Scenario 1: Formalizing Cart Expiration**
- *Given* a shopping cart created by a customer,
- *When* 1 hour passes since its creation,
- *Then* the system must reject any attempts to add, remove, or modify items within that cart.

**Scenario 2: Cross-Restaurant Cart Prevention**
- *Given* a customer with an active cart containing items from Restaurant A,
- *When* the customer attempts to add a product from Restaurant B,
- *Then* the system must reject the operation and require the cart to be cleared first.

**Scenario 3: Using Current Catalog Prices**
- *Given* an active cart containing a product whose Catalog price changed,
- *When* the customer views cart details or proceeds to checkout,
- *Then* the system uses the current Catalog price because Cart stores no historical price.

**Scenario 4: Merging Duplicate Products**
- *Given* an active cart containing 2 units of Product A,
- *When* the customer attempts to add another unit of Product A,
- *Then* the system must increase the existing item's quantity to 3 rather than creating a duplicate row.

### Edge Cases

**Edge Case 1: Restaurant Closure During Checkout**
- *Given* a customer has built a cart from an open restaurant,
- *When* the restaurant's `ClosesAt` time passes while the customer is actively checking out,
- *Then* the checkout operation must be rejected with a clear message indicating the restaurant is now closed.

**Edge Case 2: Product Soft-Deletion Prior to Checkout**
- *Given* a cart containing a specific product,
- *When* the restaurant marks that product as unavailable before the customer completes checkout,
- *Then* the checkout must fail and inform the customer which items are no longer available.

## Functional Requirements

1. **Business Rule Formalization**: All requirements must be captured as Given/When/Then scenarios.
2. **Context Mapping**: The domain must be divided into Catalog, Basket, Ordering, and Customer contexts, with documented data dependencies between them.
3. **Aggregate Definition**: Four primary aggregate roots must be defined: Restaurant, Cart, Order, and Customer.
4. **Invariant Assignment**: The 8 core business invariants (e.g., "Quantity > 0", "Orders store immutable prices") must be explicitly assigned to their responsible aggregate root.
5. **Ubiquitous Language**: A glossary must be produced defining terminology per bounded context (e.g., differentiating `Product` in Catalog from `CartItem` in Basket).

## Key Entities

While implementation is deferred to Phase 2, the following strategic entities are identified:

- **Catalog Context**: `Restaurant` (Aggregate Root), `Product` (Entity)
- **Basket Context**: `Cart` (Aggregate Root), `CartItem` (Entity)
- **Ordering Context**: `Order` (Aggregate Root), `OrderItem` (Entity)
- **Customer Context**: `Customer` (Aggregate Root), `CustomerAddress` (Entity)

## Success Criteria

- 100% of business rules from the project brief are captured as testable Given/When/Then statements.
- Every identified entity is assigned to exactly one bounded context.
- Every business invariant is assigned to exactly one aggregate root.
- A reviewer can definitively state which aggregate enforces any given business rule without referencing implementation details.
- Context dependencies are clearly documented (e.g., Ordering depends on Basket and Catalog).

## Scope

### In Scope
- Strategic Design
- Tactical Design
- Domain Modeling
- ASP.NET Core implementation
- EF Core implementation
- API implementation
- Clean Architecture implementation
- Validating and documenting business rules.
- Defining bounded contexts and their relationships.
- Defining aggregate boundaries and identifying roots vs. child entities.
- Documenting business invariants.

Implementation is explicitly in scope because learning how to transform architectural design into working software is a core educational objective of this project.

### Out of Scope
- Production deployment
- CI/CD pipelines
- Monitoring
- Logging infrastructure
- Microservices
- Payment gateway
- Notifications
- Delivery drivers
- Coupons
- Discounts
- Product variants
- Inventory management
- Performance optimization

## Dependencies

- **Project Prompt/Context**: The foundational requirements and MVP exclusions (e.g., no payment gateway, no delivery drivers) provided in the project prompt.
- **Project Constitution**: Alignment with Principle 1 (Domain-First Design) and Principle 6 (Design Before Code).

## Assumptions

- The MVP operates in a single timezone for restaurant opening hours.
- Product prices may change at any time. Basket stores no price; cart reads and checkout use current Catalog prices, while Ordering preserves the accepted checkout price historically.
- The MVP requires only a single currency, deferring multi-currency support.
- Carts are aggressively expired (1 hour) to reduce stale price conflicts.
