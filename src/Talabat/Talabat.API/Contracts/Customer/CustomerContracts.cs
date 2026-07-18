namespace Talabat.Customer.API.Contracts.Customer;

public sealed record CreateProfileRequest(
    string FullName,
    int Age,
    string? PhoneNumber);

public sealed record UpdateProfileRequest(
    string FullName,
    int Age,
    string? PhoneNumber);

public sealed record ProfileResponse(
    int Id,
    string FullName,
    int Age,
    string? PhoneNumber,
    IReadOnlyCollection<AddressDto> Addresses);

public sealed record AddressDto(
    int Id,
    string Street,
    string City,
    string BuildingNumber,
    string? Floor,
    bool IsDefault);
