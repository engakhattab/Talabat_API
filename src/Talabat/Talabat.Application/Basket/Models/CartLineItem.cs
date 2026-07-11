using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Basket.Models;

public sealed record CartLineItem(
    int ProductId,
    string ProductName,
    int Quantity,
    Money CurrentUnitPrice,
    Money LineTotal);
