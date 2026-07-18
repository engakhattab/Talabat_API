namespace Talabat.Customer.API.Contracts.Catalog;

public sealed record RestaurantListResponse(
    IReadOnlyCollection<RestaurantSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record RestaurantSummaryDto(
    int Id,
    string Name,
    string Description,
    string? ImageUrl,
    bool IsActive);
