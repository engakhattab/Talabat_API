# Quickstart: Authorization Strategy And Quality Gates

**Feature**: Phase 10 — Authorization Strategy And Quality Gates
**Date**: 2026-07-26

## Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB or full instance)
- Git

## Build

```bash
cd src/Talabat
dotnet restore
dotnet build -c Release --no-restore
```

## Run Tests

```bash
# All tests
dotnet test --no-build -c Release

# Specific test project
dotnet test tests/Talabat.Domain.Tests --no-build -c Release
dotnet test tests/Talabat.Delivery.API.Tests --no-build -c Release
dotnet test tests/Talabat.ArchitectureTests --no-build -c Release
dotnet test tests/Talabat.Customer.API.Tests --no-build -c Release
dotnet test tests/Talabat.Application.Tests --no-build -c Release
dotnet test tests/Talabat.Identity.Tests --no-build -c Release
dotnet test tests/Talabat.Infrastructure.Tests --no-build -c Release

# With code coverage
dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
```

## Vulnerability Scan

```bash
dotnet list package --vulnerable --include-transitive
```

Fails the CI if any vulnerable package is found.

## Local CI Equivalent

```bash
cd src/Talabat
dotnet restore
dotnet build -c Release --no-restore
dotnet test --no-build -c Release --collect:"XPlat Code Coverage"
dotnet list package --vulnerable --include-transitive
```

## Authorization Testing

### Manual Testing with curl

**Customer API — valid token (mock headers in test env)**:
```bash
curl -H "X-Test-Subject: 1" -H "X-Test-Roles: Customer" -H "X-Test-Scopes: customer.api" http://localhost:5000/api/me/profile
```

**Customer API — missing scope**:
```bash
curl -H "X-Test-Subject: 1" -H "X-Test-Roles: Customer" http://localhost:5000/api/me/profile
# Expected: 403 Forbidden
```

**Customer API — wrong role**:
```bash
curl -H "X-Test-Subject: 1" -H "X-Test-Roles: DeliveryAgent" -H "X-Test-Scopes: customer.api" http://localhost:5000/api/me/profile
# Expected: 403 Forbidden
```

**Customer API — no token**:
```bash
curl http://localhost:5000/api/me/profile
# Expected: 401 Unauthorized
```

**Delivery API — valid token**:
```bash
curl -H "X-Test-Subject: 2" -H "X-Test-Roles: DeliveryAgent" -H "X-Test-Scopes: delivery.api" http://localhost:5002/api/agent/status/online -X PUT
```

**Delivery API — wrong audience (customer token → Delivery API)**:
```bash
curl -H "X-Test-Subject: 2" -H "X-Test-Roles: Customer" -H "X-Test-Scopes: customer.api" http://localhost:5002/api/agent/status/online -X PUT
# Expected: 401 Unauthorized (wrong audience)
```

### Ownership Testing

**Agent A tries to progress Agent B's delivery**:
```bash
# Agent A (id=2) tries to progress delivery assigned to Agent B (id=3)
curl -H "X-Test-Subject: 2" -H "X-Test-Roles: DeliveryAgent" -H "X-Test-Scopes: delivery.api" \
  http://localhost:5002/api/agent/deliveries/99/out-for-delivery -X POST
# Expected: 404 Not Found (delivery not assigned to agent 2)
```

## Architecture Tests

```bash
dotnet test tests/Talabat.ArchitectureTests --no-build -c Release -v detailed
```

Verifies:
- `Talabat.Domain` references no prohibited packages (EF Core, ASP.NET Core, Duende)
- `Talabat.Application` references no prohibited packages
- No aggregate types reference `ICurrentUser` or claim/role types
- No API project references `Talabat.Identity`

## Key Files

| File | Purpose |
|------|---------|
| `docs/authorization-strategy.md` | Four-gate model, status-code policy, naming reconciliation |
| `docs/authorization-endpoint-matrix.md` | Endpoint × policy matrix for both APIs |
| `tests/Talabat.ArchitectureTests/` | Architecture constraint tests |
| `tests/Talabat.Domain.Tests/` | Domain unit tests |
| `tests/Talabat.Delivery.API.Tests/` | Delivery API integration tests |
| `.github/workflows/ci.yml` | CI pipeline |
