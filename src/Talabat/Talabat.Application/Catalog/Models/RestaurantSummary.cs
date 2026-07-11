namespace Talabat.Application.Catalog.Models;

public sealed record RestaurantSummary(
    int Id,
    string Name,
    string Description,
    string? ImageUrl,
    bool IsOpen);
