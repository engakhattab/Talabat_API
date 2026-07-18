using Talabat.Customer.API.Contracts.Common;

namespace Talabat.Customer.API.Contracts.Orders;

public sealed record OrderListResponse(
    IReadOnlyCollection<OrderSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record OrderSummaryDto(
    int Id,
    int RestaurantId,
    MoneyDto Total,
    DateTime PlacedAt,
    int ItemCount);

public sealed record OrderDetailResponse(
    int Id,
    int CustomerId,
    int RestaurantId,
    OrderDeliveryAddressDto DeliveryAddress,
    IReadOnlyCollection<OrderLineItemDto> Items,
    MoneyDto Total,
    DateTime PlacedAt);

public sealed record OrderDeliveryAddressDto(
    string Street,
    string City,
    string BuildingNumber,
    string? Floor);

public sealed record OrderLineItemDto(
    string ProductName,
    MoneyDto UnitPrice,
    int Quantity,
    MoneyDto LineTotal);
