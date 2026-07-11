using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Ordering.Models;

public sealed record OrderLineItem(
    int ProductId,
    string ProductName,
    Money UnitPrice,
    int Quantity,
    Money LineTotal);
