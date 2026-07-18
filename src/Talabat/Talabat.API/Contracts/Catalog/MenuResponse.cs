using Talabat.Customer.API.Contracts.Common;

namespace Talabat.Customer.API.Contracts.Catalog;

public sealed record MenuResponse(
    int RestaurantId,
    string RestaurantName,
    IReadOnlyCollection<MenuProductDto> Products);

public sealed record MenuProductDto(
    int Id,
    string Name,
    string Description,
    MoneyDto Price,
    bool IsAvailable);
