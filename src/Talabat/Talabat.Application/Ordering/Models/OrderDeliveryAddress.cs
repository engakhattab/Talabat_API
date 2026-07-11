namespace Talabat.Application.Ordering.Models;

public sealed record OrderDeliveryAddress(
    string Street,
    string City,
    string BuildingNumber,
    string? Floor);
