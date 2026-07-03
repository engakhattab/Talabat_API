<!--
SYNC IMPACT REPORT
==================
Version change: N/A → 1.0.0
Bump rationale: MAJOR — initial constitution ratification

Added principles:
  1. Domain-First Design
  2. Clean Architecture Dependency Rule
  3. Rich Domain Model
  4. Aggregate Integrity
  5. Simplicity Over Enterprise Complexity
  6. Design Before Code

Added sections:
  - Preamble
  - Principles (6)
  - Governance (Amendment, Versioning, Compliance)

Removed sections: None (initial creation)

Templates requiring updates:
  ⚠ .specify/templates/spec-template.md — not yet created
  ⚠ .specify/templates/plan-template.md — not yet created
  ⚠ .specify/templates/tasks-template.md — not yet created

Follow-up TODOs: None
-->

# Talabat Food Delivery MVP — Project Constitution

> Version: 1.0.0
> Ratified: 2026-07-01
> Last Amended: 2026-07-01

## Preamble

This constitution governs the design and implementation of the
Talabat-like food delivery backend — a personal learning project
focused on Domain-Driven Design (DDD), Clean Architecture,
ASP.NET Core, and EF Core Code First.

The primary purpose of this project is **learning**, not building a
production-ready enterprise system. Every architectural decision
MUST prioritize educational value and practical understanding of
DDD concepts. The constitution ensures consistency across all
phases (Strategic Design → Tactical Design → Application Layer →
Implementation → Testing) as defined in the project roadmap.

The MVP scope covers four bounded contexts: Catalog (Restaurants
and Products), Basket (Cart and CartItems), Ordering (Orders and
OrderItems), and Identity (Customer and CustomerAddresses).

## Principles

### Principle 1: Domain-First Design

All business rules MUST be captured, validated, and documented
before any code is written. Requirements MUST be expressed as
testable Given/When/Then statements. No implementation work
(entities, repositories, APIs) may begin until Phase 1 (Strategic
Design) deliverables are complete and reviewed.

- Every business invariant MUST have a corresponding testable
  scenario before it is implemented.
- Edge cases MUST be identified and documented with explicit
  resolution decisions during the design phase.
- The ubiquitous language glossary MUST be maintained and used
  consistently across all documentation and code.

**Rationale:** A domain model is only as good as the requirements
it captures. Skipping design leads to scattered business logic and
expensive refactors.

### Principle 2: Clean Architecture Dependency Rule

Project references MUST enforce a strict dependency hierarchy.
Violations of this rule are non-negotiable and MUST be rejected
during code review.

- `Talabat.Domain` MUST reference **nothing** — zero NuGet
  packages, zero project references.
- `Talabat.Application` MUST reference only `Talabat.Domain`.
- `Talabat.Infrastructure` MUST reference `Talabat.Application`
  and `Talabat.Domain`.
- `Talabat.API` is the composition root and MAY reference all
  projects.
- No inner layer MAY depend on an outer layer. The Domain layer
  MUST NOT know about EF Core, HTTP, or any infrastructure
  concern.

**Rationale:** The Dependency Rule is the core of Clean
Architecture. If the Domain references Infrastructure, the entire
architecture collapses and the learning objective is defeated.

### Principle 3: Rich Domain Model

Business logic MUST live inside domain entities and aggregate
roots, not in application services, controllers, or infrastructure
code. Entities MUST encapsulate their own state through private
setters and behavior-exposing methods.

- Entities MUST NOT expose public setters. State changes MUST
  occur through named domain methods (e.g., `Cart.AddItem()`,
  not `cart.Items.Add()`).
- Domain exceptions MUST be typed and carry business-language
  messages. Generic `Exception` or `ArgumentException` MUST NOT
  be used for business rule violations.
- Value objects MUST be used for concepts defined by their
  attributes (Money, TimeRange, Address) to prevent primitive
  obsession.
- Anemic domain models (entities with only getters/setters and
  no behavior) are explicitly prohibited.

**Rationale:** The entire purpose of DDD is that behavior belongs
with the data it operates on. An anemic model teaches the wrong
lesson and defeats the project's learning goals.

### Principle 4: Aggregate Integrity

Each aggregate root MUST be the sole enforcer of its business
invariants. Children MUST be accessed only through their aggregate
root. No code outside the aggregate MAY directly query or modify
child entities.

- The 8 core business invariants MUST each be assigned to exactly
  one aggregate root:
  1. One active cart per customer → Cart
  2. Cart belongs to one restaurant → Cart
  3. All CartItems from same restaurant → Cart
  4. Quantity greater than zero → Cart
  5. Duplicate products increase quantity → Cart
  6. Expired carts cannot be modified → Cart
  7. Checkout validates current prices → Domain Service
  8. Orders store immutable prices → Order
- Repository interfaces MUST be defined per aggregate root (not
  per table or per entity).
- No `IQueryable` MAY leak from repository interfaces. All
  repositories MUST return materialized collections.
- Unit of Work pattern MUST be used so the application layer
  controls transaction boundaries.

**Rationale:** Aggregates are transactional consistency boundaries.
Bypassing the root breaks invariant enforcement and creates data
integrity bugs that are extremely difficult to trace.

### Principle 5: Simplicity Over Enterprise Complexity

When a trade-off exists between simplicity and enterprise-grade
patterns, the simpler solution MUST be chosen unless the
simplification would teach an incorrect architectural concept.

- MVP MUST use a single shared database and DbContext. Separate
  databases per bounded context are deferred to post-MVP.
- CQRS-Lite (same DB for reads and writes) MUST be used instead
  of full CQRS with separate read/write stores.
- Direct references between contexts (via shared DB) are
  acceptable for MVP. Anti-corruption layers and domain events
  are deferred.
- Features excluded from MVP (payment, delivery, notifications,
  discounts, coupons, product options, variants, branches,
  multi-currency, inventory) MUST NOT be designed or scaffolded
  until the MVP is complete and stable.
- Over-engineering MUST be avoided. If a pattern adds complexity
  without teaching a core DDD/Clean Architecture concept, it
  MUST be deferred.

**Rationale:** This is a learning project. Premature complexity
obscures the fundamental concepts being learned. The architecture
MUST be designed to evolve naturally post-MVP, not to handle
every future scenario upfront.

### Principle 6: Design Before Code

Every implementation phase MUST be preceded by a completed design
phase. The phased roadmap (Strategic → Tactical → Application →
Implementation → Testing) MUST be followed in order.

- Phase 1 (Strategic Design) MUST produce: validated business
  rules, bounded context map, aggregate diagrams, invariant
  assignments, and ubiquitous language glossary.
- Phase 2 (Tactical Design) MUST produce: entity designs with
  properties and methods, value object definitions, domain
  method pseudocode, exception hierarchy, and repository
  interface signatures.
- Phase 3 (Application Design) MUST produce: use case catalog,
  command/query definitions, and handler patterns.
- No code for Phase 4 (Implementation) MAY begin until Phases
  1–3 deliverables exist.
- Every domain method MUST have pseudocode with guard clauses
  documented before C# implementation begins.

**Rationale:** Writing code without a design leads to ad-hoc
architecture. The phased approach ensures each concept is
understood before it is coded, which is the core of mentored
learning.

## Governance

### Amendment Procedure

1. Any team member (or the learner themselves) MAY propose an
   amendment by documenting the change rationale and affected
   principles.
2. Amendments MUST be reviewed against all existing principles
   for consistency.
3. If an amendment contradicts an existing principle, both the
   amendment and the affected principle MUST be updated
   simultaneously.
4. All amendments MUST be recorded in the Sync Impact Report at
   the top of this file.
5. The constitution MUST be re-read before starting any new
   phase of the roadmap.

### Versioning Policy

This constitution follows semantic versioning:

- **MAJOR** (X.0.0): Removal or redefinition of an existing
  principle, or backward-incompatible governance change.
- **MINOR** (x.Y.0): Addition of a new principle, or material
  expansion of existing guidance.
- **PATCH** (x.y.Z): Clarifications, wording improvements, typo
  fixes, or non-semantic refinements.

### Compliance Review

- Before starting each implementation phase, the constitution
  MUST be reviewed to confirm all principles are being followed.
- Each specification created via `/speckit-specify` MUST be
  validated against Principles 1 (Domain-First) and 5
  (Simplicity) before proceeding to planning.
- Each implementation plan created via `/speckit-plan` MUST be
  validated against Principles 2 (Dependency Rule), 3 (Rich
  Domain Model), and 4 (Aggregate Integrity).
- Code reviews MUST check for violations of Principle 2
  (dependency direction) and Principle 3 (anemic models, public
  setters, misplaced business logic).
- Any violation of Principle 2 (Dependency Rule) is a blocking
  issue that MUST be resolved before merging.
