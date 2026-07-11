# Phase 3.5 ID Strategy Refactor

Date: 2026-07-11

## Decision

Talabat uses SQL Server IDENTITY-compatible integer IDs for persisted entities. New Domain entities are constructed with `Id == 0`; persistence assigns positive IDs during `SaveChangesAsync`.

This supersedes the Phase 3 bridge where Application generated IDs before persistence existed. Phase 4 EF Core mappings should use the default generated-on-add behavior for identity keys. Do not add sequences, do not configure generated-never keys, and do not reintroduce application-side ID generation.

## Changes

- Removed self-ID parameters from Domain factories and constructors for `Cart`, `Order`, `Customer`, `CustomerAddress`, `Restaurant`, `Product`, `Delivery`, and `DeliveryAgent`.
- Kept positive guards on cross-aggregate reference IDs such as `customerId`, `restaurantId`, `productId`, `orderId`, and `agentId`.
- Changed `Cart.Id` and `Customer.Id` to private setters so test doubles and future persistence can set generated IDs consistently.
- Changed `Restaurant.AddProduct` duplicate detection from ID-based to normalized product-name based, preserving `DuplicateProductException`.
- Changed `Customer.AddAddress` to rely only on value-equality duplicate address detection, preserving `DuplicateAddressException`.
- Removed the temporary Application ID generator abstraction and its fake test double.
- Updated create handlers so returned generated IDs are read after `IUnitOfWork.SaveChangesAsync`.
- Added `TestIds` and fake save-time ID assignment for Application tests, while preserving deterministic IDs for pre-seeded test data.

## Verification

- `dotnet build src\Talabat\Talabat.slnx`
- `dotnet test` from `src\Talabat`
- Repository-wide search for the retired generator interface name returns no matches.

No EF Core packages, migrations, API endpoints, Identity/Auth implementation, repository interface changes, or business-rule changes were introduced.
