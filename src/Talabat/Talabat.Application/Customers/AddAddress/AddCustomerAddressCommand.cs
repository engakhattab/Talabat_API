namespace Talabat.Application.Customers.AddAddress;

public sealed record AddCustomerAddressCommand(
    int CustomerId,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    bool MakeDefault);
