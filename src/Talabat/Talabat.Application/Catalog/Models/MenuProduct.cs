using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Catalog.Models;

public sealed record MenuProduct(
    int Id,
    string Name,
    string Description,
    Money CurrentPrice,
    string? ImageUrl,
    bool IsAvailable);
