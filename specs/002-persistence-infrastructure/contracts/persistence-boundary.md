# Persistence Boundary Contract

Phase 4 does not add HTTP endpoints or public API request/response contracts. The contract surface is the Infrastructure implementation of existing Domain repository interfaces and the API composition-root registration.

## Dependency Injection Contract

`Talabat.Infrastructure` exposes one registration entry point:

```csharp
IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
```

The registration reads `ConnectionStrings:TalabatDb` and registers:

- `TalabatDbContext`
- EF Core SQL Server provider configuration
- audit SaveChanges interceptor
- aggregate-root repository implementations
- `IUnitOfWork`

The method must not register controllers, endpoints, authentication, authorization, MediatR, or Delivery Application use cases.

## Repository Contract

Infrastructure implements the existing interfaces in `src/Talabat/Talabat.Domain/Interfaces/` exactly as declared.

Rules:

- No repository interface signatures change.
- No child repositories are added.
- No repository returns `IQueryable`, `DbContext`, EF entries, or provider-specific types.
- Repository methods load only the aggregate data required by the contract.
- Soft-deleted rows are excluded by default.

## UnitOfWork Contract

`IUnitOfWork.SaveChangesAsync` is the single commit boundary for Application use cases. Generated IDs are read only after this save completes.

Expected behavior:

- Calls EF Core `SaveChangesAsync`.
- Runs audit timestamp stamping.
- Commits all tracked aggregate changes atomically.
- Does not introduce application-side ID generation.

## Migration Contract

The reviewed Phase 4 migration is generated from Infrastructure using API as startup:

```powershell
dotnet ef migrations add InitialPersistence `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API `
  --output-dir Persistence\Migrations
```

The migration must contain:

- SQL Server IDENTITY keys for all runtime generated entity IDs.
- Composite keys for `CartItems` and `OrderItems`.
- Owned value-object columns, not independent value-object tables.
- Filtered unique indexes and check constraints listed in `research.md`.
- Deterministic catalog seed rows with explicit IDs.

The migration must not contain Identity/Auth tables or API endpoint behavior.
