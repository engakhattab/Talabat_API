# Quickstart: Phase 10 Remediation

## Prerequisites

- .NET 10 SDK
- SQL Server (local instance or Docker)
- Git

## Local Development

### 1. Restore and Build

```bash
dotnet restore src/Talabat/Talabat.slnx
dotnet build src/Talabat/Talabat.slnx --no-restore -c Release
```

### 2. Run Tests

```bash
dotnet test src/Talabat/Talabat.slnx --no-build -c Release
```

**Note**: Tests requiring SQL Server (`Infrastructure.Tests`, `Identity.Tests`, `Customer.API.Tests`, `Delivery.API.Tests`) need a running SQL Server instance. Set the connection string via environment variable:

```bash
$env:ConnectionStrings__TalabatDb = "Server=localhost,1433;Database=Talabat;User Id=sa;Password=Your_strong_Passw0rd;TrustServerCertificate=True"
```

### 3. Vulnerability Check

```bash
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

## CI Pipeline

The GitHub Actions workflow (`.github/workflows/ci.yml`) provisions a SQL Server 2022 container automatically. No local setup required for CI.

### Running CI Locally (act)

```bash
# Install act: https://github.com/nektos/act
act push
```

## Per-Phase Gate

Before each commit, verify:

```bash
dotnet build src/Talabat/Talabat.slnx -c Release --no-restore
dotnet test src/Talabat/Talabat.slnx -c Release --no-build
dotnet list src/Talabat/Talabat.slnx package --vulnerable --include-transitive
```

All three must pass. No commit with a failing gate.

## Test Projects

| Project | Requires SQL | Purpose |
|---------|-------------|---------|
| `Talabat.Application.Tests` | No | Handler unit tests |
| `Talabat.Customer.API.Tests` | Yes | Customer API integration tests |
| `Talabat.Delivery.API.Tests` | Yes | Delivery API integration tests |
| `Talabat.Identity.Tests` | Yes | Identity host tests |
| `Talabat.Infrastructure.Tests` | Yes | Persistence + migration tests |
| `Talabat.ArchitectureTests` | No | Architecture boundary tests |
| `Talabat.Domain.Tests` | No | Domain unit tests (new) |
