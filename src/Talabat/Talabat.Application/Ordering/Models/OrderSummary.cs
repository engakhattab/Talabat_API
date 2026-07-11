using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Ordering.Models;

public sealed record OrderSummary(
    int Id,
    int RestaurantId,
    DateTime CreatedAtUtc,
    Money TotalAmount);
