using Talabat.Application.Ordering.Status;
using Talabat.Customer.API.Contracts.Common;
using Talabat.Domain.Aggregates.RestaurantOperations;

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
    int ItemCount,
    CustomerOrderStatus Status,
    bool DeliveryAgentAssigned);

public sealed record OrderDetailResponse(
    int Id,
    int CustomerId,
    int RestaurantId,
    OrderDeliveryAddressDto DeliveryAddress,
    IReadOnlyCollection<OrderLineItemDto> Items,
    MoneyDto Total,
    DateTime PlacedAt,
    CustomerOrderStatus Status,
    bool DeliveryAgentAssigned,
    CancellationActor? CancelledBy,
    FulfillmentReasonCode? ReasonCode);

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

/// <summary>An optional customer reason code (design D13) and note; the note is required when the code is Other.</summary>
public sealed record CancelOrderRequest(FulfillmentReasonCode? ReasonCode, string? Note);

public sealed record CancelOrderResponse(
    int OrderId,
    CustomerOrderStatus Status,
    FulfillmentReasonCode? ReasonCode,
    DateTime CancelledAt);
