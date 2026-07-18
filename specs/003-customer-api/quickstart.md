# Quickstart: Talabat Customer API (Phase 7)

## Prerequisites

- .NET 10 SDK
- SQL Server (local or express)
- `Talabat.Identity` host running at `https://localhost:7237` (for real token validation;
  not required for test-minted JWTs in integration tests)

## Build

```powershell
dotnet build src/Talabat/Talabat.slnx
```

## Run the Customer API

```powershell
dotnet run --project src/Talabat/Talabat.Customer.API
```

The API will start at `https://localhost:{port}` (check `launchSettings.json` for the port).

## Run Tests

```powershell
# All tests
dotnet test src/Talabat/Talabat.slnx

# Customer API tests only
dotnet test tests/Talabat.Customer.API.Tests
```

## Key Endpoints

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/catalog/restaurants` | GET | No | Browse restaurants |
| `/api/catalog/restaurants/{id}/menu` | GET | No | Get restaurant menu |
| `/api/me/cart` | GET | Yes | Get active cart |
| `/api/me/cart/items` | POST | Yes | Add item to cart |
| `/api/me/profile` | GET/PUT | Yes | Get/update profile |
| `/api/me/addresses` | POST | Yes | Add address |
| `/api/me/checkout` | POST | Yes | Checkout active cart |
| `/api/me/orders` | GET | Yes | Order history |
| `/api/me/orders/{id}` | GET | Yes | Order details |
| `/health` | GET | No | Health check |

## Configuration

`appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "TalabatDb": "Server=...;Database=Talabat;..."
  },
  "Identity": {
    "Authority": "https://localhost:7237"
  }
}
```
