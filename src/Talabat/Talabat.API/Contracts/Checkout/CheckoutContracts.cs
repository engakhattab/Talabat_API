using Talabat.Customer.API.Contracts.Common;

namespace Talabat.Customer.API.Contracts.Checkout;

public sealed record CheckoutRequest(int DeliveryAddressId);

public sealed record CheckoutSuccessResponse(int OrderId, string Status = "success");

public sealed record CheckoutUnavailableResponse(
    string Status = "unavailable",
    IReadOnlyCollection<UnavailableItemDto>? UnavailableItems = null);

public sealed record UnavailableItemDto(
    int ProductId,
    string ProductName,
    string Reason);
