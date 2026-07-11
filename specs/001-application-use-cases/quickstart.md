# Quickstart: Phase 3 Application Use Cases

This quickstart is for implementing Phase 3 after the plan is approved. It does not implement the phase by itself.

## Prerequisites

- Read [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), and [contracts/application-use-cases.md](contracts/application-use-cases.md).
- Keep the scope limited to `Talabat.Application` plus tests.
- Do not add EF Core, Identity/Auth, API endpoints, Delivery use cases, migrations, frontend code, or production transport mapping.

## Recommended Implementation Order

1. Add Application common result contracts.
2. Add Application abstractions:
   - ID generation for cart, customer address, and order IDs.
   - Restaurant local-time resolution.
3. Add Catalog query handlers.
4. Add Basket command/query handlers.
5. Add Customer command/query handlers.
6. Add Ordering checkout and order-read handlers.
7. Add Application tests with fake repositories and fake clocks.
8. Build and test.

## Suggested Source Folders

```text
src/Talabat/Talabat.Application/
|-- Abstractions/
|-- Common/
|   `-- Results/
|-- Catalog/
|   |-- BrowseRestaurants/
|   `-- GetRestaurantMenu/
|-- Basket/
|   |-- GetCart/
|   |-- AddItem/
|   |-- UpdateQuantity/
|   |-- RemoveItem/
|   `-- ClearCart/
|-- Customers/
|   |-- GetProfile/
|   |-- UpdateProfile/
|   |-- AddAddress/
|   |-- RemoveAddress/
|   `-- SetDefaultAddress/
`-- Ordering/
    |-- Checkout/
    |-- GetOrderHistory/
    `-- GetOrderDetails/
```

## Suggested Test Folders

```text
tests/Talabat.Application.Tests/
|-- Catalog/
|-- Basket/
|-- Customers/
|-- Ordering/
`-- TestDoubles/
```

## Verification Commands

```powershell
dotnet build src\Talabat\Talabat.slnx --no-restore
dotnet test
```

If a test project is added during implementation, include it in the solution before running `dotnet test`.

## Scope Guardrails

- Use Domain aggregate methods and domain services for invariants.
- Use repository interfaces from `Talabat.Domain.Interfaces`.
- Commit state changes once per command through `IUnitOfWork`.
- Return Application result models, not HTTP responses.
- Keep `customerId` as explicit request data.
- Treat unavailable checkout products as a structured expected outcome.
- Preserve the existing cart when cross-restaurant add is attempted.
- Keep Delivery workflows deferred.
