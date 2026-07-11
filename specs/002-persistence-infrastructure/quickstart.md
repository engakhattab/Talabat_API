# Quickstart: Persistence And Infrastructure

This quickstart is for the reviewed Phase 4 implementation. Do not run package installation, migrations, or code changes until the plan is approved.

## 1. Confirm Scope

Review:

- `PROJECT_IMPLEMENTATION_ROADMAP.md` Section 5, Phase 4
- `.specify/memory/constitution.md`
- `specs/002-persistence-infrastructure/spec.md`
- `specs/002-persistence-infrastructure/research.md`
- `specs/002-persistence-infrastructure/data-model.md`
- `specs/002-persistence-infrastructure/tasks.md`

Allowed work is EF Core SQL Server persistence, Infrastructure repositories, UnitOfWork, migration, seed data, SQL Server integration tests, API composition-root wiring, and the approved OpenAPI vulnerability fix.

## 2. Add Packages

Planned package set:

```powershell
dotnet add src\Talabat\Talabat.Infrastructure\Talabat.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.9
dotnet add src\Talabat\Talabat.Infrastructure\Talabat.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design --version 10.0.9
dotnet add src\Talabat\Talabat.Infrastructure\Talabat.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Tools --version 10.0.9
```

Add the Infrastructure integration test project with xUnit, SQL Server EF Core package, and `Testcontainers.MsSql` 4.13.0. Patch the API OpenAPI dependency only as needed to remove NU1903/GHSA-v5pm-xwqc-g5wc.

## 3. Configure Persistence

Implement:

- `TalabatDbContext`
- per-aggregate EF configurations
- audit SaveChanges interceptor
- soft-delete query filters
- repository implementations for existing Domain interfaces
- `UnitOfWork`
- `AddInfrastructure`

Use `ConnectionStrings:TalabatDb`. Do not commit production credentials.

## 4. Test Against SQL Server

Integration tests should use Testcontainers SQL Server first and LocalDB only as fallback. SQLite is forbidden.

Minimum proof points:

- generated IDs are positive after `SaveChangesAsync`
- owned value objects round-trip
- filtered unique indexes reject invalid duplicates
- checkout persists one order and closes one cart atomically
- soft-deleted rows are excluded from normal reads
- repositories satisfy existing Domain contracts

## 5. Generate The Reviewed Migration

Generate the migration only after mappings and constraints are reviewed:

```powershell
dotnet ef migrations add InitialPersistence `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API `
  --output-dir Persistence\Migrations

dotnet ef migrations script --idempotent `
  --project src\Talabat\Talabat.Infrastructure `
  --startup-project src\Talabat\Talabat.API `
  --output src\Talabat\Talabat.Infrastructure\Persistence\Migrations\InitialPersistence.idempotent.sql
```

Review the generated migration `.cs` files, idempotent SQL script, and model snapshot for IDENTITY on all integer keys, composite child keys `(CartId, ProductId)` and `(OrderId, ProductId)`, owned value-object columns with no separate snapshot tables, filtered unique indexes for active cart per customer, default address per customer, unique `Delivery(OrderId)`, unique `(RestaurantId, Name)`, check constraints for `Quantity > 0` and `Money >= 0`, deterministic seed data, and absence of Identity/Auth tables.

## 6. Final Verification

```powershell
dotnet build src\Talabat\Talabat.slnx
dotnet test
dotnet list src\Talabat\Talabat.slnx package --vulnerable
```

Also verify:

- `Talabat.Domain.csproj` and `Talabat.Application.csproj` still have no package references.
- No API endpoints, Identity/Auth code, Delivery use cases, MediatR packages, child repositories, or repository interface changes were added.
