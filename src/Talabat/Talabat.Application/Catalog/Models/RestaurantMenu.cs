namespace Talabat.Application.Catalog.Models;

public sealed record RestaurantMenu(
    int RestaurantId,
    string RestaurantName,
    IReadOnlyCollection<MenuProduct> Products);
