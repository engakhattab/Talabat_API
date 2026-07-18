namespace Talabat.Customer.API.Contracts.Address;

public sealed record AddAddressRequest(
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    bool MakeDefault);

public sealed record AddressResponse(
    int Id,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    bool IsDefault);
