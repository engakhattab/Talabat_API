namespace Talabat.Application.Basket.AddItem;

public sealed record AddCartItemCommand(
    int CustomerId,
    int RestaurantId,
    int ProductId,
    int Quantity);
