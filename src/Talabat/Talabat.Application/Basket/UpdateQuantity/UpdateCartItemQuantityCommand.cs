namespace Talabat.Application.Basket.UpdateQuantity;

public sealed record UpdateCartItemQuantityCommand(
    int CustomerId,
    int ProductId,
    int Quantity);
