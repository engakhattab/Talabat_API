using Talabat.Customer.API.Contracts.Common;

namespace Talabat.Customer.API.Contracts.Cart;

public sealed record CartResponse(
    int? Id,
    int CustomerId,
    int? RestaurantId,
    string Status,
    IReadOnlyCollection<CartLineItemDto> Items,
    MoneyDto Total);

public sealed record CartLineItemDto(
    int ProductId,
    string ProductName,
    MoneyDto UnitPrice,
    int Quantity,
    MoneyDto LineTotal);

public sealed record AddCartItemRequest(
    int RestaurantId,
    int ProductId,
    int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);
