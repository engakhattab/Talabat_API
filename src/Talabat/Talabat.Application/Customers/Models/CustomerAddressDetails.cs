namespace Talabat.Application.Customers.Models;

public sealed record CustomerAddressDetails(
    int Id,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    bool IsDefault);
