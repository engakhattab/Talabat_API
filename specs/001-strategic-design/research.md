# Tactical Implementation Research & Decisions

## Context
Phase 2 (Tactical Design) requires mapping the strategic DDD concepts to concrete C# language features.

## Decision 1: Value Object Implementation
**Decision**: Use C# 9+ `record` types for Value Objects.
**Rationale**: Records inherently provide value-based equality, immutability, and concise syntax, which perfectly maps to DDD Value Object requirements without the need for complex custom equality comparers or base classes.
**Alternatives considered**: Custom `ValueObject` base class with reflection-based equality (rejected as over-engineering in modern .NET).

## Decision 2: Domain Exception Handling
**Decision**: Create a custom abstract `DomainException` base class and throw typed child exceptions for invariant violations.
**Rationale**: Prevents throwing generic `ArgumentException` or `InvalidOperationException`. In the API layer, a global exception handler middleware will catch `DomainException` and map it to appropriate HTTP status codes (e.g., 400, 409, 422).
**Alternatives considered**: Result/Either monads for business failures (rejected as adding too much functional programming complexity for an MVP learning project).

## Decision 3: Repository Interfaces
**Decision**: Define `IRepository` contracts strictly returning `Task<IReadOnlyList<T>>` or `Task<T?>`. No `IQueryable`.
**Rationale**: `IQueryable` leaks Infrastructure details (EF Core query translation) into the Domain and Application layers. Materialized lists force the Application layer to respect Domain boundaries.
**Alternatives considered**: Returning `IQueryable<T>` for easier filtering (rejected due to Clean Architecture violation).
